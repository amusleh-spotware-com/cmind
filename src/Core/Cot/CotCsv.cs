using System.Globalization;
using System.Text;

namespace Core.Cot;

/// <summary>
/// Serializes COT history into a flat CSV — one row per weekly report, a fixed leading block
/// (report date, open interest, COT index, speculator net) then long/short/spread/net columns for every
/// trader category the report kind carries. All values are culture-invariant so the file round-trips
/// identically regardless of the user's locale. Pure; no I/O.
/// </summary>
public static class CotCsv
{
    public static string Build(CotReportKind kind, IReadOnlyList<CotHistoryPoint> points)
    {
        var categories = kind.Categories();
        var builder = new StringBuilder();

        var header = new List<string> { "report_date", "open_interest", "cot_index", "speculator_net" };
        foreach (var category in categories)
        {
            var name = category.ToString();
            header.Add($"{name}_long");
            header.Add($"{name}_short");
            header.Add($"{name}_spread");
            header.Add($"{name}_net");
        }

        builder.Append(string.Join(',', header)).Append('\n');

        foreach (var point in points)
        {
            var row = new List<string>
            {
                point.ReportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                point.OpenInterest.ToString(CultureInfo.InvariantCulture),
                point.CotIndex?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
                point.SpeculatorNet.ToString(CultureInfo.InvariantCulture)
            };

            foreach (var category in categories)
            {
                var view = point.Categories.FirstOrDefault(c => c.Category == category);
                row.Add(view?.Long.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                row.Add(view?.Short.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                row.Add(view?.Spread.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                row.Add(view?.Net.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            }

            builder.Append(string.Join(',', row)).Append('\n');
        }

        return builder.ToString();
    }
}
