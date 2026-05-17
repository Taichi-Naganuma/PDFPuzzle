namespace PDFPuzzle
{
    public enum LicenseTier
    {
        Personal = 0,   // 個人版 (デフォルト)
        Business = 1,   // 事業者版 (1端末/1ライセンス)
        Team = 2        // チーム版 (N席/1ライセンス, v0 は N=3 固定)
    }

    public static class LicenseTierExtensions
    {
        // v0 は Team=3 固定。v0.2 で Gumroad quantity 取得値に置換予定。
        public static int GetDefaultSeatCount(this LicenseTier tier) => tier switch
        {
            LicenseTier.Personal => 1,
            LicenseTier.Business => 1,
            LicenseTier.Team => 3,
            _ => 1
        };
    }
}
