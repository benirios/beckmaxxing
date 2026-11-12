namespace ResearchCandidates;

internal static class ResultSummary
{
    public static string Write(string detailPath, IEnumerable<string> detailRows,
        int groupColumn, params (string Name, int Column)[] variants)
    {
        var rows = detailRows.Skip(1).Select(ResearchData.Fields).ToList();
        var groups = new[] { "ALL" }.Concat(rows.Select(r => r[groupColumn]).Distinct().Order());
        var output = new List<string> { "group,variant,trades,net_per_ounce,mean_net_per_ounce,profit_factor" };
        foreach (var group in groups)
        {
            var subset = group == "ALL" ? rows : rows.Where(r => r[groupColumn] == group).ToList();
            foreach (var (name, column) in variants)
            {
                var values = subset.Where(r => r.Length > column && !string.IsNullOrWhiteSpace(r[column]))
                    .Select(r => ResearchData.Number(r[column])).Where(double.IsFinite).ToList();
                var profits = values.Sum(v => Math.Max(0, v));
                var losses = -values.Sum(v => Math.Min(0, v));
                output.Add(ResearchData.Row(group, name, values.Count, values.Sum(),
                    values.Count == 0 ? double.NaN : values.Average(),
                    losses == 0 ? double.NaN : profits / losses));
            }
        }
        var path = Path.Combine(Path.GetDirectoryName(detailPath) ?? "",
            Path.GetFileNameWithoutExtension(detailPath) + "-summary.csv");
        File.WriteAllLines(path, output);
        return path;
    }
}
