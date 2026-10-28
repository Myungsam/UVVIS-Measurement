using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace UvVisMeasurement.Localization
{
    /// <summary>
    /// Korean / English text for the user interface.
    ///
    /// English is written inline at every call site - L.T(key, english) - so the
    /// code reads naturally and a missing translation degrades to English rather than to a
    /// key. Korean lives in the embedded UTF-8 file Localization\Strings_ko.txt
    /// (<c>key = 번역</c>), which a translator can edit without touching code and which
    /// ships inside the EXE - no satellite assembly folder to forget when deploying.
    /// A unit test checks that every key used in the code has a Korean entry.
    ///
    /// Numbers are always formatted with the invariant culture, like the CSV files.
    /// </summary>
    internal static class L
    {
        public const string English = "en";
        public const string Korean = "ko";

        // A "Strings.ko.txt" name would make MSBuild treat ".ko." as a culture and move the
        // resource into a ko\ satellite assembly; the underscore keeps it inside the EXE.
        public const string KoreanResourceName = "UvVisMeasurement.Localization.Strings_ko.txt";

        private static Dictionary<string, string> _korean;

        // Read from the acquisition thread while only the UI thread writes it, so the field
        // is volatile and the Korean table is filled in before Language points at it.
        private static volatile string _language = English;

        public static string Language
        {
            get { return _language; }
        }

        public static bool IsKorean
        {
            get { return Language == Korean; }
        }

        public static event EventHandler LanguageChanged;

        /// <summary>The saved choice when there is one, otherwise the Windows display language.</summary>
        public static string ResolveStartupLanguage(string saved)
        {
            if (saved == English || saved == Korean)
                return saved;

            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Korean ? Korean : English;
        }

        public static void SetLanguage(string language)
        {
            string wanted = language == Korean ? Korean : English;
            if (wanted == Korean && _korean == null)
                _korean = LoadEmbedded(KoreanResourceName);

            if (wanted == _language)
                return;

            _language = wanted;
            EventHandler handler = LanguageChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        public static string T(string key, string english)
        {
            string text;
            if (IsKorean && _korean != null && _korean.TryGetValue(key, out text))
                return text;

            return english;
        }

        public static string F(string key, string englishFormat, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(key, englishFormat), args);
        }

        private static Dictionary<string, string> LoadEmbedded(string resourceName)
        {
            using (Stream stream = typeof(L).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return new Dictionary<string, string>(StringComparer.Ordinal);

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return Parse(reader);
            }
        }

        /// <summary>
        /// <c>key = text</c> per line; blank lines and lines starting with <c>#</c> are ignored;
        /// <c>\n</c> in the text becomes a line break.
        /// </summary>
        public static Dictionary<string, string> Parse(TextReader reader)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                    continue;

                int separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = trimmed.Substring(0, separator).Trim();
                string value = trimmed.Substring(separator + 1).Trim().Replace("\\n", "\n");
                table[key] = value;
            }
            return table;
        }
    }
}
