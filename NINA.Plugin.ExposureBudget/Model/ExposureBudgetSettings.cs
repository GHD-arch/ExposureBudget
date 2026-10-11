using NINA.Core.Utility;
using System;
using System.Globalization;
using System.IO;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>
    /// Derniers reglages utilises, memorises dans un petit fichier texte place a cote du
    /// plugin (le dossier du plugin est conserve lors d'une mise a jour du dll).
    ///
    /// Le format est volontairement minimal et sans dependance externe (ni JSON, ni
    /// package NuGet) : un plugin charge dans l'application hote ne doit pas dependre
    /// d'assemblies de serialisation dont la version peut varier.
    ///
    /// Format : une ligne "cle=valeur" par reglage, les lignes vides et celles
    /// commencant par # ou ; sont ignorees. Le fichier est modifiable a la main.
    /// </summary>
    public sealed class ExposureBudgetSettings {

        private const string FileName = "ExposureBudget.settings";

        /// <summary>
        /// Version du format des reglages.
        ///
        /// Un fichier sans ce marqueur provient d'une version anterieure du plugin : ses
        /// valeurs par defaut peuvent avoir change. C'est notamment le cas de la langue,
        /// que la premiere version ecrivait en anglais par defaut ; sans cette migration,
        /// un fichier ecrit a l'epoque continuerait de forcer l'anglais et le panneau ne
        /// suivrait plus la langue de N.I.N.A. Les autres reglages de l'utilisateur
        /// (heures, temps de pose) sont conserves.
        /// </summary>
        public const int CurrentSettingsVersion = 1;

        public int SettingsVersion { get; set; } = CurrentSettingsVersion;

        /// <summary>
        /// Langue de l'interface : "auto" (defaut : suit la langue de N.I.N.A.), "en" ou "fr".
        /// </summary>
        public string Language { get; set; } = Localization.PluginStrings.DefaultLanguage;

        public int StartHour { get; set; } = 21;

        public int StartMinute { get; set; } = 30;

        public int EndHour { get; set; } = 3;

        public int EndMinute { get; set; } = 15;

        public double ExposureSeconds { get; set; } = 300d;

        /// <summary>
        /// Chemin du fichier de reglages : dossier du plugin, avec repli sur
        /// %LOCALAPPDATA%\NINA\Plugins\ExposureBudget si ce dossier n'est pas accessible.
        /// </summary>
        public static string SettingsFilePath {
            get {
                try {
                    var assemblyFolder = Path.GetDirectoryName(typeof(ExposureBudgetSettings).Assembly.Location);
                    if (!string.IsNullOrEmpty(assemblyFolder) && Directory.Exists(assemblyFolder)) {
                        return Path.Combine(assemblyFolder, FileName);
                    }
                } catch (Exception ex) {
                    Logger.Error("ExposureBudget : impossible de determiner le dossier du plugin", ex);
                }

                var fallbackFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NINA", "Plugins", "ExposureBudget");
                return Path.Combine(fallbackFolder, FileName);
            }
        }

        public static ExposureBudgetSettings Load() {
            var settings = new ExposureBudgetSettings();

            try {
                var path = SettingsFilePath;
                if (!File.Exists(path)) { return settings; }

                // La version lue est celle du fichier : 0 tant que le marqueur n'a pas ete vu.
                settings.SettingsVersion = 0;

                foreach (var rawLine in File.ReadAllLines(path)) {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) {
                        continue;
                    }

                    var separator = line.IndexOf('=');
                    if (separator <= 0) { continue; }

                    var key = line.Substring(0, separator).Trim();
                    var value = line.Substring(separator + 1).Trim();
                    settings.Apply(key, value);
                }

                if (settings.SettingsVersion < CurrentSettingsVersion) {
                    settings.Language = Localization.PluginStrings.DefaultLanguage;
                    Logger.Info("ExposureBudget: settings file from an older version detected, the language is reset to the current default");
                }
            } catch (Exception ex) {
                Logger.Error("ExposureBudget : lecture des reglages impossible, valeurs par defaut utilisees", ex);
            }

            return settings;
        }

        public void Save() {
            try {
                var path = SettingsFilePath;
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) {
                    Directory.CreateDirectory(folder);
                }

                var lines = new[] {
                    "# Exposure Budget - plugin settings, editable by hand.",
                    "# language : auto (default, follows the N.I.N.A. user interface language), en, or fr.",
                    "# The window times use the 24 hour clock, the exposure time is in seconds.",
                    "settingsVersion=" + CurrentSettingsVersion.ToString(CultureInfo.InvariantCulture),
                    "language=" + (string.IsNullOrWhiteSpace(Language) ? Localization.PluginStrings.DefaultLanguage : Language.Trim()),
                    "startHour=" + StartHour.ToString(CultureInfo.InvariantCulture),
                    "startMinute=" + StartMinute.ToString(CultureInfo.InvariantCulture),
                    "endHour=" + EndHour.ToString(CultureInfo.InvariantCulture),
                    "endMinute=" + EndMinute.ToString(CultureInfo.InvariantCulture),
                    "exposureSeconds=" + ExposureSeconds.ToString("0.###", CultureInfo.InvariantCulture)
                };

                File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
            } catch (Exception ex) {
                Logger.Error("ExposureBudget : ecriture des reglages impossible", ex);
            }
        }

        private void Apply(string key, string value) {
            switch (key.ToLowerInvariant()) {
                case "settingsversion":
                    if (TryParseInt(value, out var settingsVersion)) { SettingsVersion = settingsVersion; }
                    break;

                case "language":
                    if (!string.IsNullOrWhiteSpace(value)) { Language = value.Trim(); }
                    break;

                case "starthour":
                    if (TryParseInt(value, out var startHour)) { StartHour = startHour; }
                    break;

                case "startminute":
                    if (TryParseInt(value, out var startMinute)) { StartMinute = startMinute; }
                    break;

                case "endhour":
                    if (TryParseInt(value, out var endHour)) { EndHour = endHour; }
                    break;

                case "endminute":
                    if (TryParseInt(value, out var endMinute)) { EndMinute = endMinute; }
                    break;

                case "exposureseconds":
                    if (TryParseDouble(value, out var exposureSeconds)) { ExposureSeconds = exposureSeconds; }
                    break;

                default:
                    break;
            }
        }

        private static bool TryParseInt(string value, out int parsed) {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                || int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed);
        }

        private static bool TryParseDouble(string value, out double parsed) {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);
        }
    }
}
