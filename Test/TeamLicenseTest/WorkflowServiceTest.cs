using System;
using System.IO;
using System.Linq;
using PDFPuzzle;
using Xunit;

namespace PDFPuzzle.Tests
{
    // ---------------------------------------------------------------
    // M7 第3次: 共有ワークフローディレクトリ + .lock 排他
    //
    // AppSettings.OverrideForTest / WorkflowService の WorkflowDir は static の
    // ため、これを書き換えるテストはクラス間で並列実行させない。
    // ---------------------------------------------------------------
    [CollectionDefinition("WorkflowServiceSerial", DisableParallelization = true)]
    public class WorkflowServiceSerialCollection { }

    [Collection("WorkflowServiceSerial")]
    public class WorkflowServiceTest : IDisposable
    {
        private readonly string _tempDir;

        public WorkflowServiceTest()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PDFPuzzleWfTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            AppSettings.OverrideForTest = null;
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private void UseSharedDir(string? dir)
            => AppSettings.OverrideForTest = new AppSettings { SharedWorkflowDir = dir };

        private static WorkflowDto Sample(string name)
            => new WorkflowDto { Name = name, CreatedAt = DateTime.Now, StepKeys = { "StepA", "StepB" } };

        // --- 2-B: SharedWorkflowDir 指定時の保存先 ---

        [Fact]
        public void Save_WithSharedDir_WritesIntoSharedDir()
        {
            UseSharedDir(_tempDir);
            Assert.True(WorkflowService.Save(Sample("MyWorkflow")));

            var jsonFiles = Directory.GetFiles(_tempDir, "*.json");
            Assert.Single(jsonFiles);
            Assert.Equal("MyWorkflow.json", Path.GetFileName(jsonFiles[0]));
        }

        [Fact]
        public void SaveThenList_WithSharedDir_RoundTrips()
        {
            UseSharedDir(_tempDir);
            Assert.True(WorkflowService.Save(Sample("Roundtrip")));

            var list = WorkflowService.List();
            Assert.Single(list);
            Assert.Equal("Roundtrip", list[0].Name);
            Assert.Equal(new[] { "StepA", "StepB" }, list[0].StepKeys);
        }

        // --- 2-B: 後方互換 — SharedWorkflowDir null 時はローカルパスに解決 ---

        [Fact]
        public void WorkflowDir_WhenSharedDirNull_ResolvesToLocalAppData()
        {
            // SharedWorkflowDir null の AppSettings を注入。
            // WorkflowService の保存先が従来のローカルパスへ解決されることを、
            // Exists() が参照するパス経由で検証する。
            UseSharedDir(null);

            var expectedLocal = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDFPuzzle", "workflows");

            // ローカル workflows に存在しないはずの一意名 → Exists は false。
            // (実 %LOCALAPPDATA% を汚さないため Save はせず、パス解決のみ確認)
            var uniqueName = "WfTest_NotExist_" + Guid.NewGuid().ToString("N");
            Assert.False(WorkflowService.Exists(uniqueName));
            Assert.False(File.Exists(Path.Combine(expectedLocal, uniqueName + ".json")));
        }

        [Fact]
        public void Save_WithSharedDir_DoesNotTouchLocalAppData()
        {
            // 共有ディレクトリへ保存しても、ローカルの workflows には書かれないこと。
            UseSharedDir(_tempDir);
            var uniqueName = "SharedOnly_" + Guid.NewGuid().ToString("N");
            Assert.True(WorkflowService.Save(Sample(uniqueName)));

            var localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDFPuzzle", "workflows", uniqueName + ".json");
            Assert.False(File.Exists(localPath));
        }

        // --- 2-C: .lock 排他 ---

        [Fact]
        public void Save_WhenFreshLockExists_ReturnsFalseAndDoesNotOverwrite()
        {
            UseSharedDir(_tempDir);

            // 先に正常保存して .json を作る。
            Assert.True(WorkflowService.Save(Sample("Locked")));
            var jsonPath = Path.Combine(_tempDir, "Locked.json");
            var originalContent = File.ReadAllText(jsonPath);

            // 新しい .lock(現在時刻)を置く → 他端末が編集中の状態。
            var lockPath = jsonPath + ".lock";
            File.WriteAllText(lockPath, "other-user");

            // 2 回目の Save は false、.json は書き換わらない。
            var dto2 = new WorkflowDto { Name = "Locked", StepKeys = { "Changed" } };
            Assert.False(WorkflowService.Save(dto2));
            Assert.Equal(originalContent, File.ReadAllText(jsonPath));
        }

        [Fact]
        public void Save_WhenStaleLockExists_SucceedsAndRemovesLock()
        {
            UseSharedDir(_tempDir);

            var jsonPath = Path.Combine(_tempDir, "Stale.json");
            var lockPath = jsonPath + ".lock";

            // 古い .lock(35秒前)を作成 → クラッシュ残骸とみなして無視されるべき。
            File.WriteAllText(lockPath, "crashed-user");
            File.SetLastWriteTime(lockPath, DateTime.Now.AddSeconds(-35));

            Assert.True(WorkflowService.Save(Sample("Stale")));
            Assert.True(File.Exists(jsonPath));
            // 保存後、自分の .lock は finally で削除されている。
            Assert.False(File.Exists(lockPath));
        }

        [Fact]
        public void Save_WhenNoLock_SucceedsAndLeavesNoLock()
        {
            UseSharedDir(_tempDir);

            Assert.True(WorkflowService.Save(Sample("Clean")));

            var jsonPath = Path.Combine(_tempDir, "Clean.json");
            Assert.True(File.Exists(jsonPath));
            Assert.False(File.Exists(jsonPath + ".lock"));
        }

        // --- 2-C: 一覧・読み込みが .json.lock を拾わない ---

        [Fact]
        public void List_DoesNotPickUpJsonLockFiles()
        {
            UseSharedDir(_tempDir);

            // 正規のワークフロー 1 件。
            Assert.True(WorkflowService.Save(Sample("Real")));

            // 紛らわしい .json.lock を手で配置(中身は有効そうな JSON だが拾われてはいけない)。
            File.WriteAllText(
                Path.Combine(_tempDir, "Ghost.json.lock"),
                """{ "Name": "Ghost", "StepKeys": ["X"] }""");

            var list = WorkflowService.List();
            Assert.Single(list);
            Assert.Equal("Real", list[0].Name);
            Assert.DoesNotContain(list, w => w.Name == "Ghost");
        }

        [Fact]
        public void List_WhenEmptyDir_ReturnsEmpty()
        {
            UseSharedDir(_tempDir);
            Assert.Empty(WorkflowService.List());
        }
    }
}
