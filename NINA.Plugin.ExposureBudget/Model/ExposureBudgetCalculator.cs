using System;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>
    /// Logique de calcul pure du plugin, sans aucune dependance WPF : elle est donc
    /// directement verifiable par le harnais tools\verify.
    ///
    /// En astronomie, une session peut enjamber minuit : Hd = 21:30 et Hf = 03:15
    /// decrivent une fenetre de 5 h 45 min et non une fenetre negative.
    /// </summary>
    public static class ExposureBudgetCalculator {

        /// <summary>Pas de reglage de l'ascenseur de temps de pose (en secondes).</summary>
        public const int ExposureStepSeconds = 15;

        /// <summary>Temps de pose minimal accepte (en secondes).</summary>
        public const double MinExposureSeconds = 0.1;

        /// <summary>Temps de pose maximal accepte (24 h, en secondes).</summary>
        public const double MaxExposureSeconds = 86400d;

        /// <summary>
        /// Duree disponible entre Hd et Hf.
        /// Si Hf est anterieure ou egale a Hd, la fin est comprise comme appartenant
        /// au lendemain ; Hf strictement inferieure a Hd donne donc +24 h.
        /// Hf egale a Hd donne une fenetre nulle (saisie incomplete, signalee a l'utilisateur).
        /// </summary>
        public static TimeSpan GetWindow(int startHour, int startMinute, int endHour, int endMinute) {
            var start = new TimeSpan(startHour, startMinute, 0);
            var end = new TimeSpan(endHour, endMinute, 0);
            var window = end - start;
            if (window < TimeSpan.Zero) {
                window += TimeSpan.FromDays(1);
            }
            return window;
        }

        /// <summary>
        /// Arrondi demande : la partie decimale inferieure a 0,5 fait passer a la pose
        /// inferieure, une partie decimale superieure ou egale a 0,5 ajoute une pose.
        /// </summary>
        public static int RoundToPoseCount(decimal exactCount) {
            if (exactCount <= 0m) {
                return 0;
            }
            return (int)decimal.Floor(exactCount + 0.5m);
        }

        /// <summary>
        /// Calcule le nombre de poses pour une fenetre et un temps de pose donnes.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Si le temps de pose n'est pas strictement positif.</exception>
        public static ExposureCountResult Compute(TimeSpan window, double exposureSeconds) {
            if (double.IsNaN(exposureSeconds) || double.IsInfinity(exposureSeconds) || exposureSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(exposureSeconds), exposureSeconds,
                    "Le temps de pose doit etre strictement superieur a 0 seconde.");
            }

            var exposure = (decimal)exposureSeconds;
            var windowSeconds = (decimal)window.TotalSeconds;
            var exactCount = windowSeconds / exposure;
            var count = RoundToPoseCount(exactCount);

            return new ExposureCountResult {
                Window = window,
                ExposureSeconds = exposureSeconds,
                ExactCount = exactCount,
                Count = count,
                CoveredSeconds = exposure * count
            };
        }

        /// <summary>
        /// Confort de saisie : arrondit un temps de pose au pas de l'ascenseur (15 s)
        /// en respectant les bornes acceptees.
        /// </summary>
        public static double SnapToExposureStep(double exposureSeconds) {            if (double.IsNaN(exposureSeconds) || double.IsInfinity(exposureSeconds)) {
                return ExposureStepSeconds;
            }
            var snapped = Math.Round(exposureSeconds / ExposureStepSeconds, MidpointRounding.AwayFromZero) * ExposureStepSeconds;
            return Clamp(snapped, MinExposureSeconds, MaxExposureSeconds);
        }

        /// <summary>Borne une valeur de temps de pose dans l'intervalle accepte.</summary>
        public static double Clamp(double value, double min, double max) {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                return min;
            }
            if (value < min) { return min; }
            if (value > max) { return max; }
            return value;
        }

        /// <summary>Borne une heure/minute dans son intervalle valide (0-23 / 0-59).</summary>
        public static int ClampHour(int hour) => hour < 0 ? 0 : (hour > 23 ? 23 : hour);

        /// <summary>Borne une minute dans son intervalle valide (0-59).</summary>
        public static int ClampMinute(int minute) => minute < 0 ? 0 : (minute > 59 ? 59 : minute);
    }
}
