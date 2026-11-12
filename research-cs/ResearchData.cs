using System.Globalization;

namespace ResearchCandidates;

internal record Quote(DateTimeOffset Time, double Bid, double Ask, string Contract = "")
{
    public double Mid => (Bid + Ask) / 2;
    public double Spread => Ask - Bid;
}

internal record Event(DateTimeOffset Time, string Kind, string Status, bool Overlap);

internal static class ResearchData
{
    public static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string[] Fields(string line)
    {
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ',' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Unclosed CSV quote");
        fields.Add(field.ToString());
        return fields.ToArray();
    }

    public static IEnumerable<Dictionary<string, string>> Csv(string path)
    {
        using var reader = new StreamReader(path);
        var header = reader.ReadLine() ?? throw new FormatException($"Empty CSV: {path}");
        var names = Fields(header).Select(x => x.Trim()).ToArray();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            throw new FormatException($"Duplicate CSV header: {path}");
        string? line;
        var number = 1;
        while ((line = reader.ReadLine()) != null)
        {
            number++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = Fields(line);
            if (values.Length != names.Length) throw new FormatException($"{path}:{number}: column count");
            yield return names.Select((name, i) => (name, value: values[i].Trim()))
                .ToDictionary(x => x.name, x => x.value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static DateTimeOffset Utc(string value)
    {
        if (!value.EndsWith('Z') && !System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d{2}:\d{2}$"))
            throw new FormatException($"Timestamp requires UTC or explicit offset: {value}");
        return DateTimeOffset.Parse(value, Invariant, DateTimeStyles.AssumeUniversal).ToUniversalTime();
    }

    public static double Number(string value) => double.Parse(value, NumberStyles.Float, Invariant);

    public static List<Quote> Quotes(string path, bool contractRequired = false)
    {
        var rows = Csv(path).Select(row => new Quote(Utc(row["timestamp_utc"]),
            Number(row["bid"]), Number(row["ask"]),
            contractRequired ? row["contract"] : row.GetValueOrDefault("contract", ""))).ToList();
        if (rows.Count == 0) throw new FormatException($"No quotes: {path}");
        for (var i = 0; i < rows.Count; i++)
        {
            if (!double.IsFinite(rows[i].Bid) || !double.IsFinite(rows[i].Ask) ||
                rows[i].Bid <= 0 || rows[i].Ask < rows[i].Bid ||
                (i > 0 && rows[i].Time <= rows[i - 1].Time))
                throw new FormatException($"Invalid or out-of-order quote at row {i + 2}: {path}");
            if (contractRequired && string.IsNullOrWhiteSpace(rows[i].Contract))
                throw new FormatException($"Missing contract at row {i + 2}: {path}");
        }
        return rows;
    }

    public static List<Event> Events(string path) => Csv(path).Select(row =>
        new Event(Utc(row["timestamp_utc"]), row["kind"], row["status"],
            bool.Parse(row["overlap"]))).OrderBy(x => x.Time).ToList();

    public static int FirstAtOrAfter(List<Quote> quotes, DateTimeOffset time)
    {
        var lo = 0; var hi = quotes.Count;
        while (lo < hi) { var mid = (lo + hi) / 2; if (quotes[mid].Time < time) lo = mid + 1; else hi = mid; }
        return lo;
    }

    public static Quote? LatestAtOrBefore(List<Quote> quotes, DateTimeOffset time, TimeSpan maxAge)
    {
        var i = FirstAtOrAfter(quotes, time);
        if (i < quotes.Count && quotes[i].Time == time) return quotes[i];
        if (i == 0) return null;
        var quote = quotes[i - 1];
        return time - quote.Time <= maxAge ? quote : null;
    }

    public static DateTimeOffset EasternTime(DateTime easternDate, int hour, int minute, int second = 0)
    {
        var local = new DateTime(easternDate.Year, easternDate.Month, easternDate.Day,
            hour, minute, second, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Eastern.GetUtcOffset(local)).ToUniversalTime();
    }

    public static string Row(params object?[] values) => string.Join(',', values.Select(value =>
    {
        var s = Convert.ToString(value, Invariant) ?? "";
        return s.Contains(',') || s.Contains('"') || s.Contains('\n')
            ? '"' + s.Replace("\"", "\"\"") + '"' : s;
    }));

    public static double Median(IEnumerable<double> source)
    {
        var a = source.Order().ToArray();
        if (a.Length == 0) throw new InvalidOperationException("Empty median");
        return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    public static double Percentile95(IEnumerable<double> source)
    {
        var a = source.Order().ToArray();
        if (a.Length == 0) throw new InvalidOperationException("Empty percentile");
        return a[(int)Math.Ceiling(.95 * a.Length) - 1];
    }
}
