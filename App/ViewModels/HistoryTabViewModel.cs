using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class HistoryTabViewModel : ObservableObject
{
    private readonly IHistoryReader _reader;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ObservableCollection<HistoryItemVm> AllItems { get; } = new();
    public ObservableCollection<HistoryItemVm> FilteredItems { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public HistoryTabViewModel(IHistoryReader reader)
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
            var entries = await _reader.ReadAsync();
            AllItems.Clear();
            foreach (var e in entries) AllItems.Add(new HistoryItemVm(e));
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

    public HistoryItemVm(HistoryEntry entry) => _entry = entry;

    public string SessionId => _entry.Id.Value;
    public string ProjectPath => _entry.ProjectPath;
    public string Display => _entry.LatestDisplay;
    public string LastActivityText => _entry.LastActivityAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public int MessageCount => _entry.MessageCount;

    public bool Matches(string query) =>
        (_entry.LatestDisplay?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (_entry.ProjectPath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || _entry.Id.Value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
