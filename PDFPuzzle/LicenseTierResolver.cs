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
            if (purchase.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.String)
            {
                var v = variants.GetString();
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, BusinessVariantId))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, PersonalVariantId))
                    return LicenseTier.Personal;
            }

            // 2) sku_external_id（Gumroad の SKU 設定で発行されるID）
            if (purchase.TryGetProperty("sku_external_id", out var skuId) && skuId.ValueKind == JsonValueKind.String)
            {
                var s = skuId.GetString();
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, BusinessVariantId, Cmp))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(s) && string.Equals(s, PersonalVariantId, Cmp))
                    return LicenseTier.Personal;
            }

            // 3) variants_and_quantity（"(Business Edition) (1)" 形式の表示用文字列）
            if (purchase.TryGetProperty("variants_and_quantity", out var vq) && vq.ValueKind == JsonValueKind.String)
            {
                var v = vq.GetString();
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, BusinessVariantId))
                    return LicenseTier.Business;
                if (!string.IsNullOrWhiteSpace(v) && Match(v!, PersonalVariantId))
                    return LicenseTier.Personal;
            }

            return LicenseTier.Personal;
        }

        private static bool Match(string haystack, string needle)
        {
            return haystack.IndexOf(needle, Cmp) >= 0;
        }
    }
}
