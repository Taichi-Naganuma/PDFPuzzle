using System.Net.Http;
using System.Text.Json;

namespace PDFPuzzle
{
    public static class LicenseService
    {
        private const string ProductPermalink = "zjmmda";
        private const string VerifyUrl = "https://api.gumroad.com/v2/licenses/verify";

        public static bool IsActivated()
        {
#if DEBUG
            return true;
#endif
            var settings = AppSettings.Load();
            return !string.IsNullOrEmpty(settings.LicenseKey);
        }

        public static LicenseTier GetCurrentTier()
        {
#if DEBUG
            return LicenseTier.Business;
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
                    var settings = AppSettings.Load();
                    settings.LicenseKey = licenseKey.Trim();
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
            settings.Save();
        }
    }
}

