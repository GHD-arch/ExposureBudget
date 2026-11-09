using System;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>
    /// Un filtre du plan : son nom, s'il est utilisé, son temps de pose et le pourcentage du
    /// temps disponible qui lui est alloué.
    /// </summary>
    public sealed class FilterPlanEntry {

        /// <summary>Nom du filtre, tel qu'il apparaît dans le panneau (L, R, G, B, S, H, O…).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Vrai si le filtre participe au plan.</summary>
        public bool Enabled { get; set; }

        /// <summary>Temps de pose unitaire du filtre, en secondes.</summary>
        public double ExposureSeconds { get; set; } = 120d;

        /// <summary>Part du temps disponible allouée à ce filtre, en pourcentage entier.</summary>
        public int Percent { get; set; }

        public FilterPlanEntry() { }

        public FilterPlanEntry(string name, bool enabled, double exposureSeconds, int percent) {
            Name = name ?? string.Empty;
            Enabled = enabled;
            ExposureSeconds = exposureSeconds;
            Percent = percent;
        }

        public FilterPlanEntry Clone() => new FilterPlanEntry(Name, Enabled, ExposureSeconds, Percent);
    }
}
