using System.Globalization;
using System.Text;
using Core.Constants;

namespace Core.Domain;

// Anti-corruption translation between the CSV interchange format (cMAM-style symbol-map import/export) and
// the SymbolMapEntry value objects. Pure — validates each row through the VOs so a malformed file can
// never produce an invalid map. Columns: Source,Destination,VolumeMultiplier (multiplier optional, = 1).
public static class CopySymbolMapCsv
{
    public const string Header = "Source,Destination,VolumeMultiplier";

    public static string Format(IEnumerable<(string Source, string Destination, double VolumeMultiplier)> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Header);
        foreach (var entry in entries)
            builder.AppendLine(string.Join(',', entry.Source, entry.Destination,
                entry.VolumeMultiplier.ToString(CultureInfo.InvariantCulture)));
        return builder.ToString();
    }

    public static IReadOnlyList<SymbolMapEntry> Parse(string csv)
    {
        var entries = new List<SymbolMapEntry>();
        foreach (var rawLine in csv.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("Source", StringComparison.OrdinalIgnoreCase)) continue; // header row

            var columns = line.Split(',');
            if (columns.Length < 2) throw new DomainException(DomainErrors.CopySymbolMapCsvInvalid);

            var source = columns[0].Trim();
            var destination = columns[1].Trim();
            if (source.Length == 0 || destination.Length == 0)
                throw new DomainException(DomainErrors.CopySymbolMapCsvInvalid);

            var multiplier = 1.0;
            if (columns.Length >= 3 && columns[2].Trim().Length > 0
                && !double.TryParse(columns[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier))
                throw new DomainException(DomainErrors.CopySymbolMapCsvInvalid);

            entries.Add(new SymbolMapEntry(new Symbol(source), new Symbol(destination), multiplier));
        }

        return entries;
    }
}
