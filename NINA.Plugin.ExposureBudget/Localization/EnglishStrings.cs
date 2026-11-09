using System;
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

        public string NowButtonTooltip => "Fills the start time with the current time (the reference date becomes today, so a click in the morning does not move the session to the previous evening)";

        public string ExposureGroupHeader => "Exposure time";

        public string ExposureLabel => "Exposure";

        public string ExposureTooltip => "Single exposure time in seconds: 15 s steps with the arrows, free typing allowed";

        public string ExposureMinutesTooltip => "Exposure time expressed in minutes (information only)";

        public string NightButtonText => "Astronomical night (N.I.N.A.)";

        public string NightButtonTooltip => "Fills the start and end times with the astronomical dusk and dawn computed by N.I.N.A. for the active observatory profile";

        public string ResultGroupHeader => "Number of exposures";

        public string CopyButtonText => "Copy result";

        public string CopyButtonTooltip => "Copies only the number of exposures (the value shown in large) to the clipboard";

        public string CopyFeedback => "Number of exposures copied to the clipboard.";

        public string CopyFailedFeedback => "Could not access the clipboard.";

        #region Plan par filtres

        public string TabSingleExposure => "Single exposure";

        public string TabFilterPlan => "Filter plan";

        public string FilterColumn => "Filter";

        public string FilterExposureColumn => "Exposure (s)";

        public string FilterPercentColumn => "%";

        public string FilterCountColumn => "Exposures";

        public string FilterCheckAllButton => "Check all";

        public string FilterCheckAllTooltip => "Checks every filter of the plan; exposure times and percentages are kept";

        public string FilterDistributeButton => "Split 100 %";

        public string FilterDistributeTooltip => "Splits 100 % evenly between the checked filters, in steps of 5 points (the remainder goes to the first ones)";

        public string FilterResetButton => "Reset";

        public string FilterResetTooltip => "Restores the default filters (L, R, G, B, S, H, O at 40/15/15/15/5/7/3 %)";

        public string FilterTotalsFormat => "{0} % · {1} {2} · {3}";

        public string FilterRemainingFormat => "Remaining {0}";

        public string FilterPercentOverflowFormat => "Percentages total {0} % (maximum 100 %).";

        public string FilterCoverageOverflowFormat => "Rounded exposures exceed the window by {0}: reduce a percentage or an exposure time.";

        public string FilterEmptyWarning => "No filter selected.";

        public string FilterExposureTooltip => "Exposure time of this filter; 5 s steps with the arrows, free typing allowed";

        public string FilterPercentTooltip => "Share of the available time allocated to this filter";

        public string FilterRemainderButton => ">";

        public string FilterRemainderTooltip => "Gives the unused percentage points (100 minus the total) to this filter; only enabled when some are left";

        public string FilterAddButton => "+ filter";

        public string FilterAddTooltip => "Add a filter at the end of the list";

        public string FilterRemoveButton => "- filter";

        public string FilterRemoveTooltip => "Remove the last filter of the list";

        public string FilterNewName => "New";

        public string FilterNameTooltip => "Filter name, editable";

        public string FilterPlanCopyButton => "Copy plan";

        public string FilterPlanCopyTooltip => "Copies one line per active filter (exposures and covered time), then the totals; disabled while the plan is refused";

        public string FilterPlanCopyFeedback => "Filter plan copied to the clipboard.";

        public string FilterPlanLineFormat => "{0} : {1} × {2} s ({3})";

        public string OverheadLabel => "Overhead";

        public string OverheadTooltip => "Time consumed by each exposure on top of the exposure itself (download, dither, stabilisation); 1 s steps";

        public string OverheadInfoFormat => "= {0} per exposure";

        public string OverheadDetailFormat => "Window {0} ÷ ({1} + {2} overhead) = {3} → {4} {5}";

        public string ReservesTabTitle => "Night reserves";

        public string ReservesIntro => "Deducts the operations that will not take exposures, so the count fits the real night.";

        public string FlipReserveLabel => "Meridian flip (min)";

        public string FixedReserveLabel => "Fixed margin (min)";

        public string AutofocusEveryLabel => "Autofocus every (exposures, 0 = off)";

        public string AutofocusDurationLabel => "Autofocus duration (s)";

        public string ReservesSummaryFormat => "Reserved {0} · usable {1}";

        public string ReservesEmptyHint => "No reserve: the whole window is available.";

        public string ReservesExceedFormat => "The reserves exceed the window by {0}.";

        public string PlanOverheadHintFormat => "Includes {0} per exposure";

        #endregion Plan par filtres

        public string PluginShortDescription => "Plans a session: how many exposures fit between dusk and dawn, with an optional filter plan per percentage of the night — overhead, meridian flip, autofocus and safety margin included.";

        public string PluginLongDescription =>
            "Exposure Budget works out how many exposures fit between a start time and an end time, including " +
            "sessions that run past midnight — and, for a mono camera, how to share that night between filters." +
            Environment.NewLine + Environment.NewLine +
            "SESSION WINDOW" + Environment.NewLine +
            "Start and end times use the 24 hour clock, adjusted with steppers. The panel shows the available " +
            "duration, the exact dates of the session (so a session ending the next morning is explicit) and a " +
            "warning when both times are identical. The Astronomical night button fills both times from the dusk " +
            "and dawn computed by N.I.N.A. for your observatory profile, falling back to nautical twilight, civil " +
            "twilight or sunset when the astronomical twilight does not exist at your latitude. Now sets the start " +
            "to the current time, to see what is left to do in the middle of a session." + Environment.NewLine + Environment.NewLine +
            "EXPOSURE COUNT" + Environment.NewLine +
            "One exposure of any duration, adjusted with 15 second steps, with its equivalent in minutes. The count " +
            "follows the requested rule: a decimal part below 0.5 rounds down, 0.5 and above adds one more exposure. " +
            "The panel shows the exact value, the rounding applied, the time covered, what is left and the end time " +
            "of the last exposure. Everything is recomputed as you type: there is no Calculate button, and the " +
            "number can be copied to the clipboard in one click." + Environment.NewLine + Environment.NewLine +
            "A REALISTIC NIGHT" + Environment.NewLine +
            "An optional overhead per exposure (download, dither, stabilisation) makes each exposure cost its " +
            "duration plus that overhead, and optional reserves are deducted from the window before counting: " +
            "meridian flip, autofocus every N exposures and a safety margin. The count is then the one you will " +
            "really achieve." + Environment.NewLine + Environment.NewLine +
            "MONO CAMERAS AND FILTERS" + Environment.NewLine +
            "The Filter plan tab shares the same window: one row per filter with a checkbox, an exposure time and " +
            "a percentage of the available time. Each row shows its own exposure count, the totals stay within " +
            "100 %, and the plan is refused when the rounded exposures would exceed the window, so a plan can never " +
            "promise more than the night holds. A single line per filter can be copied, ready to fill in a sequence." + Environment.NewLine + Environment.NewLine +
            "SETTINGS AND LANGUAGE" + Environment.NewLine +
            "The last settings (times, exposure, overhead, reserves and filter plan) are remembered in " +
            "ExposureBudget.settings, a small text file next to the plugin that can be edited by hand. The panel " +
            "and this description follow the N.I.N.A. interface language: English or French, forceable with " +
            "language=en or language=fr in that file.";

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

        public string NightFallbackInfoFormat => "No astronomical twilight that night: using {0}.";

        public string NightCalculatorUnavailableWarning => "The N.I.N.A. night calculator is not available: enter the times manually.";

        public string NightCalculationFailedWarning => "The astronomical night calculation failed: enter the times manually.";

        public string NightUnavailableWarning => "No usable twilight for this date (white night or polar latitude): enter the times manually.";
    }
}
