using System.Collections.Generic;
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

    public void Sort(IComparer<SessionVm> comparer)
    {
        var ordered = this.OrderBy(v => v, comparer).ToList();
        for (int target = 0; target < ordered.Count; target++)
        {
            var desired = ordered[target];
            if (!ReferenceEquals(this[target], desired))
            {
                Move(IndexOf(desired), target);
            }
        }
    }
}
