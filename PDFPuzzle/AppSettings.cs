using System.IO;
using System.Text.Json;

namespace PDFPuzzle
{
    public class AppSettings
    {
        public string? OutputFolderPath { get; set; }
        public string? Language { get; set; }
        public bool OpenFolderAfterExecution { get; set; }
        public string? LicenseKey { get; set; }
        public int SplitPageCount { get; set; } = 1;
        public string? ExtractPageRange { get; set; }
        public string? WatermarkText { get; set; }

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFPuzzle");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}

