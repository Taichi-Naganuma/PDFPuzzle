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

        // チーム版 v0.2: 共有ワークフローディレクトリ。
        // null = 共有しない(従来どおり %LOCALAPPDATA%\PDFPuzzle\workflows をローカル使用)。
        // 値が入っているとき WorkflowService がそのパスを保存先に使う(仕様書 §4.2)。
        public string? SharedWorkflowDir { get; set; }

        // STORES 事業者版 商品ページ(2026-05-15 公開・URL 確定)
        public string StoresUpgradeUrl { get; set; } = "https://s8qjuvqtnyjthihxnwq1.stores.jp/items/6a06dd24e7d722093598a745";

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFPuzzle");
        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// テスト用: Load() の戻り値を差し替える。null のとき通常どおり settings.json を読む。
        /// 単体テストはこれを使い、実 %APPDATA%\PDFPuzzle\settings.json を汚さないこと。
        /// </summary>
        internal static AppSettings? OverrideForTest { get; set; }

        public static AppSettings Load()
        {
            if (OverrideForTest != null) return OverrideForTest;
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
