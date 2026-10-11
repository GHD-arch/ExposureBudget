using System.Globalization;

namespace NINA.Plugin.ExposureBudget.Localization {

    /// <summary>Chaines anglaises (langue par defaut du plugin).</summary>
    internal sealed class EnglishStrings : IPluginStrings {

        public string Language => "en";

        public CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-US");

        /// <summary>
        /// Titre du panneau : nom propre du plugin, identique dans toutes les langues
        /// (voir aussi GermanStrings/FrenchStrings s'ils sont ajoutes).
        /// </summary>
        public string PanelTitle => "Exposure Budget";

        public string WindowGroupHeader => "Acquisition window";

        public string StartLabel => "Start";

        public string EndLabel => "End";

        public string StartHourTooltip => "Start hour of the acquisition window";

        public string StartMinuteTooltip => "Start minutes of the acquisition window";

        public string EndHourTooltip => "End hour of the acquisition window; an end earlier than the start is understood as the next morning";

        public string EndMinuteTooltip => "End minutes of the acquisition window";

        public string WindowDurationLabel => "Available duration";

        public string SessionTooltip => "Reconstructed session: the end date switches to the next day when Hf comes before Hd";

        public string NowButtonText => "Now";

        public string NowButtonTooltip => "Fills the start time with the current time";

        public string ExposureGroupHeader => "Exposure time";

        public string ExposureLabel => "Exposure";

        public string ExposureTooltip => "Single exposure time in seconds: 15 s steps with the arrows, free typing allowed";

        public string ExposureMinutesTooltip => "Exposure time expressed in minutes (information only)";

        public string NightButtonText => "Astronomical night (N.I.N.A.)";

        public string NightButtonTooltip => "Fills the start and end times with the astronomical dusk and dawn computed by N.I.N.A. for the active observatory profile";

        public string ResultGroupHeader => "Number of exposures";

        public string CopyButtonText => "Copy result";

        public string CopyButtonTooltip => "Copies the number of exposures to the clipboard";

        public string CopyFeedback => "Number of exposures copied to the clipboard.";

        public string CopyFailedFeedback => "Could not access the clipboard.";

        public string PluginShortDescription => "Computes how many exposures of a given duration fit between a start time and an end time, taking the midnight rollover into account.";

        public string PluginLongDescription =>
            "Exposure Budget works out how many exposures of a given duration can be captured between the start " +
            "and the end of a session, including sessions that run past midnight. The exposure count follows the " +
            "requested rule: a decimal part below 0.5 rounds down, 0.5 and above adds one more exposure. " +
            "The acquisition window can be filled in automatically from the astronomical night that N.I.N.A. " +
            "computes for your observatory profile. The panel and this description follow the N.I.N.A. user " +
            "interface language (English or French, or forced with language=en / language=fr in " +
            "ExposureBudget.settings).";

        public string EmptyCountDisplay => "–";

        public string WindowSecondsFormat => "{0} s";

        public string MinutesSecondsFormat => "{0} min {1} s";

        public string SecondsOnlyFormat => "{0} s";

        public string DecimalMinutesFormat => "i.e. {0} min";

        public string ResultDetailFormat => "{0} s ÷ {1} s = {2} → {3} {4}";

        public string RoundingUpFormat => "Decimal part {0} ≥ 0.5: one extra exposure is counted.";

        public string RoundingDownFormat => "Decimal part {0} < 0.5: the incomplete exposure is not counted.";

        public string RoundingExact => "The exposure count comes out exact: no rounding is applied.";

        public string CoverageFormat => "Covered time: {0} ({1} × {2} s) · Remaining: {3}";

        public string PoseEndFormat => "Last exposure ends: {0}";

        public string PoseWordSingular => "exposure";

        public string PoseWordPlural => "exposures";

        public string WarningSameHours => "Start and end times are identical: the window is empty, no exposure is possible. Check the start and end times.";

        public string WarningExposureTooLongFormat => "The exposure time ({0} s) is longer than the available window ({1}): no complete exposure is possible.";

        public string WarningOverrunFormat => "The {0} retained {1} exceed the window by {2}: the last exposure ends after the end time. Reduce the exposure time or start earlier.";

        public string InvalidExposureWarning => "The exposure time must be greater than 0 second.";

        public string NightAstronomical => "Astronomical night";

        public string NightNautical => "Nautical night";

        public string NightCivil => "Civil night";

        public string NightSun => "Sunset / sunrise";

        public string NightInfoFormat => "{0} on {1:dd/MM/yyyy}: {2:HH:mm} → {3:HH:mm} on {4:dd/MM/yyyy}.";

        public string NightCalculatorUnavailableWarning => "The N.I.N.A. night calculator is not available: enter the times manually.";

        public string NightCalculationFailedWarning => "The astronomical night calculation failed: enter the times manually.";

        public string NightUnavailableWarning => "No usable twilight for this date (white night or polar latitude): enter the times manually.";
    }
}
