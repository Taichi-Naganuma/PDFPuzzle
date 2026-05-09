using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        public LicenseTier LicenseTier { get; set; } = LicenseTier.Personal;

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFPuzzle");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
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
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
