using System.Windows;

namespace PDFPuzzle
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            var settings = AppSettings.Load();
            LocalizationService.Apply(settings.Language ?? LocalizationService.Japanese);

            if (!LicenseService.IsActivated())
            {
                var licenseWindow = new LicenseWindow();
                bool? activated = licenseWindow.ShowDialog();
                if (activated != true)
                {
                    Shutdown();
                    return;
                }
            }

            new MainWindow().Show();
        }
    }
}
