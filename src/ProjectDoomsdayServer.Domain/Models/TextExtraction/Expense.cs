namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class Expense
{
    public int ExpenseIndex { get; set; }
    public ExtractedField? VendorName { get; set; }
    public ExtractedField? VendorAddress { get; set; }
    public ExtractedField? VendorPhone { get; set; }
    public ExtractedField? ReceiptId { get; set; }
    public ExtractedDateField? ReceiptDate { get; set; }
    public AmmountField? Subtotal { get; set; }
    public AmmountField? Tax { get; set; }
    public AmmountField? Total { get; set; }
    public AmmountField? AmountPaid { get; set; }
    public List<ExtractedField> SummaryFields { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();
}
