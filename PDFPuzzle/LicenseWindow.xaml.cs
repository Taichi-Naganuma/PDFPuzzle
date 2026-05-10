using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;

namespace PDFPuzzle
{
    public partial class LicenseWindow : Window
    {
        public LicenseWindow()
        {
            InitializeComponent();
            RefreshTierDisplay();
        }

        private void RefreshTierDisplay()
        {
            var tier = LicenseService.GetCurrentTier();
            CurrentTierText.Text = LocalizationService.Get(tier == LicenseTier.Business
                ? "Tier_Business" : "Tier_Personal");

            // 階層別の機能リスト + CTA ボタンの出し分け
            bool isBusiness = tier == LicenseTier.Business;
            PersonalFeaturesPanel.Visibility = isBusiness ? Visibility.Collapsed : Visibility.Visible;
            BusinessFeaturesPanel.Visibility = isBusiness ? Visibility.Visible : Visibility.Collapsed;
            UpgradeCtaButton.Visibility = isBusiness ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpgradeCtaButton_Click(object sender, RoutedEventArgs e)
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
                // 既定ブラウザ起動失敗時もウィンドウは維持
            }
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            ActivateButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            StatusText.Foreground = Brushes.Gray;
            StatusText.Text = LocalizationService.Get("License_Verifying");

            var (success, message) = await LicenseService.ActivateAsync(LicenseKeyBox.Text);

            if (success)
            {
                StatusText.Foreground = Brushes.DarkGreen;
                StatusText.Text = LocalizationService.Get("License_Success");
                // 認証で確定した階層をその場で反映（再起動を待たない）
                RefreshTierDisplay();
                await Task.Delay(800);
                DialogResult = true;
            }
            else
            {
                StatusText.Foreground = Brushes.Crimson;
                StatusText.Text = message;
                ActivateButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BuyLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DialogResult == null)
                DialogResult = false;
        }
    }
}

