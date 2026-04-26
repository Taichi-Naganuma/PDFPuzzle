using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static PDFPuzzle.MainWindow;

namespace PDFPuzzle
{
    public class ItemService : BaseItemsDefine
    {
        public void WhileDropCopy(DragEventArgs e, Type type)
        {
            e.Effects = e.Data.GetDataPresent(type) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        public void WhileDropNone(DragEventArgs e, Type type)
        {
            if (e.Data.GetDataPresent(type))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        public static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public int GetUnderIndex(ListBox listBox, Point point, ObservableCollection<ExeItem> collection)
        {
            HitTestResult result = VisualTreeHelper.HitTest(listBox, point);
            ListBoxItem? item = FindAncestor<ListBoxItem>(result?.VisualHit);
            if (item?.DataContext is ExeItem data)
                return collection.IndexOf(data);
            return 0;
        }

        public int GetFileIndex(ListBox listBox, Point point, ObservableCollection<FileItem> collection)
        {
            HitTestResult result = VisualTreeHelper.HitTest(listBox, point);
            ListBoxItem? item = FindAncestor<ListBoxItem>(result?.VisualHit);
            if (item?.DataContext is FileItem data)
                return collection.IndexOf(data);
            return 0;
        }

        public ExeItem CreatePlaceholder() => new ExeItem { DisplayName = "" };
        public FileItem Createfileplace() => new FileItem { DisplayName = "" };

        public ExeItem? ConvertToExeitem(MethodItem? item)
        {
            if (item == null) return null;
            return new ExeItem { DisplayName = item.DisplayName, DisplayNameKey = item.DisplayNameKey, ExecuteAsync = item.ExecuteAsync };
        }

        public void MovePlaceholder(ExeItem placeholder, ListBox listbox, Point pos, int targetIndex, ObservableCollection<ExeItem> collection, DragEventArgs e)
        {
            if (placeholder == null) return;
            int currentIndex = collection.IndexOf(placeholder);
            if (targetIndex < 0 || targetIndex == currentIndex) return;
            if (currentIndex < 0) return;
            if (targetIndex >= collection.Count) targetIndex = collection.Count - 1;
            collection.Move(currentIndex, targetIndex);
            listbox.SelectedIndex = targetIndex;
        }

        public void InsertUnderCursor(ListBox listBox, Point position, ExeItem? insertItem, ExeItem placeholder, ObservableCollection<ExeItem> collection)
        {
            int underIndex = GetUnderIndex(listBox, position, collection);
            if (underIndex != -1 && insertItem != null)
            {
                collection.Remove(insertItem);
                collection.Insert(underIndex, insertItem);
            }
            Removeplaceholder(placeholder, collection);
        }

        public void Removeplaceholder(ExeItem? placeholder, ObservableCollection<ExeItem> collection)
        {
            if (placeholder != null) collection.Remove(placeholder);
        }

        public void ShowInsertLine(Point mousePos, int targetIndex, ListBox listBox, FileItem placeholder, ObservableCollection<FileItem> items, DragEventArgs e)
        {
            if (targetIndex == 0)
            {
                HitTestResult result = VisualTreeHelper.HitTest(listBox, mousePos);
                ListBoxItem? item = FindAncestor<ListBoxItem>(result?.VisualHit);
                if (item != null && placeholder != null)
                {
                    int currentIndex = items.IndexOf(placeholder);
                    if (currentIndex != -1) { items.Move(currentIndex, 0); listBox.SelectedIndex = 0; }
                }
            }
            else if (targetIndex > 0 && placeholder != null)
            {
                Movefileplace(placeholder, targetIndex, mousePos, listBox, items, e);
            }
        }

        public void Movefileplace(FileItem placeholder, int targetIndex, Point point, ListBox listbox, ObservableCollection<FileItem> collection, DragEventArgs e)
        {
            if (placeholder == null) return;
            int currentIndex = collection.IndexOf(placeholder);
            if (targetIndex < 0 || targetIndex == currentIndex) return;
            if (targetIndex >= collection.Count) targetIndex = collection.Count - 1;
            if (targetIndex == -1 || currentIndex == -1) return;
            collection.Move(currentIndex, targetIndex);
            listbox.SelectedIndex = targetIndex;
        }

        public void InsertUnderfile(ListBox listBox, Point position, int underIndex, FileItem? insertItem, FileItem? placeholder, ObservableCollection<FileItem> collection)
        {
            if (underIndex != -1 && insertItem != null)
            {
                collection.Remove(insertItem);
                collection.Insert(underIndex, insertItem);
            }
            Removefileplace(placeholder, collection);
        }

        public void Removefileplace(FileItem? placeholder, ObservableCollection<FileItem> collection)
        {
            if (placeholder != null) collection.Remove(placeholder);
        }

        public string[]? SelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDFファイル (*.pdf)|*.pdf",
                Multiselect = true,
                Title = "PDFファイルを選択"
            };
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        public string? SelectFolder()
        {
            var dialog = new OpenFolderDialog();
            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        public string SavePdf(string folderPath, string fileName)
        {
            return GetUniqueFilePath(Path.Combine(folderPath, fileName + ".pdf"));
        }

        public static string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;
            string candidate;
            do { candidate = Path.Combine(dir, $"{name} ({i++}){ext}"); }
            while (File.Exists(candidate));
            return candidate;
        }

        public void RegiAfterItems(string? resultPath)
        {
            if (resultPath == null) return;
            MainWindow? mw = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mw == null) return;
            mw.FileItems.Clear();
            mw.FileItems.Add(new FileItem { Path = resultPath, DisplayPath = Path.GetFileName(resultPath) });
        }

        public void AddAfterItem(string resultPath)
        {
            MainWindow? mw = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mw == null) return;
            mw.FileItems.Add(new FileItem { Path = resultPath, DisplayPath = Path.GetFileName(resultPath) });
        }
    }
}
