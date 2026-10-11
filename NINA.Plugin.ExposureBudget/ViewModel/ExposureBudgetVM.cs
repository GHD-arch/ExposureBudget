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
using System.ComponentModel.Composition;
using System.Globalization;
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

            UseNightTimesCommand = new MvvmCommand(UseNightTimes);
            SetStartToNowCommand = new MvvmCommand(() => SetStartToNow());
            CopyResultCommand = new MvvmCommand(() => CopyResult());

            Recalculate();
        }

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
            private set { nightInfoText = value; RaisePropertyChanged(); }
        }

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

            NightInfoText = string.Format(strings.Culture, strings.NightInfoFormat,
                source, dusk.Date, dusk, dawn, dawn);

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
                result = ExposureBudgetCalculator.Compute(window, exposureSeconds);
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
                return;
            }

            PoseCountText = FormatInteger(result.Count);
            ResultDetailText = string.Format(strings.Culture, strings.ResultDetailFormat,
                FormatNumber((decimal)window.TotalSeconds),
                FormatNumber((decimal)result.ExposureSeconds),
                result.ExactCount.ToString("0.###", strings.Culture),
                FormatInteger(result.Count),
                PoseWord(result.Count));

            RoundingText = DescribeRounding(result);

            var covered = TimeSpan.FromSeconds((double)result.CoveredSeconds);
            var remaining = window - covered;
            CoverageText = string.Format(strings.Culture, strings.CoverageFormat,
                FormatDuration(covered),
                FormatInteger(result.Count),
                FormatNumber((decimal)result.ExposureSeconds),
                FormatDuration(remaining));

            PoseEndDateTimeText = result.Count > 0
                ? string.Format(strings.Culture, strings.PoseEndFormat, (start + covered).ToString("dd/MM/yyyy HH:mm:ss", strings.Culture))
                : string.Empty;

            // Le bouton « Copier le résultat » ne dépose que le nombre de poses.
            ClipboardText = PoseCountText;

            // La confirmation de copie ne concerne que l'etat precedent : on l'efface.
            SetCopyFeedback(string.Empty);

            RefreshWarnings(result, window);
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
            } else if (result.Count == 0) {
                warnings.Add(string.Format(strings.Culture, strings.WarningExposureTooLongFormat,
                    FormatNumber((decimal)result.ExposureSeconds), FormatDuration(window)));
            } else if (result.Overflows) {
                var overrun = TimeSpan.FromSeconds((double)(-result.RemainingSeconds));
                warnings.Add(string.Format(strings.Culture, strings.WarningOverrunFormat,
                    FormatInteger(result.Count),
                    PoseWord(result.Count),
                    FormatDuration(overrun)));
            }

            SetWarning(string.Join(Environment.NewLine, warnings));
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
