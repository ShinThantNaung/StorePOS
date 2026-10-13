using System.ComponentModel;
using System.Windows.Controls;

namespace StorePOS.Infrastructure;

/// <summary>
/// Keeps a DataGrid's ▲/▼ arrows in step with the sort of the view it shows. The DataGrid
/// clears column arrows when it attaches to a view, so screens call Sync() when loaded.
/// </summary>
public static class GridSort
{
    public static void Sync(DataGrid grid)
    {
        if (grid.ItemsSource is not ICollectionView view) return;
        var first = view.SortDescriptions.Count > 0 ? view.SortDescriptions[0] : (SortDescription?)null;
        foreach (var column in grid.Columns)
            column.SortDirection = first != null && column.SortMemberPath == first.Value.PropertyName
                ? first.Value.Direction
                : null;
    }

    /// <summary>Single-column sort: a click sorts by that column, a second click reverses.</summary>
    public static void OnSorting(DataGrid grid, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (grid.ItemsSource is not ICollectionView view) return;
        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(e.Column.SortMemberPath, direction));
        }
        Sync(grid);
    }
}
