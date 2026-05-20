using System.Text;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Demo_WPF_MSIX_Packaged
{
    public partial class MainWindow : Window
    {
        private GridViewColumnHeader? _lastSortHeader;
        private ListSortDirection _lastSortDirection = ListSortDirection.Ascending;
        private AdornerLayer? _adornerLayer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select a folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                FolderPathTextBox.Text = dialog.FolderName;
                ListFilesButton.IsEnabled = true;
                FilesListView.ItemsSource = null;
                StatusTextBlock.Text = string.Empty;
                ClearSortAdorner();
            }
        }

        private void ListFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPath = FolderPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(folderPath))
                return;

            try
            {
                var entries = new DirectoryInfo(folderPath)
                    .GetFileSystemInfos()
                    .Select(info => new FileEntry
                    {
                        Name = info.Name,
                        Size = info is FileInfo fi ? fi.Length : (long?)null,
                        LastModified = info.LastWriteTime,
                        Type = info is DirectoryInfo ? "Folder" : Path.GetExtension(info.Name).TrimStart('.').ToUpperInvariant() + " File"
                    })
                    .OrderBy(x => x.Type == "Folder" ? 0 : 1)
                    .ThenBy(x => x.Name)
                    .ToList();

                FilesListView.ItemsSource = entries;
                StatusTextBlock.Text = $"{entries.Count} item(s) found.";
                ClearSortAdorner();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error: {ex.Message}";
            }
        }

        private void EnvPathsButton_Click(object sender, RoutedEventArgs e)
        {
            new EnvironmentPathsWindow { Owner = this }.ShowDialog();
        }

        private void FilesListView_ColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader header || header.Tag is not string propertyName)
                return;

            var direction = (_lastSortHeader == header && _lastSortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            var view = CollectionViewSource.GetDefaultView(FilesListView.ItemsSource);
            if (view == null) return;

            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(propertyName, direction));
            view.Refresh();

            // Update adorner
            ClearSortAdorner();
            _adornerLayer = AdornerLayer.GetAdornerLayer(header);
            if (_adornerLayer != null)
            {
                var adorner = new SortArrowAdorner(header, direction);
                _adornerLayer.Add(adorner);
            }

            _lastSortHeader = header;
            _lastSortDirection = direction;
        }

        private void ClearSortAdorner()
        {
            if (_lastSortHeader == null) return;
            var layer = AdornerLayer.GetAdornerLayer(_lastSortHeader);
            if (layer != null)
            {
                var adorners = layer.GetAdorners(_lastSortHeader);
                if (adorners != null)
                    foreach (var a in adorners.OfType<SortArrowAdorner>())
                        layer.Remove(a);
            }
            _lastSortHeader = null;
        }
    }

    public class FileEntry
    {
        public string Name { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    internal sealed class SortArrowAdorner : Adorner
    {
        private readonly ListSortDirection _direction;

        public SortArrowAdorner(UIElement element, ListSortDirection direction) : base(element)
        {
            _direction = direction;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext ctx)
        {
            base.OnRender(ctx);
            if (AdornedElement is not FrameworkElement fe) return;

            const double size = 6;
            double x = fe.ActualWidth - size - 4;
            double y = (fe.ActualHeight - size) / 2;

            var points = _direction == ListSortDirection.Ascending
                ? new[] { new Point(x, y + size), new Point(x + size, y + size), new Point(x + size / 2, y) }
                : new[] { new Point(x, y), new Point(x + size, y), new Point(x + size / 2, y + size) };

            var geo = new StreamGeometry();
            using (var sc = geo.Open())
            {
                sc.BeginFigure(points[0], isFilled: true, isClosed: true);
                sc.LineTo(points[1], isStroked: false, isSmoothJoin: false);
                sc.LineTo(points[2], isStroked: false, isSmoothJoin: false);
            }
            geo.Freeze();
            ctx.DrawGeometry(Brushes.Black, null, geo);
        }
    }
}