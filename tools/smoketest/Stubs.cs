using NINA.Astrometry;
using NINA.Astrometry.Body;
using NINA.Astrometry.Interfaces;
using NINA.Astrometry.RiseAndSet;
using NINA.Core.Utility;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Globalization;

namespace NINA.Plugin.ExposureBudget.SmokeTest {

    /// <summary>
    /// Faux service de profil : le ViewModel n'utilise du profil que l'evenement
    /// LocationChanged (constructeur de DockableVM) et la langue de l'interface.
    /// ActiveProfile renvoie null pour reproduire l'etat "aucun profil encore charge" :
    /// le plugin doit alors se replier sur la culture d'interface du fil courant.
    /// </summary>
    internal sealed class StubProfileService : IProfileService {

        public bool ProfileWasSpecifiedFromCommandLineArgs => false;

        public AsyncObservableCollection<ProfileMeta> Profiles { get; } = new AsyncObservableCollection<ProfileMeta>();

        public IProfile ActiveProfile => null;

        public bool Clone(ProfileMeta profileInfos) => false;

        public void Add() { }

        public bool SelectProfile(ProfileMeta profileInfo) => false;

        public bool RemoveProfile(ProfileMeta profileInfo) => false;

        public void ChangeLocale(CultureInfo language) { }

        public void ChangeLatitude(double latitude) { }

        public void ChangeLongitude(double longitude) { }

        public void ChangeElevation(double elevation) { }

        public void ChangeHorizon(string horizonFilePath) { }

        public void Release() { }

        public event EventHandler LocaleChanged;

        public event EventHandler LocationChanged { add { } remove { } }

        public event EventHandler BeforeProfileChanging { add { } remove { } }

        public event EventHandler ProfileChanged { add { } remove { } }

        public event EventHandler HorizonChanged { add { } remove { } }

        /// <summary>Reproduit le changement de langue de N.I.N.A. (Options &gt; Langue).</summary>
        public void SimulateLocaleChanged() => LocaleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Faux calculateur de nuit : renvoie les donnees fournies, sans invoquer
    /// l'astrometrie de N.I.N.A. (pas de dependance native dans le harnais).
    /// </summary>
    internal sealed class StubNighttimeCalculator : INighttimeCalculator {

        public NighttimeData Data { get; set; }

        public NighttimeData Calculate(DateTime? selectedDate = null) => Data;

        public event EventHandler OnReferenceDayChanged { add { } remove { } }
    }

    /// <summary>
    /// Evenement crepuscule / aube fabrique pour les tests : les heures sont imposees
    /// au lieu d'etre calculees.
    /// </summary>
    internal sealed class FakeRiseAndSetEvent : RiseAndSetEvent {

        private readonly DateTime? rise;
        private readonly DateTime? set;

        public FakeRiseAndSetEvent(DateTime date, DateTime? set, DateTime? rise)
            : base(date, 48.85, 2.35, 0) {
            this.set = set;
            this.rise = rise;
        }

        public override DateTime? Rise => rise;

        public override DateTime? Set => set;

        protected override double AdjustAltitude(BasicBody body) => 0d;

        protected override BasicBody GetBody(DateTime date) => null;
    }

    internal static class FakeNighttimeDataFactory {

        public static NighttimeData Create(DateTime referenceDate, DateTime? dusk, DateTime? dawn, bool withAstronomicalTwilight = true) {
            var twilight = withAstronomicalTwilight
                ? new FakeRiseAndSetEvent(referenceDate, dusk, dawn)
                : null;

            return new NighttimeData(
                date: referenceDate,
                referenceDate: referenceDate,
                moonPhase: default(AstroUtil.MoonPhase),
                moonIllumination: null,
                twilightRiseAndSet: twilight,
                nauticalTwilightRiseAndSet: null,
                sunRiseAndSet: null,
                moonRiseAndSet: null,
                civilTwilightRiseAndSet: null);
        }

        /// <summary>
        /// Donnees sans crepuscule astronomique mais avec un crepuscule nautique :
        /// verifie le repli successif du plugin.
        /// </summary>
        public static NighttimeData CreateNauticalOnly(DateTime referenceDate, DateTime? dusk, DateTime? dawn) {
            return new NighttimeData(
                date: referenceDate,
                referenceDate: referenceDate,
                moonPhase: default(AstroUtil.MoonPhase),
                moonIllumination: null,
                twilightRiseAndSet: null,
                nauticalTwilightRiseAndSet: new FakeRiseAndSetEvent(referenceDate, dusk, dawn),
                sunRiseAndSet: null,
                moonRiseAndSet: null,
                civilTwilightRiseAndSet: null);
        }
    }
}
