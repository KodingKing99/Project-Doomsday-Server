namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public sealed class ExtractedDateField : ExtractedField
{
    public DateOnly? Date { get; set; }
}
