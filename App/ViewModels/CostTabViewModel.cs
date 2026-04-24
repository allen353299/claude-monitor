using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class CostTabViewModel : ObservableObject
{
    private readonly ICostReader _reader;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GrandTotalText))]
    private double _grandTotalUsd;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ObservableCollection<SessionCostVm> Sessions { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public string GrandTotalText => $"${GrandTotalUsd:0.00}";

    public CostTabViewModel(ICostReader reader)
    {
        _reader = reader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var report = await _reader.ReadAsync();
            Sessions.Clear();
            foreach (var s in report.Sessions) Sessions.Add(new SessionCostVm(s));
            GrandTotalUsd = report.GrandTotalUsd;
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        catch (JsonException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public sealed class SessionCostVm
{
    private readonly SessionCost _cost;
    public SessionCostVm(SessionCost cost) => _cost = cost;

    public string SessionId => _cost.Id.Value;
    public string ProjectPath => _cost.ProjectPath;
    public string Model => _cost.Model;
    public string UsdText => $"${_cost.UsdTotal:0.0000}";
    public string InputText => $"{_cost.InputTokens:N0}";
    public string OutputText => $"{_cost.OutputTokens:N0}";
    public string CacheReadText => $"{_cost.CacheReadTokens:N0}";
    public string CacheWriteText => $"{_cost.CacheWriteTokens:N0}";
    public string LastActivityText =>
        _cost.LastActivity == DateTimeOffset.MinValue
            ? "—"
            : _cost.LastActivity.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
