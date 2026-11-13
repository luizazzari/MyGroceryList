using System.Collections.ObjectModel;

namespace MyGroceryList.Mobile.ViewModels;

internal static class CollectionChanges
{
    public static void Reconcile<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
    {
        for (var i = target.Count - 1; i >= 0; i--)
            if (!desired.Contains(target[i])) target.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            if (i < target.Count && EqualityComparer<T>.Default.Equals(target[i], desired[i])) continue;
            var existing = target.IndexOf(desired[i]);
            if (existing >= 0) target.Move(existing, i);
            else target.Insert(i, desired[i]);
        }
    }
}
