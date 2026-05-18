using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PDFPuzzle.Utilities;

namespace PDFPuzzle
{
    /// <summary>
    /// チーム版 v0.2 ── 席数管理ウィンドウ（仕様書 §4.3）。
    /// 現在のライセンスの席数・登録端末を表示し、端末ごとの席解除と
    /// 監査ログ CSV 出力を提供する。M5 で実装済の <see cref="ActivationStore"/> /
    /// <see cref="LogService"/> を呼ぶだけで、それらのロジックは変更しない。
    /// </summary>
    public partial class SeatManagementWindow : Window
    {
        /// <summary>端末リスト DataTemplate のバインド元（表示専用の行プレゼンタ）。</summary>
        public sealed class DeviceRow
        {
            public string DeviceId { get; init; } = string.Empty;
            public string MachineName { get; init; } = string.Empty;
            public string UserName { get; init; } = string.Empty;
            public string LastUsedAtDisplay { get; init; } = string.Empty;

            /// <summary>
            /// この端末が管理者席（最も早くアクティベートした端末）か。
            /// v0.2 は識別・表示のみ ── true でも権限差は無い（解除も従来どおり可能）。
            /// </summary>
            public bool IsAdmin { get; init; }

            /// <summary>
            /// 端末の表示ラベル（任意のユーザー名タグ）。TextBox に TwoWay バインドして
            /// 書き戻すため init ではなく set。<see cref="ActivationStore.DeviceRecord.DisplayLabel"/>
            /// が null のときは空文字で初期化する。
            /// </summary>
            public string DisplayLabel { get; set; } = string.Empty;
        }

        // 現在表示中のライセンスキー（生キー）。Refresh のたびに store を Load し直す。
        private readonly string _licenseKey;

        public SeatManagementWindow()
        {
            InitializeComponent();
            _licenseKey = AppSettings.Load().LicenseKey ?? string.Empty;
            RefreshDisplay();
        }

        /// <summary>
        /// 現在のライセンスキーから <see cref="ActivationStore"/> を読み直し、
        /// マスクキー・席数・ProgressBar・端末リストを再描画する。
        /// </summary>
        private void RefreshDisplay()
        {
            MaskedKeyText.Text = SeatDisplayFormatter.MaskLicenseKey(_licenseKey);

            // 席返却用途と同じ「席数を権威更新しない」1引数版で読む。
            var store = ActivationStore.Load(_licenseKey);

            SeatCountText.Text = SeatDisplayFormatter.FormatSeatCount(store.UsedSeats, store.SeatCount);

            // ダウングレードで UsedSeats > SeatCount でも Value <= Maximum を保ち例外を出さない。
            SeatProgressBar.Maximum = SeatDisplayFormatter.ProgressMaximum(store.UsedSeats, store.SeatCount);
            SeatProgressBar.Value = store.UsedSeats;

            // 管理者席（最も早くアクティベートした端末）の deviceId を一度だけ取得。
            string? adminId = store.GetAdminDeviceId();

            var rows = new List<DeviceRow>();
            foreach (var d in store.Devices)
            {
                rows.Add(new DeviceRow
                {
                    DeviceId = d.DeviceId,
                    MachineName = d.MachineName,
                    UserName = d.UserName,
                    LastUsedAtDisplay = d.LastUsedAt.ToString("yyyy/MM/dd HH:mm"),
                    IsAdmin = adminId != null
                        && string.Equals(d.DeviceId, adminId, StringComparison.OrdinalIgnoreCase),
                    DisplayLabel = d.DisplayLabel ?? string.Empty,
                });
            }
            DeviceListControl.ItemsSource = rows;
            EmptyHint.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 端末1件の席を解除する。席解除ボタンは DataTemplate 内で端末ごとに複製されるため、
        /// x:Name 突合は不可。対象端末は <see cref="Button.Tag"/>（DeviceId）で識別する。
        /// </summary>
        private void ReleaseSeatButton_Click(object sender, RoutedEventArgs e)
        {
            // §5 サニティチェック: Tag に正しい deviceId が載っているかをガード。
            if (sender is not Button btn || btn.Tag is not string deviceId || string.IsNullOrWhiteSpace(deviceId))
            {
                Trace.TraceWarning(
                    "[SeatManagementWindow.ReleaseSeatButton_Click] Tag に deviceId が無い ── 配線確認要。");
                return;
            }

            var confirm = MessageBox.Show(
                this,
                LocalizationService.Get("Seat_Release_Confirm"),
                LocalizationService.Get("Seat_Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            // v0 のローカル activations\ 方式どおり、解除はローカルファイルのみに作用（中央同期なし）。
            var store = ActivationStore.Load(_licenseKey);
            bool removed = store.RemoveDevice(deviceId);
            if (removed)
                store.Save();

            RefreshDisplay();

            StatusText.Foreground = removed ? Brushes.DarkGreen : Brushes.Crimson;
            StatusText.Text = LocalizationService.Get(
                removed ? "Seat_Release_Done" : "Seat_Release_Failed");
        }

        /// <summary>
        /// 端末1件の表示ラベルを保存する。保存ボタンは DataTemplate 内で端末ごとに複製されるため、
        /// x:Name 突合（<see cref="WiringGuard"/>）は不可。対象端末は <see cref="Button.DataContext"/>
        /// （= <see cref="DeviceRow"/>）から取得する。編集中のラベル文字列は同じ行の TextBox に
        /// TwoWay バインドされた <see cref="DeviceRow.DisplayLabel"/> から読む。
        /// </summary>
        private void SaveLabelButton_Click(object sender, RoutedEventArgs e)
        {
            // §5 サニティチェック: DataContext に DeviceRow が載り、DeviceId が空でないかをガード。
            if (sender is not Button btn
                || btn.DataContext is not DeviceRow row
                || string.IsNullOrWhiteSpace(row.DeviceId))
            {
                Trace.TraceWarning(
                    "[SeatManagementWindow.SaveLabelButton_Click] DataContext に DeviceRow が無い ── 配線確認要。");
                return;
            }

            // v0 のローカル activations\ 方式どおり、ラベル編集はローカルファイルのみに作用（中央同期なし）。
            var store = ActivationStore.Load(_licenseKey);
            bool ok = store.SetDisplayLabel(row.DeviceId, row.DisplayLabel);
            if (ok)
                store.Save();

            RefreshDisplay();

            StatusText.Foreground = ok ? Brushes.DarkGreen : Brushes.Crimson;
            StatusText.Text = LocalizationService.Get(
                ok ? "Seat_Label_Saved" : "Seat_Label_Failed");
        }

        /// <summary>監査ログを CSV として書き出す（<see cref="LogService.ExportTeamAuditCsv"/> を呼ぶだけ）。</summary>
        private void ExportAuditCsvButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "ExportAuditCsvButton");

            var dialog = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = "PDFPuzzle_audit_" + DateTime.Now.ToString("yyyyMMdd") + ".csv",
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                int rowCount = LogService.ExportTeamAuditCsv(dialog.FileName);
                StatusText.Foreground = Brushes.DarkGreen;
                StatusText.Text = string.Format(
                    LocalizationService.Get("Seat_ExportCsv_Done"), rowCount, dialog.FileName);
            }
            catch (Exception ex)
            {
                StatusText.Foreground = Brushes.Crimson;
                StatusText.Text = LocalizationService.Get("Seat_ExportCsv_Failed") + " " + ex.Message;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            WiringGuard.WarnIfWrongSender(sender, "CloseButton");
            Close();
        }
    }
}
