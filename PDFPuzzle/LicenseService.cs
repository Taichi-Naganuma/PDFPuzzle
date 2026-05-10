using System.Net.Http;
using System.Text.Json;

namespace PDFPuzzle
{
    public static class LicenseService
    {
        private const string ProductPermalink = "zjmmda";
        private const string VerifyUrl = "https://api.gumroad.com/v2/licenses/verify";

#if DEBUG
        // DEBUG 切替用環境変数（値: "Personal" / "Business"、大小無視）
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

        public static async Task<(bool Success, string Message)> ActivateAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return (false, LocalizationService.Get("License_ErrorEmpty"));

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                var payload = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("product_permalink", ProductPermalink),
                    new KeyValuePair<string, string>("license_key", licenseKey.Trim()),
                    new KeyValuePair<string, string>("increment_uses_count", "true"),
                });

                var response = await client.PostAsync(VerifyUrl, payload);
                string json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                bool success = doc.RootElement.GetProperty("success").GetBoolean();

                if (success)
                {
                    // 階層を判定（判定不能時は Personal にフォールバック）
                    var tier = LicenseTierResolver.Resolve(doc.RootElement);

                    var settings = AppSettings.Load();
                    settings.LicenseKey = licenseKey.Trim();
                    settings.LicenseTier = tier;
                    settings.Save();
                    return (true, string.Empty);
                }

                string msg = doc.RootElement.TryGetProperty("message", out var m)
                    ? m.GetString() ?? string.Empty
                    : LocalizationService.Get("License_ErrorInvalid");
                return (false, msg);
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
            settings.LicenseKey = null;
            settings.LicenseTier = LicenseTier.Personal;
            settings.Save();
        }
    }
}
