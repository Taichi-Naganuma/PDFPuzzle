using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PDFPuzzle
{
    public partial class Property : Window
    {
        public Property()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = AppSettings.Load();
            SplitPageCountBox.Text = s.SplitPageCount.ToString();
            ExtractPageRangeBox.Text = s.ExtractPageRange ?? "";
            OpenFolderCheckBox.IsChecked = s.OpenFolderAfterExecution;

            LanguageComboBox.SelectedIndex = (s.Language == LocalizationService.English) ? 1 : 0;
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Load();
            s.SplitPageCount = int.TryParse(SplitPageCountBox.Text, out int n) && n > 0 ? n : 1;
            s.ExtractPageRange = ExtractPageRangeBox.Text.Trim();
            s.OpenFolderAfterExecution = OpenFolderCheckBox.IsChecked == true;
            s.Save();
            Hide();
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(LogService.LogDirectory);
                System.Diagnostics.Process.Start("explorer.exe", LogService.LogDirectory);
            }
            catch { }
        }

        private void ExportLogCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"pdfpuzzle_logs_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                int rows = LogService.ExportToCsv(dialog.FileName);
                MessageBox.Show(
                    string.Format(LocalizationService.Get("Msg_CsvExportDone"), rows, dialog.FileName),
                    LocalizationService.Get("Btn_ExportLogCsv"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                    LocalizationService.Get("Btn_ExportLogCsv"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item)
            {
                string lang = item.Tag?.ToString() == "en" ? LocalizationService.English : LocalizationService.Japanese;
                var s = AppSettings.Load();
                s.Language = lang;
                s.Save();
                LocalizationService.Apply(lang);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
