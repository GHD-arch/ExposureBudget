using System;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>
    /// Temps à réserver dans la fenêtre pour les opérations inévitables de la nuit : retournement
    /// au méridien, autofocus périodiques et marge fixe. Le nombre de poses est calculé sur le
    /// temps restant après ces réserves.
    ///
    /// Les autofocus dépendent du nombre de poses (un toutes les N poses) : le calcul itère donc
    /// jusqu'à ce que le nombre de poses et le temps réservé soient cohérents.
    /// </summary>
    public sealed class NightReserves {

        /// <summary>Durée d'un retournement au méridien, en minutes.</summary>
        public double FlipMinutes { get; set; }

        /// <summary>Marge fixe de sécurité, en minutes.</summary>
        public double FixedMinutes { get; set; }

        /// <summary>Un autofocus toutes les N poses (0 = pas d'autofocus périodique).</summary>
        public int AutofocusEvery { get; set; }

        /// <summary>Durée d'un autofocus, en secondes.</summary>
        public double AutofocusSeconds { get; set; }

        public bool IsEmpty => Math.Max(0d, FlipMinutes + FixedMinutes) <= 0d
                            && (AutofocusEvery <= 0 || AutofocusSeconds <= 0d);

        /// <summary>Nombre d'autofocus pour un nombre de poses donné.</summary>
        public int AutofocusCount(int poseCount) {
            if (AutofocusEvery <= 0 || AutofocusSeconds <= 0d || poseCount <= 0) { return 0; }
            return poseCount / AutofocusEvery;
        }

        /// <summary>Temps total réservé pour un nombre de poses donné.</summary>
        public TimeSpan Total(int poseCount) {
            var fixedSeconds = Math.Max(0d, FlipMinutes + FixedMinutes) * 60d;
            var autofocusSeconds = AutofocusCount(poseCount) * Math.Max(0d, AutofocusSeconds);
            return TimeSpan.FromSeconds(fixedSeconds + autofocusSeconds);
        }

        public NightReserves Clone() => new NightReserves {
            FlipMinutes = FlipMinutes,
            FixedMinutes = FixedMinutes,
            AutofocusEvery = AutofocusEvery,
            AutofocusSeconds = AutofocusSeconds
        };
    }
}
