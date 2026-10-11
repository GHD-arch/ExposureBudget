using NINA.Core.Utility;
using NINA.Plugin.ExposureBudget.Localization;
using NINA.Plugin.ExposureBudget.Model;
using NINA.Plugin.Interfaces;
using NINA.Plugin.ManifestDefinition;
using NINA.Profile.Interfaces;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading.Tasks;

namespace NINA.Plugin.ExposureBudget {

    /// <summary>
    /// Manifeste du plugin. NINA decouvre ce point d'export MEF (IPluginManifest) pour
    /// identifier le plugin, verifier sa compatibilite puis charger ses autres exports
    /// (le dockable <see cref="ViewModel.ExposureBudgetVM"/> et le dictionnaire de ressources).
    ///
    /// Nom, auteur, version et version minimale de NINA sont lus dans les attributs
    /// d'assembly declares dans Properties\AssemblyInfo.cs. Les textes de presentation
    /// (fiche du plugin dans Options &gt; Plugins) sont, eux, traduits a l'execution selon
    /// la langue de l'interface de N.I.N.A.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class ExposureBudgetPlugin : PluginBase, IPluginManifest {

        /// <summary>
        /// Image du plugin (ressource integree a l'assembly). N.I.N.A. l'affiche dans
        /// Options &gt; Plugins : la valeur de FeaturedImageURL est passee telle quelle a
        /// l'Image.Source (le convertisseur de N.I.N.A. ne fait que laisser passer la
        /// valeur), donc un pack URI WPF suffit. L'image est preparee par tools\makeicon
        /// (mode --resize) : 256x256, carree, dimensionnee pour un affichage jusqu'a
        /// 150 px (et jusqu'a 225 px sur un ecran a 150 % de mise a l'echelle).
        /// </summary>
        private const string IconUri = "pack://application:,,,/NINA.Plugin.ExposureBudget;component/Resources/PluginImage.png";

        private IPluginStrings strings;

        /// <summary>
        /// Service de profil : il porte la langue de l'interface de N.I.N.A.
        /// Import optionnel, pour que le plugin reste chargeable meme si N.I.N.A. ne le fournit pas.
        /// </summary>
        [Import(AllowDefault = true)]
        public IProfileService ProfileService { get; set; }

        /// <summary>
        /// Presentation du plugin affichee dans Options &gt; Plugins.
        ///
        /// Masque volontairement la propriete de PluginBase, qui ne sait lire que les
        /// attributs d'assembly figes en anglais. Trois points sont necessaires :
        ///   - "public new" : la page des plugins de N.I.N.A. affiche ces textes par
        ///     liaison WPF, qui utilise la reflexion sur les membres PUBLICS du type
        ///     reel ; une implementation d'interface explicite serait invisible et WPF
        ///     retomberait sur la propriete de PluginBase (textes en anglais) ;
        ///   - IPluginManifest figure dans la liste de base de cette classe : la
        ///     propriete reimplemente donc aussi l'interface, pour les appels C# ;
        ///   - le nom du plugin n'est pas traduit : N.I.N.A. en derive le nom du dossier
        ///     d'installation (PluginInstaller.GetDestinationFolderFromManifest).
        /// </summary>
        public new IPluginDescription Descriptions => new PluginDescription {
            ShortDescription = Strings.PluginShortDescription,
            LongDescription = Strings.PluginLongDescription,
            FeaturedImageURL = IconUri,
            ScreenshotURL = string.Empty,
            AltScreenshotURL = string.Empty
        };

        public override Task Initialize() {
            return Task.CompletedTask;
        }

        public override Task Teardown() {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Langue retenue pour la fiche du plugin, mise en cache puis invalidee lorsque
        /// l'utilisateur change la langue de N.I.N.A. (aucun redemarrage necessaire).
        /// </summary>
        private IPluginStrings Strings {
            get {
                if (strings == null) {
                    var ninaLanguage = PluginLanguage.GetNinaLanguage(ProfileService);
                    strings = PluginStrings.Resolve(ExposureBudgetSettings.Load().Language, ninaLanguage);

                    Logger.Info(string.Format(CultureInfo.InvariantCulture,
                        "ExposureBudget: plugin sheet language '{0}' (N.I.N.A. language '{1}')",
                        strings.Language, ninaLanguage?.Name ?? "<unavailable>"));

                    var profileService = ProfileService;
                    if (profileService != null) {
                        profileService.LocaleChanged += (sender, args) => strings = null;
                    }
                }

                return strings;
            }
        }
    }
}
