using System;
using System.Collections.Generic;
using System.Linq;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>Résultat du plan pour un filtre.</summary>
    public sealed class FilterPlanLine {

        public FilterPlanEntry Entry { get; init; }

        /// <summary>Nom du filtre (raccourci de lecture).</summary>
        public string Name => Entry?.Name ?? string.Empty;

        /// <summary>Vrai si le filtre participe au plan.</summary>
        public bool Enabled => Entry?.Enabled == true;

        /// <summary>Temps alloué par le pourcentage : <c>fenêtre × % / 100</c>.</summary>
        public TimeSpan Allocated { get; init; }

        /// <summary>Nombre de poses théorique, non arrondi.</summary>
        public decimal ExactCount { get; init; }

        /// <summary>Nombre de poses retenu (règle du 0,5).</summary>
        public int Count { get; init; }

        /// <summary>Temps réellement couvert par les poses retenues.</summary>
        public TimeSpan Covered { get; init; }

        /// <summary>Temps réellement consommé, surcharge par pose comprise.</summary>
        public TimeSpan Consumed { get; init; }

        /// <summary>Temps consommé par une pose (pose + surcharge).</summary>
        public decimal PerPoseSeconds { get; init; }

        /// <summary>Vrai si le temps de pose du filtre est exploitable.</summary>
        public bool HasValidExposure { get; init; }
    }

    /// <summary>
    /// Résultat complet d'un plan par filtres : une ligne par filtre, les totaux et les
    /// contrôles de cohérence.
    ///
    /// Les deux contrôles sont indépendants : les pourcentages saisis doivent tenir dans 100 %
    /// (contrôle de saisie), et le temps réellement couvert par les poses **arrondies** doit
    /// tenir dans la fenêtre (contrôle de fond : c'est lui qui empêche de dépasser la nuit).
    /// </summary>
    public sealed class FilterPlanResult {

        public IReadOnlyList<FilterPlanLine> Lines { get; init; } = Array.Empty<FilterPlanLine>();

        /// <summary>Fenêtre de temps disponible.</summary>
        public TimeSpan Window { get; init; }

        /// <summary>Somme des pourcentages des filtres actifs.</summary>
        public int TotalPercent { get; init; }

        /// <summary>Somme des poses retenues des filtres actifs.</summary>
        public int TotalCount { get; init; }

        /// <summary>Somme des temps couverts des filtres actifs.</summary>
        public TimeSpan TotalCovered { get; init; }

        /// <summary>Somme des temps consommés (surcharges comprises) des filtres actifs.</summary>
        public TimeSpan TotalConsumed { get; init; }

        /// <summary>Surcharge appliquée à chaque pose, en secondes.</summary>
        public double OverheadSeconds { get; init; }

        /// <summary>Nombre de filtres actifs.</summary>
        public int EnabledCount { get; init; }

        /// <summary>Temps disponible non couvert par les poses retenues (négatif = dépassement).</summary>
        public TimeSpan Remaining => Window - TotalConsumed;

        /// <summary>La saisie dépasse 100 % de pourcentage.</summary>
        public bool PercentOverflow => TotalPercent > 100;

        /// <summary>
        /// Les poses arrondies dépassent la fenêtre disponible. Le contrôle porte sur le temps
        /// réellement consommé (surcharge comprise), pas sur la seule somme des expositions.
        /// </summary>
        public bool CoverageOverflow => TotalConsumed > Window;

        /// <summary>Dépassement de la fenêtre par les poses (zéro si aucun).</summary>
        public TimeSpan Overrun => CoverageOverflow ? TotalConsumed - Window : TimeSpan.Zero;

        /// <summary>Points de pourcentage non utilisés (0 si la saisie dépasse déjà 100 %).</summary>
        public int UnusedPercent => Math.Max(0, 100 - TotalPercent);

        /// <summary>Le plan est utilisable : au moins un filtre actif et aucun dépassement.</summary>
        public bool IsValid => EnabledCount > 0 && !PercentOverflow && !CoverageOverflow;

        /// <summary>Aucun filtre n'est actif.</summary>
        public bool IsEmpty => EnabledCount == 0;
    }

    /// <summary>
    /// Calcul du plan par filtres. Logique pure, sans WPF ni N.I.N.A., donc vérifiée par
    /// tools\verify. Elle réutilise la règle d'arrondi du calcul simple : un filtre actif à
    /// 100 % doit donner exactement le même nombre de poses que l'onglet « Pose unique ».
    /// </summary>
    public static class FilterPlanCalculator {

        /// <summary>
        /// Calcule le plan pour une fenêtre et une liste de filtres.
        /// </summary>
        /// <param name="window">Temps disponible (calculé par le panneau, passage de minuit inclus).</param>
        /// <param name="entries">Filtres du plan ; seuls les filtres actifs comptent dans les totaux.</param>
        /// <param name="overheadSeconds">
        /// Surcharge appliquée à chaque pose : le temps consommé par une pose du filtre est
        /// <c>exposition + surcharge</c>, alors que le temps couvert reste <c>poses × exposition</c>.
        /// </param>
        public static FilterPlanResult Compute(TimeSpan window, IEnumerable<FilterPlanEntry> entries, double overheadSeconds = 0d) {
            var list = entries?.ToList() ?? new List<FilterPlanEntry>();
            var lines = new List<FilterPlanLine>(list.Count);

            var overhead = ExposureBudgetCalculator.Clamp(overheadSeconds, 0d, ExposureBudgetCalculator.MaxOverheadSeconds);
            var overheadDecimal = (decimal)overhead;

            var totalPercent = 0;
            var totalCount = 0;
            var totalCoveredSeconds = 0m;
            var totalConsumedSeconds = 0m;

            foreach (var entry in list) {
                if (entry == null) { continue; }

                var hasValidExposure = entry.ExposureSeconds > 0d
                                    && !double.IsNaN(entry.ExposureSeconds)
                                    && !double.IsInfinity(entry.ExposureSeconds);

                if (!entry.Enabled || !hasValidExposure || window <= TimeSpan.Zero) {
                    lines.Add(new FilterPlanLine {
                        Entry = entry,
                        Allocated = TimeSpan.Zero,
                        ExactCount = 0m,
                        Count = 0,
                        Covered = TimeSpan.Zero,
                        Consumed = TimeSpan.Zero,
                        PerPoseSeconds = hasValidExposure ? (decimal)entry.ExposureSeconds + overheadDecimal : 0m,
                        HasValidExposure = hasValidExposure
                    });
                    continue;
                }

                var perPose = (decimal)entry.ExposureSeconds + overheadDecimal;
                var allocatedSeconds = (decimal)window.TotalSeconds * entry.Percent / 100m;
                var exactCount = perPose > 0m ? allocatedSeconds / perPose : 0m;
                var count = ExposureBudgetCalculator.RoundToPoseCount(exactCount);
                var coveredSeconds = (decimal)entry.ExposureSeconds * count;
                var consumedSeconds = perPose * count;

                lines.Add(new FilterPlanLine {
                    Entry = entry,
                    Allocated = TimeSpan.FromSeconds((double)allocatedSeconds),
                    ExactCount = exactCount,
                    Count = count,
                    Covered = TimeSpan.FromSeconds((double)coveredSeconds),
                    Consumed = TimeSpan.FromSeconds((double)consumedSeconds),
                    PerPoseSeconds = perPose,
                    HasValidExposure = true
                });

                totalPercent += entry.Percent;
                totalCount += count;
                totalCoveredSeconds += coveredSeconds;
                totalConsumedSeconds += consumedSeconds;
            }

            return new FilterPlanResult {
                Lines = lines,
                Window = window,
                OverheadSeconds = overhead,
                TotalPercent = totalPercent,
                TotalCount = totalCount,
                TotalCovered = TimeSpan.FromSeconds((double)totalCoveredSeconds),
                TotalConsumed = TimeSpan.FromSeconds((double)totalConsumedSeconds),
                EnabledCount = list.Count(x => x != null && x.Enabled)
            };
        }

        /// <summary>
        /// Répartit 100 % entre les filtres actifs, par pas de 5 points, le plus équitablement
        /// possible : le reliquat de la division est donné aux premiers filtres.
        /// </summary>
        public static void DistributeEvenly(IList<FilterPlanEntry> entries, int step = 5) {
            if (entries == null || entries.Count == 0) { return; }
            if (step <= 0) { step = 5; }

            var enabled = entries.Where(x => x != null && x.Enabled).ToList();
            if (enabled.Count == 0) { return; }

            var share = step * (100 / (step * enabled.Count));
            var remainder = 100 - (share * enabled.Count);

            for (var i = 0; i < enabled.Count; i++) {
                var extra = remainder >= step && i < (remainder / step) ? step : 0;
                enabled[i].Percent = share + extra;
            }

            foreach (var disabled in entries.Where(x => x != null && !x.Enabled)) {
                disabled.Percent = 0;
            }
        }
    }
}
