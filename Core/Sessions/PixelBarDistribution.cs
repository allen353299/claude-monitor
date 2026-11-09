namespace ClaudeMonitor.Core.Sessions;

public enum BarColumn
{
    Empty = -1,
    Attention = 0,
    Done = 1,
    Working = 2,
    Idle = 3,
}

public static class PixelBarDistribution
{
    public const int CategoryCount = 4;

    public static BarColumn[] Distribute(int[] countsByCategory, int totalSessions, int columns)
    {
        var layout = new BarColumn[columns];
        Array.Fill(layout, BarColumn.Empty);
        if (totalSessions <= 0 || columns <= 0) return layout;
        if (countsByCategory.Length != CategoryCount)
            throw new ArgumentException($"Expected {CategoryCount} categories", nameof(countsByCategory));

        var percentages = new double[CategoryCount];
        var allocated = new int[CategoryCount];
        var remainders = new double[CategoryCount];
        var totalAllocated = 0;
        var sumTallied = 0;
        for (int i = 0; i < CategoryCount; i++)
        {
            percentages[i] = (double)countsByCategory[i] / totalSessions * columns;
            allocated[i] = (int)Math.Floor(percentages[i]);
            remainders[i] = percentages[i] - allocated[i];
            totalAllocated += allocated[i];
            sumTallied += countsByCategory[i];
        }

        var capacity = sumTallied >= totalSessions
            ? columns
            : Math.Min(columns, sumTallied);

        while (totalAllocated < capacity)
        {
            int bestIdx = -1;
            double bestRemainder = -1;
            for (int i = 0; i < CategoryCount; i++)
            {
                if (remainders[i] > bestRemainder)
                {
                    bestRemainder = remainders[i];
                    bestIdx = i;
                }
            }
            if (bestIdx < 0) break;
            allocated[bestIdx]++;
            remainders[bestIdx] = -1;
            totalAllocated++;
        }

        int cursor = 0;
        for (int kind = 0; kind < CategoryCount; kind++)
        {
            for (int j = 0; j < allocated[kind] && cursor < columns; j++)
            {
                layout[cursor++] = (BarColumn)kind;
            }
        }
        return layout;
    }
}
