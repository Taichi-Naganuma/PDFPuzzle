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

        // チーム版監査フィールド（v0・第3次で追加）。
        // nullable — 旧スキーマの既存ログは null でデシリアライズされる（後方互換）。
        public string? UserName { get; set; }        // Environment.UserName を自動付与
        public string? DeviceId { get; set; }        // DeviceIdentifier.GetCurrent() の短縮ハッシュ
        public string? LicenseTierName { get; set; } // "Personal" / "Business" / "Team"
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
            var run = new RunLogEntry { OutputFolder = outputFolder };
            PopulateAuditFields(run);
            return run;
        }

        // チーム版監査フィールド（UserName / DeviceId / LicenseTierName）を付与する。
        // 全 tier 無条件付与（ローカルログのみ・外部送信なし。仕様書 §4.3）。
        // 付与失敗（レジストリ読取・tier 取得の例外）は呼出側へ伝播させず、
        // 各フィールド null のまま継続する（LogService の「logging never throws」方針に整合）。
        private static void PopulateAuditFields(RunLogEntry run)
        {
            try
            {
                run.UserName = Environment.UserName;
                run.DeviceId = DeviceIdentifier.GetCurrent();
                run.LicenseTierName = LicenseService.GetCurrentTier().ToString();
            }
            catch
            {
                // 監査フィールドの付与失敗で処理ログ自体を止めない
            }
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

        // チーム版監査用 CSV エクスポート（仕様書 §3.4）。
        // 既存 ExportToCsv の15列の末尾に UserName / DeviceId / LicenseTierName の3列を
        // 追加した計18列構成。ExportToCsv は不変（個人版破壊回避）。
        // LoadAllRuns() → BuildAuditCsv() → File.WriteAllText の薄いラッパ。
        public static int ExportTeamAuditCsv(string filePath)
        {
            var runs = LoadAllRuns();
            var (csv, rowCount) = BuildAuditCsv(runs);
            File.WriteAllText(filePath, csv, new UTF8Encoding(true));
            return rowCount;
        }

        // 監査 CSV の行組み立て（純関数・ファイルシステム非依存）。
        // 合成 RunLogEntry を渡して単体テスト可能。LogService に可変 static 状態は足さない。
        // 戻り値: (CSV 本文, データ行数)。
        internal static (string Csv, int RowCount) BuildAuditCsv(IEnumerable<RunLogEntry> runs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RunId,RunStartedAt,RunCompletedAt,OutputFolder,StepIndex,MethodKey,MethodName,StepStartedAt,StepCompletedAt,InputFileCount,InputFiles,OutputFileCount,OutputFiles,Success,ErrorMessage,UserName,DeviceId,LicenseTierName");

            int rowCount = 0;
            foreach (var run in runs)
            {
                if (run.Steps.Count == 0)
                {
                    var fields = new[]
                    {
                        run.RunId, FormatDate(run.StartedAt), FormatDate(run.CompletedAt),
                        run.OutputFolder ?? "", "", "", "", "", "", "0", "", "0", "", "", "",
                        run.UserName ?? "", run.DeviceId ?? "", run.LicenseTierName ?? ""
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
                        step.ErrorMessage ?? "",
                        run.UserName ?? "",
                        run.DeviceId ?? "",
                        run.LicenseTierName ?? ""
                    };
                    sb.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
                    rowCount++;
                }
            }

            return (sb.ToString(), rowCount);
        }

        private static string FormatDate(DateTime? dt) =>
            dt.HasValue ? dt.Value.ToString("yyyy/MM/dd HH:mm:ss") : "";

        private static string EscapeCsv(string field)
        {
            if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        // 単発の操作ログ（UpgradeDialog 等のテレメトリ用）。
        // 既存 RunLogEntry スキーマを流用し、1 件 1 ステップの run として記録する。
        // 失敗しても呼出側に例外を伝播しない（ログ取得失敗で UI を止めない設計）。
        public static void LogAction(string action)
        {
            try
            {
                var run = new RunLogEntry();
                PopulateAuditFields(run);
                run.Steps.Add(new StepLogEntry
                {
                    MethodKey = action,
                    MethodName = action,
                    StartedAt = run.StartedAt,
                    CompletedAt = DateTime.Now,
                    Success = true,
                });
                EndRun(run);
            }
            catch
            {
                // never throw to caller
            }
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
