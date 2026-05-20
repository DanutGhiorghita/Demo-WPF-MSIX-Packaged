using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Demo_WPF_MSIX_Packaged
{
    public partial class EnvironmentPathsWindow : Window
    {
        private List<EnvEntry> _allEntries = [];
        private GridViewColumnHeader? _lastSortHeader;
        private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;

        public EnvironmentPathsWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => LoadEntries();
        }

        private void LoadEntries()
        {
            _allEntries = [];

            foreach (EnvironmentVariableTarget scope in Enum.GetValues<EnvironmentVariableTarget>())
            {
                IDictionary vars;
                try { vars = Environment.GetEnvironmentVariables(scope); }
                catch { continue; }

                foreach (DictionaryEntry kv in vars)
                {
                    var value = kv.Value?.ToString() ?? string.Empty;
                    if (ContainsPathLike(value))
                        _allEntries.Add(new EnvEntry
                        {
                            Name = kv.Key.ToString() ?? string.Empty,
                            Value = value,
                            Scope = scope.ToString()
                        });
                }
            }

            // Deduplicate: prefer Machine scope entry when value is identical
            _allEntries = _allEntries
                .GroupBy(e => e.Name.ToUpperInvariant())
                .Select(g => g.OrderBy(e => e.Scope).First())
                .OrderBy(e => e.Name)
                .ToList();

            ApplyFilter();
        }

        private static bool ContainsPathLike(string value)
        {
            // Treat the value as a semicolon-separated list and check each token
            foreach (var token in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var t = token.Trim();
                if (t.Length < 2) continue;

                // Rooted local path (C:\...) or UNC path (\\server\share)
                if (Path.IsPathRooted(t))
                    return true;

                // Paths containing path separators or percent-expanded env refs
                if (t.Contains('\\') || t.Contains('%'))
                    return true;
            }
            return false;
        }

        private void ApplyFilter()
        {
            var filter = FilterTextBox.Text.Trim();
            var view = CollectionViewSource.GetDefaultView(
                string.IsNullOrEmpty(filter)
                    ? _allEntries
                    : _allEntries.Where(e =>
                        e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList());

            EnvListView.ItemsSource = view;

            // Re-apply last sort if any
            if (_lastSortHeader?.Tag is string prop)
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(prop, _lastSortDirection));
            }

            var count = (view as ICollectionView)?.Cast<object>().Count() ?? _allEntries.Count;
            StatusTextBlock.Text = $"{count} variable(s) shown.";
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void EnvListView_ColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader header || header.Tag is not string propertyName)
                return;

            var direction = (_lastSortHeader == header && _lastSortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            var view = CollectionViewSource.GetDefaultView(EnvListView.ItemsSource);
            if (view == null) return;

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(propertyName, direction));
            view.Refresh();

            ClearSortAdorner();
            var layer = AdornerLayer.GetAdornerLayer(header);
            if (layer != null)
                layer.Add(new SortArrowAdorner(header, direction));

            _lastSortHeader = header;
            _lastSortDirection = direction;
        }

        private void ClearSortAdorner()
        {
            if (_lastSortHeader == null) return;
            var layer = AdornerLayer.GetAdornerLayer(_lastSortHeader);
            if (layer != null)
                foreach (var a in layer.GetAdorners(_lastSortHeader)?.OfType<SortArrowAdorner>() ?? [])
                    layer.Remove(a);
            _lastSortHeader = null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class EnvEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}
