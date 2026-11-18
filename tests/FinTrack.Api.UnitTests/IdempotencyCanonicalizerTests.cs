using FinTrack.Api.Idempotency;
using Xunit;

namespace FinTrack.Api.UnitTests;

/// <summary>
/// docs/11 §3.2 / S13 / S26 / §3.2.1: RFC 8785 (Jcs.Net) canonik eşdeğerlik ve
/// 2^39 sayı güvenli sınırı. Tamamı deterministiktir; DB/Docker gerekmez.
/// </summary>
public sealed class IdempotencyCanonicalizerTests
{
    private static string Hash(string body) => IdempotencyCanonicalizer.ComputeRequestHash(body);

    [Fact]
    public void S13_Pozellik_sirasi_kanonik_olarak_esittir()
    {
        Assert.Equal(
            Hash("""{"b":1,"a":2,"c":3}"""),
            Hash("""{"c":3,"a":2,"b":1}"""));
    }

    [Fact]
    public void S13_İcice_nesnelerin_ozellik_sirasi_da_esittir()
    {
        Assert.Equal(
            Hash("""{"outer":{"z":1,"a":{"y":2,"b":3}},"n":4}"""),
            Hash("""{"n":4,"outer":{"a":{"b":3,"y":2},"z":1}}"""));
    }

    [Fact]
    public void S13_1_0_ve_1_ayni_hash_uretir()
    {
        // RFC 8785 sayıları IEEE-754 double olarak ECMAScript biçiminde serileştirir.
        Assert.Equal(Hash("""{"n":1.0}"""), Hash("""{"n":1}"""));
        Assert.Equal(Hash("""{"n":1E+2}"""), Hash("""{"n":100}"""));
        Assert.Equal(Hash("""{"n":-0}"""), Hash("""{"n":0}"""));
    }

    [Fact]
    public void S13_Dizi_sirasi_degisirse_hash_degisir()
    {
        Assert.NotEqual(Hash("""{"a":[1,2,3]}"""), Hash("""{"a":[3,2,1]}"""));
    }

    [Fact]
    public void S13_Unicode_kacislari_ve_surrogat_cifti_ayni_hash_uretir()
    {
        var emoji = char.ConvertFromUtf32(0x1F600); // 😀 — kaynak dosya kodlamasına bağlı değil

        Assert.Equal(Hash("""{"s":"\u00e9"}"""), Hash("""{"s":"é"}"""));
        Assert.Equal(Hash("""{"s":"\u00E9"}"""), Hash("""{"s":"\u00e9"}"""));
        Assert.Equal(Hash("""{"s":"\uD83D\uDE00"}"""), Hash($"{{\"s\":\"{emoji}\"}}"));
        Assert.Equal(Hash("""{"s":"\/"}"""), Hash("""{"s":"/"}"""));
    }

    [Theory]
    [InlineData("""{"x":1,"x":2}""")] // yinelenen anahtar: strict I-JSON reddeder
    [InlineData("""{"s":"\uD800"}""")] // tek başına surrogate
    [InlineData("""{"n":NaN}""")]
    [InlineData("""{"n":Infinity}""")]
    [InlineData("""{"n":01}""")] // geçersiz sayı gösterimi
    [InlineData("""{"n":1,}""")] // trailing comma
    [InlineData("not json")]
    public void S13_Strict_I_JSON_disindaki_govdeler_reddedilir(string body)
    {
        Assert.Throws<IdempotencyCanonicalizer.InvalidBodyException>(() => Hash(body));
    }

    [Theory]
    [InlineData("549755813887.9997", "549755813887.9998")]
    [InlineData("549755813887.9998", "549755813887.9999")]
    [InlineData("-549755813887.9998", "-549755813887.9997")]
    public void S27_Sinirin_hemen_altindaki_ardisik_4_ondalik_degerler_farkli_hash_uretir(
        string first, string second)
    {
        var firstHash = Hash($"{{\"amount\":{first}}}");
        var secondHash = Hash($"{{\"amount\":{second}}}");

        Assert.NotEqual(firstHash, secondHash);
    }

    [Theory]
    [InlineData("549755813888.0000")] // 2^39 — sınır dahil değil
    [InlineData("549755813888.0001")]
    [InlineData("549755813889")]
    [InlineData("-549755813888.0000")]
    [InlineData("-549755813888.0001")]
    public void S27_Sinir_ve_ustu_degerler_InvalidBodyException_uretin(string number)
    {
        Assert.Throws<IdempotencyCanonicalizer.InvalidBodyException>(
            () => Hash($"{{\"amount\":{number}}}"));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    [InlineData("1e309")]
    public void S27_Sonlu_olmayan_veya_tasan_sayilar_reddedilir(string number)
    {
        Assert.Throws<IdempotencyCanonicalizer.InvalidBodyException>(
            () => Hash($"{{\"amount\":{number}}}"));
    }

    [Fact]
    public void S27_Sinir_kontrolu_nesneler_ve_diziler_icerinde_de_gecerlidir()
    {
        Assert.Equal(
            Hash("""{"items":[{"amount":549755813887.9999,"note":"x"}]}"""),
            Hash("""{"items":[{"note":"x","amount":549755813887.9999}]}"""));
        Assert.NotEqual(
            Hash("""{"items":[{"amount":549755813887.9998}]}"""),
            Hash("""{"items":[{"amount":549755813887.9999}]}"""));
        Assert.Throws<IdempotencyCanonicalizer.InvalidBodyException>(
            () => Hash("""{"items":[{"amount":549755813888.0000}]}"""));
    }

    [Fact]
    public void Hash_64_karakter_kucuk_hex_olmalidir()
    {
        var hash = Hash("""{"a":1}""");

        Assert.Matches("^[0-9a-f]{64}$", hash);
    }
}
