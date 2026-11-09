using System;
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

        public string NowButtonTooltip => "Remplit l'heure de début avec l'heure courante (la date de référence passe au jour courant : un clic le matin ne place pas la séance la veille)";

        public string ExposureGroupHeader => "Temps de pose";

        public string ExposureLabel => "Pose";

        public string ExposureTooltip => "Temps de pose unitaire en secondes : pas de 15 s avec les flèches, saisie libre au clavier";

        public string ExposureMinutesTooltip => "Équivalence du temps de pose en minutes (information seule)";

        public string NightButtonText => "Nuit astronomique (N.I.N.A.)";

        public string NightButtonTooltip => "Remplit l'heure de début et l'heure de fin avec le crépuscule et l'aube astronomiques calculés par N.I.N.A. pour le profil d'observatoire actif";

        public string ResultGroupHeader => "Nombre de poses";

        public string CopyButtonText => "Copier le résultat";

        public string CopyButtonTooltip => "Copie uniquement le nombre de poses (la valeur affichée en grand) dans le presse-papiers";

        public string CopyFeedback => "Nombre de poses copié dans le presse-papiers.";

        public string CopyFailedFeedback => "Accès au presse-papiers impossible.";

        #region Plan par filtres

        public string TabSingleExposure => "Pose unique";

        public string TabFilterPlan => "Plan par filtres";

        public string FilterColumn => "Filtre";

        public string FilterExposureColumn => "Pose (s)";

        public string FilterPercentColumn => "%";

        public string FilterCountColumn => "Poses";

        public string FilterCheckAllButton => "Tout cocher";

        public string FilterCheckAllTooltip => "Coche tous les filtres du plan ; les temps de pose et les pourcentages sont conservés";

        public string FilterDistributeButton => "Répartir 100 %";

        public string FilterDistributeTooltip => "Répartit 100 % entre les filtres cochés, par pas de 5 points (le reliquat va aux premiers)";

        public string FilterResetButton => "Réinitialiser";

        public string FilterResetTooltip => "Rétablit les filtres par défaut (L, R, G, B, S, H, O à 40/15/15/15/5/7/3 %)";

        public string FilterTotalsFormat => "{0} % · {1} {2} · {3}";

        public string FilterRemainingFormat => "Reste {0}";

        public string FilterPercentOverflowFormat => "Total des pourcentages : {0} % (maximum 100 %).";

        public string FilterCoverageOverflowFormat => "Les poses arrondies dépassent la fenêtre de {0} : réduisez un pourcentage ou un temps de pose.";

        public string FilterEmptyWarning => "Aucun filtre sélectionné.";

        public string FilterExposureTooltip => "Temps de pose de ce filtre ; pas de 5 s avec les flèches, saisie libre au clavier";

        public string FilterPercentTooltip => "Part du temps disponible allouée à ce filtre";

        public string FilterRemainderButton => ">";

        public string FilterRemainderTooltip => "Attribue les points de pourcentage non utilisés (100 moins le total) à ce filtre ; actif seulement s'il en reste";

        public string FilterAddButton => "+ filtre";

        public string FilterAddTooltip => "Ajouter un filtre en fin de liste";

        public string FilterRemoveButton => "- filtre";

        public string FilterRemoveTooltip => "Supprimer le dernier filtre de la liste";

        public string FilterNewName => "Nouveau";

        public string FilterNameTooltip => "Nom du filtre, modifiable";

        public string FilterPlanCopyButton => "Copier le plan";

        public string FilterPlanCopyTooltip => "Copie une ligne par filtre actif (poses et temps couvert), puis les totaux ; désactivé tant que le plan est refusé";

        public string FilterPlanCopyFeedback => "Plan par filtres copié dans le presse-papiers.";

        public string FilterPlanLineFormat => "{0} : {1} × {2} s ({3})";

        public string OverheadLabel => "Surcharge";

        public string OverheadTooltip => "Temps consommé par chaque pose en plus de l'exposition elle-même (téléchargement, dither, stabilisation) ; pas de 1 s";

        public string OverheadInfoFormat => "= {0} par pose";

        public string OverheadDetailFormat => "Fenêtre {0} ÷ ({1} + {2} de surcharge) = {3} → {4} {5}";

        public string ReservesTabTitle => "Temps à réserver";

        public string ReservesIntro => "Déduit de la fenêtre les opérations qui ne prendront pas de poses, pour que le nombre tienne dans la vraie nuit.";

        public string FlipReserveLabel => "Retournement au méridien (min)";

        public string FixedReserveLabel => "Marge fixe (min)";

        public string AutofocusEveryLabel => "Autofocus toutes les (poses, 0 = arrêt)";

        public string AutofocusDurationLabel => "Durée d'un autofocus (s)";

        public string ReservesSummaryFormat => "Réservé {0} · exploitable {1}";

        public string ReservesEmptyHint => "Aucune réserve : toute la fenêtre est disponible.";

        public string ReservesExceedFormat => "Les temps réservés dépassent la fenêtre de {0}.";

        public string PlanOverheadHintFormat => "Surcharge de {0} par pose incluse";

        #endregion Plan par filtres

        public string PluginShortDescription => "Planifie une séance : combien de poses tiennent entre le crépuscule et l'aube, avec un plan par filtres en pourcentage de la nuit — surcharge, retournement au méridien, autofocus et marge de sécurité compris.";

        public string PluginLongDescription =>
            "Exposure Budget calcule combien de poses tiennent entre une heure de début et une heure de fin, y " +
            "compris pour les séances qui dépassent minuit — et, pour une caméra monochrome, comment répartir " +
            "cette nuit entre les filtres." + Environment.NewLine + Environment.NewLine +
            "FENÊTRE DE SÉANCE" + Environment.NewLine +
            "Les heures de début et de fin se règlent au format 24 h avec des ascenseurs. Le panneau affiche la " +
            "durée disponible, les dates exactes de la séance (pour qu'une séance qui finit le lendemain matin soit " +
            "explicite) et un avertissement quand les deux heures sont identiques. Le bouton Nuit astronomique " +
            "remplit les deux heures à partir du crépuscule et de l'aube calculés par N.I.N.A. pour votre profil " +
            "d'observatoire, avec repli sur le crépuscule nautique, le crépuscule civil ou le coucher du Soleil " +
            "lorsque le crépuscule astronomique n'existe pas à votre latitude. Le bouton Maintenant place le début " +
            "à l'heure courante, pour voir ce qu'il reste à faire en pleine séance." + Environment.NewLine + Environment.NewLine +
            "NOMBRE DE POSES" + Environment.NewLine +
            "Une pose de n'importe quelle durée, réglée de 15 s en 15 s, avec son équivalent en minutes. Le calcul " +
            "suit la règle demandée : une partie décimale inférieure à 0,5 arrondit à la pose inférieure, 0,5 et " +
            "au-delà ajoute une pose. Le panneau affiche la valeur exacte, l'arrondi appliqué, le temps couvert, le " +
            "reliquat et l'heure de fin de la dernière pose. Tout est recalculé à la frappe : il n'y a pas de bouton " +
            "Calculer, et le nombre se copie dans le presse-papiers en un clic." + Environment.NewLine + Environment.NewLine +
            "UNE NUIT RÉALISTE" + Environment.NewLine +
            "Une surcharge par pose facultative (téléchargement, dither, stabilisation) fait coûter à chaque pose " +
            "sa durée plus cette surcharge, et des temps à réserver facultatifs sont déduits de la fenêtre avant de " +
            "compter : retournement au méridien, autofocus toutes les N poses et marge de sécurité. Le nombre " +
            "annoncé est alors celui que vous atteindrez réellement." + Environment.NewLine + Environment.NewLine +
            "CAMÉRA MONO ET FILTRES" + Environment.NewLine +
            "L'onglet Plan par filtres partage la même fenêtre : une ligne par filtre avec une case à cocher, un " +
            "temps de pose et un pourcentage du temps disponible. Chaque ligne affiche son propre nombre de poses, " +
            "les totaux restent sous 100 %, et le plan est refusé quand les poses arrondies dépasseraient la " +
            "fenêtre : un plan ne promet donc jamais plus que la nuit ne contient. Une ligne par filtre se copie " +
            "facilement, prête à remplir une séquence." + Environment.NewLine + Environment.NewLine +
            "RÉGLAGES ET LANGUE" + Environment.NewLine +
            "Les derniers réglages (heures, temps de pose, surcharge, réserves et plan par filtres) sont mémorisés " +
            "dans ExposureBudget.settings, un petit fichier texte à côté du plugin, modifiable à la main. Le panneau " +
            "et cette description suivent la langue de N.I.N.A. : français ou anglais, forçable par language=fr ou " +
            "language=en dans ce fichier.";

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

        public string NightFallbackInfoFormat => "Pas de crépuscule astronomique cette nuit : repli sur {0}.";

        public string NightCalculatorUnavailableWarning => "Le calculateur de nuit de N.I.N.A. n'est pas disponible : saisissez les heures manuellement.";

        public string NightCalculationFailedWarning => "Le calcul de la nuit astronomique a échoué : saisissez les heures manuellement.";

        public string NightUnavailableWarning => "Aucun crépuscule exploitable pour cette date (nuit blanche ou latitude polaire) : saisissez les heures manuellement.";
    }
}
