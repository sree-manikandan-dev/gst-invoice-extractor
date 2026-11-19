namespace GstInvoice.Core.Validation;

public enum FlagSeverity
{
    /// <summary>Probably wrong. A human must check before approving.</summary>
    Error,
    /// <summary>Unusual but can be legitimate (e.g. mixed tax rates).</summary>
    Warning,
}

/// <param name="Code">Stable machine-readable code, e.g. GSTIN_CHECKSUM.</param>
/// <param name="Field">Which field the flag is about, if any.</param>
public sealed record ValidationFlag(string Code, FlagSeverity Severity, string Message, string? Field = null);
