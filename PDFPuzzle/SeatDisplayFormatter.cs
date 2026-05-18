namespace PDFPuzzle
{
    /// <summary>
    /// 席数管理ウィンドウの表示用テキスト整形（純関数・UI 非依存）。
    /// UI から切り離して <c>TeamLicenseTest</c> で単体テストできるようにする。
    /// </summary>
    public static class SeatDisplayFormatter
    {
        /// <summary>
        /// ライセンスキーを末尾4文字以外マスクして返す。
        /// 例: <c>ABCD-1234-EFGH-A1B2</c> → <c>••••••••••••••••A1B2</c>。
        /// - 末尾4文字（または全長が4以下ならその文字数）以外を <c>•</c> に置換する。
        /// - ハイフン等の区切り文字もマスク対象（生キーの構造を露出させない）。
        /// - null / 空文字は空文字を返す。
        /// </summary>
        public static string MaskLicenseKey(string? licenseKey)
        {
            if (string.IsNullOrEmpty(licenseKey))
                return string.Empty;

            const char Mask = '•'; // •
            int len = licenseKey.Length;
            if (len <= 4)
                return new string(Mask, len);

            string tail = licenseKey.Substring(len - 4);
            return new string(Mask, len - 4) + tail;
        }

        /// <summary>
        /// 「N / M」形式の席数テキストを返す（例 <c>2 / 3</c>）。
        /// 周辺の文言（「使用中」等）はローカライズリソース側で付与する。
        /// </summary>
        public static string FormatSeatCount(int usedSeats, int seatCount)
            => $"{usedSeats} / {seatCount}";

        /// <summary>
        /// ProgressBar の Maximum に与える値。
        /// <paramref name="seatCount"/> が 0 以下のとき 0 除算的な見た目崩れを避けるため
        /// 最低 1 を返す。<paramref name="usedSeats"/> が <paramref name="seatCount"/> を
        /// 超える（ダウングレード超過）ケースでは usedSeats を Maximum に採用し、
        /// バーが満杯クランプされて例外も出ないようにする。
        /// </summary>
        public static double ProgressMaximum(int usedSeats, int seatCount)
        {
            int effective = seatCount > 0 ? seatCount : 1;
            return usedSeats > effective ? usedSeats : effective;
        }

        /// <summary>
        /// 席数超過（ダウングレード等で UsedSeats > SeatCount）かどうか。
        /// </summary>
        public static bool IsOverSeated(int usedSeats, int seatCount)
            => usedSeats > seatCount;

        /// <summary>
        /// 契約席数を超えて登録されている端末数。超過していなければ 0。
        /// 警告文に出す「あと何台 解除すべきか」の値。
        /// </summary>
        public static int ExcessSeatCount(int usedSeats, int seatCount)
            => usedSeats > seatCount ? usedSeats - seatCount : 0;
    }
}
