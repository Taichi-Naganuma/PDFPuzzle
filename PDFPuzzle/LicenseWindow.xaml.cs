using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using PDFPuzzle.Utilities;

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

            // 階層別の機能リスト + CTA ボタンの出し分け。
            // Personal / Business の表示制御は従来どおり（Team 分岐の追加のみ）。
            bool isBusiness = tier == LicenseTier.Business;
            PersonalFeaturesPanel.Visibility = isBusiness ? Visibility.Collapsed : Visibility.Visible;
            BusinessFeaturesPanel.Visibility = isBusiness ? Visibility.Visible : Visibility.Collapsed;
            UpgradeCtaButton.Visibility = isBusiness ? Visibility.Collapsed : Visibility.Visible;

            // Row 5 は UpgradeCtaButton と ManageSeatsButton を共有する（同時表示はしない）。
            // Personal → UpgradeCta 表示 / Team → ManageSeats 表示 / Business → どちらも非表示。
            bool isTeam = tier == LicenseTier.Team;
            ManageSeatsButton.Visibility = isTeam ? Visibility.Visible : Visibility.Collapsed;
            if (isTeam)
            {
                // Team のときは UpgradeCta（個人版向け）を退避し、Row 5 を ManageSeats に明け渡す。
                UpgradeCtaButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpgradeCtaButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "UpgradeCtaButton");
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

        private void ManageSeatsButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "ManageSeatsButton");
            new SeatManagementWindow { Owner = this }.ShowDialog();
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "ActivateButton");
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
            WiringGuard.WarnIfWrongSender(sender, "CancelButton");
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

