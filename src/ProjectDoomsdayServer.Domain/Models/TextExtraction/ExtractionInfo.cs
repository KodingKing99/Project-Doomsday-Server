namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class ExtractionInfo
{
    public DateTimeOffset ExtractedAtUtc { get; set; }
    public string Source { get; set; } = default!;
    public string RawText { get; set; } = string.Empty;
    public List<Expense> Expenses { get; set; } = new();
}
