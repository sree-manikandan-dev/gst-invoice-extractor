using System.Text.RegularExpressions;

namespace GstInvoice.Core.Validation;

/// <summary>
/// GSTIN = 2-digit state code + 10-char PAN + entity number + 'Z' + check character.
/// The last character is a mod-36 checksum over the first 14, so most single-character
/// misreads by the model (O vs 0, 8 vs B) are caught here.
/// </summary>
public static partial class GstinValidator
{
    private const string Charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    [GeneratedRegex("^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")]
    private static partial Regex FormatRegex();

    public static bool HasValidFormat(string gstin) => FormatRegex().IsMatch(gstin);

    /// <summary>State codes 01-38 plus 97 (Other Territory).</summary>
    public static bool HasValidStateCode(string gstin) =>
        int.TryParse(gstin.AsSpan(0, 2), out var code) && ((code >= 1 && code <= 38) || code == 97);

    public static int? StateCode(string? gstin) =>
        gstin is { Length: >= 2 } && int.TryParse(gstin.AsSpan(0, 2), out var code) ? code : null;

    public static char ComputeCheckCharacter(string first14)
    {
        var sum = 0;
        for (var i = 0; i < 14; i++)
        {
            var value = Charset.IndexOf(first14[i]);
            var product = value * (i % 2 == 0 ? 1 : 2);
            sum += product / 36 + product % 36;
        }
        return Charset[(36 - sum % 36) % 36];
    }

    public static bool HasValidChecksum(string gstin) =>
        gstin.Length == 15 && ComputeCheckCharacter(gstin[..14]) == gstin[14];
}
