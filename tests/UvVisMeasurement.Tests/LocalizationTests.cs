using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Localization;

namespace UvVisMeasurement.Tests
{
    /// <summary>
    /// Guards the Korean UI text against the two failures that would otherwise only show up
    /// on a Korean machine: a key used in the code with no translation, and a translation
    /// whose {0}... placeholders do not match the English text it replaces.
    /// </summary>
    [TestClass]
    public class LocalizationTests
    {
        private static readonly Regex CallPattern = new Regex(
            "L\\.[TF]\\(\\s*\"(?<key>[^\"]+)\"\\s*,\\s*(?<english>(?:\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\+?\\s*)+)",
            RegexOptions.Compiled);

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)", RegexOptions.Compiled);

        private static string AppRoot
        {
            get
            {
                var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (directory != null)
                {
                    string candidate = Path.Combine(directory.FullName, "UV-VIS_measurement_code");
                    if (Directory.Exists(candidate))
                        return candidate;

                    directory = directory.Parent;
                }

                throw new InvalidOperationException("UV-VIS_measurement_code was not found above " +
                                                    AppDomain.CurrentDomain.BaseDirectory);
            }
        }

        private static Dictionary<string, string> UsedKeys()
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string file in Directory.GetFiles(AppRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(@"\obj\") || file.Contains(@"\bin\"))
                    continue;

                foreach (Match match in CallPattern.Matches(File.ReadAllText(file)))
                {
                    var english = new StringBuilder();
                    foreach (Match piece in Regex.Matches(match.Groups["english"].Value, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                        english.Append(piece.Groups[1].Value);

                    keys[match.Groups["key"].Value] = english.ToString();
                }
            }

            return keys;
        }

        private static Dictionary<string, string> Korean()
        {
            string path = Path.Combine(AppRoot, "Localization", "Strings_ko.txt");
            Assert.IsTrue(File.Exists(path), path + " is missing");

            using (var reader = new StreamReader(path, Encoding.UTF8))
                return L.Parse(reader);
        }

        [TestMethod]
        public void EveryKeyUsedInTheCodeHasAKoreanTranslation()
        {
            Dictionary<string, string> used = UsedKeys();
            Dictionary<string, string> korean = Korean();

            Assert.IsTrue(used.Count > 100, "only " + used.Count + " keys found - the scan looks broken");

            string[] missing = used.Keys.Where(k => !korean.ContainsKey(k)).OrderBy(k => k).ToArray();
            Assert.AreEqual(0, missing.Length, "no Korean text for: " + string.Join(", ", missing));
        }

        [TestMethod]
        public void TheKoreanFileHasNoKeysTheCodeNeverUses()
        {
            string[] orphans = Korean().Keys.Where(k => !UsedKeys().ContainsKey(k)).OrderBy(k => k).ToArray();

            Assert.AreEqual(0, orphans.Length, "unused Korean entries: " + string.Join(", ", orphans));
        }

        [TestMethod]
        public void TranslationsKeepThePlaceholdersOfTheEnglishText()
        {
            Dictionary<string, string> korean = Korean();
            var wrong = new List<string>();

            foreach (KeyValuePair<string, string> pair in UsedKeys())
            {
                string translation;
                if (!korean.TryGetValue(pair.Key, out translation))
                    continue;

                if (!Indexes(pair.Value).SetEquals(Indexes(translation)))
                    wrong.Add(pair.Key);
            }

            Assert.AreEqual(0, wrong.Count, "placeholders differ from the English text: " + string.Join(", ", wrong));
        }

        private static HashSet<string> Indexes(string text)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Placeholder.Matches(text))
                found.Add(match.Groups[1].Value);

            return found;
        }

        [TestMethod]
        public void ParseReadsKeysCommentsAndLineBreaks()
        {
            using (var reader = new StringReader("# comment\n\nkey.one = 값\nkey.two=두 줄\\n둘째\nbroken\n"))
            {
                Dictionary<string, string> table = L.Parse(reader);

                Assert.AreEqual(2, table.Count);
                Assert.AreEqual("값", table["key.one"]);
                Assert.AreEqual("두 줄\n둘째", table["key.two"]);
            }
        }

        [TestMethod]
        public void StartupLanguageFollowsTheSavedChoiceThenWindows()
        {
            Assert.AreEqual(L.Korean, L.ResolveStartupLanguage(L.Korean));
            Assert.AreEqual(L.English, L.ResolveStartupLanguage(L.English));

            string fromWindows = L.ResolveStartupLanguage(string.Empty);
            Assert.IsTrue(fromWindows == L.English || fromWindows == L.Korean);
            Assert.AreEqual(fromWindows, L.ResolveStartupLanguage("de"));
        }

        [TestMethod]
        public void EnglishIsUsedWhenNoTranslationIsLoaded()
        {
            Assert.AreEqual(L.English, L.Language, "the test host must start in English");
            Assert.AreEqual("English text", L.T("no.such.key", "English text"));
            Assert.AreEqual("value 7", L.F("no.such.key", "value {0}", 7));
        }
    }
}
