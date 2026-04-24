using System.Collections.ObjectModel;
using System.Linq;
using ClaudeMonitor.Core;

namespace ClaudeMonitor.App.ViewModels;

public sealed class SessionVmCollection : ObservableCollection<SessionVm>
{
    public SessionVm? FindById(SessionId id) => this.FirstOrDefault(v => v.Id == id);

    public bool RemoveById(SessionId id)
    {
        var existing = FindById(id);
        if (existing is null) return false;
        return Remove(existing);
    }
}
