using System.IO;
using System.Text;
using System.Text.Json;

namespace PDFPuzzle
{
    public class StepLogEntry
    {
        public string? MethodKey { get; set; }
        public string? MethodName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public List<string> InputFiles { get; set; } = new();
        public List<string> OutputFiles { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RunLogEntry
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
        public string? OutputFolder { get; set; }
        public List<StepLogEntry> Steps { get; set; } = new();
    }

    public class DailyLogFile
    {
        public string Date { get; set; } = string.Empty;
        public List<RunLogEntry> Runs { get; set; } = new();
    }

    public static class LogService
    {
        private static readonly string LogRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFPuzzle", "logs");

        public static string LogDirectory => LogRoot;

        public static RunLogEntry StartRun(string? outputFolder)
        {
            return new RunLogEntry { OutputFolder = outputFolder };
        }

        public static void AddStep(RunLogEntry run, StepLogEntry step) =>
            run.Steps.Add(step);

        public static List<RunLogEntry> LoadAllRuns()
        {
            var result = new List<RunLogEntry>();
            try
            {
                if (!Directory.Exists(LogRoot)) return result;
                foreach (var monthDir in Directory.GetDirectories(LogRoot))
                {
                    foreach (var file in Directory.GetFiles(monthDir, "*.json"))
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var daily = JsonSerializer.Deserialize<DailyLogFile>(json);
                            if (daily?.Runs != null) result.AddRange(daily.Runs);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return result.OrderBy(r => r.StartedAt).ToList();
        }

        public static int ExportToCsv(string filePath)
        {
            var runs = LoadAllRuns();
            var sb = new StringBuilder();
            sb.AppendLine("RunId,RunStartedAt,RunCompletedAt,OutputFolder,StepIndex,MethodKey,MethodName,StepStartedAt,StepCompletedAt,InputFileCount,InputFiles,OutputFileCount,OutputFiles,Success,ErrorMessage");

            int rowCount = 0;
            foreach (var run in runs)
            {
                if (run.Steps.Count == 0)
                {
                    var fields = new[]
                    {
                        run.RunId, FormatDate(run.StartedAt), FormatDate(run.CompletedAt),
                        run.OutputFolder ?? "", "", "", "", "", "", "0", "", "0", "", "", ""
                    };
                    sb.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
                    rowCount++;
                    continue;
                }

                for (int i = 0; i < run.Steps.Count; i++)
                {
                    var step = run.Steps[i];
                    var fields = new[]
                    {
                        run.RunId,
                        FormatDate(run.StartedAt),
                        FormatDate(run.CompletedAt),
                        run.OutputFolder ?? "",
                        (i + 1).ToString(),
                        step.MethodKey ?? "",
                        step.MethodName ?? "",
                        FormatDate(step.StartedAt),
                        FormatDate(step.CompletedAt),
                        step.InputFiles.Count.ToString(),
                        string.Join(" | ", step.InputFiles),
                        step.OutputFiles.Count.ToString(),
                        string.Join(" | ", step.OutputFiles),
                        step.Success ? "Success" : "Failure",
                        step.ErrorMessage ?? ""
                    };
                    sb.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
                    rowCount++;
                }
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
            return rowCount;
        }

        private static string FormatDate(DateTime? dt) =>
            dt.HasValue ? dt.Value.ToString("yyyy/MM/dd HH:mm:ss") : "";

        private static string EscapeCsv(string field)
        {
            if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        public static void EndRun(RunLogEntry run)
        {
            run.CompletedAt = DateTime.Now;
            try
            {
                var date = run.StartedAt.Date;
                var monthDir = Path.Combine(LogRoot, date.ToString("yyyy-MM"));
                Directory.CreateDirectory(monthDir);
                var logPath = Path.Combine(monthDir, date.ToString("yyyy-MM-dd") + ".json");

                DailyLogFile daily;
                if (File.Exists(logPath))
                {
                    try
                    {
                        var existing = File.ReadAllText(logPath);
                        daily = JsonSerializer.Deserialize<DailyLogFile>(existing) ?? new DailyLogFile();
                    }
                    catch
                    {
                        daily = new DailyLogFile();
                    }
                }
                else
                {
                    daily = new DailyLogFile();
                }

                if (string.IsNullOrEmpty(daily.Date)) daily.Date = date.ToString("yyyy-MM-dd");
                daily.Runs.Add(run);

                var json = JsonSerializer.Serialize(daily, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(logPath, json);
            }
            catch
            {
                // logging never throws to caller
            }
        }
    }
}
