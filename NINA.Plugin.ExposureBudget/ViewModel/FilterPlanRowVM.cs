using NINA.Core.Utility;
using NINA.Plugin.ExposureBudget.Model;
using System;
using System.Windows.Input;
using MvvmCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NINA.Plugin.ExposureBudget.ViewModel {

    /// <summary>
    /// Une ligne du plan par filtres : filtre utilisé ou non, nom modifiable, temps de pose et
    /// pourcentage du temps disponible, plus le nombre de poses calculé.
    ///
    /// Chaque modification prévient le panneau (<c>changed</c>), qui recalcule le plan entier et
    /// enregistre les réglages : les totaux et les validations restent donc toujours à jour.
    /// </summary>
    public sealed class FilterPlanRowVM : BaseINPC {

        private readonly Action changed;

        private int pendingRemainder;

        public FilterPlanRowVM(FilterPlanEntry entry, Action changed) {
            Entry = entry ?? new FilterPlanEntry();
            this.changed = changed;
            ReceiveRemainderCommand = new MvvmCommand(() => ReceiveRemainder());
        }

        /// <summary>Données persistées de ce filtre.</summary>
        public FilterPlanEntry Entry { get; }

        /// <summary>Nom du filtre, modifiable (un nom vide conserve la valeur précédente).</summary>
        public string Name {
            get => Entry.Name;
            set {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0 || trimmed == Entry.Name) { return; }
                Entry.Name = trimmed;
                RaisePropertyChanged();
                changed?.Invoke();
            }
        }

        /// <summary>Donne les pourcentages non utilisés à ce filtre.</summary>
        public ICommand ReceiveRemainderCommand { get; }

        private bool canReceiveRemainder;

        /// <summary>Vrai quand il reste des points de pourcentage et que le filtre est actif.</summary>
        public bool CanReceiveRemainder {
            get => canReceiveRemainder;
            private set {
                if (canReceiveRemainder == value) { return; }
                canReceiveRemainder = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Appelé par le panneau après chaque calcul : mémorise les points non utilisés et
        /// active le bouton « attribuer le reliquat » si le filtre peut les recevoir.
        /// </summary>
        internal void SetRemainderAvailability(int unusedPercent) {
            pendingRemainder = Math.Max(0, unusedPercent);
            CanReceiveRemainder = Entry.Enabled && pendingRemainder > 0 && Entry.Percent < 100;
        }

        private void ReceiveRemainder() {
            if (pendingRemainder <= 0) { return; }

            // Le total des pourcentages ne peut pas dépasser 100 : l'addition est donc sûre,
            // la borne haute protège seulement contre une saisie incohérente.
            Percent = Math.Min(100, Entry.Percent + pendingRemainder);
        }

        /// <summary>Vrai si ce filtre participe au plan.</summary>
        public bool Enabled {
            get => Entry.Enabled;
            set {
                if (Entry.Enabled == value) { return; }
                Entry.Enabled = value;
                RaisePropertyChanged();
                changed?.Invoke();
            }
        }

        /// <summary>Temps de pose du filtre, en secondes.</summary>
        public double ExposureSeconds {
            get => Entry.ExposureSeconds;
            set {
                var clamped = ExposureBudgetCalculator.Clamp(
                    value,
                    ExposureBudgetCalculator.MinExposureSeconds,
                    ExposureBudgetCalculator.MaxExposureSeconds);

                if (Math.Abs(clamped - Entry.ExposureSeconds) < 1e-9) { return; }
                Entry.ExposureSeconds = clamped;
                RaisePropertyChanged();
                changed?.Invoke();
            }
        }

        /// <summary>Part du temps disponible allouée à ce filtre, en pourcentage entier.</summary>
        public int Percent {
            get => Entry.Percent;
            set {
                var clamped = Math.Clamp(value, MinPercent, MaxPercent);
                if (clamped == Entry.Percent) { return; }
                Entry.Percent = clamped;
                RaisePropertyChanged();
                changed?.Invoke();
            }
        }

        private string countText = string.Empty;

        /// <summary>Nombre de poses calculé, tel qu'affiché (vide si le filtre n'est pas utilisé).</summary>
        public string CountText {
            get => countText;
            internal set {
                if (countText == value) { return; }
                countText = value;
                RaisePropertyChanged();
            }
        }

        public double MinExposureSeconds => ExposureBudgetCalculator.MinExposureSeconds;

        public double MaxExposureSeconds => ExposureBudgetCalculator.MaxExposureSeconds;

        /// <summary>
        /// Pas de l'ascenseur de temps de pose dans le plan par filtres : **5 secondes**
        /// (l'onglet « Pose unique » garde son pas de 15 s).
        /// </summary>
        public double ExposureStepSeconds => FilterExposureStepSeconds;

        /// <summary>Pas de réglage du temps de pose dans le plan par filtres.</summary>
        public const double FilterExposureStepSeconds = 5d;

        public int MinPercent => 0;

        public int MaxPercent => 100;

        public int PercentStep => 5;

        /// <summary>Répercute une modification faite en masse (répartition, réinitialisation).</summary>
        internal void RaiseAllChanged() {
            RaisePropertyChanged(nameof(Enabled));
            RaisePropertyChanged(nameof(ExposureSeconds));
            RaisePropertyChanged(nameof(Percent));
        }
    }
}
