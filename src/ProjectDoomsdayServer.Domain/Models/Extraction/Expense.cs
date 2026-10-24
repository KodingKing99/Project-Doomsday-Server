namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public sealed class Expense
{
    public int ExpenseIndex { get; set; }
    public ExtractedField? VendorName { get; set; }
    public ExtractedField? VendorAddress { get; set; }
    public ExtractedField? VendorPhone { get; set; }
    public ExtractedField? ReceiptId { get; set; }
    public ExtractedDateField? ReceiptDate { get; set; }
    public ExtractedAmountField? Subtotal { get; set; }
    public ExtractedAmountField? Tax { get; set; }
    public ExtractedAmountField? Total { get; set; }
    public ExtractedAmountField? AmountPaid { get; set; }
    public List<ExtractedField> SummaryFields { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();
}
