namespace ProjectDoomsdayServer.TestSupport;

/// <summary>
/// Single source of truth for the file API URLs used by tests.
/// </summary>
public static class FileRoutes
{
    public const string Base = "/file";

    public static string ById(string id) => $"{Base}/{id}";

    public static string Content(string id) => $"{ById(id)}/content";

    public static string ExtractExpenseInfo(string id, bool? force = null) =>
        force is null
            ? $"{ById(id)}/extractExpenseInfo"
            : $"{ById(id)}/extractExpenseInfo?force={force.Value.ToString().ToLowerInvariant()}";

    public static string ExportCsv(string id) => $"{ById(id)}/export/csv";

    public static string List(int? skip = null, int? take = null)
    {
        var queryParts = new List<string>();
        if (skip is not null)
            queryParts.Add($"skip={skip.Value}");
        if (take is not null)
            queryParts.Add($"take={take.Value}");
        return queryParts.Count == 0 ? Base : $"{Base}?{string.Join('&', queryParts)}";
    }
}
