using ClaudeMonitor.Platform.Windows;
using static ClaudeMonitor.Platform.Windows.WindowsSessionActivator;

namespace ClaudeMonitor.Platform.Windows.UnitTests;

public class WindowCandidateScoringTests
{
    private static WindowCandidate Cand(
        RelationKind kind,
        int depth,
        bool titleHit,
        long area = 1000 * 700,
        bool claudeTitle = false)
        => new WindowCandidate(new IntPtr(1), kind, depth, titleHit, claudeTitle, area);

    [Fact]
    public void Ancestor_WithoutTitleHit_BeatsTitleOnly_WithTitleHit()
    {
        // Powershell session: conhost is AncestorChild without title hit;
        // an unrelated Rider window happens to have project name in title.
        // The shell wins — process tree is stronger signal than name overlap.
        var conhost = Cand(RelationKind.AncestorChild, depth: 1, titleHit: false);
        var rider   = Cand(RelationKind.TitleOnly,     depth: int.MaxValue, titleHit: true);
        Assert.True(conhost.IsBetterThan(rider));
        Assert.False(rider.IsBetterThan(conhost));
    }

    [Fact]
    public void CloserAncestorChild_BeatsFartherAncestor()
    {
        // Standalone PowerShell with Rider (or any IDE) higher in the chain —
        // the depth-1 conhost child of pwsh must beat the depth-2 Rider
        // ancestor. Process-tree distance trumps the Ancestor/AncestorChild
        // distinction.
        var conhost = Cand(RelationKind.AncestorChild, depth: 1, titleHit: false);
        var rider   = Cand(RelationKind.Ancestor,      depth: 2, titleHit: false);
        Assert.True(conhost.IsBetterThan(rider));
        Assert.False(rider.IsBetterThan(conhost));
    }

    [Fact]
    public void WithinSameKind_TitleHitWins()
    {
        // Two Rider JVM windows both in ancestor chain (multi-project Rider);
        // the one whose title contains the project hint should win.
        var hit  = Cand(RelationKind.Ancestor, depth: 3, titleHit: true);
        var miss = Cand(RelationKind.Ancestor, depth: 3, titleHit: false);
        Assert.True(hit.IsBetterThan(miss));
    }

    [Fact]
    public void WithinSameKindAndTitle_CloserDepthWins()
    {
        var nearer = Cand(RelationKind.Ancestor, depth: 1, titleHit: true);
        var farther = Cand(RelationKind.Ancestor, depth: 4, titleHit: true);
        Assert.True(nearer.IsBetterThan(farther));
    }

    [Fact]
    public void TitleOnly_BeatsClaudeTitleOnly()
    {
        var titleOnly = Cand(RelationKind.TitleOnly, depth: int.MaxValue, titleHit: true);
        var claude    = Cand(RelationKind.ClaudeTitleOnly, depth: int.MaxValue, titleHit: false, claudeTitle: true);
        Assert.True(titleOnly.IsBetterThan(claude));
    }

    [Fact]
    public void AreaTiebreak_LargerWins()
    {
        var big   = Cand(RelationKind.Ancestor, depth: 2, titleHit: false, area: 1920 * 1080);
        var small = Cand(RelationKind.Ancestor, depth: 2, titleHit: false, area: 800 * 600);
        Assert.True(big.IsBetterThan(small));
    }
}

public class BuildRelatedProcessMapTests
{
    /// <summary>
    /// Powershell standalone scenario:
    ///   csrss(2) <- powershell(10) <- claude(20)
    ///                  └── conhost(30)
    /// claude is in pwsh's ancestor chain; conhost is reparented to pwsh
    /// (modern Windows). Expect conhost included as AncestorChild.
    /// </summary>
    [Fact]
    public void Conhost_AsDirectChildOfShell_IncludedAsAncestorChild()
    {
        var parents = new Dictionary<int, int>
        {
            [20] = 10,  // claude -> powershell
            [10] = 2,   // powershell -> csrss (excluded)
            [30] = 10,  // conhost -> powershell (reparented)
            [2] = 0,
        };
        var names = new Dictionary<int, string>
        {
            [20] = "node.exe",
            [10] = "powershell.exe",
            [30] = "conhost.exe",
            [2] = "csrss.exe",
        };
        var related = WindowsSessionActivator.BuildRelatedProcessMap(20, parents, names);

        Assert.True(related.ContainsKey(20));
        Assert.True(related.ContainsKey(10));
        Assert.False(related.ContainsKey(2)); // csrss is excluded
        Assert.True(related.ContainsKey(30)); // conhost via descendant walk
        Assert.Equal(WindowsSessionActivator.RelationKind.AncestorChild, related[30].Kind);
    }

    /// <summary>
    /// Windows Terminal scenario:
    ///   WindowsTerminal(100) <- OpenConsole(110) <- pwsh(120) <- claude(130)
    /// All are direct ancestors of claude.
    /// </summary>
    [Fact]
    public void WindowsTerminalChain_AllListedAsAncestor()
    {
        var parents = new Dictionary<int, int>
        {
            [130] = 120,
            [120] = 110,
            [110] = 100,
            [100] = 0,
        };
        var names = new Dictionary<int, string>
        {
            [130] = "node.exe",
            [120] = "pwsh.exe",
            [110] = "OpenConsole.exe",
            [100] = "WindowsTerminal.exe",
        };
        var related = WindowsSessionActivator.BuildRelatedProcessMap(130, parents, names);
        Assert.Equal(WindowsSessionActivator.RelationKind.Ancestor, related[100].Kind);
        Assert.Equal(WindowsSessionActivator.RelationKind.Ancestor, related[110].Kind);
        Assert.Equal(WindowsSessionActivator.RelationKind.Ancestor, related[120].Kind);
        Assert.Equal(WindowsSessionActivator.RelationKind.Ancestor, related[130].Kind);
    }

    /// <summary>
    /// Transitive descendant (depth 2 from shell):
    ///   shell <- helper <- conhost
    /// Walk should still reach conhost.
    /// </summary>
    [Fact]
    public void TransitiveDescendant_OfAncestor_Included()
    {
        var parents = new Dictionary<int, int>
        {
            [50] = 40,
            [40] = 30,  // helper -> shell
            [30] = 20,  // shell -> claude's parent? Actually let's set claude=10, shell=20.
            [10] = 20,  // claude -> shell
            [20] = 0,   // shell root
        };
        var names = new Dictionary<int, string>
        {
            [10] = "node.exe",
            [20] = "pwsh.exe",
            [30] = "child1.exe",
            [40] = "helper.exe",
            [50] = "conhost.exe",
        };
        var related = WindowsSessionActivator.BuildRelatedProcessMap(10, parents, names);
        Assert.Contains(50, related.Keys);
    }

    /// <summary>
    /// Cycles in the parent map (defensive — should not infinite-loop).
    /// </summary>
    [Fact]
    public void CyclicParent_DoesNotLoop()
    {
        var parents = new Dictionary<int, int> { [10] = 20, [20] = 10 };
        var names = new Dictionary<int, string> { [10] = "a.exe", [20] = "b.exe" };
        var related = WindowsSessionActivator.BuildRelatedProcessMap(10, parents, names);
        Assert.Equal(2, related.Count);
    }
}
