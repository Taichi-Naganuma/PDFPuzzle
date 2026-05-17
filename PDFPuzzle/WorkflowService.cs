using System.IO;
using System.Text.Json;

namespace PDFPuzzle
{
    public class WorkflowDto
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<string> StepKeys { get; set; } = new();
    }

    public static class WorkflowService
    {
        // ワークフロー保存先(仕様書 §4.2)。
        // AppSettings.SharedWorkflowDir が設定されていれば共有ディレクトリ、
        // null なら従来どおり %LOCALAPPDATA%\PDFPuzzle\workflows(後方互換)。
        private static string WorkflowDir => AppSettings.Load().SharedWorkflowDir
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDFPuzzle", "workflows");

        // 他端末が編集中とみなす .lock の有効秒数。これより古い .lock は
        // クラッシュ等の残骸として無視し保存を続行する(仕様書 §7.1 R-3)。
        private const int LockStalenessSeconds = 30;

        public static List<WorkflowDto> List()
        {
            try
            {
                if (!Directory.Exists(WorkflowDir)) return new List<WorkflowDto>();
                var result = new List<WorkflowDto>();
                // *.json のみ列挙。.json.lock(保存排他ファイル)は拡張子が .lock のため
                // 対象外。Windows の検索パターン揺れ対策に末尾も明示チェックする。
                foreach (var path in Directory.GetFiles(WorkflowDir, "*.json"))
                {
                    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var json = File.ReadAllText(path);
                        var dto = JsonSerializer.Deserialize<WorkflowDto>(json);
                        if (dto != null && !string.IsNullOrEmpty(dto.Name)) result.Add(dto);
                    }
                    catch { }
                }
                return result.OrderByDescending(w => w.CreatedAt).ToList();
            }
            catch { return new List<WorkflowDto>(); }
        }

        public static bool Save(WorkflowDto workflow)
        {
            try
            {
                Directory.CreateDirectory(WorkflowDir);
                var path = Path.Combine(WorkflowDir, MakeSafeFileName(workflow.Name) + ".json");
                var lockPath = path + ".lock";

                // 他端末が編集中(30秒以内の新しい .lock)なら保存しない(共有フォルダの衝突防止)。
                // 30秒超の古い .lock はクラッシュ残骸とみなし無視して続行(仕様書 §7.1 R-3)。
                if (File.Exists(lockPath) &&
                    (DateTime.Now - File.GetLastWriteTime(lockPath)).TotalSeconds < LockStalenessSeconds)
                {
                    return false;
                }

                File.WriteAllText(lockPath, Environment.UserName);
                try
                {
                    var json = JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                    return true;
                }
                finally
                {
                    try { File.Delete(lockPath); } catch { }
                }
            }
            catch { return false; }
        }

        public static bool Delete(string name)
        {
            try
            {
                var path = Path.Combine(WorkflowDir, MakeSafeFileName(name) + ".json");
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        public static bool Exists(string name) =>
            File.Exists(Path.Combine(WorkflowDir, MakeSafeFileName(name) + ".json"));

        public static string GenerateDefaultName(IEnumerable<string> stepDisplayNames)
        {
            var names = stepDisplayNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            if (names.Count == 0) return $"Workflow_{DateTime.Now:yyyyMMdd_HHmm}";

            string baseName = string.Join(" + ", names);
            if (baseName.Length > 60) baseName = baseName.Substring(0, 60);

            string candidate = baseName;
            int suffix = 2;
            while (Exists(candidate)) candidate = $"{baseName} ({suffix++})";
            return candidate;
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
