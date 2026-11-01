namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class DateField : ExtractedField
{
    public DateOnly? Date { get; set; }
}
