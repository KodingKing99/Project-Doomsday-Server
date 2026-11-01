namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class LineItem
{
    public int GroupIndex { get; set; }
    public ExtractedField? Item { get; set; }
    public AmmountField? Quantity { get; set; }
    public AmmountField? UnitPrice { get; set; }
    public AmmountField? Price { get; set; }
    public ExtractedField? ProductCode { get; set; }
    public ExtractedField? ExpenseRow { get; set; }
    public List<ExtractedField> OtherFields { get; set; } = new();
}
