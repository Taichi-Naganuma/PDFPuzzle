using Microsoft.Win32;
using PDFPuzzle.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PDFPuzzle
{
    public partial class Property : Window
    {
        // チーム版 v0.2: 「ワークフロー共有」タブの現在値。
        // null = 共有しない(この端末のみ)。OK ボタンで AppSettings.SharedWorkflowDir に一括保存。
        private string? _sharedWorkflowDir;

        public Property()
        {
            InitializeComponent();
            ConfigureTeamTabs();
            LoadSettings();
        }

        /// <summary>
        /// Team 階層でないとき「ワークフロー共有」タブを TabControl から除外する。
        /// 席数管理の入口と同じ方針(指示書 §3-B)。
        /// </summary>
        private void ConfigureTeamTabs()
        {
            if (LicenseService.GetCurrentTier() != LicenseTier.Team)
            {
                if (WorkflowSharingTab.Parent is TabControl tabs)
                {
                    tabs.Items.Remove(WorkflowSharingTab);
                }
            }
        }

        private void LoadSettings()
        {
            var s = AppSettings.Load();
            SplitPageCountBox.Text = s.SplitPageCount.ToString();
            ExtractPageRangeBox.Text = s.ExtractPageRange ?? "";
            OpenFolderCheckBox.IsChecked = s.OpenFolderAfterExecution;

            LanguageComboBox.SelectedIndex = (s.Language == LocalizationService.English) ? 1 : 0;

            _sharedWorkflowDir = string.IsNullOrWhiteSpace(s.SharedWorkflowDir) ? null : s.SharedWorkflowDir;
            UpdateSharedWorkflowDirText();
        }

        /// <summary>現在の <see cref="_sharedWorkflowDir"/> を読み取り専用 TextBox に反映する。</summary>
        private void UpdateSharedWorkflowDirText()
        {
            SharedWorkflowDirText.Text = string.IsNullOrWhiteSpace(_sharedWorkflowDir)
                ? LocalizationService.Get("Settings_WorkflowShare_None")
                : _sharedWorkflowDir;
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            var s = AppSettings.Load();
            s.SplitPageCount = int.TryParse(SplitPageCountBox.Text, out int n) && n > 0 ? n : 1;
            s.ExtractPageRange = ExtractPageRangeBox.Text.Trim();
            s.OpenFolderAfterExecution = OpenFolderCheckBox.IsChecked == true;
            s.SharedWorkflowDir = _sharedWorkflowDir;
            s.Save();
            Hide();
        }

        private void BrowseSharedWorkflowDirButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "BrowseSharedWorkflowDirButton");

            var dialog = new OpenFolderDialog
            {
                Title = LocalizationService.Get("Settings_Tab_WorkflowShare")
            };
            if (!string.IsNullOrWhiteSpace(_sharedWorkflowDir) && Directory.Exists(_sharedWorkflowDir))
            {
                dialog.InitialDirectory = _sharedWorkflowDir;
            }

            if (dialog.ShowDialog(this) != true) return;

            string selected = dialog.FolderName;
            _sharedWorkflowDir = string.IsNullOrWhiteSpace(selected) ? null : selected;
            UpdateSharedWorkflowDirText();
        }

        private void ClearSharedWorkflowDirButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "ClearSharedWorkflowDirButton");

            _sharedWorkflowDir = null;
            UpdateSharedWorkflowDirText();
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
