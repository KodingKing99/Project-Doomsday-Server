namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public sealed class ExtractedExpenseInfo
{
    public DateTimeOffset ExtractedAtUtc { get; set; }
    public string Source { get; set; } = default!;
    public string RawText { get; set; } = string.Empty;
    public List<Expense> Expenses { get; set; } = new();
}
