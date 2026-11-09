using System.Text;

namespace ClaudeMonitor.Core.Persistence;

public static class WindowPositionPolicy
{
    public const string EmptyLayoutFingerprint = "<no-screens>";

    public static (int X, int Y)? ClampToVisibleScreens(
        int x, int y, int width, int height,
        IReadOnlyList<ScreenInfo> screens)
    {
        if (screens.Count == 0) return null;

        for (var i = 0; i < screens.Count; i++)
        {
            var s = screens[i];
            if (x + width > s.X && x < s.X + s.Width
                && y + height > s.Y && y < s.Y + s.Height)
            {
                return null;
            }
        }

        var primary = FindPrimary(screens);
        return (primary.X + (primary.Width - width) / 2,
                primary.Y + (primary.Height - height) / 2);
    }

    public static string ComputeLayoutFingerprint(IReadOnlyList<ScreenInfo> screens)
    {
        if (screens.Count == 0) return EmptyLayoutFingerprint;

        var sorted = screens.ToArray();
        Array.Sort(sorted, static (a, b) =>
        {
            var c = a.X.CompareTo(b.X);
            if (c != 0) return c;
            c = a.Y.CompareTo(b.Y);
            if (c != 0) return c;
            c = a.Width.CompareTo(b.Width);
            if (c != 0) return c;
            return a.Height.CompareTo(b.Height);
        });

        var sb = new StringBuilder();
        for (var i = 0; i < sorted.Length; i++)
        {
            if (i > 0) sb.Append(';');
            var s = sorted[i];
            if (!string.IsNullOrEmpty(s.DisplayName))
            {
                sb.Append("name=").Append(s.DisplayName).Append('@').Append(s.Width).Append('x').Append(s.Height);
            }
            else
            {
                sb.Append("bounds=").Append(s.X).Append(',').Append(s.Y).Append(',').Append(s.Width).Append('x').Append(s.Height);
            }
            if (s.IsPrimary) sb.Append("|primary");
        }
        return sb.ToString();
    }

    private static ScreenInfo FindPrimary(IReadOnlyList<ScreenInfo> screens)
    {
        for (var i = 0; i < screens.Count; i++)
        {
            if (screens[i].IsPrimary) return screens[i];
        }
        return screens[0];
    }
}
