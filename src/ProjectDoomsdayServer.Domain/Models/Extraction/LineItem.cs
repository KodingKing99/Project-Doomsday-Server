namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public sealed class LineItem
{
    public int GroupIndex { get; set; }
    public ExtractedField? Item { get; set; }
    public ExtractedAmountField? Quantity { get; set; }
    public ExtractedAmountField? UnitPrice { get; set; }
    public ExtractedAmountField? Price { get; set; }
    public ExtractedField? ProductCode { get; set; }
    public ExtractedField? ExpenseRow { get; set; }
    public List<ExtractedField> OtherFields { get; set; } = new();
}
