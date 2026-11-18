using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace FinTrack.ArchitectureTests;

/// <summary>
/// R6 (docs/11 §4.6 / §1.1): modüllerin her MapPost rotası ya /api/v1/auth
/// altında ya da middleware'in pozitif allowlist'inde olmalı. Allowlist host
/// kaynağından okunur; test, middleware ile ayrı bir sabit listesi tutmaz.
/// Bu bir yapısal (kaynak metni) doğrulamadır; çalışma zamanı kapsama davranışı
/// entegrasyon testleri S1–S17 ile örtülür.
/// </summary>
public sealed class MapPostAllowlistTests
{
    private static readonly Regex QuotedLiteral = new("\"((?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Compiled);

    [Fact]
    public void Tum_modul_MapPost_rotalari_pozitif_listede_veya_auth_altinda_olmali()
    {
        var root = FindRepoRoot();
        var modulesRoot = Path.Combine(root, "src", "Modules");
        Assert.True(Directory.Exists(modulesRoot), $"src/Modules bulunamadı: {modulesRoot}");

        var allowed = ReadMiddlewareAllowlist(root);

        var files = Directory.EnumerateFiles(modulesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(IsEndpointSourceFile)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);

        var prefixes = files.SelectMany(ExtractRoutePrefixes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(prefixes);

        var violations = new List<string>();
        var mapPostCount = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"\.MapPost\s*\("))
            {
                mapPostCount++;
                var line = text[..match.Index].Count(character => character == '\n') + 1;
                var location = $"{Relative(root, file)}:{line}";

                if (!TryReadStringLiteral(text, match.Index + match.Length, out var literal) ||
                    literal is null)
                {
                    violations.Add($"{location} - MapPost rotası statik string literal değil; elle gözden geçirilmeli.");
                    continue;
                }

                var relative = literal.Length == 0 || literal == "/" ? string.Empty : literal;
                var candidates = prefixes.Select(prefix => prefix + relative).ToArray();
                if (!candidates.Any(path => allowed.Any(prefix =>
                        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                {
                    violations.Add(
                        $"{location} - MapPost(\"{literal}\") allowlist dışında. " +
                        $"Aday tam yollar: {string.Join(", ", candidates)}");
                }
            }
        }

        Assert.True(mapPostCount > 0, "Hiç MapPost bulunamadı; kaynak ayrıştırıcısı (parser) bozuk olabilir.");
        Assert.True(violations.Count == 0,
            "R6 ihlali: tüm mutasyon POST'ları /api/v1/auth veya middleware pozitif listesinde olmalı.\n" +
            string.Join("\n", violations));
    }

    private static string[] ReadMiddlewareAllowlist(string root)
    {
        var middleware = Path.Combine(
            root, "src", "Host", "FinTrack.Api", "Idempotency", "IdempotencyMiddleware.cs");
        Assert.True(File.Exists(middleware), $"Middleware kaynağı bulunamadı: {middleware}");

        var text = File.ReadAllText(middleware);
        var block = Regex.Match(
            text, @"InScopePrefixes\s*=\s*\[(?<body>.*?)\];", RegexOptions.Singleline);
        Assert.True(block.Success, "IdempotencyMiddleware.InScopePrefixes dizisi okunamadı.");

        var allowed = QuotedLiteral.Matches(block.Groups["body"].Value)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .Where(value => value.StartsWith("/api/", StringComparison.Ordinal))
            .ToList();

        // auth, middleware'da açıkça hariç tutulur (IsInScope).
        var exclusions = Regex.Matches(text, @"StartsWith\s*\(\s*""(?<path>/api/[^""]+)""")
            .Cast<Match>()
            .Select(match => match.Groups["path"].Value);
        allowed.AddRange(exclusions);

        Assert.NotEmpty(allowed);
        Assert.Contains(allowed, prefix => prefix.Contains("auth", StringComparison.OrdinalIgnoreCase));
        return allowed.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> ExtractRoutePrefixes(string file)
    {
        var text = File.ReadAllText(file);
        var starts = Regex.Matches(text, @"\bconst\s+string\s+[A-Za-z0-9_]+\s*=\s*")
            .Cast<Match>()
            .Select(match => match.Index + match.Length)
            .Concat(Regex.Matches(text, @"\.MapGroup\s*\(")
                .Cast<Match>()
                .Select(match => match.Index + match.Length));

        foreach (var start in starts)
        {
            if (TryReadStringLiteral(text, start, out var literal) &&
                literal!.StartsWith("/api/", StringComparison.Ordinal))
            {
                yield return literal;
            }
        }
    }

    private static bool IsEndpointSourceFile(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !parts.Any(part => part is "bin" or "obj") &&
               parts.Any(part => part == "Endpoints" ||
                                 part.EndsWith(".Endpoints", StringComparison.Ordinal));
    }

    private static bool TryReadStringLiteral(string text, int start, out string? value)
    {
        value = null;
        var index = start;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length)
        {
            return false;
        }

        var builder = new StringBuilder();
        if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
        {
            index += 2;
            while (index < text.Length)
            {
                if (text[index] == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        builder.Append('"');
                        index += 2;
                        continue;
                    }

                    value = builder.ToString();
                    return true;
                }

                builder.Append(text[index++]);
            }

            return false;
        }

        if (text[index] != '"')
        {
            return false;
        }

        index++;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '\\')
            {
                if (index + 1 >= text.Length)
                {
                    return false;
                }

                builder.Append(text[index + 1] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    var other => other,
                });
                index += 2;
                continue;
            }

            if (character == '"')
            {
                value = builder.ToString();
                return true;
            }

            builder.Append(character);
            index++;
        }

        return false;
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Modules")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repo kökü (src/Modules + tests) bulunamadı.");
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');
}
