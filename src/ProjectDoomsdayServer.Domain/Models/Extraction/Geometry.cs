namespace ProjectDoomsdayServer.Domain.Models.Extraction;

public sealed class Geometry
{
    public int PageNumber { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public List<Point> Polygon { get; set; } = new();
}

public sealed class BoundingBox
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class Point
{
    public double X { get; set; }
    public double Y { get; set; }
}
