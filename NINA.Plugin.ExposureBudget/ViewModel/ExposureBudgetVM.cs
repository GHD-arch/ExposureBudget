using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Astrometry.RiseAndSet;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.ExposureBudget.Localization;
using NINA.Plugin.ExposureBudget.Model;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
// NINA.Core.Utility expose aussi un RelayCommand (obsolète) : on utilise celui du MVVM Toolkit,
// comme le recommande N.I.N.A. depuis la 3.x, via un alias explicite.
using MvvmCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.ExposureBudget.ViewModel {

    /// <summary>
    /// Panneau ancrable (onglet Imagerie) qui calcule le nombre de poses réalisables
    /// entre une heure de début et une heure de fin, pour un temps de pose donné.
    /// La langue suit celle de N.I.N.A. par défaut (réglage "language=auto") et peut être
    /// forcée en anglais ou en français dans les réglages.
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class ExposureBudgetVM : DockableVM {

        /// <summary>Valeur du réglage de langue, telle que saisie par l'utilisateur (jamais la langue résolue).</summary>
        private readonly string configuredLanguage;

        private readonly ExposureBudgetSettings settings;

        private IPluginStrings strings;

        /// <summary>Date de la soirée de référence : Hd est prise ce jour-là, Hf peut tomber le lendemain.</summary>
        private DateTime referenceDate = GetDefaultNightDate();

        [ImportingConstructor]
        public ExposureBudgetVM(IProfileService profileService) : base(profileService) {
            settings = ExposureBudgetSettings.Load();
            configuredLanguage = settings.Language;

            var ninaLanguage = GetNinaLanguage();
            strings = PluginStrings.Resolve(configuredLanguage, ninaLanguage);
            LogLanguageResolution(ninaLanguage);

            Title = strings.PanelTitle;
            ImageGeometry = LoadPanelIcon();

            // N.I.N.A. signale tout changement de langue de l'interface (et tout changement de
            // profil) : le panneau se retraduit alors immediatement, sans redemarrage.
            profileService.LocaleChanged += (sender, args) => RefreshLanguage();

            startHour = ExposureBudgetCalculator.ClampHour(settings.StartHour);
            startMinute = ExposureBudgetCalculator.ClampMinute(settings.StartMinute);
            endHour = ExposureBudgetCalculator.ClampHour(settings.EndHour);
            endMinute = ExposureBudgetCalculator.ClampMinute(settings.EndMinute);
            exposureSeconds = ExposureBudgetCalculator.Clamp(
                settings.ExposureSeconds,
                ExposureBudgetCalculator.MinExposureSeconds,
                ExposureBudgetCalculator.MaxExposureSeconds);
            overheadSeconds = ExposureBudgetCalculator.Clamp(
                settings.OverheadSeconds, 0d, ExposureBudgetCalculator.MaxOverheadSeconds);
            flipReserveMinutes = Math.Max(0d, settings.FlipReserveMinutes);
            fixedReserveMinutes = Math.Max(0d, settings.FixedReserveMinutes);
            autofocusEvery = Math.Max(0, settings.AutofocusEvery);
            autofocusSeconds = Math.Max(0d, settings.AutofocusSeconds);

            UseNightTimesCommand = new MvvmCommand(UseNightTimes);
            SetStartToNowCommand = new MvvmCommand(() => SetStartToNow());
            CopyResultCommand = new MvvmCommand(() => CopyResult());
            CheckAllFiltersCommand = new MvvmCommand(() => CheckAllFilters());
            DistributeFiltersCommand = new MvvmCommand(() => DistributeFilters());
            ResetFilterPlanCommand = new MvvmCommand(() => ResetFilterPlan());
            CopyFilterPlanCommand = new MvvmCommand(() => CopyFilterPlan());
            AddFilterCommand = new MvvmCommand(() => AddFilter());
            RemoveLastFilterCommand = new MvvmCommand(() => RemoveLastFilter());

            BuildFilterPlanRows();
            Recalculate();
        }

        /// <summary>
        /// Lignes du plan par filtres. La liste est fixe mais éditable : elle vient des réglages
        /// (sept filtres usuels au premier lancement) et chaque modification est mémorisée.
        /// </summary>
        public ObservableCollection<FilterPlanRowVM> FilterPlan { get; } = new ObservableCollection<FilterPlanRowVM>();

        /// <summary>
        /// Chaînes de l'interface, liées par la vue (libellés, infobulles, messages).
        /// Exposé en tant que propriété pour que les libellés restent localisables.
        /// </summary>
        public IPluginStrings Labels => strings;

        /// <summary>
        /// Calculateur de nuit de N.I.N.A. (crépuscule et aube astronomiques du profil
        /// d'observatoire). Import optionnel : si N.I.N.A. ne le fournit pas, le plugin
        /// reste utilisable et le bouton de remplissage affiche un message.
        /// </summary>
        [Import(AllowDefault = true)]
        public INighttimeCalculator NighttimeCalculator { get; set; }

        public ICommand UseNightTimesCommand { get; }

        /// <summary>Remplit l'heure de début avec l'heure courante (séance qui commence maintenant).</summary>
        public ICommand SetStartToNowCommand { get; }

        /// <summary>Copie tout le calcul dans le presse-papiers.</summary>
        public ICommand CopyResultCommand { get; }

        /// <summary>Coche tous les filtres du plan.</summary>
        public ICommand CheckAllFiltersCommand { get; }

        /// <summary>Répartit 100 % entre les filtres cochés, par pas de 5 points.</summary>
        public ICommand DistributeFiltersCommand { get; }

        /// <summary>Remet le plan par filtres à ses valeurs par défaut.</summary>
        public ICommand ResetFilterPlanCommand { get; }

        /// <summary>Copie le plan par filtres (une ligne par filtre) dans le presse-papiers.</summary>
        public ICommand CopyFilterPlanCommand { get; }

        /// <summary>Ajoute un filtre en fin de liste.</summary>
        public ICommand AddFilterCommand { get; }

        /// <summary>Supprime le dernier filtre de la liste.</summary>
        public ICommand RemoveLastFilterCommand { get; }

        #region Saisie : fenêtre d'acquisition

        private int startHour;

        /// <summary>Heure de début Hd (0-23).</summary>
        public int StartHour {
            get => startHour;
            set {
                var clamped = ExposureBudgetCalculator.ClampHour(value);
                if (clamped == startHour) { return; }
                startHour = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private int startMinute;

        /// <summary>Minutes de début Hd (0-59).</summary>
        public int StartMinute {
            get => startMinute;
            set {
                var clamped = ExposureBudgetCalculator.ClampMinute(value);
                if (clamped == startMinute) { return; }
                startMinute = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private int endHour;

        /// <summary>Heure de fin Hf (0-23).</summary>
        public int EndHour {
            get => endHour;
            set {
                var clamped = ExposureBudgetCalculator.ClampHour(value);
                if (clamped == endHour) { return; }
                endHour = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private int endMinute;

        /// <summary>Minutes de fin Hf (0-59).</summary>
        public int EndMinute {
            get => endMinute;
            set {
                var clamped = ExposureBudgetCalculator.ClampMinute(value);
                if (clamped == endMinute) { return; }
                endMinute = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        #endregion Saisie : fenêtre d'acquisition

        #region Saisie : temps de pose

        private double exposureSeconds;

        /// <summary>Temps de pose en secondes : valeur de référence, liée aux boutons de l'ascenseur.</summary>
        public double ExposureSeconds {
            get => exposureSeconds;
            set {
                var clamped = ExposureBudgetCalculator.Clamp(
                    value,
                    ExposureBudgetCalculator.MinExposureSeconds,
                    ExposureBudgetCalculator.MaxExposureSeconds);
                if (Math.Abs(clamped - exposureSeconds) < 1e-9) { return; }
                exposureSeconds = clamped;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ExposureSecondsText));
                Recalculate();
            }
        }

        /// <summary>Saisie clavier du temps de pose ; la valeur est relue puis bornée.</summary>
        public string ExposureSecondsText {
            get => FormatNumber((decimal)exposureSeconds);
            set {
                if (!TryParseSeconds(value, out var parsed)) { return; }
                var clamped = ExposureBudgetCalculator.Clamp(
                    parsed,
                    ExposureBudgetCalculator.MinExposureSeconds,
                    ExposureBudgetCalculator.MaxExposureSeconds);
                if (Math.Abs(clamped - exposureSeconds) < 1e-9) { return; }
                exposureSeconds = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        /// <summary>Équivalent en minutes et secondes du temps de pose (information).</summary>
        public string ExposureMinutesInfo => FormatMinutesSeconds(exposureSeconds);

        /// <summary>Équivalent en minutes décimales du temps de pose (information).</summary>
        public string ExposureDecimalMinutes => string.Format(strings.Culture, strings.DecimalMinutesFormat,
            ((decimal)exposureSeconds / 60m).ToString("0.###", strings.Culture));

        /// <summary>Pas de réglage de l'ascenseur de temps de pose (15 s).</summary>
        public double ExposureStepSeconds => ExposureBudgetCalculator.ExposureStepSeconds;

        public double MinExposureSeconds => ExposureBudgetCalculator.MinExposureSeconds;

        public double MaxExposureSeconds => ExposureBudgetCalculator.MaxExposureSeconds;

        #endregion Saisie : temps de pose

        #region Saisie : surcharge et réserves

        private double overheadSeconds;

        /// <summary>
        /// Surcharge appliquée à chaque pose, en secondes : temps réellement consommé par une pose
        /// en plus de son exposition (téléchargement, dither, stabilisation). 0 = désactivé.
        /// </summary>
        public double ExposureOverheadSeconds {
            get => overheadSeconds;
            set {
                var clamped = ExposureBudgetCalculator.Clamp(value, 0d, ExposureBudgetCalculator.MaxOverheadSeconds);
                if (Math.Abs(clamped - overheadSeconds) < 1e-9) { return; }
                overheadSeconds = clamped;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(OverheadInfoText));
                RaisePropertyChanged(nameof(HasOverhead));
                Recalculate();
            }
        }

        public bool HasOverhead => overheadSeconds > 0d;

        /// <summary>Information affichée à côté du champ de surcharge (temps consommé par pose).</summary>
        public string OverheadInfoText => overheadSeconds > 0d
            ? string.Format(strings.Culture, strings.OverheadInfoFormat,
                FormatDuration(TimeSpan.FromSeconds(exposureSeconds + overheadSeconds)))
            : string.Empty;

        /// <summary>Pas de réglage de la surcharge (1 s).</summary>
        public double OverheadStepSeconds => ExposureBudgetCalculator.OverheadStepSeconds;

        public double MaxOverheadSeconds => ExposureBudgetCalculator.MaxOverheadSeconds;

        private double flipReserveMinutes;

        /// <summary>Retournement au méridien à réserver, en minutes.</summary>
        public double FlipReserveMinutes {
            get => flipReserveMinutes;
            set {
                var clamped = Math.Max(0d, Math.Min(value, 600d));
                if (Math.Abs(clamped - flipReserveMinutes) < 1e-9) { return; }
                flipReserveMinutes = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private double fixedReserveMinutes;

        /// <summary>Marge fixe de sécurité à réserver, en minutes.</summary>
        public double FixedReserveMinutes {
            get => fixedReserveMinutes;
            set {
                var clamped = Math.Max(0d, Math.Min(value, 600d));
                if (Math.Abs(clamped - fixedReserveMinutes) < 1e-9) { return; }
                fixedReserveMinutes = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private int autofocusEvery;

        /// <summary>Un autofocus toutes les N poses (0 = pas d'autofocus périodique).</summary>
        public int AutofocusEvery {
            get => autofocusEvery;
            set {
                var clamped = Math.Max(0, Math.Min(value, 1000));
                if (clamped == autofocusEvery) { return; }
                autofocusEvery = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        private double autofocusSeconds;

        /// <summary>Durée d'un autofocus, en secondes.</summary>
        public double AutofocusDurationSeconds {
            get => autofocusSeconds;
            set {
                var clamped = Math.Max(0d, Math.Min(value, 3600d));
                if (Math.Abs(clamped - autofocusSeconds) < 1e-9) { return; }
                autofocusSeconds = clamped;
                RaisePropertyChanged();
                Recalculate();
            }
        }

        /// <summary>Vrai si au moins une réserve est saisie.</summary>
        public bool HasReserves => !BuildNightReserves().IsEmpty;

        private string reservesSummaryText = string.Empty;

        /// <summary>Résumé « réservé · exploitable » affiché dans l'onglet des réserves.</summary>
        public string ReservesSummaryText {
            get => reservesSummaryText;
            private set { reservesSummaryText = value; RaisePropertyChanged(); }
        }

        /// <summary>Temps réservé pour un nombre de poses donné (utilisé par le résumé).</summary>
        private NightReserves BuildNightReserves() => new NightReserves {
            FlipMinutes = flipReserveMinutes,
            FixedMinutes = fixedReserveMinutes,
            AutofocusEvery = autofocusEvery,
            AutofocusSeconds = autofocusSeconds
        };

        #endregion Saisie : surcharge et réserves

        #region Résultats

        private string windowText = string.Empty;

        /// <summary>Durée disponible entre Hd et Hf, au format hh:mm:ss.</summary>
        public string WindowText {
            get => windowText;
            private set { windowText = value; RaisePropertyChanged(); }
        }

        private string windowSecondsText = string.Empty;

        /// <summary>Durée disponible exprimée en secondes.</summary>
        public string WindowSecondsText {
            get => windowSecondsText;
            private set { windowSecondsText = value; RaisePropertyChanged(); }
        }

        private string startDateTimeText = string.Empty;

        /// <summary>Date et heure de début de la fenêtre (ex. 10/10/2026 21:30).</summary>
        public string StartDateTimeText {
            get => startDateTimeText;
            private set { startDateTimeText = value; RaisePropertyChanged(); }
        }

        private string endDateTimeText = string.Empty;

        /// <summary>Date et heure de fin de la fenêtre, qui peut tomber le lendemain.</summary>
        public string EndDateTimeText {
            get => endDateTimeText;
            private set { endDateTimeText = value; RaisePropertyChanged(); }
        }

        private string poseCountText = "0";

        /// <summary>Nombre de poses retenu, mis en avant dans le panneau.</summary>
        public string PoseCountText {
            get => poseCountText;
            private set { poseCountText = value; RaisePropertyChanged(); }
        }

        private string resultDetailText = string.Empty;

        /// <summary>Détail du calcul : durée ÷ temps de pose = nombre exact, puis arrondi.</summary>
        public string ResultDetailText {
            get => resultDetailText;
            private set { resultDetailText = value; RaisePropertyChanged(); }
        }

        private string roundingText = string.Empty;

        /// <summary>Explication de la règle d'arrondi appliquée (0,5), vide si le compte est exact.</summary>
        public string RoundingText {
            get => roundingText;
            private set { roundingText = value; RaisePropertyChanged(); }
        }

        private string coverageText = string.Empty;

        /// <summary>Temps couvert par les poses retenues et reliquat de la fenêtre.</summary>
        public string CoverageText {
            get => coverageText;
            private set { coverageText = value; RaisePropertyChanged(); }
        }

        private string poseEndDateTimeText = string.Empty;

        /// <summary>Horodatage de la fin de la dernière pose si la séquence démarre à Hd.</summary>
        public string PoseEndDateTimeText {
            get => poseEndDateTimeText;
            private set { poseEndDateTimeText = value; RaisePropertyChanged(); }
        }

        private string warningText = string.Empty;

        public string WarningText {
            get => warningText;
            private set { warningText = value; RaisePropertyChanged(); }
        }

        private bool hasWarning;

        public bool HasWarning {
            get => hasWarning;
            private set { hasWarning = value; RaisePropertyChanged(); }
        }

        private string nightInfoText = string.Empty;

        /// <summary>Information sur la nuit astronomique récupérée depuis N.I.N.A.</summary>
        public string NightInfoText {
            get => nightInfoText;
            private set {
                nightInfoText = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasNightInfo));
            }
        }

        /// <summary>Vrai quand le bouton de nuit a quelque chose a signaler (repli).</summary>
        public bool HasNightInfo => !string.IsNullOrEmpty(nightInfoText);

        private string copyFeedbackText = string.Empty;

        /// <summary>Confirmation affichée après une copie (effacée au calcul suivant).</summary>
        public string CopyFeedbackText {
            get => copyFeedbackText;
            private set { copyFeedbackText = value; RaisePropertyChanged(); }
        }

        private bool hasCopyFeedback;

        public bool HasCopyFeedback {
            get => hasCopyFeedback;
            private set { hasCopyFeedback = value; RaisePropertyChanged(); }
        }

        private string clipboardText = string.Empty;

        /// <summary>
        /// Nombre de poses déposé dans le presse-papiers par le bouton « Copier le résultat » :
        /// uniquement la valeur affichée en grand (par exemple « 69 »).
        /// </summary>
        public string ClipboardText {
            get => clipboardText;
            private set { clipboardText = value; RaisePropertyChanged(); }
        }

        private string filterPlanTotalsText = string.Empty;

        /// <summary>Totaux du plan : pourcentage, nombre de poses et durée couverte.</summary>
        public string FilterPlanTotalsText {
            get => filterPlanTotalsText;
            private set { filterPlanTotalsText = value; RaisePropertyChanged(); }
        }

        private string filterPlanRemainingText = string.Empty;

        /// <summary>Temps disponible non couvert par le plan (vide en cas de dépassement).</summary>
        public string FilterPlanRemainingText {
            get => filterPlanRemainingText;
            private set { filterPlanRemainingText = value; RaisePropertyChanged(); }
        }

        private string filterPlanErrorText = string.Empty;

        /// <summary>Message de refus du plan (pourcentages &gt; 100 %, ou poses qui dépassent la fenêtre).</summary>
        public string FilterPlanErrorText {
            get => filterPlanErrorText;
            private set { filterPlanErrorText = value; RaisePropertyChanged(); }
        }

        private bool hasFilterPlanError;

        public bool HasFilterPlanError {
            get => hasFilterPlanError;
            private set { hasFilterPlanError = value; RaisePropertyChanged(); }
        }

        private bool hasFilterPlan;

        /// <summary>Vrai si le plan contient au moins une ligne (la vue masque sinon le tableau).</summary>
        public bool HasFilterPlan {
            get => hasFilterPlan;
            private set { hasFilterPlan = value; RaisePropertyChanged(); }
        }

        private string filterPlanClipboardText = string.Empty;

        /// <summary>Texte copié par « Copier le plan » : une ligne par filtre actif.</summary>
        public string FilterPlanClipboardText {
            get => filterPlanClipboardText;
            private set { filterPlanClipboardText = value; RaisePropertyChanged(); }
        }

        private string filterPlanStatusText = string.Empty;

        /// <summary>Retour du bouton « Copier le plan ».</summary>
        public string FilterPlanStatusText {
            get => filterPlanStatusText;
            private set { filterPlanStatusText = value; RaisePropertyChanged(); }
        }

        private bool hasFilterPlanStatus;

        public bool HasFilterPlanStatus {
            get => hasFilterPlanStatus;
            private set { hasFilterPlanStatus = value; RaisePropertyChanged(); }
        }

        private bool canCopyFilterPlan;

        /// <summary>
        /// Vrai quand le plan peut être copié : au moins une ligne de résultat et aucun refus,
        /// pour ne jamais exporter un plan incohérent.
        /// </summary>
        public bool CanCopyFilterPlan {
            get => canCopyFilterPlan;
            private set { canCopyFilterPlan = value; RaisePropertyChanged(); }
        }

        /// <summary>Rappel de la surcharge utilisée par le plan (vide si aucune).</summary>
        public string PlanOverheadHintText => overheadSeconds > 0d
            ? string.Format(strings.Culture, strings.PlanOverheadHintFormat, FormatDuration(TimeSpan.FromSeconds(overheadSeconds)))
            : string.Empty;

        public bool HasPlanOverhead => overheadSeconds > 0d;

        #endregion Résultats

        #region Commandes

        /// <summary>
        /// Remplit la fenêtre d'acquisition avec le crépuscule et l'aube astronomiques
        /// calculés par N.I.N.A. pour le profil d'observatoire courant.
        /// </summary>
        private void UseNightTimes() {
            var calculator = NighttimeCalculator;
            if (calculator == null) {
                NightInfoText = string.Empty;
                SetWarning(strings.NightCalculatorUnavailableWarning);
                return;
            }

            NighttimeData data;
            try {
                data = calculator.Calculate(DateTime.Now);
            } catch (Exception ex) {
                Logger.Error("ExposureBudget: night calculation failed", ex);
                NightInfoText = string.Empty;
                SetWarning(strings.NightCalculationFailedWarning);
                return;
            }

            if (!TryResolveNightWindow(data, out var dusk, out var dawn, out var source)) {
                NightInfoText = string.Empty;
                SetWarning(strings.NightUnavailableWarning);
                return;
            }

            referenceDate = dusk.Date;

            startHour = dusk.Hour;
            startMinute = dusk.Minute;
            endHour = dawn.Hour;
            endMinute = dawn.Minute;

            RaisePropertyChanged(nameof(StartHour));
            RaisePropertyChanged(nameof(StartMinute));
            RaisePropertyChanged(nameof(EndHour));
            RaisePropertyChanged(nameof(EndMinute));

            // Le resultat est deja visible dans les heures de debut et de fin : on ne repete pas
            // les horaires. Le message ne sert qu'a signaler un repli (pas de crepuscule
            // astronomique cette nuit-la).
            NightInfoText = source == strings.NightAstronomical
                ? string.Empty
                : string.Format(strings.Culture, strings.NightFallbackInfoFormat, source);

            SaveSettings();
            Recalculate();
        }

        /// <summary>
        /// Choisit le couple crépuscule / aube à utiliser, en repliant successivement sur le
        /// crépuscule nautique, le crépuscule civil puis le coucher / lever du Soleil lorsque
        /// le crépuscule astronomique n'existe pas (nuit blanche aux hautes latitudes).
        /// </summary>
        private bool TryResolveNightWindow(NighttimeData data, out DateTime dusk, out DateTime dawn, out string source) {
            dusk = default;
            dawn = default;
            source = string.Empty;

            if (data == null) { return false; }

            var candidates = new (RiseAndSetEvent Event, string Label)[] {
                (data.TwilightRiseAndSet, strings.NightAstronomical),
                (data.NauticalTwilightRiseAndSet, strings.NightNautical),
                (data.CivilTwilightRiseAndSet, strings.NightCivil),
                (data.SunRiseAndSet, strings.NightSun)
            };

            foreach (var candidate in candidates) {
                if (candidate.Event?.Set == null || candidate.Event.Rise == null) { continue; }

                var candidateDusk = candidate.Event.Set.Value;
                var candidateDawn = candidate.Event.Rise.Value;

                // Le coucher du crépuscule doit précéder l'aube du matin suivant.
                while (candidateDusk >= candidateDawn) {
                    candidateDusk = candidateDusk.AddDays(-1);
                }

                dusk = candidateDusk;
                dawn = candidateDawn;
                source = candidate.Label;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remplit l'heure de début avec l'heure courante : utile en début de séance, pour
        /// calculer ce qu'il reste à faire à partir de maintenant.
        ///
        /// La date de référence est celle du jour courant, et non la convention de « nuit
        /// astronomique » utilisée ailleurs (une séance consultée le matin appartient à la
        /// nuit de la veille) : une séance qui démarre « maintenant » démarre bien aujourd'hui,
        /// y compris en matinée.
        /// </summary>
        private void SetStartToNow() {
            var now = DateTime.Now;
            referenceDate = now.Date;

            StartHour = now.Hour;
            StartMinute = now.Minute;
        }

        /// <summary>
        /// Copie le calcul complet dans le presse-papiers (voir <see cref="ClipboardText"/>).
        /// L'accès au presse-papiers peut échouer (verrou d'un autre programme) : l'échec est
        /// signalé dans le panneau, jamais par une exception.
        /// </summary>
        private void CopyResult() {
            var text = ClipboardText;
            if (string.IsNullOrEmpty(text)) { return; }

            try {
                Clipboard.SetText(text);
                SetCopyFeedback(strings.CopyFeedback);
            } catch (Exception ex) {
                Logger.Warning($"ExposureBudget: could not access the clipboard ({ex.Message})");
                SetCopyFeedback(strings.CopyFailedFeedback);
            }
        }

        private void SetCopyFeedback(string message) {
            CopyFeedbackText = message;
            HasCopyFeedback = !string.IsNullOrEmpty(message);
        }

        #endregion Commandes

        #region Plan par filtres

        /// <summary>Construit les lignes du plan à partir des réglages mémorisés.</summary>
        private void BuildFilterPlanRows() {
            var plan = settings.FilterPlan ?? ExposureBudgetSettings.CreateDefaultFilterPlan();
            if (plan.Count == 0) { plan = ExposureBudgetSettings.CreateDefaultFilterPlan(); }

            FilterPlan.Clear();
            foreach (var entry in plan) {
                FilterPlan.Add(new FilterPlanRowVM(entry.Clone(), OnFilterPlanRowChanged));
            }

            HasFilterPlan = FilterPlan.Count > 0;
        }

        private void OnFilterPlanRowChanged() {
            Recalculate();
        }

        /// <summary>Coche tous les filtres du plan (les valeurs de pose et de % sont conservées).</summary>
        private void CheckAllFilters() {
            foreach (var row in FilterPlan) {
                row.Enabled = true;
            }
        }

        /// <summary>Répartit 100 % entre les filtres cochés, par pas de 5 points.</summary>
        private void DistributeFilters() {
            var entries = FilterPlan.Select(x => x.Entry).ToList();
            FilterPlanCalculator.DistributeEvenly(entries);

            foreach (var row in FilterPlan) {
                row.RaiseAllChanged();
            }

            Recalculate();
        }

        /// <summary>Remet le plan à ses valeurs par défaut (les filtres usuels, répartis sur 100 %).</summary>
        private void ResetFilterPlan() {
            settings.FilterPlan = ExposureBudgetSettings.CreateDefaultFilterPlan();
            BuildFilterPlanRows();
            Recalculate();
        }

        /// <summary>Ajoute un filtre en fin de liste (nom modifiable, 120 s, 0 %).</summary>
        private void AddFilter() {
            FilterPlan.Add(new FilterPlanRowVM(
                new FilterPlanEntry(strings.FilterNewName, true, 120d, 0),
                OnFilterPlanRowChanged));

            HasFilterPlan = FilterPlan.Count > 0;
            Recalculate();
        }

        /// <summary>Supprime le dernier filtre de la liste (la liste ne peut pas être vidée).</summary>
        private void RemoveLastFilter() {
            if (FilterPlan.Count <= 1) { return; }

            FilterPlan.RemoveAt(FilterPlan.Count - 1);
            Recalculate();
        }

        /// <summary>
        /// Copie le plan par filtres : une ligne par filtre actif (« L : 48 × 120 s (01:36:00) »),
        /// puis les totaux et le reliquat. Utile pour configurer la séquence dans N.I.N.A.
        /// </summary>
        private void CopyFilterPlan() {
            var text = FilterPlanClipboardText;
            if (string.IsNullOrEmpty(text)) { return; }

            try {
                Clipboard.SetText(text);
                SetFilterPlanStatus(strings.FilterPlanCopyFeedback);
            } catch (Exception ex) {
                Logger.Warning($"ExposureBudget: could not access the clipboard ({ex.Message})");
                SetFilterPlanStatus(strings.CopyFailedFeedback);
            }
        }

        private void SetFilterPlanStatus(string message) {
            FilterPlanStatusText = message;
            HasFilterPlanStatus = !string.IsNullOrEmpty(message);
        }

        /// <summary>Construit le texte du plan : une ligne par filtre actif, puis les totaux.</summary>
        private string BuildFilterPlanClipboardText(FilterPlanResult result) {
            var lines = new List<string>();

            foreach (var line in result.Lines) {
                if (!line.Enabled || !line.HasValidExposure || line.Count <= 0) { continue; }

                lines.Add(string.Format(strings.Culture, strings.FilterPlanLineFormat,
                    line.Name,
                    FormatInteger(line.Count),
                    FormatNumber((decimal)line.Entry.ExposureSeconds),
                    FormatDuration(line.Covered)));
            }

            if (lines.Count == 0) { return string.Empty; }

            lines.Add(string.Format(strings.Culture, strings.FilterTotalsFormat,
                result.TotalPercent.ToString("0", strings.Culture),
                FormatInteger(result.TotalCount),
                PoseWord(result.TotalCount),
                FormatDuration(result.TotalCovered)));

            if (result.Remaining >= TimeSpan.Zero) {
                lines.Add(string.Format(strings.Culture, strings.FilterRemainingFormat, FormatDuration(result.Remaining)));
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Recalcule le plan par filtres pour la fenêtre courante : nombre de poses par filtre,
        /// totaux, et **refus** si les pourcentages dépassent 100 % ou si les poses arrondies
        /// dépassent la fenêtre (aucun arbitrage automatique : l'utilisateur corrige une entrée).
        /// </summary>
        private void RecalculateFilterPlan(TimeSpan window) {
            if (FilterPlan.Count == 0) {
                HasFilterPlan = false;
                FilterPlanTotalsText = string.Empty;
                FilterPlanRemainingText = string.Empty;
                FilterPlanClipboardText = string.Empty;
                SetFilterPlanError(string.Empty);
                CanCopyFilterPlan = false;
                return;
            }

            HasFilterPlan = true;

            var result = FilterPlanCalculator.Compute(window, FilterPlan.Select(x => x.Entry), overheadSeconds);

            for (var i = 0; i < FilterPlan.Count && i < result.Lines.Count; i++) {
                var line = result.Lines[i];
                FilterPlan[i].CountText = line.Enabled && line.HasValidExposure && window > TimeSpan.Zero
                    ? FormatInteger(line.Count)
                    : strings.EmptyCountDisplay;

                // Bouton « attribuer le reliquat » : actif si des points restent et que ce filtre
                // peut les recevoir.
                FilterPlan[i].SetRemainderAvailability(result.UnusedPercent);
            }

            FilterPlanTotalsText = string.Format(strings.Culture, strings.FilterTotalsFormat,
                result.TotalPercent.ToString("0", strings.Culture),
                FormatInteger(result.TotalCount),
                PoseWord(result.TotalCount),
                FormatDuration(result.TotalCovered));

            FilterPlanRemainingText = result.Remaining >= TimeSpan.Zero
                ? string.Format(strings.Culture, strings.FilterRemainingFormat, FormatDuration(result.Remaining))
                : string.Empty;

            FilterPlanClipboardText = BuildFilterPlanClipboardText(result);

            // Le retour de copie ne concerne que l'etat precedent : on l'efface.
            SetFilterPlanStatus(string.Empty);

            var messages = new List<string>();

            if (result.IsEmpty) {
                messages.Add(strings.FilterEmptyWarning);
            }

            if (result.PercentOverflow) {
                messages.Add(string.Format(strings.Culture, strings.FilterPercentOverflowFormat,
                    result.TotalPercent.ToString("0", strings.Culture)));
            }

            if (result.CoverageOverflow) {
                messages.Add(string.Format(strings.Culture, strings.FilterCoverageOverflowFormat,
                    FormatDuration(result.Overrun)));
            }

            SetFilterPlanError(string.Join(Environment.NewLine, messages));

            // On n'exporte jamais un plan incohérent (pourcentages ou fenêtre dépassés).
            CanCopyFilterPlan = !string.IsNullOrEmpty(FilterPlanClipboardText) && !HasFilterPlanError;
        }

        private void SetFilterPlanError(string message) {
            FilterPlanErrorText = message;
            HasFilterPlanError = !string.IsNullOrEmpty(message);
        }

        #endregion Plan par filtres

        #region Calcul

        /// <summary>
        /// Recalcule toutes les valeurs affichées. Appelé au chargement du panneau puis à
        /// chaque modification d'une saisie ou d'une commande : le résultat affiché est
        /// donc toujours à jour, aucun bouton de calcul n'est nécessaire.
        /// </summary>
        private void Recalculate() {
            var window = ExposureBudgetCalculator.GetWindow(StartHour, StartMinute, EndHour, EndMinute);
            var start = referenceDate.Date + new TimeSpan(StartHour, StartMinute, 0);
            var end = start + window;

            WindowText = FormatDuration(window);
            WindowSecondsText = string.Format(strings.Culture, strings.WindowSecondsFormat, FormatNumber((decimal)window.TotalSeconds));
            StartDateTimeText = start.ToString("dd/MM/yyyy HH:mm", strings.Culture);
            EndDateTimeText = end.ToString("dd/MM/yyyy HH:mm", strings.Culture);

            RaisePropertyChanged(nameof(ExposureMinutesInfo));
            RaisePropertyChanged(nameof(ExposureDecimalMinutes));

            ExposureCountResult result;
            try {
                result = ExposureBudgetCalculator.Compute(window, exposureSeconds, overheadSeconds, BuildNightReserves());
            } catch (Exception ex) {
                Logger.Error("ExposureBudget: computation failed", ex);
                PoseCountText = strings.EmptyCountDisplay;
                ResultDetailText = string.Empty;
                RoundingText = string.Empty;
                CoverageText = string.Empty;
                PoseEndDateTimeText = string.Empty;
                ClipboardText = string.Empty;
                SetCopyFeedback(string.Empty);
                SetWarning(strings.InvalidExposureWarning);
                RecalculateFilterPlan(window);
                return;
            }

            PoseCountText = FormatInteger(result.Count);
            ResultDetailText = result.OverheadSeconds > 0d
                ? string.Format(strings.Culture, strings.OverheadDetailFormat,
                    FormatNumber((decimal)result.UsableWindow.TotalSeconds),
                    FormatNumber((decimal)result.ExposureSeconds),
                    FormatNumber(result.PerPoseSeconds - (decimal)result.ExposureSeconds),
                    result.ExactCount.ToString("0.###", strings.Culture),
                    FormatInteger(result.Count),
                    PoseWord(result.Count))
                : string.Format(strings.Culture, strings.ResultDetailFormat,
                    FormatNumber((decimal)result.UsableWindow.TotalSeconds),
                    FormatNumber((decimal)result.ExposureSeconds),
                    result.ExactCount.ToString("0.###", strings.Culture),
                    FormatInteger(result.Count),
                    PoseWord(result.Count));

            RoundingText = DescribeRounding(result);

            // Le temps couvert reste la somme des temps d'exposition ; le reliquat est calcule
            // sur le temps reellement consomme (surcharges et reserves comprises).
            var covered = TimeSpan.FromSeconds((double)result.CoveredSeconds);
            var remaining = TimeSpan.FromSeconds((double)result.RemainingSeconds);
            CoverageText = string.Format(strings.Culture, strings.CoverageFormat,
                FormatDuration(covered),
                FormatInteger(result.Count),
                FormatNumber((decimal)result.ExposureSeconds),
                FormatDuration(remaining));

            // Fin de la derniere exposition : les surcharges s'appliquent entre les poses, pas
            // apres la derniere.
            if (result.Count > 0) {
                var endOffset = TimeSpan.FromSeconds((double)result.ConsumedSeconds - result.OverheadSeconds);
                PoseEndDateTimeText = string.Format(strings.Culture, strings.PoseEndFormat,
                    (start + endOffset).ToString("dd/MM/yyyy HH:mm:ss", strings.Culture));
            } else {
                PoseEndDateTimeText = string.Empty;
            }

            // Le bouton « Copier le résultat » ne dépose que le nombre de poses.
            ClipboardText = PoseCountText;

            // La confirmation de copie ne concerne que l'etat precedent : on l'efface.
            SetCopyFeedback(string.Empty);

            ReservesSummaryText = BuildReservesSummary(result);
            RaisePropertyChanged(nameof(HasReserves));
            RaisePropertyChanged(nameof(OverheadInfoText));
            RaisePropertyChanged(nameof(HasOverhead));
            RaisePropertyChanged(nameof(PlanOverheadHintText));
            RaisePropertyChanged(nameof(HasPlanOverhead));

            RefreshWarnings(result, window);
            RecalculateFilterPlan(window);
            SaveSettings();
        }

        private string DescribeRounding(ExposureCountResult result) {
            if (result.EmptyWindow || result.Count == 0) { return string.Empty; }

            if (result.RoundedUp) {
                return string.Format(strings.Culture, strings.RoundingUpFormat, result.Fraction.ToString("0.###", strings.Culture));
            }

            if (result.RoundedDown) {
                return string.Format(strings.Culture, strings.RoundingDownFormat, result.Fraction.ToString("0.###", strings.Culture));
            }

            return strings.RoundingExact;
        }

        private void RefreshWarnings(ExposureCountResult result, TimeSpan window) {
            var warnings = new List<string>();

            if (window == TimeSpan.Zero) {
                warnings.Add(strings.WarningSameHours);
            } else if (result.ReservesExceedWindow) {
                var excess = result.ReservedSeconds - window;
                warnings.Add(string.Format(strings.Culture, strings.ReservesExceedFormat, FormatDuration(excess)));
            } else if (result.Count == 0) {
                warnings.Add(string.Format(strings.Culture, strings.WarningExposureTooLongFormat,
                    FormatNumber(result.PerPoseSeconds), FormatDuration(result.UsableWindow)));
            } else if (result.Overflows) {
                var overrun = TimeSpan.FromSeconds((double)(-result.RemainingSeconds));
                warnings.Add(string.Format(strings.Culture, strings.WarningOverrunFormat,
                    FormatInteger(result.Count),
                    PoseWord(result.Count),
                    FormatDuration(overrun)));
            }

            SetWarning(string.Join(Environment.NewLine, warnings));
        }

        /// <summary>Résumé des réserves (« réservé · exploitable ») ou message d'absence.</summary>
        private string BuildReservesSummary(ExposureCountResult result) {
            if (BuildNightReserves().IsEmpty) {
                return strings.ReservesEmptyHint;
            }

            return string.Format(strings.Culture, strings.ReservesSummaryFormat,
                FormatDuration(result.ReservedSeconds),
                FormatDuration(result.UsableWindow));
        }

        private void SetWarning(string message) {
            WarningText = message;
            HasWarning = !string.IsNullOrEmpty(message);
        }

        #endregion Calcul

        #region Outils

        /// <summary>
        /// Relit la langue de N.I.N.A. et retraduit le panneau si elle a change.
        /// Appele par l'evenement LocaleChanged du service de profil (declenche depuis le
        /// fil d'interface par N.I.N.A.), donc les liaisons sont rafraichies au bon endroit.
        /// </summary>
        private void RefreshLanguage() {
            var resolved = PluginStrings.Resolve(configuredLanguage, GetNinaLanguage());
            if (ReferenceEquals(resolved, strings)) { return; }

            strings = resolved;
            Title = strings.PanelTitle;
            RaisePropertyChanged(nameof(Labels));
            Recalculate();
        }

        /// <summary>
        /// Trace la langue retenue dans le journal de N.I.N.A. : c'est le premier element a
        /// consulter si le panneau ne s'affiche pas dans la langue attendue
        /// (%LOCALAPPDATA%\NINA\Logs).
        /// </summary>
        private void LogLanguageResolution(CultureInfo ninaLanguage) {
            try {
                var assembly = typeof(ExposureBudgetVM).Assembly;
                Logger.Info(string.Format(CultureInfo.InvariantCulture,
                    "ExposureBudget: panel language '{0}' (setting '{1}', N.I.N.A. language '{2}'), plugin {3} loaded from {4}",
                    strings.Language,
                    configuredLanguage,
                    ninaLanguage?.Name ?? "<unavailable>",
                    assembly.GetName().Version,
                    assembly.Location));
            } catch (Exception ex) {
                Logger.Warning($"ExposureBudget: could not log the language resolution ({ex.Message})");
            }
        }

        /// <summary>
        /// Langue de l'interface de N.I.N.A., lue dans le profil actif
        /// (voir <see cref="PluginLanguage.GetNinaLanguage"/>).
        /// </summary>
        private CultureInfo GetNinaLanguage() => PluginLanguage.GetNinaLanguage(profileService);

        private void SaveSettings() {
            // On memorise la valeur saisie ("auto" reste "auto") et non la langue resolue.
            settings.Language = configuredLanguage;
            settings.StartHour = startHour;
            settings.StartMinute = startMinute;
            settings.EndHour = endHour;
            settings.EndMinute = endMinute;
            settings.ExposureSeconds = exposureSeconds;
            settings.OverheadSeconds = overheadSeconds;
            settings.FlipReserveMinutes = flipReserveMinutes;
            settings.FixedReserveMinutes = fixedReserveMinutes;
            settings.AutofocusEvery = autofocusEvery;
            settings.AutofocusSeconds = autofocusSeconds;

            // Plan par filtres : on repart d'une copie pour ne pas exposer les objets de la vue.
            settings.FilterPlan = FilterPlan.Select(row => row.Entry.Clone()).ToList();

            settings.Save();
        }

        /// <summary>
        /// Date de référence de la nuit en cours, selon la même règle que N.I.N.A. :
        /// une session consultée l'après-midi ou le soir appartient à la nuit du jour
        /// courant, une session consultée le matin appartient à la nuit de la veille.
        /// </summary>
        private static DateTime GetDefaultNightDate() {
            var now = DateTime.Now;
            return now.Hour >= 12 ? now.Date : now.Date.AddDays(-1);
        }

        private bool TryParseSeconds(string text, out double seconds) {
            seconds = 0d;
            if (string.IsNullOrWhiteSpace(text)) { return false; }

            var trimmed = text.Trim().Replace("s", string.Empty).Trim();
            if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, strings.Culture, out seconds)
                && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) {
                return true;
            }

            // Saisie au format invariant (300.5) sur un poste configuré avec la virgule,
            // ou inversement.
            var normalized = strings.Culture.NumberFormat.NumberDecimalSeparator == "."
                ? trimmed.Replace(',', '.')
                : trimmed.Replace('.', ',');

            if (double.TryParse(normalized, NumberStyles.Float, strings.Culture, out seconds)
                && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) {
                return true;
            }

            seconds = 0d;
            return false;
        }

        private string PoseWord(int count) => count == 1 ? strings.PoseWordSingular : strings.PoseWordPlural;

        private string FormatInteger(int value) => value.ToString("0", strings.Culture);

        private string FormatNumber(decimal value) => value.ToString("#,0.###", strings.Culture);

        private string FormatDuration(TimeSpan value) {
            var sign = value < TimeSpan.Zero ? "-" : string.Empty;
            if (value < TimeSpan.Zero) { value = value.Negate(); }
            return sign + string.Format(strings.Culture, "{0:00}:{1:00}:{2:00}",
                (int)value.TotalHours, value.Minutes, value.Seconds);
        }

        private string FormatMinutesSeconds(double totalSeconds) {
            var minutes = (int)(totalSeconds / 60d);
            var seconds = totalSeconds - (minutes * 60d);

            if (minutes == 0) {
                return string.Format(strings.Culture, strings.SecondsOnlyFormat, seconds.ToString("0.###", strings.Culture));
            }

            return string.Format(strings.Culture, strings.MinutesSecondsFormat,
                minutes.ToString("0", strings.Culture),
                seconds.ToString("00.###", strings.Culture));
        }

        private static GeometryGroup LoadPanelIcon() {
            foreach (var key in new[] { "CalculatorSVG", "HourglassSVG", "PuzzlePieceSVG" }) {
                try {
                    var resources = Application.Current?.Resources;
                    if (resources != null && resources.Contains(key) && resources[key] is GeometryGroup geometry) {
                        return geometry;
                    }
                } catch (Exception ex) {
                    Logger.Error($"ExposureBudget: icon {key} unavailable", ex);
                }
            }
            return null;
        }

        #endregion Outils
    }
}
