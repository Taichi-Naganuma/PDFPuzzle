using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PDFPuzzle.Utilities
{
    /// <summary>
    /// XAML Click / Command 配線のサニティチェック。
    /// 配線ズレを runtime に検知し、Trace.TraceWarning で警告ログを出す(throw しない)。
    /// 詳細: 設計書/失敗学習/20260512_Editor配線不具合_汎用パターン.md §3-2-b
    /// </summary>
    public static class WiringGuard
    {
        /// <summary>
        /// Click ハンドラ用: sender の x:Name を期待値と突合し、不一致なら警告。
        /// </summary>
        /// <param name="sender">Click イベントの sender(通常 Button)</param>
        /// <param name="expectedName">期待する FrameworkElement.Name(= XAML の x:Name)</param>
        /// <param name="caller">呼び出し元メソッド名(CallerMemberName で自動付与)</param>
        public static void WarnIfWrongSender(
            object sender,
            string expectedName,
            [CallerMemberName] string caller = "")
        {
            if (sender is FrameworkElement fe)
            {
                var actual = fe.Name;
                if (!string.Equals(actual, expectedName, StringComparison.Ordinal))
                {
                    Trace.TraceWarning(
                        $"[Wiring.{caller}] XAML Click sender mismatch: " +
                        $"expected x:Name='{expectedName}', got='{actual}'. " +
                        $"XAML の Click 属性を確認してください。");
                }
            }
        }

        /// <summary>
        /// Command バインディング用: CommandParameter で受け取った発火元名を突合。
        /// MVVM で ICommand 実装する場合に使用。今回の LicenseWindow / WorkflowLoadDialog は
        /// 直接 Click ハンドラ方式のため §4-3 / §4-4 では使わないが、将来の MVVM 化に備えて公開しておく。
        /// </summary>
        public static void WarnIfWrongCommandSource(
            object? parameter,
            string expectedSource,
            [CallerMemberName] string caller = "")
        {
            if (parameter is string actual &&
                !string.Equals(actual, expectedSource, StringComparison.Ordinal))
            {
                Trace.TraceWarning(
                    $"[Wiring.{caller}] Command source mismatch: " +
                    $"expected CommandParameter='{expectedSource}', got='{actual}'.");
            }
        }
    }
}
