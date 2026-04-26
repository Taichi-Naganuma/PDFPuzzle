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

