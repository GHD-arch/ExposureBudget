using System;
using System.Globalization;

namespace NINA.Plugin.ExposureBudget.Localization {

    /// <summary>
    /// Selection de la langue du panneau.
    ///
    /// "auto" : valeur par defaut. Le panneau suit la langue de N.I.N.A.
    ///          (<c>profil actif &gt; ApplicationSettings.Language</c>), avec repli sur la
    ///          culture d'interface du fil courant si le profil n'est pas encore disponible.
    /// "en"   : anglais, quelle que soit la langue de N.I.N.A.
    /// "fr"   : francais, quelle que soit la langue de N.I.N.A.
    ///
    /// La culture d'affichage des nombres et des dates suit la langue retenue, afin que
    /// le panneau reste coherent dans les deux cas (le separateur de milliers et le
    /// separateur decimal different entre l'anglais et le francais).
    /// </summary>
    public static class PluginStrings {

        public const string AutoLanguage = "auto";

        /// <summary>Langue utilisee tant que l'utilisateur n'a rien choisi : suivi de N.I.N.A.</summary>
        public const string DefaultLanguage = AutoLanguage;

        private static readonly IPluginStrings English = new EnglishStrings();

        private static readonly IPluginStrings French = new FrenchStrings();

        /// <summary>
        /// Resout la langue a utiliser.
        /// </summary>
        /// <param name="language">Valeur du reglage "language" ("auto", "en", "fr", vide ou inconnue).</param>
        public static IPluginStrings Resolve(string language) => Resolve(language, null);

        /// <summary>
        /// Resout la langue a utiliser en tenant compte de la langue de N.I.N.A.
        /// </summary>
        /// <param name="language">Valeur du reglage "language" ("auto", "en", "fr", vide ou inconnue).</param>
        /// <param name="ninaLanguage">
        /// Langue de l'interface de N.I.N.A. (<c>ActiveProfile.ApplicationSettings.Language</c>).
        /// Peut etre nulle : la culture d'interface courante sert alors de repli.
        /// </param>
        public static IPluginStrings Resolve(string language, CultureInfo ninaLanguage) {
            switch ((language ?? string.Empty).Trim().ToLowerInvariant()) {
                case "fr":
                case "fra":
                case "french":
                case "fr-fr":
                    return French;

                case "en":
                case "eng":
                case "english":
                case "en-us":
                    return English;

                // "auto", vide ou valeur inconnue : on suit N.I.N.A.
                default:
                    return IsFrench(ninaLanguage ?? CultureInfo.CurrentUICulture) ? French : English;
            }
        }

        /// <summary>Vrai si la culture donnée est une culture française.</summary>
        public static bool IsFrench(CultureInfo culture) =>
            culture != null && string.Equals(culture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase);
    }
}
