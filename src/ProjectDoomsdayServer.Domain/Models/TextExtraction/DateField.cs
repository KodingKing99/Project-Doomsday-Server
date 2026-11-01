namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class ExtractedDateField : ExtractedField
{
    public DateOnly? Date { get; set; }
}
