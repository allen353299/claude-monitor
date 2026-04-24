using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class SettingsTabViewModel : ObservableObject
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/allen353299/claude-monitor/releases/latest";

    [ObservableProperty] private string _currentVersion;
    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private string? _latestReleaseUrl;
    [ObservableProperty] private string? _updateStatus;
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _hasUpdate;

    public IAsyncRelayCommand CheckForUpdateCommand { get; }

    public SettingsTabViewModel()
    {
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        CheckForUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);
    }

    public async Task CheckForUpdateAsync()
    {
        IsChecking = true;
        UpdateStatus = "Checking...";
        HasUpdate = false;
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeMonitor");
            http.Timeout = TimeSpan.FromSeconds(10);
            var json = await http.GetStringAsync(ReleasesApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.GetProperty("html_url").GetString();
            LatestVersion = tag.TrimStart('v');
            LatestReleaseUrl = url;
            HasUpdate = !string.Equals(LatestVersion, CurrentVersion, StringComparison.OrdinalIgnoreCase);
            UpdateStatus = HasUpdate ? "Update available" : "Up to date";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Check failed: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }
}
