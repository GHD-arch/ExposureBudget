using NINA.Core.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Globalization;

namespace NINA.Plugin.ExposureBudget.Localization {

    /// <summary>
    /// Determination de la langue du plugin, partagee par le panneau et par la fiche du
    /// plugin (Options &gt; Plugins) : le reglage "language" du fichier de reglages est
    /// prioritaire, sinon la langue de l'interface de N.I.N.A. est utilisee.
    /// </summary>
    public static class PluginLanguage {

        /// <summary>Resout la langue a utiliser a partir du reglage et de la langue de N.I.N.A.</summary>
        public static IPluginStrings Resolve(string languageSetting, IProfileService profileService)
            => PluginStrings.Resolve(languageSetting, GetNinaLanguage(profileService));

        /// <summary>
        /// Langue de l'interface de N.I.N.A. : c'est le profil actif qui la porte
        /// (ApplicationSettings.Language, renseigne par Options &gt; Langue).
        /// Le profil peut ne pas encore etre charge au demarrage du plugin : on se replie
        /// alors sur la culture d'interface du fil courant.
        /// </summary>
        public static CultureInfo GetNinaLanguage(IProfileService profileService) {
            try {
                var activeProfile = profileService?.ActiveProfile;
                var language = activeProfile?.ApplicationSettings?.Language;
                if (language != null) { return language; }
            } catch (Exception ex) {
                // Repli sans gravite : le plugin reste utilisable, il suit simplement la
                // culture d'interface du fil courant.
                Logger.Warning($"ExposureBudget: reading the N.I.N.A. language failed ({ex.Message}), falling back to the current UI culture");
            }

            return CultureInfo.CurrentUICulture;
        }
    }
}
