using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System.IO;
using System.Windows;
using static PDFPuzzle.MainWindow;

namespace PDFPuzzle
{
    public class Methods
    {
        private readonly Property _property;
        private readonly ItemService _itemService = new();

        public Methods(Property property)
        {
            _property = property;
        }

        public List<MethodItem> AddMethod()
        {
            return new List<MethodItem>
            {
                new MethodItem { DisplayNameKey = "Method_Merge",   DisplayName = LocalizationService.Get("Method_Merge"),   ExecuteAsync = MergeAsync },
                new MethodItem { DisplayNameKey = "Method_Split",   DisplayName = LocalizationService.Get("Method_Split"),   ExecuteAsync = SplitAsync },
                new MethodItem { DisplayNameKey = "Method_Extract", DisplayName = LocalizationService.Get("Method_Extract"), ExecuteAsync = ExtractAsync },
            };
        }

        private MainWindow? GetMainWindow() =>
            Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

        private async Task MergeAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();

            if (files.Count < 2)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(LocalizationService.Get("Msg_NeedMoreFiles")));
                return;
            }

            string outputPath = _itemService.SavePdf(mw.FolderPath!, "merged");

            await Task.Run(() =>
            {
                using var output = new PdfDocument();
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    for (int i = 0; i < input.PageCount; i++)
                        output.AddPage(input.Pages[i]);
                }
                output.Save(outputPath);
            });

            _itemService.RegiAfterItems(outputPath);
        }

        private async Task SplitAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();
            int pageCount = Math.Max(1, settings.SplitPageCount);

            mw.FileItems.Clear();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    int total = input.PageCount;
                    int part = 1;

                    for (int start = 0; start < total; start += pageCount)
                    {
                        using var output = new PdfDocument();
                        for (int j = start; j < Math.Min(start + pageCount, total); j++)
                            output.AddPage(input.Pages[j]);

                        string outPath = ItemService.GetUniqueFilePath(
                            Path.Combine(mw.FolderPath!, $"{baseName}_part{part++}.pdf"));
                        output.Save(outPath);
                        Application.Current.Dispatcher.Invoke(() =>
                            _itemService.AddAfterItem(outPath));
                    }
                }
            });
        }

        private async Task ExtractAsync(IProgress<string> progress)
        {
            var mw = GetMainWindow();
            if (mw == null) return;
            var files = mw.FileItems.Where(f => f.Path != null).Select(f => f.Path!).ToList();
            var settings = AppSettings.Load();

            if (string.IsNullOrWhiteSpace(settings.ExtractPageRange))
            {
                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show(LocalizationService.Get("Msg_NoPageRange")));
                return;
            }

            mw.FileItems.Clear();

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    progress.Report(Path.GetFileName(file));
                    using var input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                    var indices = ParsePageRange(settings.ExtractPageRange!, input.PageCount);
                    if (indices.Count == 0) continue;

                    using var output = new PdfDocument();
                    foreach (int idx in indices)
                        output.AddPage(input.Pages[idx - 1]);

                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string outPath = ItemService.GetUniqueFilePath(
                        Path.Combine(mw.FolderPath!, $"{baseName}_extracted.pdf"));
                    output.Save(outPath);
                    Application.Current.Dispatcher.Invoke(() =>
                        _itemService.AddAfterItem(outPath));
                }
            });
        }

        private static List<int> ParsePageRange(string range, int maxPage)
        {
            var result = new List<int>();
            foreach (var token in range.Split(','))
            {
                string t = token.Trim();
                if (t.Contains('-'))
                {
                    var parts = t.Split('-');
                    if (int.TryParse(parts[0], out int from) && int.TryParse(parts[1], out int to))
                        for (int i = from; i <= Math.Min(to, maxPage); i++)
                            if (i >= 1 && !result.Contains(i)) result.Add(i);
                }
                else if (int.TryParse(t, out int page) && page >= 1 && page <= maxPage && !result.Contains(page))
                {
                    result.Add(page);
                }
            }
            result.Sort();
            return result;
        }
    }
}
