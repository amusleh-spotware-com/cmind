namespace Core.Quant;

/// <summary>
/// Combinatorially-Symmetric Cross-Validation (Bailey, Borwein, López de Prado &amp; Zhu, 2015) — the
/// Probability of Backtest Overfitting. It splits the observations into S groups, and for every way of
/// choosing S/2 groups as in-sample (the rest out-of-sample) it selects the in-sample best trial and
/// checks whether that trial lands in the bottom half out-of-sample. PBO is the fraction of splits
/// where the winner fails to generalize. Deterministic.
/// </summary>
public static class CombinatorialCrossValidation
{
    public static double ProbabilityOfBacktestOverfitting(TrialSurface surface, int slices)
    {
        ArgumentNullException.ThrowIfNull(surface);
        slices = NormalizeSlices(slices, surface.Observations);

        var groups = PartitionRows(surface.Observations, slices);
        var trials = surface.Count;
        var overfit = 0;
        var total = 0;

        foreach (var isGroups in Combinations(slices, slices / 2))
        {
            var isMask = new bool[slices];
            foreach (var g in isGroups) isMask[g] = true;

            var isRows = CollectRows(groups, isMask, inSample: true);
            var oosRows = CollectRows(groups, isMask, inSample: false);
            if (isRows.Count == 0 || oosRows.Count == 0) continue;

            var bestTrial = 0;
            var bestSharpe = double.NegativeInfinity;
            for (var j = 0; j < trials; j++)
            {
                var s = SubsetSharpe(surface.Trials[j].Values, isRows);
                if (s > bestSharpe) { bestSharpe = s; bestTrial = j; }
            }

            var bestOos = SubsetSharpe(surface.Trials[bestTrial].Values, oosRows);
            var rank = 0;
            for (var j = 0; j < trials; j++)
                if (SubsetSharpe(surface.Trials[j].Values, oosRows) < bestOos) rank++;

            var omega = (rank + 1.0) / (trials + 1.0); // relative rank in (0,1)
            var lambda = Math.Log(omega / (1.0 - omega)); // logit
            if (lambda < 0.0) overfit++; // the in-sample winner fell into the bottom half out-of-sample
            total++;
        }

        return total == 0 ? 0.0 : (double)overfit / total;
    }

    private static int NormalizeSlices(int slices, int observations)
    {
        if (slices < 2) slices = 2;
        if (slices % 2 != 0) slices++;
        if (slices > observations) slices = observations % 2 == 0 ? observations : observations - 1;
        return slices < 2 ? 2 : slices;
    }

    private static int[][] PartitionRows(int total, int groups)
    {
        var result = new int[groups][];
        var baseSize = total / groups;
        var remainder = total % groups;
        var cursor = 0;
        for (var g = 0; g < groups; g++)
        {
            var size = baseSize + (g < remainder ? 1 : 0);
            result[g] = new int[size];
            for (var k = 0; k < size; k++) result[g][k] = cursor++;
        }
        return result;
    }

    private static List<int> CollectRows(int[][] groups, bool[] mask, bool inSample)
    {
        var rows = new List<int>();
        for (var g = 0; g < groups.Length; g++)
            if (mask[g] == inSample)
                rows.AddRange(groups[g]);
        return rows;
    }

    private static double SubsetSharpe(IReadOnlyList<double> values, List<int> rows)
    {
        var n = rows.Count;
        if (n < 2) return 0.0;
        var mean = 0.0;
        foreach (var r in rows) mean += values[r];
        mean /= n;
        var variance = 0.0;
        foreach (var r in rows)
        {
            var d = values[r] - mean;
            variance += d * d;
        }
        variance /= n;
        var std = Math.Sqrt(variance);
        return std > 0.0 ? mean / std : 0.0;
    }

    private static IEnumerable<int[]> Combinations(int n, int k)
    {
        var indices = new int[k];
        for (var i = 0; i < k; i++) indices[i] = i;
        while (true)
        {
            yield return (int[])indices.Clone();
            var pos = k - 1;
            while (pos >= 0 && indices[pos] == n - k + pos) pos--;
            if (pos < 0) yield break;
            indices[pos]++;
            for (var i = pos + 1; i < k; i++) indices[i] = indices[i - 1] + 1;
        }
    }
}
