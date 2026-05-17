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
        /// verify レスポンスから席数を抽出する（v0.2 ── Gumroad multiseat 連携）。
        /// 判定順:
        ///  1) Team 以外（Personal / Business）→ 常に 1。
        ///  2) verify root に <c>is_multiseat_license == true</c> があり、かつ
        ///     <c>purchase.quantity</c> が正の整数として読める → その値を席数とする。
        ///  3) 補助フォールバック: <c>purchase.variants_and_quantity</c> 末尾の "(N)"
        ///     （N は正の整数）を抽出 → その値を席数とする。
        ///  4) 上記いずれにも当てはまらない → 3（v0 フェイルセーフ。安全側）。
        /// <para>
        /// <c>is_multiseat_license</c> フラグと <c>quantity</c> は必ず組で見る。
        /// フラグを見ずに <c>quantity</c> を席数に使うと、単席購入の購入数量を席数と
        /// 誤認する事故になる（Gumroad API 検証ノート Q-5）。
        /// </para>
        /// </summary>
        public static int ResolveSeatCount(JsonElement root, LicenseTier tier)
        {
            // ルール1: 個人版・事業者版は常に1席。
            if (tier != LicenseTier.Team) return 1;

            // ルール2: is_multiseat_license フラグ + purchase.quantity を組で見る。
            if (IsMultiseatLicense(root)
                && root.TryGetProperty("purchase", out var purchase)
                && purchase.ValueKind == JsonValueKind.Object
                && TryGetPositiveInt(purchase, "quantity", out var seats))
            {
                return seats;
            }

            // ルール3（補助フォールバック）: variants_and_quantity 末尾 "(N)" を抽出。
            if (root.TryGetProperty("purchase", out var p) && p.ValueKind == JsonValueKind.Object
                && p.TryGetProperty("variants_and_quantity", out var vq)
                && vq.ValueKind == JsonValueKind.String)
            {
                var m = System.Text.RegularExpressions.Regex.Match(vq.GetString() ?? "", @"\((\d+)\)\s*$");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0)
                    return n;
            }

            // ルール4: フェイルセーフ（v0 と同じ 3 固定）。
            return 3;
        }

        /// <summary>
        /// verify root の <c>is_multiseat_license</c> が真偽値 true のときのみ true。
        /// 不在・型不一致・false は false（安全側）。
        /// </summary>
        private static bool IsMultiseatLicense(JsonElement root)
        {
            return root.TryGetProperty("is_multiseat_license", out var flag)
                && flag.ValueKind == JsonValueKind.True;
        }

        /// <summary>
        /// 指定オブジェクトのプロパティを正の整数として安全に取得する。
        /// Number でない／整数化できない／0 以下なら false（例外を投げない）。
        /// </summary>
        private static bool TryGetPositiveInt(JsonElement obj, string propertyName, out int value)
        {
            value = 0;
            if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Number)
                return false;
            if (!prop.TryGetInt32(out var n) || n <= 0)
                return false;
            value = n;
            return true;
        }

        private static bool Match(string haystack, string needle)
        {
            return haystack.IndexOf(needle, Cmp) >= 0;
        }
    }
}
