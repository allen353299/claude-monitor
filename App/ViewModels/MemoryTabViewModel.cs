using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ClaudeMonitor.Core.Sessions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudeMonitor.App.ViewModels;

public sealed partial class MemoryTabViewModel : ObservableObject
{
    private readonly IMemoryReader _reader;

    [ObservableProperty] private MemoryProjectVm? _selectedProject;
    [ObservableProperty] private MemoryFileVm? _selectedFile;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public ObservableCollection<MemoryProjectVm> Projects { get; } = new();

    public IAsyncRelayCommand RefreshCommand { get; }

    public MemoryTabViewModel(IMemoryReader reader)
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
            var projects = await _reader.ReadAsync();
            Projects.Clear();
            foreach (var p in projects) Projects.Add(new MemoryProjectVm(p));
            SelectedProject = Projects.Count > 0 ? Projects[0] : null;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedProjectChanged(MemoryProjectVm? value)
    {
        SelectedFile = value?.Files.Count > 0 ? value.Files[0] : null;
    }
}

public sealed class MemoryProjectVm
{
    private readonly MemoryProject _project;

    public MemoryProjectVm(MemoryProject project)
    {
        _project = project;
        Files = new ObservableCollection<MemoryFileVm>();
        foreach (var f in project.Files) Files.Add(new MemoryFileVm(f));
    }

    public string DisplayPath => _project.DecodedPath;
    public int FileCount => _project.Files.Count;
    public string FileCountText => $"{FileCount} file{(FileCount == 1 ? "" : "s")}";
    public ObservableCollection<MemoryFileVm> Files { get; }
}

public sealed class MemoryFileVm
{
    private readonly MemoryFile _file;
    public MemoryFileVm(MemoryFile file) => _file = file;
    public string FileName => _file.FileName;
    public string FullPath => _file.FullPath;
    public string Content => _file.Content;
}
