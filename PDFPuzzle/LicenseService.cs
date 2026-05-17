using System.Net.Http;
using System.Text.Json;

namespace PDFPuzzle
{
    public static class LicenseService
    {
        private const string ProductPermalink = "zjmmda";   // 個人版/事業者版（既存）
        // TODO(Gumroad): チーム版商品 作成後に実 permalink へ差替。COO が hotfix commit する。
        private const string TeamProductPermalink = "PLACEHOLDER-team-pending";
        private const string VerifyUrl = "https://api.gumroad.com/v2/licenses/verify";

#if DEBUG
        // DEBUG 切替用環境変数（値: "Personal" / "Business" / "Team"、大小無視）
        private const string DebugTierEnvVar = "PDFPUZZLE_DEBUG_TIER";
#endif

        public static bool IsActivated()
        {
#if DEBUG
            return true;
#else
            var settings = AppSettings.Load();
            return !string.IsNullOrEmpty(settings.LicenseKey);
#endif
        }

        public static LicenseTier GetCurrentTier()
        {
#if DEBUG
            // 環境変数優先。未設定 / 解釈不能なら AppSettings 値にフォールバック
            var envValue = Environment.GetEnvironmentVariable(DebugTierEnvVar);
            if (!string.IsNullOrWhiteSpace(envValue) &&
                Enum.TryParse<LicenseTier>(envValue, ignoreCase: true, out var envTier))
            {
                return envTier;
            }
            return AppSettings.Load().LicenseTier;
#else
            return AppSettings.Load().LicenseTier;
#endif
        }

        /// <summary>1つの product_permalink に対し verify POST を1回叩いた結果。</summary>
        private readonly struct VerifyOutcome
        {
            /// <summary>HTTP 応答を受け取り、JSON の <c>success</c> が true。</summary>
            public bool Success { get; init; }
            /// <summary>verify 成功時の root JSON（success=true のときのみ有効）。</summary>
            public JsonDocument? Document { get; init; }
            /// <summary>success=false のときにユーザーへ返すメッセージ。</summary>
            public string Message { get; init; }
        }

        /// <summary>
        /// 指定 permalink で verify POST を1回実行する。
        /// HTTP 例外は呼び出し側に伝播させる（既存の個人版挙動と同じ catch で処理するため）。
        /// </summary>
        private static async Task<VerifyOutcome> VerifyWithPermalinkAsync(
            HttpClient client, string permalink, string licenseKey)
        {
            var payload = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("product_permalink", permalink),
                new KeyValuePair<string, string>("license_key", licenseKey),
                new KeyValuePair<string, string>("increment_uses_count", "true"),
            });

            var response = await client.PostAsync(VerifyUrl, payload);
            string json = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(json);
            bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();

            if (success)
            {
                return new VerifyOutcome { Success = true, Document = doc };
            }

            string msg = doc.RootElement.TryGetProperty("message", out var m)
                ? m.GetString() ?? string.Empty
                : LocalizationService.Get("License_ErrorInvalid");
            doc.Dispose();
            return new VerifyOutcome { Success = false, Document = null, Message = msg };
        }

        public static async Task<(bool Success, string Message)> ActivateAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return (false, LocalizationService.Get("License_ErrorEmpty"));

            string key = licenseKey.Trim();

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                // 1) 個人版/事業者版 permalink で verify。成功したら即終了
                //    （個人版の挙動・所要時間に影響させない）。
                var primary = await VerifyWithPermalinkAsync(client, ProductPermalink, key);

                VerifyOutcome outcome = primary;

                // 2) 個人版が失敗した場合のみ、チーム版 permalink で verify をリトライ。
                if (!primary.Success)
                {
                    var team = await VerifyWithPermalinkAsync(client, TeamProductPermalink, key);
                    if (team.Success)
                    {
                        outcome = team;
                    }
                    else
                    {
                        // 両方失敗 → 1回目（個人版）の失敗メッセージを返す。
                        team.Document?.Dispose();
                        return (false, primary.Message);
                    }
                }

                // --- ここから verify 成功（outcome.Document は非 null）---
                using var doc = outcome.Document!;
                var root = doc.RootElement;

                // 階層を判定（判定不能時は Personal にフォールバック）
                var tier = LicenseTierResolver.Resolve(root);
                int seatCount = LicenseTierResolver.ResolveSeatCount(root, tier);

                // 席消費。Team 以外は常に true（席を消費しない）。
                bool allowed = TeamSeatService.TryConsumeSeat(
                    key, tier, seatCount,
                    DeviceIdentifier.GetCurrent(), Environment.MachineName, Environment.UserName);

                if (!allowed)
                {
                    // 満席。settings を保存せず（=認証しない）に失敗を返す。
                    return (false, LocalizationService.Get("License_ErrorSeatsExceeded"));
                }

                var settings = AppSettings.Load();
                settings.LicenseKey = key;
                settings.LicenseTier = tier;
                settings.Save();
                return (true, string.Empty);
            }
            catch (HttpRequestException)
            {
                return (false, LocalizationService.Get("License_ErrorNetwork"));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static void Deactivate()
        {
            var settings = AppSettings.Load();

            // Team の場合、settings クリア前に席を返却する。
            if (settings.LicenseTier == LicenseTier.Team && !string.IsNullOrEmpty(settings.LicenseKey))
            {
                TeamSeatService.ReleaseSeat(
                    settings.LicenseKey, settings.LicenseTier, DeviceIdentifier.GetCurrent());
            }

            settings.LicenseKey = null;
            settings.LicenseTier = LicenseTier.Personal;
            settings.Save();
        }
    }
}
