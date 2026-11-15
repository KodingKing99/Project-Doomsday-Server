namespace ProjectDoomsdayServer.Application.Files.Services;

/// <summary>
/// A single typed export cell. Exactly one of the values is set, or none when the value is missing.
/// </summary>
public readonly record struct ExportCell(string? Text, decimal? Number, DateOnly? Date)
{
    public static ExportCell FromText(string? text) => new(text, null, null);

    public static ExportCell FromNumber(decimal? number) => new(null, number, null);

    public static ExportCell FromDate(DateOnly? date) => new(null, null, date);
}
