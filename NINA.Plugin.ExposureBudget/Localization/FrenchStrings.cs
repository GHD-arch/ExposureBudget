using System.Globalization;

namespace NINA.Plugin.ExposureBudget.Localization {

    /// <summary>Chaines francaises (option "language=fr" ou "language=auto" sur une interface francaise).</summary>
    internal sealed class FrenchStrings : IPluginStrings {

        public string Language => "fr";

        public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

        /// <summary>
        /// Titre du panneau : comme le nom du plugin, il reste "Exposure Budget" dans toutes
        /// les langues. C'est un nom propre (identite du plugin), pas un libelle d'interface.
        /// </summary>
        public string PanelTitle => "Exposure Budget";

        public string WindowGroupHeader => "Fenêtre d'acquisition";

        public string StartLabel => "Début";

        public string EndLabel => "Fin";

        public string StartHourTooltip => "Heure de début de la fenêtre d'acquisition";

        public string StartMinuteTooltip => "Minutes du début de la fenêtre d'acquisition";

        public string EndHourTooltip => "Heure de fin de la fenêtre ; une fin antérieure au début est comprise comme le lendemain matin";

        public string EndMinuteTooltip => "Minutes de la fin de la fenêtre d'acquisition";

        public string WindowDurationLabel => "Durée disponible";

        public string SessionTooltip => "Séance reconstituée : la date de fin passe au lendemain lorsque Hf précède Hd";

        public string NowButtonText => "Maintenant";

        public string NowButtonTooltip => "Remplit l'heure de début avec l'heure courante";

        public string ExposureGroupHeader => "Temps de pose";

        public string ExposureLabel => "Pose";

        public string ExposureTooltip => "Temps de pose unitaire en secondes : pas de 15 s avec les flèches, saisie libre au clavier";

        public string ExposureMinutesTooltip => "Équivalence du temps de pose en minutes (information seule)";

        public string NightButtonText => "Nuit astronomique (N.I.N.A.)";

        public string NightButtonTooltip => "Remplit l'heure de début et l'heure de fin avec le crépuscule et l'aube astronomiques calculés par N.I.N.A. pour le profil d'observatoire actif";

        public string ResultGroupHeader => "Nombre de poses";

        public string CopyButtonText => "Copier le résultat";

        public string CopyButtonTooltip => "Copie le nombre de poses dans le presse-papiers";

        public string CopyFeedback => "Nombre de poses copié dans le presse-papiers.";

        public string CopyFailedFeedback => "Accès au presse-papiers impossible.";

        public string PluginShortDescription => "Calcule combien de poses d'une durée donnée tiennent entre une heure de début et une heure de fin, en tenant compte du passage de minuit.";

        public string PluginLongDescription =>
            "Exposure Budget détermine combien de poses d'une durée donnée peuvent être capturées entre le début " +
            "et la fin d'une séance, y compris lorsque celle-ci se poursuit après minuit. Le nombre de poses suit " +
            "la règle demandée : une partie décimale inférieure à 0,5 arrondit à la pose inférieure, 0,5 et " +
            "au-delà ajoute une pose. La fenêtre d'acquisition peut être remplie automatiquement à partir de la " +
            "nuit astronomique calculée par N.I.N.A. pour votre profil d'observatoire. Le panneau et cette " +
            "description suivent la langue de N.I.N.A. (français ou anglais, ou forcée par language=fr / " +
            "language=en dans ExposureBudget.settings).";

        public string EmptyCountDisplay => "–";

        public string WindowSecondsFormat => "{0} s";

        public string MinutesSecondsFormat => "{0} min {1} s";

        public string SecondsOnlyFormat => "{0} s";

        public string DecimalMinutesFormat => "soit {0} min";

        public string ResultDetailFormat => "{0} s ÷ {1} s = {2} → {3} {4}";

        public string RoundingUpFormat => "Partie décimale {0} ≥ 0,5 : une pose supplémentaire est comptée.";

        public string RoundingDownFormat => "Partie décimale {0} < 0,5 : la pose incomplète n'est pas comptée.";

        public string RoundingExact => "Le nombre de poses tombe juste : aucun arrondi n'est appliqué.";

        public string CoverageFormat => "Temps couvert : {0} ({1} × {2} s) · Reliquat : {3}";

        public string PoseEndFormat => "Dernière pose : {0}";

        public string PoseWordSingular => "pose";

        public string PoseWordPlural => "poses";

        public string WarningSameHours => "Heures de début et de fin identiques : la fenêtre est nulle, aucune pose n'est possible. Vérifiez les heures de début et de fin.";

        public string WarningExposureTooLongFormat => "Le temps de pose ({0} s) est plus long que la fenêtre disponible ({1}) : aucune pose complète n'est possible.";

        public string WarningOverrunFormat => "Les {0} {1} retenues dépassent la fenêtre de {2} : la dernière pose se termine après l'heure de fin. Réduisez le temps de pose ou avancez le début.";

        public string InvalidExposureWarning => "Le temps de pose doit être supérieur à 0 seconde.";

        public string NightAstronomical => "Nuit astronomique";

        public string NightNautical => "Nuit nautique";

        public string NightCivil => "Nuit civile";

        public string NightSun => "Coucher / lever du Soleil";

        public string NightInfoFormat => "{0} du {1:dd/MM/yyyy} : {2:HH:mm} → {3:HH:mm} le {4:dd/MM/yyyy}.";

        public string NightCalculatorUnavailableWarning => "Le calculateur de nuit de N.I.N.A. n'est pas disponible : saisissez les heures manuellement.";

        public string NightCalculationFailedWarning => "Le calcul de la nuit astronomique a échoué : saisissez les heures manuellement.";

        public string NightUnavailableWarning => "Aucun crépuscule exploitable pour cette date (nuit blanche ou latitude polaire) : saisissez les heures manuellement.";
    }
}
