using FinTrack.SharedKernel;
using System.Globalization;

namespace FinTrack.Modules.Expenses.Domain;

/// <summary>
/// Kullanıcıya ait harcama kategorisi. Fiziksel silme yoktur; arşivlenir.
/// </summary>
public sealed class Category : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int IconKeyMaxLength = 50;

    private Category()
    {
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Büyük/küçük harf duyarsız benzersizlik için normalize edilmiş ad.</summary>
    public string NormalizedName { get; private set; } = string.Empty;
    public string? IconKey { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Result<Category> Create(Guid userId, string? name, string? iconKey, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure<Category>(DomainErrors.UserRequired());
        }

        var nameError = ValidateName(name, out var trimmedName);
        if (nameError is not null)
        {
            return Result.Failure<Category>(nameError);
        }

        var iconError = ValidateIcon(iconKey, out var trimmedIcon);
        if (iconError is not null)
        {
            return Result.Failure<Category>(iconError);
        }

        return Result.Success(new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            NormalizedName = Normalize(trimmedName),
            IconKey = trimmedIcon,
            CreatedAtUtc = nowUtc,
        });
    }

    public Result Rename(string? name, string? iconKey)
    {
        var nameError = ValidateName(name, out var trimmedName);
        if (nameError is not null)
        {
            return Result.Failure(nameError);
        }

        var iconError = ValidateIcon(iconKey, out var trimmedIcon);
        if (iconError is not null)
        {
            return Result.Failure(iconError);
        }

        Name = trimmedName;
        NormalizedName = Normalize(trimmedName);
        IconKey = trimmedIcon;
        return Result.Success();
    }

    /// <summary>Arşivleme idempotenttir; ikinci çağrı hata üretmez.</summary>
    public Result Archive(DateTime nowUtc)
    {
        if (IsArchived)
        {
            return Result.Success();
        }

        IsArchived = true;
        ArchivedAtUtc = nowUtc;
        return Result.Success();
    }

    public static string Normalize(string name) =>
        CultureInfo.GetCultureInfo("tr-TR").TextInfo.ToUpper(name.Trim());

    private static Error? ValidateName(string? name, out string trimmedName)
    {
        trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0)
        {
            return DomainErrors.CategoryNameRequired();
        }

        return trimmedName.Length > NameMaxLength ? DomainErrors.CategoryNameTooLong() : null;
    }

    private static Error? ValidateIcon(string? iconKey, out string? trimmedIcon)
    {
        trimmedIcon = string.IsNullOrWhiteSpace(iconKey) ? null : iconKey.Trim();
        return trimmedIcon is not null && trimmedIcon.Length > IconKeyMaxLength
            ? DomainErrors.CategoryIconTooLong()
            : null;
    }
}
