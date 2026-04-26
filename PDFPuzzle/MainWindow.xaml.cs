using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PDFPuzzle
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _itemService = new ItemService();
            _settings = AppSettings.Load();

            _property = new Property();
            _property.Visibility = Visibility.Collapsed;
            _methods = new Methods(_property);
            MethodItems = new ObservableCollection<MethodItem>(_methods.AddMethod());

            LocalizationService.LanguageChanged += (s, e) =>
            {
                foreach (var item in MethodItems) item.RefreshDisplayName();
                foreach (var item in ExeItems) item.RefreshDisplayName();
            };

            if (_settings.OutputFolderPath != null && Directory.Exists(_settings.OutputFolderPath))
            {
                FolderPath = _settings.OutputFolderPath;
                OutputLabel.Content = Path.GetFileName(FolderPath);
            }
        }

        private readonly Methods _methods;
        private readonly ItemService _itemService;
        private AppSettings _settings;
        private readonly Property _property;

        ListBox? MethodDrag;
        ListBox? ExeDrag;
        ExeItem? placeholder;
        ExeItem? DragItem;
        FileItem? fileplace;
        FileItem? file;
        FileItem? Remofile;
        ExeItem? RemoItem;

        public string? FolderPath;

        public ObservableCollection<MethodItem> MethodItems { get; }
        public ObservableCollection<ExeItem> ExeItems { get; } = new();
        public ObservableCollection<FileItem> FileItems { get; } = new();

        public class MethodItem : BaseItemsDefine
        {
            public virtual MethodItem Clone() => new MethodItem
            {
                DisplayName = DisplayName,
                DisplayNameKey = DisplayNameKey,
                ExecuteAsync = ExecuteAsync,
            };
        }

        public class FileItem : BaseItemsDefine
        {
            public string? Path { get; set; }
            public string? DisplayPath { get; set; }
        }

        public class ExeItem : BaseItemsDefine { }

        public class BaseItemsDefine : INotifyPropertyChanged
        {
            private string? _displayName;
            public string? DisplayName
            {
                get => _displayName;
                set { _displayName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName))); }
            }
            public string? DisplayNameKey { get; set; }
            public void RefreshDisplayName()
            {
                if (DisplayNameKey != null) DisplayName = LocalizationService.Get(DisplayNameKey);
            }
            public Func<IProgress<string>, Task>? ExecuteAsync { get; set; }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public void CleanDragDropData()
        {
            ExeDrag = null; MethodDrag = null; DragItem = null; file = null;
            if (placeholder != null) ExeItems.Remove(placeholder);
            if (fileplace != null) FileItems.Remove(fileplace);
            placeholder = null; fileplace = null;
        }

        // ===== Method List =====
        private void MethodList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void MethodList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => CleanDragDropData();
        private void MethodList_DragEnter(object sender, DragEventArgs e) { }
        private void MethodList_MouseLeave(object sender, MouseEventArgs e) { }
        private void MethodList_DragOver(object sender, DragEventArgs e)
        {
            _itemService.WhileDropCopy(e, typeof(MethodItem));
            _itemService.WhileDropNone(e, typeof(ExeItem));
        }
        private void MethodList_Drop(object sender, DragEventArgs e) => CleanDragDropData();
        private void MethodList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (ExeDrag == ExeList) return;
            MethodDrag = MethodList;
            MethodItem? selected = (MethodItem)MethodList.SelectedItem;
            if (selected != null)
            {
                DragItem = _itemService.ConvertToExeitem(selected.Clone());
                DragDrop.DoDragDrop(MethodList, DragItem, DragDropEffects.Copy);
            }
        }

        // ===== Exe List =====
        private void ExeList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void ExeList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }
        private void ExeList_DragEnter(object sender, DragEventArgs e)
        {
            MethodList.SelectedItem = null;
            if (MethodDrag != MethodList || ExeDrag == ExeList)
            {
                if (!e.Data.GetDataPresent(typeof(ExeItem))) return;
                if (placeholder != null) return;
                placeholder = _itemService.CreatePlaceholder();
                ExeItems.Add(placeholder);
            }
        }
        private void ExeList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (MethodDrag == MethodList) return;
            ExeDrag = ExeList;
            DragItem = (ExeItem)ExeList.SelectedItem;
            if (DragItem != null)
                DragDrop.DoDragDrop(ExeList, DragItem, DragDropEffects.Copy);
        }
        private void ExeList_DragOver(object sender, DragEventArgs e)
        {
            Point pos = e.GetPosition(ExeList);
            int targetIndex = _itemService.GetUnderIndex(ExeList, pos, ExeItems);
            if (ExeDrag == ExeList)
            {
                _itemService.WhileDropCopy(e, typeof(ExeItem));
                if (targetIndex == 0)
                {
                    HitTestResult result = VisualTreeHelper.HitTest(ExeList, pos);
                    ListBoxItem? item = ItemService.FindAncestor<ListBoxItem>(result?.VisualHit);
                    if (item != null && placeholder != null)
                    {
                        int ci = ExeItems.IndexOf(placeholder);
                        if (ci != -1) ExeItems.Move(ci, 0);
                        ExeList.SelectedIndex = 0;
                    }
                    return;
                }
                if (targetIndex > 0 && placeholder != null)
                    _itemService.MovePlaceholder(placeholder, ExeList, pos, targetIndex, ExeItems, e);
            }
            else if (MethodDrag == MethodList)
            {
                _itemService.WhileDropCopy(e, typeof(ExeItem));
            }
        }
        private void ExeList_Drop(object sender, DragEventArgs e)
        {
            Point curPos = e.GetPosition(ExeList);
            int underIndex = _itemService.GetUnderIndex(ExeList, curPos, ExeItems);
            if (!e.Data.GetDataPresent(typeof(ExeItem))) return;
            if (ExeDrag == ExeList)
            {
                if (underIndex == 0)
                {
                    HitTestResult result = VisualTreeHelper.HitTest(ExeList, curPos);
                    ListBoxItem? item = ItemService.FindAncestor<ListBoxItem>(result?.VisualHit);
                    if (item != null && DragItem != null) { ExeItems.Remove(DragItem); ExeItems.Insert(0, DragItem); DragItem = null; }
                    else if (item == null && DragItem != null) { int li = ExeItems.Count - 1; ExeItems.Remove(DragItem); ExeItems.Insert(li, DragItem); DragItem = null; }
                }
                else if (underIndex > 0 && placeholder != null)
                {
                    _itemService.InsertUnderCursor(ExeList, curPos, DragItem, placeholder, ExeItems);
                    placeholder = null; ExeDrag = null;
                }
            }
            else if (MethodDrag == MethodList && DragItem != null)
            {
                ExeItems.Add(DragItem);
                placeholder = null; MethodDrag = null;
            }
        }

        // ===== File List =====
        private void FileList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void FileList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => CleanDragDropData();
        private void FileList_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(FileItem))) return;
            if (fileplace != null) return;
            fileplace = _itemService.Createfileplace();
            FileItems.Add(fileplace);
        }
        private void FileList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            file = (FileItem)FileList.SelectedItem;
            if (file != null) DragDrop.DoDragDrop(FileList, file, DragDropEffects.Copy);
        }
        private void FileList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy; e.Handled = true;
            }
            else if (e.Data.GetDataPresent(typeof(FileItem)))
            {
                _itemService.WhileDropCopy(e, typeof(FileItem));
                Point pos = e.GetPosition(FileList);
                int ind = _itemService.GetFileIndex(FileList, pos, FileItems);
                if (fileplace != null) _itemService.ShowInsertLine(pos, ind, FileList, fileplace, FileItems, e);
            }
        }
        private void FileList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths != null)
                    foreach (string s in paths.Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
                        FileItems.Add(new FileItem { Path = s, DisplayPath = Path.GetFileName(s) });
                fileplace = null;
            }
            else if (e.Data.GetDataPresent(typeof(FileItem)))
            {
                Point curPos = e.GetPosition(FileList);
                int underIndex = _itemService.GetFileIndex(FileList, curPos, FileItems);
                if (underIndex == 0)
                {
                    HitTestResult result = VisualTreeHelper.HitTest(FileList, curPos);
                    ListBoxItem? item = ItemService.FindAncestor<ListBoxItem>(result?.VisualHit);
                    if (item != null && file != null) { FileItems.Remove(file); FileItems.Insert(0, file); file = null; }
                    else if (item == null && file != null) { int li = FileItems.Count - 1; FileItems.Remove(file); FileItems.Insert(li, file); file = null; }
                }
                else if (underIndex > 0 && fileplace != null)
                {
                    _itemService.InsertUnderfile(FileList, curPos, underIndex, file, fileplace, FileItems);
                    fileplace = null;
                }
            }
        }

        // ===== Buttons =====
        private void Add_Exe_Button_Click(object sender, RoutedEventArgs e)
        {
            var selected = (MethodItem)MethodList.SelectedItem;
            if (selected != null)
                ExeItems.Add(_itemService.ConvertToExeitem(selected.Clone())!);
        }

        private void Delete_Button_Click(object sender, RoutedEventArgs e)
        {
            Remofile = (FileItem)FileList.SelectedItem;
            RemoItem = (ExeItem)ExeList.SelectedItem;
            if (RemoItem != null) ExeItems.Remove(RemoItem);
            if (Remofile != null) FileItems.Remove(Remofile);
        }

        private void Ref_Button_Click(object sender, RoutedEventArgs e)
        {
            string[]? paths = _itemService.SelectFile();
            if (paths != null)
                foreach (string p in paths)
                    FileItems.Add(new FileItem { Path = p, DisplayPath = Path.GetFileName(p) });
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderPath = _itemService.SelectFolder();
            if (FolderPath != null)
            {
                OutputLabel.Content = Path.GetFileName(FolderPath);
                _settings.OutputFolderPath = FolderPath;
                _settings.Save();
            }
        }

        private async void Exe_Button_Click(object sender, RoutedEventArgs e)
        {
            if (FolderPath == null)
            {
                FolderPath = _itemService.SelectFolder();
                if (FolderPath == null) return;
                OutputLabel.Content = Path.GetFileName(FolderPath);
                _settings.OutputFolderPath = FolderPath;
                _settings.Save();
            }

            if (FileItems.Count == 0 || ExeItems.Count == 0)
            {
                MessageBox.Show(L("Msg_NeedFileAndAction"));
                return;
            }

            Exe_Button.IsEnabled = false;
            ExecuteProgressBar.Visibility = Visibility.Visible;
            StatusLabel.Content = L("Status_Processing");
            StatusLabel.Foreground = Brushes.DarkOrange;

            var steps = ExeItems.ToList();
            var progress = new Progress<string>(msg => StatusLabel.Content = msg);

            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var item = steps[i];
                    if (item.ExecuteAsync != null)
                        await item.ExecuteAsync(progress);
                }
            }
            finally
            {
                Exe_Button.IsEnabled = true;
                ExecuteProgressBar.Visibility = Visibility.Collapsed;
                StatusLabel.Content = L("Status_Done");
                StatusLabel.Foreground = Brushes.DarkGreen;

                if (_settings.OpenFolderAfterExecution && FolderPath != null)
                    System.Diagnostics.Process.Start("explorer.exe", FolderPath);
            }
        }

        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            _property.Owner = this;
            _property.Show();
        }

        private void Exit_Button_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Rectangle_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FileItem)) || e.Data.GetDataPresent(typeof(ExeItem)))
                _itemService.WhileDropCopy(e, e.Data.GetDataPresent(typeof(FileItem)) ? typeof(FileItem) : typeof(ExeItem));
        }

        private void Rectangle_Drop(object sender, DragEventArgs e)
        {
            if (DragItem != null) ExeItems.Remove(DragItem);
            else if (file != null) FileItems.Remove(file);
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e) { }
        private void Window_Drop(object sender, DragEventArgs e) => CleanDragDropData();
        private void Window_QueryContinueDrag(object sender, QueryContinueDragEventArgs e) { }

        private static string L(string key) => LocalizationService.Get(key);
    }
}
