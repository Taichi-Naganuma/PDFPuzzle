using System.Windows;

namespace PDFPuzzle
{
    public static class LocalizationService
    {
        public const string Japanese = "ja";
        public const string English = "en";

        private static string _currentLanguage = Japanese;

        public static string CurrentLanguage => _currentLanguage;

        public static event EventHandler? LanguageChanged;

        public static void Apply(string language)
        {
            _currentLanguage = language;

            string dictUri = language == English
                ? "Strings.en.xaml"
                : "Strings.ja.xaml";

            var dict = new ResourceDictionary
            {
                Source = new Uri(dictUri, UriKind.Relative)
            };

            var app = Application.Current;
            var existing = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("Strings.ja") ||
                     d.Source.OriginalString.Contains("Strings.en")));

            if (existing != null)
                app.Resources.MergedDictionaries.Remove(existing);

            app.Resources.MergedDictionaries.Add(dict);

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string Get(string key)
        {
            if (Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key] as string ?? key;
            return key;
        }
    }
}

