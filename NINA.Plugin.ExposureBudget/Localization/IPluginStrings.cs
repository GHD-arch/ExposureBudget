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

        #region Plan par filtres

        /// <summary>Onglet du calcul simple (fenetre + une seule pose).</summary>
        string TabSingleExposure { get; }

        /// <summary>Onglet du plan par filtres.</summary>
        string TabFilterPlan { get; }

        string FilterColumn { get; }

        string FilterExposureColumn { get; }

        string FilterPercentColumn { get; }

        string FilterCountColumn { get; }

        /// <summary>Bouton qui coche tous les filtres.</summary>
        string FilterCheckAllButton { get; }

        /// <summary>Info-bulle du bouton « tout cocher ».</summary>
        string FilterCheckAllTooltip { get; }

        /// <summary>Info-bulle du bouton « repartir 100 % ».</summary>
        string FilterDistributeTooltip { get; }

        /// <summary>Info-bulle du bouton « reinitialiser ».</summary>
        string FilterResetTooltip { get; }

        /// <summary>Bouton qui repartit 100 % entre les filtres coches.</summary>
        string FilterDistributeButton { get; }

        /// <summary>Bouton qui remet le plan a ses valeurs par defaut.</summary>
        string FilterResetButton { get; }

        /// <summary>Ligne de totaux : {0} % total, {1} nombre de poses avec son mot, {2} duree couverte.</summary>
        string FilterTotalsFormat { get; }

        /// <summary>Temps restant : {0} duree.</summary>
        string FilterRemainingFormat { get; }

        /// <summary>Erreur de saisie : {0} % au total, maximum 100 %.</summary>
        string FilterPercentOverflowFormat { get; }

        /// <summary>Refus : les poses arrondies depassent la fenetre de {0}.</summary>
        string FilterCoverageOverflowFormat { get; }

        /// <summary>Aucun filtre coche.</summary>
        string FilterEmptyWarning { get; }

        string FilterExposureTooltip { get; }

        string FilterPercentTooltip { get; }

        /// <summary>Bouton par ligne qui donne les pourcentages non utilises a ce filtre.</summary>
        string FilterRemainderButton { get; }

        string FilterRemainderTooltip { get; }

        /// <summary>Ajout / suppression de lignes dans la liste des filtres.</summary>
        string FilterAddButton { get; }

        string FilterAddTooltip { get; }

        string FilterRemoveButton { get; }

        string FilterRemoveTooltip { get; }

        /// <summary>Nom propose pour un filtre ajoute.</summary>
        string FilterNewName { get; }

        string FilterNameTooltip { get; }

        /// <summary>Bouton qui copie le plan (une ligne par filtre).</summary>
        string FilterPlanCopyButton { get; }

        string FilterPlanCopyTooltip { get; }

        string FilterPlanCopyFeedback { get; }

        /// <summary>Ligne du plan copiee : {0} filtre, {1} poses, {2} temps de pose, {3} duree couverte.</summary>
        string FilterPlanLineFormat { get; }

        #region Surcharge et temps a reserver

        string OverheadLabel { get; }

        string OverheadTooltip { get; }

        /// <summary>Temps consomme par une pose : {0} duree.</summary>
        string OverheadInfoFormat { get; }

        /// <summary>Detail du calcul avec surcharge : fenetre, pose, surcharge, exact, poses, mot.</summary>
        string OverheadDetailFormat { get; }

        string ReservesTabTitle { get; }

        string ReservesIntro { get; }

        string FlipReserveLabel { get; }

        string FixedReserveLabel { get; }

        string AutofocusEveryLabel { get; }

        string AutofocusDurationLabel { get; }

        /// <summary>Resume : {0} temps reserve, {1} fenetre exploitable.</summary>
        string ReservesSummaryFormat { get; }

        string ReservesEmptyHint { get; }

        /// <summary>Avertissement : les reserves depassent la fenetre de {0}.</summary>
        string ReservesExceedFormat { get; }

        /// <summary>Surcharge incluse dans le plan : {0} par pose.</summary>
        string PlanOverheadHintFormat { get; }

        #endregion Surcharge et temps a reserver

        #endregion Plan par filtres

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

        string NightFallbackInfoFormat { get; }

        string NightCalculatorUnavailableWarning { get; }

        string NightCalculationFailedWarning { get; }

        string NightUnavailableWarning { get; }

        #endregion Nuit astronomique
    }
}
