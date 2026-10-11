using System;

namespace NINA.Plugin.ExposureBudget.Model {

    /// <summary>
    /// Resultat d'un calcul de nombre de poses.
    /// Toutes les durees manipulees par le modele sont exprimees en secondes decimales
    /// afin d'eviter toute imprecision d'arrondi flottant sur les cas limites (x,5).
    /// </summary>
    public sealed class ExposureCountResult {

        /// <summary>Duree disponible entre Hd et Hf (passage de minuit deja resolu).</summary>
        public TimeSpan Window { get; init; }

        /// <summary>Temps de pose unitaire en secondes.</summary>
        public double ExposureSeconds { get; init; }

        /// <summary>Nombre de poses theorique, non arrondi (ex : 70,25).</summary>
        public decimal ExactCount { get; init; }

        /// <summary>Nombre de poses retenu, arrondi a l'entier (regle du 0,5).</summary>
        public int Count { get; init; }

        /// <summary>Temps total reellement consomme par les poses retenues, en secondes.</summary>
        public decimal CoveredSeconds { get; init; }

        /// <summary>Reliquat de la fenetre en secondes. Negatif = depassement de la fenetre.</summary>
        public decimal RemainingSeconds => (decimal)Window.TotalSeconds - CoveredSeconds;

        /// <summary>Vrai si la fenetre de saisie est vide, c'est a dire Hd == Hf.</summary>
        public bool EmptyWindow => Window <= TimeSpan.Zero;

        /// <summary>Vrai si les poses retenues depassent la fenetre demandee.</summary>
        public bool Overflows => RemainingSeconds < 0;

        /// <summary>Vrai si l'arrondi a ete fait vers le haut (partie decimale >= 0,5).</summary>
        public bool RoundedUp => Count > 0 && Count > ExactCount;

        /// <summary>Vrai si l'arrondi a ete fait vers le bas (partie decimale < 0,5).</summary>
        public bool RoundedDown => Count > 0 && Count < ExactCount;

        /// <summary>Partie decimale du nombre de poses theorique, utile pour expliquer l'arrondi.</summary>
        public decimal Fraction => ExactCount - decimal.Truncate(ExactCount);
    }
}
