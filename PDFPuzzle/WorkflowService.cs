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
        private static readonly string WorkflowDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFPuzzle", "workflows");

        public static List<WorkflowDto> List()
        {
            try
            {
                if (!Directory.Exists(WorkflowDir)) return new List<WorkflowDto>();
                var result = new List<WorkflowDto>();
                foreach (var path in Directory.GetFiles(WorkflowDir, "*.json"))
                {
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
                var json = JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                return true;
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
