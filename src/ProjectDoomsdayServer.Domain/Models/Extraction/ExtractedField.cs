namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public class ExtractedField
{
    public string Type { get; set; } = default!;
    public string? Label { get; set; }
    public double? LabelConfidence { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Geometry? Geometry { get; set; }
}
