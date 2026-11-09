using ClaudeMonitor.Core.Persistence;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class LayoutFingerprintTests
{
    private static readonly ScreenInfo A = new(0, 0, 1920, 1080, "Dell U2719D", IsPrimary: true);
    private static readonly ScreenInfo B = new(1920, 0, 2560, 1440, "LG 27UK650", IsPrimary: false);
    private static readonly ScreenInfo C = new(0, 0, 1920, 1080, null, IsPrimary: true);

    [Fact]
    public void Same_Screens_In_Different_Order_Yield_Same_Fingerprint()
    {
        var f1 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A, B });
        var f2 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { B, A });
        Assert.Equal(f1, f2);
    }

    [Fact]
    public void Adding_A_Monitor_Changes_Fingerprint()
    {
        var oneMon = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A });
        var twoMon = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A, B });
        Assert.NotEqual(oneMon, twoMon);
    }

    [Fact]
    public void Different_Bounds_Yield_Different_Fingerprint()
    {
        var rotated = A with { Width = 1080, Height = 1920 };
        var f1 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A });
        var f2 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { rotated });
        Assert.NotEqual(f1, f2);
    }

    [Fact]
    public void DisplayName_Is_Used_When_Available()
    {
        var fp = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A });
        Assert.Contains("Dell U2719D", fp);
    }

    [Fact]
    public void Falls_Back_To_Bounds_When_DisplayName_Missing()
    {
        var fp = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { C });
        Assert.Contains("bounds=0,0,1920x1080", fp);
        Assert.DoesNotContain("name=", fp);
    }

    [Fact]
    public void Empty_List_Yields_Sentinel()
    {
        var fp = WindowPositionPolicy.ComputeLayoutFingerprint(Array.Empty<ScreenInfo>());
        Assert.Equal(WindowPositionPolicy.EmptyLayoutFingerprint, fp);
    }

    [Fact]
    public void Primary_Flag_Is_Reflected()
    {
        var nonPrimary = A with { IsPrimary = false };
        var f1 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { A });
        var f2 = WindowPositionPolicy.ComputeLayoutFingerprint(new[] { nonPrimary });
        Assert.NotEqual(f1, f2);
    }
}
