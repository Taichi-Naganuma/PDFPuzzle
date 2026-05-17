namespace PDFPuzzle
{
    /// <summary>
    /// チーム版 v0 の席消費オーケストレーション（§2.3 / §4.1）。
    /// <para>
    /// <see cref="LicenseService"/> から呼び出され、verify 成功後の席消費 / 解除時の席返却を
    /// <see cref="ActivationStore"/> に委譲する。WPF / <c>Application.Current</c> には依存しない
    /// （単体テスト可能に保つ）。エラー文言のローカライズ解決は呼び出し側 <see cref="LicenseService"/>
    /// の責務であり、本クラスは bool のみ返す。
    /// </para>
    /// </summary>
    public static class TeamSeatService
    {
        /// <summary>
        /// verify 成功後に呼ぶ。Team 以外（Personal / Business）は席を消費せず常に true を返す。
        /// Team の場合: 空き席があれば登録し Save して true、満席かつ未登録端末なら false。
        /// 既存端末の再認証は席を消費せず true（このとき lastUsedAt 更新を Save する）。
        /// </summary>
        /// <param name="licenseKey">verify に成功したライセンスキー（生キー。store はハッシュのみ保存）。</param>
        /// <param name="tier">verify レスポンスから判定した階層。</param>
        /// <param name="seatCount">verify レスポンスから判定した席数（Team のときのみ意味を持つ）。</param>
        /// <param name="deviceId">現端末の識別子。本番呼び出しは <see cref="DeviceIdentifier.GetCurrent"/>。</param>
        /// <param name="machineName">現端末のマシン名。本番呼び出しは <c>Environment.MachineName</c>。</param>
        /// <param name="userName">現端末のユーザー名。本番呼び出しは <c>Environment.UserName</c>。</param>
        /// <returns>認証を許可してよければ true、満席で拒否なら false。</returns>
        public static bool TryConsumeSeat(
            string licenseKey, LicenseTier tier, int seatCount,
            string deviceId, string machineName, string userName)
        {
            if (tier != LicenseTier.Team) return true;

            var store = ActivationStore.Load(licenseKey, seatCount);
            bool ok = store.TryAddDevice(deviceId, machineName, userName);
            if (ok) store.Save();
            return ok;
        }

        /// <summary>
        /// Team の場合、現端末の席を返却して Save する。Team 以外は何もしない。
        /// 対象端末が未登録の場合（RemoveDevice が false）は Save しない。
        /// </summary>
        /// <param name="licenseKey">解除対象のライセンスキー（生キー）。</param>
        /// <param name="tier">解除対象の階層。Team 以外は no-op。</param>
        /// <param name="deviceId">返却する端末の識別子。</param>
        public static void ReleaseSeat(string licenseKey, LicenseTier tier, string deviceId)
        {
            if (tier != LicenseTier.Team) return;

            var store = ActivationStore.Load(licenseKey);
            if (store.RemoveDevice(deviceId)) store.Save();
        }
    }
}
