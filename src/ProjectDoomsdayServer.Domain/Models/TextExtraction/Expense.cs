namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class Expense
{
    public int ExpenseIndex { get; set; }
    public ExtractedField? VendorName { get; set; }
    public ExtractedField? VendorAddress { get; set; }
    public ExtractedField? VendorPhone { get; set; }
    public ExtractedField? ReceiptId { get; set; }
    public DateField? ReceiptDate { get; set; }
    public AmountField? Subtotal { get; set; }
    public AmountField? Tax { get; set; }
    public AmountField? Total { get; set; }
    public AmountField? AmountPaid { get; set; }
    public List<ExtractedField> SummaryFields { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();
}
