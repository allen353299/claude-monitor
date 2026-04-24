using System;
using Avalonia.Media;
using ClaudeMonitor.App.Theme;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class SessionVm : ObservableObject
{
    private static readonly TimeSpan DoneRecencyWindow = TimeSpan.FromHours(1);

    [ObservableProperty] private SessionId _id;
    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private string _projectPath = "";
    [ObservableProperty] private string? _customTitle;
    [ObservableProperty] private string? _firstPrompt;
    [ObservableProperty] private string? _latestMessage;
    [ObservableProperty] private string? _recap;
    [ObservableProperty] private SessionStatus _status;
    [ObservableProperty] private string? _pendingTool;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private DateTimeOffset _lastActivity;
    [ObservableProperty] private bool _isCompact;

    private int? _acknowledgedMessageCount;

    public double CardOpacity => IsLive ? 1.0 : 0.4;

    public bool IsUrgent => Status == SessionStatus.NeedsAttention;

    public bool IsWorking => Status == SessionStatus.Working;

    public bool IsDoneRecent => Status == SessionStatus.WaitingForInput
        && (DateTimeOffset.UtcNow - LastActivity) < DoneRecencyWindow;

    private bool IsAcknowledged => _acknowledgedMessageCount.HasValue
        && _acknowledgedMessageCount.Value == MessageCount;

    public bool ShouldGlowUrgent => IsUrgent && !IsAcknowledged;

    public bool ShouldGlowDone => IsDoneRecent && !IsAcknowledged;

    public void Acknowledge()
    {
        _acknowledgedMessageCount = MessageCount;
        OnPropertyChanged(nameof(ShouldGlowUrgent));
        OnPropertyChanged(nameof(ShouldGlowDone));
    }

    public void NotifyTimeBasedProperties()
    {
        OnPropertyChanged(nameof(IsDoneRecent));
        OnPropertyChanged(nameof(ShouldGlowDone));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusColor));
    }

    public string StatusLabel
    {
        get
        {
            if (Status == SessionStatus.Working) return "Processing";
            if (Status == SessionStatus.NeedsAttention) return "Needs attention";
            if (Status == SessionStatus.WaitingForInput)
                return IsDoneRecent ? "Done" : "Idle";
            return "Connecting";
        }
    }

    public IBrush StatusColor => Status switch
    {
        SessionStatus.Working => StatusPalette.Working,
        SessionStatus.NeedsAttention => StatusPalette.Attention,
        SessionStatus.WaitingForInput => IsDoneRecent ? StatusPalette.Done : StatusPalette.Idle,
        _ => StatusPalette.Idle,
    };

    public string? TitleOrFirstPrompt => !string.IsNullOrWhiteSpace(CustomTitle) ? CustomTitle : FirstPrompt;

    public bool HasTitleOrFirstPrompt => !string.IsNullOrWhiteSpace(TitleOrFirstPrompt);

    public bool ShowTitleOrFirstPrompt => !IsCompact && HasTitleOrFirstPrompt;

    public string PrimaryLabel
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomTitle)) return CustomTitle!;
            if (!string.IsNullOrWhiteSpace(FirstPrompt)) return FirstPrompt!;
            return SessionName;
        }
    }

    public bool HasProjectPath => !string.IsNullOrWhiteSpace(ProjectPath);

    public bool ShowProjectPath => !IsCompact && HasProjectPath;

    public bool HasLatestMessage => !string.IsNullOrWhiteSpace(LatestMessage);

    public string? RecapOrLatest => !string.IsNullOrWhiteSpace(Recap) ? Recap : LatestMessage;

    public bool HasRecapOrLatest => !string.IsNullOrWhiteSpace(RecapOrLatest);

    public bool ShowRecapOrLatest => !IsCompact && HasRecapOrLatest;

    partial void OnIsCompactChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTitleOrFirstPrompt));
        OnPropertyChanged(nameof(ShowRecapOrLatest));
        OnPropertyChanged(nameof(ShowProjectPath));
    }

    public bool IsRecap => !string.IsNullOrWhiteSpace(Recap);

    public void UpdateFrom(Session s)
    {
        SessionName = s.SessionName;
        ProjectPath = s.ProjectPath.Value ?? "";
        CustomTitle = s.CustomTitle?.Value;
        FirstPrompt = s.FirstPrompt?.Value;
        LatestMessage = s.LatestMessage?.Value;
        Recap = s.Recap?.Value;
        Status = s.Status;
        PendingTool = s.PendingTool;
        MessageCount = s.MessageCount;
        IsLive = s.IsLive;
        LastActivity = s.LastActivity;
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(TitleOrFirstPrompt));
        OnPropertyChanged(nameof(HasTitleOrFirstPrompt));
        OnPropertyChanged(nameof(ShowTitleOrFirstPrompt));
        OnPropertyChanged(nameof(HasProjectPath));
        OnPropertyChanged(nameof(ShowProjectPath));
        OnPropertyChanged(nameof(PrimaryLabel));
        OnPropertyChanged(nameof(HasLatestMessage));
        OnPropertyChanged(nameof(RecapOrLatest));
        OnPropertyChanged(nameof(HasRecapOrLatest));
        OnPropertyChanged(nameof(ShowRecapOrLatest));
        OnPropertyChanged(nameof(IsRecap));
        OnPropertyChanged(nameof(IsUrgent));
        OnPropertyChanged(nameof(IsWorking));
        OnPropertyChanged(nameof(IsDoneRecent));
        OnPropertyChanged(nameof(ShouldGlowUrgent));
        OnPropertyChanged(nameof(ShouldGlowDone));
    }
}
