using System.Text.Json;

namespace PDFPuzzle
{
    /// <summary>
    /// Gumroad verify レスポンスの purchase 情報から LicenseTier を判定する。
    /// マーケが variants/SKU 実体を確定したら、ここのマッピングだけ差し替えれば良い。
    /// </summary>
    internal static class LicenseTierResolver
    {
        // TODO(マーケ確認後): variant ID を反映
        // 暫定ダミー値。Gumroad ダッシュボードで実際の variant 名 / SKU 確定次第差し替える。
        private const string PersonalVariantId = "personal_v1";
        private const string BusinessVariantId = "business_v1";
        // TODO(マーケ確認後): Gumroad の実 variant 名 / SKU 確定次第差し替える。
        private const string TeamVariantId = "team_v1";

        // 比較は大小無視・前後空白除去で実施（Gumroad 側の表記揺れに対する保険）
        private static readonly StringComparison Cmp = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// verify レスポンス全体（root JSON）から階層を判定する。
        /// 判定不能時は Personal を返す（フェイルセーフ：機能制限が緩い側に倒さない）。
        /// </summary>
        public static LicenseTier Resolve(JsonElement root)
        {
            if (!root.TryGetProperty("purchase", out var purchase) || purchase.ValueKind != JsonValueKind.Object)
                return LicenseTier.Personal;

            // 1) variants 文字列（例: "(Business Edition)" や "Business" 等）
            // 優先順は Team → Business → Personal（チーム版は事業者版の判定より前に置く）。
            if (purchase.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.String)
            {
                var v = variants.GetString();
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, TeamVariantId))
                    return LicenseTier.Team;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, BusinessVariantId))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, PersonalVariantId))
                    return LicenseTier.Personal;
            }

            // 2) sku_external_id（Gumroad の SKU 設定で発行されるID）
            if (purchase.TryGetProperty("sku_external_id", out var skuId) && skuId.ValueKind == JsonValueKind.String)
            {
                var s = skuId.GetString();
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, TeamVariantId, Cmp))
                    return LicenseTier.Team;
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, BusinessVariantId, Cmp))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, PersonalVariantId, Cmp))
                    return LicenseTier.Personal;
            }

            // 3) variants_and_quantity（"(Business Edition) (1)" 形式の表示用文字列）
            if (purchase.TryGetProperty("variants_and_quantity", out var vq) && vq.ValueKind == JsonValueKind.String)
            {
                var v = vq.GetString();
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, TeamVariantId))
                    return LicenseTier.Team;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, BusinessVariantId))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, PersonalVariantId))
                    return LicenseTier.Personal;
            }

            return LicenseTier.Personal;
        }

        /// <summary>
        /// verify レスポンスから席数を抽出する。Team 以外は常に 1。
        /// 抽出不能時は 3（v0 フェイルセーフ）。
        /// </summary>
        public static int ResolveSeatCount(JsonElement root, LicenseTier tier)
        {
            if (tier != LicenseTier.Team) return 1;
            if (root.TryGetProperty("purchase", out var purchase) && purchase.ValueKind == JsonValueKind.Object)
            {
                // 1) purchase.quantity(Number)を最優先
                if (purchase.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number)
                    return q.GetInt32();
                // 2) variants_and_quantity 末尾 "(N)" を正規表現で抽出(フォールバック)
                if (purchase.TryGetProperty("variants_and_quantity", out var vq) && vq.ValueKind == JsonValueKind.String)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(vq.GetString() ?? "", @"\((\d+)\)\s*$");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) return n;
                }
            }
            return 3; // フェイルセーフ
        }

        private static bool Match(string haystack, string needle)
        {
            return haystack.IndexOf(needle, Cmp) >= 0;
        }
    }
}
