using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ClaudeMonitor.Core;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class HistoryTabViewModel : ObservableObject
{
    private readonly IHistoryReader _historyReader;
    private readonly ICostReader _costReader;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private double _grandTotalUsd;

    public string GrandTotalText => $"${GrandTotalUsd:0.00}";

    public ObservableCollection<HistoryItemVm> AllItems { get; } = new();
    public ObservableCollection<HistoryItemVm> FilteredItems { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public HistoryTabViewModel(IHistoryReader historyReader, ICostReader costReader)
    {
        _historyReader = historyReader;
        _costReader = costReader;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var historyTask = _historyReader.ReadAsync();
            var costTask = _costReader.ReadAsync();
            await Task.WhenAll(historyTask, costTask);

            var costsBySession = new Dictionary<SessionId, SessionCost>();
            foreach (var c in costTask.Result.Sessions) costsBySession[c.Id] = c;

            AllItems.Clear();
            foreach (var entry in historyTask.Result)
            {
                costsBySession.TryGetValue(entry.Id, out var cost);
                AllItems.Add(new HistoryItemVm(entry, cost));
            }

            GrandTotalUsd = costTask.Result.GrandTotalUsd;
            OnPropertyChanged(nameof(GrandTotalText));
            ApplyFilter();
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var q = SearchText?.Trim();
        foreach (var item in AllItems)
        {
            if (string.IsNullOrEmpty(q) || item.Matches(q)) FilteredItems.Add(item);
        }
    }
}

public sealed class HistoryItemVm
{
    private readonly HistoryEntry _entry;
    private readonly SessionCost? _cost;

    public HistoryItemVm(HistoryEntry entry, SessionCost? cost = null)
    {
        _entry = entry;
        _cost = cost;
    }

    public SessionId Id => _entry.Id;
    public string SessionId => _entry.Id.Value;
    public string ProjectPath => _entry.ProjectPath;
    public string Display => _entry.LatestDisplay;
    public string LastActivityText => _entry.LastActivityAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public int MessageCount => _entry.MessageCount;
    public JsonlPath? JsonlPath => _entry.JsonlPath;
    public bool HasJsonlPath => _entry.JsonlPath.HasValue;

    public string PrimaryLabel => !string.IsNullOrWhiteSpace(_entry.CustomTitle)
        ? _entry.CustomTitle!
        : _entry.LatestDisplay;

    public string DetailLine => !string.IsNullOrWhiteSpace(_entry.Recap)
        ? _entry.Recap!
        : _entry.LatestDisplay;

    public string MetaLine => $"{_entry.Id.Value}  ·  {_entry.MessageCount} msg  ·  {LastActivityText}";

    public string UsdText => _cost is null ? "—" : $"${_cost.UsdTotal:0.00}";
    public string TokensText => _cost is null
        ? ""
        : $"{(_cost.InputTokens + _cost.OutputTokens + _cost.CacheReadTokens + _cost.CacheWriteTokens):N0} tok";
    public bool HasCost => _cost is not null && _cost.UsdTotal > 0;

    public bool Matches(string query) =>
        (_entry.CustomTitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (_entry.LatestDisplay?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (_entry.ProjectPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || _entry.Id.Value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
