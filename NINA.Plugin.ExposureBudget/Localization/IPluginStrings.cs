using System.Globalization;

namespace NINA.Plugin.ExposureBudget.Localization {

    /// <summary>
    /// Ensemble des chaines et de la culture d'affichage du panneau.
    /// L'anglais est la langue par defaut du plugin (convention des plugins N.I.N.A.),
    /// le francais peut etre choisi dans les reglages ("language=fr").
    /// </summary>
    public interface IPluginStrings {

        /// <summary>Code de langue utilise ("en" ou "fr").</summary>
        string Language { get; }

        /// <summary>Culture d'affichage des nombres et des dates, coherente avec la langue.</summary>
        CultureInfo Culture { get; }

        #region Libelles du panneau

        string PanelTitle { get; }

        string WindowGroupHeader { get; }

        string StartLabel { get; }

        string EndLabel { get; }

        string StartHourTooltip { get; }

        string StartMinuteTooltip { get; }

        string EndHourTooltip { get; }

        string EndMinuteTooltip { get; }

        string WindowDurationLabel { get; }

        string SessionTooltip { get; }

        /// <summary>Bouton qui remplit l'heure de debut avec l'heure courante.</summary>
        string NowButtonText { get; }

        string NowButtonTooltip { get; }

        string ExposureGroupHeader { get; }

        string ExposureLabel { get; }

        string ExposureTooltip { get; }

        string ExposureMinutesTooltip { get; }

        string NightButtonText { get; }

        string NightButtonTooltip { get; }

        string ResultGroupHeader { get; }

        /// <summary>Bouton qui copie le resultat dans le presse-papiers.</summary>
        string CopyButtonText { get; }

        string CopyButtonTooltip { get; }

        /// <summary>Confirmation affichee apres une copie reussie.</summary>
        string CopyFeedback { get; }

        /// <summary>Echec de la copie (presse-papiers indisponible).</summary>
        string CopyFailedFeedback { get; }

        #endregion Libelles du panneau

        #region Fiche du plugin (Options > Plugins)

        /// <summary>
        /// Presentation courte du plugin, affichee dans la liste des plugins de N.I.N.A.
        /// Le NOM du plugin reste "Exposure Budget" dans les deux langues : c'est son nom propre.
        /// </summary>
        string PluginShortDescription { get; }

        /// <summary>Presentation detaillee du plugin, affichee dans la fiche du plugin.</summary>
        string PluginLongDescription { get; }

        #endregion Fiche du plugin (Options > Plugins)

        #region Formats et messages

        string EmptyCountDisplay { get; }

        string WindowSecondsFormat { get; }

        string MinutesSecondsFormat { get; }

        string SecondsOnlyFormat { get; }

        string DecimalMinutesFormat { get; }

        string ResultDetailFormat { get; }

        string RoundingUpFormat { get; }

        string RoundingDownFormat { get; }

        string RoundingExact { get; }

        string CoverageFormat { get; }

        string PoseEndFormat { get; }

        string PoseWordSingular { get; }

        string PoseWordPlural { get; }

        string WarningSameHours { get; }

        string WarningExposureTooLongFormat { get; }

        string WarningOverrunFormat { get; }

        string InvalidExposureWarning { get; }

        #endregion Formats et messages

        #region Nuit astronomique

        string NightAstronomical { get; }

        string NightNautical { get; }

        string NightCivil { get; }

        string NightSun { get; }

        string NightInfoFormat { get; }

        string NightCalculatorUnavailableWarning { get; }

        string NightCalculationFailedWarning { get; }

        string NightUnavailableWarning { get; }

        #endregion Nuit astronomique
    }
}
