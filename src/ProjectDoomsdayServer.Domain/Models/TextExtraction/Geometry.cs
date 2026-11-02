namespace ProjectDoomsdayServer.Domain.Models.TextExtraction;

public sealed class Geometry
{
    public int PageNumber { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public List<Point> Polygon { get; set; } = new();
}
