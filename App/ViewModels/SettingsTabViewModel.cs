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

    private static readonly HttpClient SharedHttp = CreateHttpClient();

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
            var json = await SharedHttp.GetStringAsync(ReleasesApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.GetProperty("html_url").GetString();
            LatestVersion = tag.TrimStart('v', 'V');
            LatestReleaseUrl = url;
            HasUpdate = IsNewerVersion(LatestVersion, CurrentVersion);
            UpdateStatus = HasUpdate ? "Update available" : "Up to date";
        }
        catch (HttpRequestException ex)
        {
            UpdateStatus = $"Network error: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            UpdateStatus = "Check timed out";
        }
        catch (JsonException)
        {
            UpdateStatus = "Invalid response";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var l)) return false;
        if (!Version.TryParse(current, out var c)) return true;
        return Normalize(l) > Normalize(c);
    }

    private static Version Normalize(Version v) =>
        new(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build, v.Revision < 0 ? 0 : v.Revision);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeMonitor");
        return client;
    }
}
