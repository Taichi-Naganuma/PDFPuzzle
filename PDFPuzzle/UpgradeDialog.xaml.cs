using System.Diagnostics;
using System.Windows;

namespace PDFPuzzle
{
    public partial class UpgradeDialog : Window
    {
        public UpgradeDialog()
        {
            InitializeComponent();
        }

        private void OpenStoreButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.LogAction("UpgradeDialog_OpenedStore");
            try
            {
                var url = AppSettings.Load().StoresUpgradeUrl;
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // 既定ブラウザ起動失敗時もダイアログ閉鎖は継続（UX 優先）
            }
            DialogResult = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            LogService.LogAction("UpgradeDialog_Closed");
            DialogResult = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DialogResult == null)
                DialogResult = false;
        }
    }
}
