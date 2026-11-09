using ClaudeMonitor.Core.Persistence;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class WindowPositionPolicyTests
{
    private static readonly ScreenInfo Primary = new(0, 0, 1920, 1080, "Primary", IsPrimary: true);
    private static readonly ScreenInfo Secondary = new(1920, 0, 1920, 1080, "Secondary", IsPrimary: false);

    [Fact]
    public void Returns_Null_When_Window_Inside_Single_Screen()
    {
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 100, y: 100, width: 600, height: 400,
            screens: new[] { Primary });
        Assert.Null(result);
    }

    [Fact]
    public void Returns_Null_When_Window_Straddles_Two_Adjacent_Screens()
    {
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 1800, y: 100, width: 600, height: 400,
            screens: new[] { Primary, Secondary });
        Assert.Null(result);
    }

    [Fact]
    public void Centers_On_Primary_When_Window_Off_Screen()
    {
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 5000, y: 100, width: 600, height: 400,
            screens: new[] { Primary, Secondary });

        Assert.NotNull(result);
        var (x, y) = result!.Value;
        Assert.Equal(0 + (1920 - 600) / 2, x);
        Assert.Equal(0 + (1080 - 400) / 2, y);
    }

    [Fact]
    public void Falls_Back_To_First_Screen_When_No_Primary_Flagged()
    {
        var only = new ScreenInfo(100, 200, 1280, 720, null, IsPrimary: false);
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 9000, y: 9000, width: 400, height: 300,
            screens: new[] { only });

        Assert.NotNull(result);
        var (x, y) = result!.Value;
        Assert.Equal(100 + (1280 - 400) / 2, x);
        Assert.Equal(200 + (720 - 300) / 2, y);
    }

    [Fact]
    public void Returns_Null_For_Empty_Screen_List()
    {
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 0, y: 0, width: 600, height: 400,
            screens: Array.Empty<ScreenInfo>());
        Assert.Null(result);
    }

    [Fact]
    public void Detects_Pure_Edge_Touch_As_Off_Screen()
    {
        var result = WindowPositionPolicy.ClampToVisibleScreens(
            x: 1920, y: 0, width: 600, height: 400,
            screens: new[] { Primary });
        Assert.NotNull(result);
    }
}
