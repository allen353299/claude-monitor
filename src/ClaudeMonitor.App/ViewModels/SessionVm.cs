using System;
using Avalonia.Media;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class SessionVm : ObservableObject
{
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private string? _customTitle;
    [ObservableProperty] private string? _firstPrompt;
    [ObservableProperty] private string? _latestMessage;
    [ObservableProperty] private string? _recap;
    [ObservableProperty] private SessionStatus _status;
    [ObservableProperty] private string? _pendingTool;
    [ObservableProperty] private int _messageCount;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private DateTimeOffset _lastActivity;

    public double CardOpacity => IsLive ? 1.0 : 0.4;

    public bool IsUrgent => Status == SessionStatus.NeedsAttention;

    public bool IsDoneRecent => Status == SessionStatus.WaitingForInput
        && (DateTimeOffset.UtcNow - LastActivity) < TimeSpan.FromHours(1);

    public void NotifyTimeBasedProperties()
    {
        OnPropertyChanged(nameof(IsDoneRecent));
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

    public IBrush StatusColor
    {
        get
        {
            if (Status == SessionStatus.Working)
                return new SolidColorBrush(Color.Parse("#22c55e"));
            if (Status == SessionStatus.NeedsAttention)
                return new SolidColorBrush(Color.Parse("#ff6600"));
            if (Status == SessionStatus.WaitingForInput)
                return new SolidColorBrush(Color.Parse(IsDoneRecent ? "#06b6d4" : "#666666"));
            return new SolidColorBrush(Color.Parse("#666666"));
        }
    }

    public string? TitleOrFirstPrompt => !string.IsNullOrWhiteSpace(CustomTitle) ? CustomTitle : FirstPrompt;

    public bool HasTitleOrFirstPrompt => !string.IsNullOrWhiteSpace(TitleOrFirstPrompt);

    public bool HasLatestMessage => !string.IsNullOrWhiteSpace(LatestMessage);

    public string? RecapOrLatest => !string.IsNullOrWhiteSpace(Recap) ? Recap : LatestMessage;

    public bool HasRecapOrLatest => !string.IsNullOrWhiteSpace(RecapOrLatest);

    public bool IsRecap => !string.IsNullOrWhiteSpace(Recap);

    public void UpdateFrom(Session s)
    {
        SessionName = s.SessionName;
        CustomTitle = s.CustomTitle;
        FirstPrompt = s.FirstPrompt;
        LatestMessage = s.LatestMessage;
        Recap = s.Recap;
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
        OnPropertyChanged(nameof(HasLatestMessage));
        OnPropertyChanged(nameof(RecapOrLatest));
        OnPropertyChanged(nameof(HasRecapOrLatest));
        OnPropertyChanged(nameof(IsRecap));
        OnPropertyChanged(nameof(IsUrgent));
        OnPropertyChanged(nameof(IsDoneRecent));
    }
}
