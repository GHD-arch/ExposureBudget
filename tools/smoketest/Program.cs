using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.CustomControlLibrary;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Plugin.Interfaces;
using NINA.Plugin.ExposureBudget.Localization;
using NINA.Plugin.ExposureBudget.Model;
using NINA.Plugin.ExposureBudget.ViewModel;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NINA.Plugin.ExposureBudget.SmokeTest {

    /// <summary>
    /// Charge le plugin comme le fait N.I.N.A. et verifie de bout en bout :
    /// exports MEF, metadonnees du manifeste, DataTemplate resolvable par la cle
    /// attendue par PaneTemplateSelector, instanciation de la vue, evaluation des
    /// liaisons (valeurs et libelles), langue et calculs affiches.
    /// Code de sortie 0 si tout est conforme, 1 sinon.
    /// </summary>
    internal static class Program {

        private const string DockableTemplateSuffix = "_Dockable";

        private static readonly CultureInfo EnglishFormatting = CultureInfo.GetCultureInfo("en-US");

        private static readonly CultureInfo FrenchFormatting = CultureInfo.GetCultureInfo("fr-FR");

        private static string FormatSeconds(decimal value, CultureInfo culture) => value.ToString("#,0.###", culture);

        private static int failures;
        private static int checks;

        [STAThread]
        private static int Main() {
            // Le systeme simule ici est en francais (comme le poste de l'auteur du plugin)
            // alors que la langue par defaut du plugin est l'anglais : les nombres doivent
            // donc suivre la langue du panneau et non celle du systeme.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

            Console.WriteLine("== Exposure Budget : test de chargement du plugin (facon N.I.N.A.) ==");
            Console.WriteLine();

            var pluginAssembly = typeof(ExposureBudgetVM).Assembly;
            Console.WriteLine($"Assembly du plugin : {pluginAssembly.GetName().Name} {pluginAssembly.GetName().Version}");
            Console.WriteLine();

            // Le plugin lit ses reglages a sa construction : on part d'un etat deterministe
            // (langue anglaise explicite) et non d'un fichier laisse par une execution precedente.
            var settingsPath = ExposureBudgetSettings.SettingsFilePath;
            string settingsBackup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            WriteSettings(settingsPath, "en");

            var imports = ComposePluginExports(pluginAssembly);
            if (imports == null) { return Report(); }

            Section("1. Manifeste du plugin");
            var manifest = imports.Manifest;
            CheckEqual("Nom du plugin", "Exposure Budget", manifest.Name);
            CheckEqual("Auteur", "Gerard Hurtaud", manifest.Author);
            CheckEqual("Version", "1.5.4.0", manifest.Version.ToString());
            Check("Version a quatre composants (exigence de N.I.N.A.)",
                manifest.Version.ToString().Split('.').Length == 4
                && manifest.Version.Build >= 0
                && manifest.Version.Patch >= 0,
                "4 composants, Build et Revision >= 0",
                manifest.Version.ToString());
            CheckEqual("Version minimale de N.I.N.A.", "3.0.0.0", manifest.MinimumApplicationVersion.ToString());
            CheckEqual("Identifiant (GUID)", "8f3a1c64-2d7e-4b91-a3f5-6c0d9e7b4a28", manifest.Identifier.ToLowerInvariant());
            Check("Licence renseignee", !string.IsNullOrWhiteSpace(manifest.License));
            Check("Compatibilite avec N.I.N.A. 3.2.0.9001",
                NINA.Plugin.ManifestDefinition.PluginVersion.IsPluginCompatible(manifest.MinimumApplicationVersion, new Version(3, 2, 0, 9001)));

            // La fiche du plugin (Options > Plugins) est lue par N.I.N.A. a travers
            // IPluginManifest : ces verifications passent donc bien par la reimplementation
            // du plugin et non par les attributs d'assembly.
            Check("Fiche du plugin, description courte en anglais",
                manifest.Descriptions.ShortDescription.Contains("filter plan", StringComparison.Ordinal)
                && manifest.Descriptions.ShortDescription.Contains("how many exposures fit", StringComparison.Ordinal),
                "les poses et le plan par filtres",
                manifest.Descriptions.ShortDescription);
            Check("Fiche du plugin, description longue en anglais",
                manifest.Descriptions.LongDescription.StartsWith("Exposure Budget works out", StringComparison.Ordinal),
                "commence par \"Exposure Budget works out\"", manifest.Descriptions.LongDescription);

            // La fiche doit couvrir toutes les fonctions : un utilisateur doit comprendre le
            // panneau sans lire le README.
            var fiche = manifest.Descriptions.LongDescription;
            foreach (var rubrique in new[] { "SESSION WINDOW", "EXPOSURE COUNT", "A REALISTIC NIGHT", "MONO CAMERAS AND FILTERS", "SETTINGS AND LANGUAGE" }) {
                Check($"Fiche anglaise : rubrique {rubrique}", fiche.Contains(rubrique, StringComparison.Ordinal), rubrique, fiche);
            }

            Check("La fiche ne decrit plus un defaut anglais fige",
                !manifest.Descriptions.LongDescription.Contains("English by default", StringComparison.Ordinal),
                "aucune mention de \"English by default\"", manifest.Descriptions.LongDescription);

            // Icone de la fiche du plugin : N.I.N.A. lie Image.Source directement a
            // FeaturedImageURL, donc un pack URI doit se resoudre en image.
            Check("Icone du plugin renseignee",
                !string.IsNullOrWhiteSpace(manifest.Descriptions.FeaturedImageURL),
                "un pack URI", manifest.Descriptions.FeaturedImageURL);

            System.Windows.Media.Imaging.BitmapImage icon = null;
            try {
                if (!string.IsNullOrWhiteSpace(manifest.Descriptions.FeaturedImageURL)) {
                    icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(manifest.Descriptions.FeaturedImageURL, UriKind.Absolute));
                }
            } catch (Exception ex) {
                Console.WriteLine($"        -> {ex.Message}");
            }

            Check("L'icone se charge depuis le pack URI", icon != null && icon.PixelWidth > 0,
                "une image non vide", icon == null ? "<null>" : $"{icon.PixelWidth}x{icon.PixelHeight}");
            Check("Icone carree et de taille suffisante",
                icon != null && icon.PixelWidth == icon.PixelHeight && icon.PixelWidth >= 128,
                "au moins 128x128 carre",
                icon == null ? "<null>" : $"{icon.PixelWidth}x{icon.PixelHeight}");

            // Reproduction de la liaison de PluginsView.xaml (volet de detail).
            var iconProbe = new Image { Width = 150, Height = 150 };
            iconProbe.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Descriptions.FeaturedImageURL"));
            iconProbe.DataContext = manifest;
            var iconHost = new Grid { Width = 200, Height = 200 };
            iconHost.Children.Add(iconProbe);
            iconHost.Measure(new Size(200, 200));
            iconHost.Arrange(new Rect(0, 0, 200, 200));
            iconHost.UpdateLayout();

            Check("La liaison WPF de la fiche affiche bien l'icone (et non l'image par defaut)",
                iconProbe.Source is System.Windows.Media.Imaging.BitmapSource boundIcon && boundIcon.PixelWidth >= 128,
                "une image >= 128 px de large",
                iconProbe.Source == null ? "<null>" : $"{iconProbe.Source.GetType().Name} {((System.Windows.Media.Imaging.BitmapSource)iconProbe.Source).PixelWidth}px");

            Section("2. Exports MEF");
            Check("ResourceDictionary exporte", imports.Dictionaries.Any());
            CheckEqual("Un seul panneau (IDockableVM) exporte", 1, imports.Dockables.Count());
            var vm = imports.Dockables.OfType<ExposureBudgetVM>().SingleOrDefault();
            Check("Le panneau est bien ExposureBudgetVM", vm != null);
            if (vm == null) { return Report(); }
            CheckEqual("Titre du panneau", "Exposure Budget", vm.Title);
            CheckEqual("Identifiant de contenu", "ExposureBudgetVM", vm.ContentId);
            Check("Panneau visible par defaut", vm.IsVisible);
            Check("Calculateur de nuit injecte par MEF (Import AllowDefault)", vm.NighttimeCalculator != null);

            Section("3. Langue par defaut : anglais");
            CheckEqual("Langue retenue malgre un systeme en francais", "en", vm.Labels.Language);
            CheckEqual("Culture d'affichage anglaise", "en-US", vm.Labels.Culture.Name);
            CheckEqual("Libelle anglais", "Start", vm.Labels.StartLabel);
            CheckEqual("Libelle anglais de fin", "End", vm.Labels.EndLabel);
            CheckEqual("Titre du groupe de resultat", "Number of exposures", vm.Labels.ResultGroupHeader);

            Section("4. Ressources et DataTemplate");
            var application = new Application();
            foreach (var dictionary in imports.Dictionaries) {
                application.Resources.MergedDictionaries.Add(dictionary);
            }

            var templateKey = typeof(ExposureBudgetVM).FullName + DockableTemplateSuffix;
            CheckEqual("Cle attendue par PaneTemplateSelector", "NINA.Plugin.ExposureBudget.ViewModel.ExposureBudgetVM_Dockable", templateKey);
            Check("Cle trouvee dans Application.Resources", application.Resources.Contains(templateKey));
            var template = application.Resources[templateKey] as DataTemplate;
            Check("La cle pointe vers un DataTemplate", template != null);
            CheckEqual("DataType du DataTemplate", typeof(ExposureBudgetVM), template?.DataType as Type);

            Section("5. Instanciation et pose de la vue");
            var view = template?.LoadContent() as UserControl;
            Check("La vue se charge depuis le DataTemplate (BAML)", view != null);
            if (view == null) { return Report(); }
            CheckEqual("Type de la vue", "ExposureBudgetView", view.GetType().Name);

            vm.StartHour = 21;
            vm.StartMinute = 30;
            vm.EndHour = 3;
            vm.EndMinute = 15;
            vm.ExposureSeconds = 300d;

            view.DataContext = vm;
            var host = new Grid { Width = 380, Height = 720 };
            host.Children.Add(view);

            string layoutError = null;
            try {
                host.Measure(new Size(380, 720));
                host.Arrange(new Rect(0, 0, 380, 720));
                host.UpdateLayout();
            } catch (Exception ex) {
                layoutError = ex.Message;
            }
            Check("Pose complete de la vue sans erreur", layoutError == null, "aucune exception", layoutError);
            Check("La vue occupe une surface non nulle", host.DesiredSize.Width > 0 && host.DesiredSize.Height > 0,
                "largeur et hauteur > 0", $"({host.DesiredSize.Width} ; {host.DesiredSize.Height})");

            Section("6. Libelles effectivement affiches (liaisons sur Labels)");
            var displayedTexts = CollectTexts(host);
            CheckText(displayedTexts, "Start");
            CheckText(displayedTexts, "End");
            CheckText(displayedTexts, "Available duration");
            CheckText(displayedTexts, "Exposure");
            CheckText(displayedTexts, "Astronomical night (N.I.N.A.)");
            CheckText(displayedTexts, "Number of exposures");
            Check("Aucun libelle avec un raccourci Hd/Hf",
                !displayedTexts.Any(t => t.Contains("Hd", StringComparison.Ordinal) || t.Contains("Hf", StringComparison.Ordinal)),
                "aucun Hd/Hf", string.Join(" | ", displayedTexts));
            Check("Aucun bouton de calcul dans le panneau",
                !displayedTexts.Any(t => t.Equals("CALCULATE", StringComparison.Ordinal) || t.Equals("CALCULER", StringComparison.Ordinal)),
                "aucun bouton CALCULATE",
                string.Join(" | ", FindVisualChildren<Button>(host).Select(b => b.Content?.ToString())));
            Check("Aucun libelle francais residuel",
                !displayedTexts.Any(t => t.Contains("Début", StringComparison.Ordinal) || t.Contains("Nombre de poses", StringComparison.Ordinal)),
                "aucun libelle francais",
                string.Join(" | ", displayedTexts.Where(t => t.Contains("é", StringComparison.Ordinal))));
            CheckText(displayedTexts, "Now");
            CheckText(displayedTexts, "Copy result");

            Section("7. Liaisons des ascenseurs");
            var steppers = FindVisualChildren<IntStepperControl>(host).ToList();
            CheckEqual("Quatre ascenseurs HH/mm presents", 4, steppers.Count);
            if (steppers.Count == 4) {
                CheckEqual("Ascenseur HH debut lie a 21", 21, steppers[0].Value);
                CheckEqual("Ascenseur mm debut lie a 30", 30, steppers[1].Value);
                CheckEqual("Ascenseur HH fin lie a 3", 3, steppers[2].Value);
                CheckEqual("Ascenseur mm fin lie a 15", 15, steppers[3].Value);
                CheckEqual("Bornes de l'ascenseur HH debut", (0, 23), (steppers[0].MinValue, steppers[0].MaxValue));
                CheckEqual("Bornes de l'ascenseur mm debut", (0, 59), (steppers[1].MinValue, steppers[1].MaxValue));
            }

            var exposureSteppers = FindVisualChildren<StepperControl>(host).ToList();
            CheckEqual("Un ascenseur de temps de pose present", 1, exposureSteppers.Count);
            if (exposureSteppers.Count == 1) {
                CheckEqual("Pas de 15 s", 15d, exposureSteppers[0].StepSize);
                CheckEqual("Temps de pose lie a 300 s", 300d, exposureSteppers[0].Value);
                CheckEqual("Bornes du temps de pose", (0.1d, 86400d), (exposureSteppers[0].MinValue, exposureSteppers[0].MaxValue));
            }

            Section("8. Resultat affiche (21:30 -> 03:15, pose de 300 s)");
            CheckEqual("Duree disponible", "05:45:00", vm.WindowText);
            CheckEqual("Duree en secondes (formatage anglais)",
                FormatSeconds(20700m, EnglishFormatting) + " s", vm.WindowSecondsText);
            Check("Debut de la seance a 21:30", vm.StartDateTimeText.EndsWith("21:30", StringComparison.Ordinal), "*21:30", vm.StartDateTimeText);
            Check("Debut et fin sur deux jours differents",
                vm.StartDateTimeText.Substring(0, 10) != vm.EndDateTimeText.Substring(0, 10),
                "dates differentes", $"{vm.StartDateTimeText} / {vm.EndDateTimeText}");
            Check("Fin de la seance a 03:15", vm.EndDateTimeText.EndsWith("03:15", StringComparison.Ordinal), "*03:15", vm.EndDateTimeText);
            CheckEqual("Nombre de poses", "69", vm.PoseCountText);
            CheckEqual("Equivalence en minutes", "5 min 00 s", vm.ExposureMinutesInfo);
            CheckEqual("Equivalence en minutes decimales", "i.e. 5 min", vm.ExposureDecimalMinutes);
            CheckEqual("Detail du calcul",
                $"{FormatSeconds(20700m, EnglishFormatting)} s ÷ 300 s = 69 → 69 exposures", vm.ResultDetailText);
            CheckEqual("Temps couvert et reliquat", "Covered time: 05:45:00 (69 × 300 s) · Remaining: 00:00:00", vm.CoverageText);
            Check("Aucun avertissement pour un compte exact", !vm.HasWarning, "aucun avertissement", vm.WarningText);
            CheckEqual("Detail d'arrondi", "The exposure count comes out exact: no rounding is applied.", vm.RoundingText);
            Check("Horodatage de la derniere pose", vm.PoseEndDateTimeText.StartsWith("Last exposure ends:", StringComparison.Ordinal),
                "Last exposure ends: ...", vm.PoseEndDateTimeText);
            CheckText(displayedTexts, "69");

            Section("9. Recalcul en direct, sans bouton");
            vm.EndHour = 21;
            vm.EndMinute = 30;
            CheckEqual("Changement d'heure pris en compte sans commande", "0", vm.PoseCountText);
            vm.EndHour = 3;
            vm.EndMinute = 15;
            CheckEqual("Retour a la fenetre initiale", "69", vm.PoseCountText);

            Section("10. Regle du 0,5");
            vm.ExposureSeconds = 600d;
            CheckEqual("Pose de 600 s : 34,5 -> 35 poses", "35", vm.PoseCountText);
            Check("Depassement signale", vm.HasWarning, "avertissement", vm.WarningText);
            CheckEqual("Message de depassement",
                "The 35 retained exposures exceed the window by 00:05:00: the last exposure ends after the end time. Reduce the exposure time or start earlier.",
                vm.WarningText);
            Check("Detail d'arrondi du 0,5",
                vm.RoundingText.Contains("0.5", StringComparison.Ordinal) && vm.RoundingText.Contains("extra exposure", StringComparison.Ordinal),
                "*0.5* extra exposure*", vm.RoundingText);
            Check("Reliquat negatif affiche", vm.CoverageText.Contains("-00:05:00", StringComparison.Ordinal), "*-00:05:00*", vm.CoverageText);

            vm.ExposureSeconds = 1350d;
            CheckEqual("Pose de 1350 s : 15,333 -> 15 poses", "15", vm.PoseCountText);
            Check("Arrondi vers le bas explique", vm.RoundingText.Contains("is not counted", StringComparison.Ordinal), "*is not counted*", vm.RoundingText);
            Check("Un seul message d'avertissement a la fois", !vm.WarningText.Contains(Environment.NewLine, StringComparison.Ordinal),
                "un seul message", vm.WarningText);

            Section("11. Heures identiques (fenetre nulle)");
            vm.ExposureSeconds = 300d;
            vm.EndHour = 21;
            vm.EndMinute = 30;
            CheckEqual("Fenetre nulle", "00:00:00", vm.WindowText);
            CheckEqual("Aucune pose", "0", vm.PoseCountText);
            CheckEqual("Avertissement sur heures identiques",
                "Start and end times are identical: the window is empty, no exposure is possible. Check the start and end times.", vm.WarningText);

            Section("12. Bouton Nuit astronomique (N.I.N.A.)");
            vm.EndHour = 3;
            vm.EndMinute = 15;

            var referenceDate = new DateTime(2026, 10, 10);
            var nighttimeCalculator = GetCalculator(imports);
            nighttimeCalculator.Data = FakeNighttimeDataFactory.Create(
                referenceDate,
                dusk: new DateTime(2026, 10, 10, 19, 42, 0),
                dawn: new DateTime(2026, 10, 11, 5, 32, 0));

            vm.UseNightTimesCommand.Execute(null);
            CheckEqual("Heure de debut issue du crepuscule", 19, vm.StartHour);
            CheckEqual("Minutes de debut issues du crepuscule", 42, vm.StartMinute);
            CheckEqual("Heure de fin issue de l'aube", 5, vm.EndHour);
            CheckEqual("Minutes de fin issues de l'aube", 32, vm.EndMinute);
            CheckEqual("Debut affiche", "10/10/2026 19:42", vm.StartDateTimeText);
            CheckEqual("Fin affichee le lendemain", "11/10/2026 05:32", vm.EndDateTimeText);
            Check("Nuit astronomique : aucun message redondant (les heures suffisent)", string.IsNullOrEmpty(vm.NightInfoText), "aucun message", vm.NightInfoText);
            Check("Aucun avertissement lorsque la nuit tombe juste (9 h 50 min a 300 s)", !vm.HasWarning, "aucun avertissement", vm.WarningText);
            CheckEqual("Nombre de poses pour 9 h 50 min a 300 s", "118", vm.PoseCountText);

            nighttimeCalculator.Data = FakeNighttimeDataFactory.Create(
                referenceDate,
                dusk: new DateTime(2026, 10, 10, 19, 42, 0),
                dawn: new DateTime(2026, 10, 11, 5, 31, 0));
            vm.UseNightTimesCommand.Execute(null);
            CheckEqual("Nuit de 9 h 49 min : 117,8 -> 118 poses", "118", vm.PoseCountText);
            Check("Depassement de la fenetre signale",
                vm.HasWarning && vm.WarningText.Contains("exceed the window by 00:01:00", StringComparison.Ordinal),
                "*exceed the window by 00:01:00*", vm.WarningText);

            Section("13. Repli quand le crepuscule astronomique est absent");
            nighttimeCalculator.Data = FakeNighttimeDataFactory.Create(referenceDate, dusk: null, dawn: null, withAstronomicalTwilight: false);
            vm.UseNightTimesCommand.Execute(null);
            CheckEqual("Avertissement explicatif",
                "No usable twilight for this date (white night or polar latitude): enter the times manually.", vm.WarningText);

            Section("14. Repli sur le crepuscule nautique");
            nighttimeCalculator.Data = FakeNighttimeDataFactory.CreateNauticalOnly(
                referenceDate,
                dusk: new DateTime(2026, 10, 10, 20, 15, 0),
                dawn: new DateTime(2026, 10, 11, 4, 45, 0));

            vm.UseNightTimesCommand.Execute(null);
            CheckEqual("Repli nautique : heure de debut", 20, vm.StartHour);
            CheckEqual("Repli nautique : minutes de debut", 15, vm.StartMinute);
            CheckEqual("Repli nautique : heure de fin", 4, vm.EndHour);
            Check("Repli signale dans l'information", vm.NightInfoText.Contains("Nautical night", StringComparison.Ordinal), "*Nautical night*", vm.NightInfoText);

            Section("15. Memorisation des reglages");
            vm.StartHour = 22;
            vm.StartMinute = 5;
            vm.EndHour = 4;
            vm.EndMinute = 40;
            vm.ExposureSeconds = 420d;
            Check("Fichier de reglages ecrit a cote du plugin", File.Exists(settingsPath), settingsPath, "fichier absent");
            Check("Langue memorisee", File.ReadAllText(settingsPath).Contains("language=en", StringComparison.Ordinal), "language=en", File.ReadAllText(settingsPath));
            Check("Marqueur de version memoire", File.ReadAllText(settingsPath).Contains("settingsVersion=1", StringComparison.Ordinal), "settingsVersion=1", File.ReadAllText(settingsPath));

            var reloaded = ExposureBudgetSettings.Load();
            CheckEqual("Heures relues : debut", 22, reloaded.StartHour);
            CheckEqual("Heures relues : minutes de debut", 5, reloaded.StartMinute);
            CheckEqual("Heures relues : fin", 4, reloaded.EndHour);
            CheckEqual("Heures relues : minutes de fin", 40, reloaded.EndMinute);
            CheckEqual("Temps de pose relu", 420d, reloaded.ExposureSeconds);
            CheckEqual("Langue relue", "en", reloaded.Language);
            CheckEqual("Version des reglages relue", 1, reloaded.SettingsVersion);

            // Fichier d'une version anterieure du plugin (pas de marqueur) : la langue, que
            // l'ancienne version ecrivait en anglais par defaut, revient a la valeur courante
            // ("auto"), tandis que les heures saisies par l'utilisateur sont conservees.
            File.WriteAllLines(settingsPath, new[] {
                "language=en",
                "startHour=20",
                "startMinute=59",
                "endHour=6",
                "endMinute=34",
                "exposureSeconds=180"
            });
            var migrated = ExposureBudgetSettings.Load();
            CheckEqual("Ancien fichier : langue ramenee a la valeur par defaut", "auto", migrated.Language);
            CheckEqual("Ancien fichier : heures de l'utilisateur conservees", 20, migrated.StartHour);
            CheckEqual("Ancien fichier : minutes conservees", 59, migrated.StartMinute);
            CheckEqual("Ancien fichier : temps de pose conserve", 180d, migrated.ExposureSeconds);

            // Un fichier a jour marque ses reglages : la langue explicite est respectee.
            File.WriteAllLines(settingsPath, new[] { "settingsVersion=1", "language=en" });
            CheckEqual("Fichier a jour : langue explicite respectee", "en", ExposureBudgetSettings.Load().Language);

            File.WriteAllLines(settingsPath, new[] {
                "# fichier volontairement mal forme",
                "cleInconnue=42",
                "startHour=19",
                "startMinute=abcd",
                "exposureSeconds=301.5"
            });
            var tolerant = ExposureBudgetSettings.Load();
            CheckEqual("Cle inconnue ignoree, valeur lue conservee", 19, tolerant.StartHour);
            CheckEqual("Valeur invalide ignoree, valeur par defaut conservee", 30, tolerant.StartMinute);
            CheckEqual("Nombre au format invariant relu", 301.5d, tolerant.ExposureSeconds);
            CheckEqual("Langue par defaut si absente du fichier (auto)", "auto", tolerant.Language);

            Section("16. Resolution de la langue");
            CheckEqual("Reglage en -> anglais", "en", PluginStrings.Resolve("en", FrenchFormatting).Language);
            CheckEqual("Reglage fr -> francais", "fr", PluginStrings.Resolve("fr", EnglishFormatting).Language);
            CheckEqual("Reglage vide -> suit N.I.N.A. (francais)", "fr", PluginStrings.Resolve(string.Empty, FrenchFormatting).Language);
            CheckEqual("Reglage inconnu -> suit N.I.N.A. (anglais)", "en", PluginStrings.Resolve("klingon", EnglishFormatting).Language);
            CheckEqual("auto + N.I.N.A. en francais -> francais", "fr", PluginStrings.Resolve("auto", FrenchFormatting).Language);
            CheckEqual("auto + N.I.N.A. en anglais -> anglais", "en", PluginStrings.Resolve("auto", EnglishFormatting).Language);
            CheckEqual("auto sans langue de N.I.N.A. -> culture d'interface (fr-FR ici)", "fr",
                PluginStrings.Resolve("auto", null).Language);
            CheckEqual("Reglage explicite en -> anglais malgre N.I.N.A. en francais", "en",
                PluginStrings.Resolve("en", FrenchFormatting).Language);

            Section("17. Bascule en francais (language=fr)");
            WriteSettings(settingsPath, "fr");

            var frenchImports = ComposePluginExports(pluginAssembly);
            var frenchVm = frenchImports?.Dockables.OfType<ExposureBudgetVM>().SingleOrDefault();
            Check("Second panneau instancie avec les reglages francais", frenchVm != null);
            if (frenchVm != null) {
                CheckEqual("Langue retenue", "fr", frenchVm.Labels.Language);
                CheckEqual("Culture d'affichage francaise", "fr-FR", frenchVm.Labels.Culture.Name);
                CheckEqual("Titre du panneau inchange selon la langue (nom propre)", "Exposure Budget", frenchVm.Title);
                CheckEqual("Libelle francais", "Début", frenchVm.Labels.StartLabel);
                CheckEqual("Libelle francais de fin", "Fin", frenchVm.Labels.EndLabel);
                CheckEqual("Titre du groupe de resultat", "Nombre de poses", frenchVm.Labels.ResultGroupHeader);
                CheckEqual("Duree en secondes (formatage francais)",
                    FormatSeconds(20700m, FrenchFormatting) + " s", frenchVm.WindowSecondsText);
                CheckEqual("Detail du calcul en francais",
                    $"{FormatSeconds(20700m, FrenchFormatting)} s ÷ 300 s = 69 → 69 poses", frenchVm.ResultDetailText);
                CheckEqual("Nombre de poses", "69", frenchVm.PoseCountText);
            }

            Check("Fiche du plugin traduite en francais",
                frenchImports != null
                && frenchImports.Manifest.Descriptions.ShortDescription.Contains("plan par filtres", StringComparison.Ordinal)
                && frenchImports.Manifest.Descriptions.ShortDescription.Contains("poses", StringComparison.Ordinal),
                "les poses et le plan par filtres",
                frenchImports?.Manifest.Descriptions.ShortDescription);

            // La fiche francaise doit couvrir toutes les fonctions du panneau.
            if (frenchImports != null) {
                foreach (var rubrique in new[] { "FENÊTRE DE SÉANCE", "NOMBRE DE POSES", "UNE NUIT RÉALISTE", "CAMÉRA MONO ET FILTRES", "RÉGLAGES ET LANGUE" }) {
                    Check($"Fiche francaise : rubrique {rubrique}",
                        frenchImports.Manifest.Descriptions.LongDescription.Contains(rubrique, StringComparison.Ordinal),
                        rubrique, frenchImports.Manifest.Descriptions.LongDescription);
                }
            }

            // Reproduction de la liaison exacte de la page Options > Plugins de N.I.N.A. :
            // le DataContext du volet de detail est SelectedInstalledPlugin.Key, et le texte
            // affiche vient de Key.Descriptions.ShortDescription.
            if (frenchImports != null) {
                var sheetProbe = new TextBlock();
                sheetProbe.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Descriptions.ShortDescription"));
                sheetProbe.DataContext = frenchImports.Manifest;

                var sheetListProbe = new TextBlock();
                sheetListProbe.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Key.Descriptions.ShortDescription"));
                sheetListProbe.DataContext = new KeyValuePair<IPluginManifest, bool>(frenchImports.Manifest, true);

                var sheetHost = new Grid { Width = 700, Height = 80 };
                sheetHost.Children.Add(sheetProbe);
                sheetHost.Children.Add(sheetListProbe);
                sheetHost.Measure(new Size(700, 80));
                sheetHost.Arrange(new Rect(0, 0, 700, 80));
                sheetHost.UpdateLayout();

                Check("Fiche du plugin, liaison WPF directe en francais",
                    sheetProbe.Text.Contains("plan par filtres", StringComparison.Ordinal),
                    "mentionne le plan par filtres", sheetProbe.Text);
                Check("Liste des plugins (DataContext Key) en francais",
                    sheetListProbe.Text.Contains("plan par filtres", StringComparison.Ordinal),
                    "mentionne le plan par filtres", sheetListProbe.Text);
            }
            Check("Description longue en francais",
                frenchImports != null && frenchImports.Manifest.Descriptions.LongDescription.StartsWith("Exposure Budget calcule", StringComparison.Ordinal),
                "commence par \"Exposure Budget calcule\"",
                frenchImports?.Manifest.Descriptions.LongDescription);
            CheckEqual("Nom du plugin (stable : N.I.N.A. en derive le dossier d'installation)", "Exposure Budget", frenchImports?.Manifest.Name);

            Section("18. Reglage par defaut \"auto\" : suivi de la langue de N.I.N.A.");
            WriteSettings(settingsPath, "auto");

            var autoImports = ComposePluginExports(pluginAssembly);
            var autoVm = autoImports?.Dockables.OfType<ExposureBudgetVM>().SingleOrDefault();
            Check("Panneau instancie avec language=auto", autoVm != null);
            if (autoVm != null) {
                // Le faux service de profil ne fournit pas de profil : le plugin se replie sur
                // la culture d'interface du fil courant, qui est le francais dans ce harnais.
                CheckEqual("auto suit la culture francaise de l'interface", "fr", autoVm.Labels.Language);
                CheckEqual("Libelle francais", "Début", autoVm.Labels.StartLabel);

                // N.I.N.A. signale le changement de langue (Options > Langue) : le panneau
                // doit se retraduire immediatement.
                CultureInfo.CurrentUICulture = EnglishFormatting;
                CultureInfo.CurrentCulture = EnglishFormatting;
                autoImports.ProfileService.SimulateLocaleChanged();

                CheckEqual("Retraduction immediate sans redemarrage", "en", autoVm.Labels.Language);
                CheckEqual("Libelle anglais apres changement", "Start", autoVm.Labels.StartLabel);
                CheckEqual("Formatage des nombres aligne sur la langue",
                    FormatSeconds(20700m, EnglishFormatting) + " s", autoVm.WindowSecondsText);
                Check("Le reglage reste \"auto\" apres retraduction",
                    File.ReadAllText(settingsPath).Contains("language=auto", StringComparison.Ordinal),
                    "language=auto", File.ReadAllText(settingsPath));
            }

            Section("19. Confort : bouton Maintenant et copie du resultat");
            // Bouton « Maintenant » : l'heure de debut doit devenir l'heure courante
            // (on tolere le passage de minute entre l'appel et la verification).
            var beforeNow = DateTime.Now;
            vm.SetStartToNowCommand.Execute(null);
            var afterNow = DateTime.Now;
            var startIsNow = (vm.StartHour == beforeNow.Hour && vm.StartMinute == beforeNow.Minute)
                          || (vm.StartHour == afterNow.Hour && vm.StartMinute == afterNow.Minute);
            Check("Bouton Maintenant : heure de debut = heure courante", startIsNow,
                $"{beforeNow:HH:mm} ou {afterNow:HH:mm}", $"{vm.StartHour:00}:{vm.StartMinute:00}");

            // Copie du resultat (fenetre 21:30 -> 03:15, pose de 300 s : 69 poses).
            vm.StartHour = 21;
            vm.StartMinute = 30;
            vm.EndHour = 3;
            vm.EndMinute = 15;
            vm.ExposureSeconds = 300d;

            // Le bouton ne depose que le nombre de poses dans le presse-papiers.
            CheckEqual("Presse-papiers : uniquement le nombre de poses", "69", vm.ClipboardText);
            Check("Presse-papiers : ni date ni detail",
                !vm.ClipboardText.Contains(vm.StartDateTimeText, StringComparison.Ordinal)
                && !vm.ClipboardText.Contains(Environment.NewLine, StringComparison.Ordinal),
                "le seul nombre", vm.ClipboardText);

            vm.CopyResultCommand.Execute(null);
            Check("Bouton Copier : confirmation affichee", vm.HasCopyFeedback, "un message", vm.CopyFeedbackText);
            Console.WriteLine($"        -> presse-papiers : {(vm.CopyFeedbackText == vm.Labels.CopyFeedback ? "copie reussie" : vm.CopyFeedbackText)}");

            // Le calcul suivant efface la confirmation et met le nombre a jour.
            vm.ExposureSeconds = 600d;
            Check("Confirmation de copie effacee au calcul suivant", !vm.HasCopyFeedback, "effacée", vm.CopyFeedbackText);
            CheckEqual("Presse-papiers : suit le calcul (600 s -> 35 poses)", "35", vm.ClipboardText);
            vm.ExposureSeconds = 300d;

            Section("20. Libelles du confort en francais");
            CheckEqual("Bouton Maintenant en francais", "Maintenant", frenchImports?.Dockables.OfType<ExposureBudgetVM>().SingleOrDefault()?.Labels.NowButtonText);
            CheckEqual("Bouton Copier en francais", "Copier le résultat", frenchImports?.Dockables.OfType<ExposureBudgetVM>().SingleOrDefault()?.Labels.CopyButtonText);

            Section("21. Plan par filtres : calcul et validations");

            // Fenetre de 4 h (21:30 -> 01:30) : c'est l'exemple documente dans afaire.md.
            vm.StartHour = 21;
            vm.StartMinute = 30;
            vm.EndHour = 1;
            vm.EndMinute = 30;

            CheckEqual("Sept filtres par defaut", 7, vm.FilterPlan.Count);
            CheckEqual("Noms des filtres par defaut", "L,R,G,B,S,H,O", string.Join(",", vm.FilterPlan.Select(r => r.Name)));
            CheckEqual("L a 40 % de 4 h avec 120 s -> 48 poses", "48", vm.FilterPlan[0].CountText);
            CheckEqual("H a 7 % de 4 h avec 300 s -> 3 poses", "3", vm.FilterPlan[5].CountText);
            Check("Totaux du plan affiches (100 % et 108 poses)",
                vm.FilterPlanTotalsText.Contains("100", StringComparison.Ordinal)
                && vm.FilterPlanTotalsText.Contains("108", StringComparison.Ordinal),
                "100 % et 108 poses", vm.FilterPlanTotalsText);
            Check("Plan par defaut sans erreur", !vm.HasFilterPlanError, "aucune erreur", vm.FilterPlanErrorText);
            Check("Reliquat affiche (6 min perdues par les arrondis)",
                vm.FilterPlanRemainingText.Contains("00:06:00", StringComparison.Ordinal),
                "*00:06:00*", vm.FilterPlanRemainingText);

            // Refus : les pourcentages depassent 100 %.
            vm.FilterPlan[0].Percent = 100;
            Check("Pourcentages > 100 % : plan refuse",
                vm.HasFilterPlanError && vm.FilterPlanErrorText.Contains("100", StringComparison.Ordinal),
                "un message de depassement", vm.FilterPlanErrorText);
            vm.FilterPlan[0].Percent = 40;

            // Refus : les poses arrondies depassent la fenetre. Ici 4 h / 9600 s = 1,5 -> 2 poses
            // (regle du 0,5), soit 5 h 20 de poses pour une fenetre de 4 h.
            foreach (var row in vm.FilterPlan.Skip(1).ToList()) { row.Enabled = false; }
            vm.FilterPlan[0].Percent = 100;
            vm.FilterPlan[0].ExposureSeconds = 9600d;
            Check("Poses arrondies au-dela de la fenetre : plan refuse",
                vm.HasFilterPlanError && vm.FilterPlanErrorText.Contains("exceed the window", StringComparison.Ordinal),
                "un message de depassement de fenetre", vm.FilterPlanErrorText);

            // Reinitialisation : retour aux valeurs par defaut, plus d'erreur.
            vm.ResetFilterPlanCommand.Execute(null);
            CheckEqual("Reinitialisation : 7 filtres", 7, vm.FilterPlan.Count);
            CheckEqual("Reinitialisation : L a 40 %", 40, vm.FilterPlan[0].Percent);
            Check("Reinitialisation : plus d'erreur", !vm.HasFilterPlanError, "aucune erreur", vm.FilterPlanErrorText);

            // Repartition automatique : 100 % au total, par pas de 5.
            vm.DistributeFiltersCommand.Execute(null);
            CheckEqual("Repartition : total de 100 %", 100, vm.FilterPlan.Sum(r => r.Percent));
            Check("Repartition : chaque part est un multiple de 5", vm.FilterPlan.All(r => r.Percent % 5 == 0),
                "multiples de 5", string.Join(",", vm.FilterPlan.Select(r => r.Percent)));

            // Tout cocher.
            vm.FilterPlan[3].Enabled = false;
            vm.CheckAllFiltersCommand.Execute(null);
            Check("Tout cocher : tous les filtres actifs", vm.FilterPlan.All(r => r.Enabled), "tous coches", "au moins un decoche");

            // Persistance du plan.
            Check("Plan memorise dans les reglages",
                File.ReadAllText(settingsPath).Contains("plan.filter.", StringComparison.Ordinal),
                "une ligne plan.filter.", File.ReadAllText(settingsPath));

            Section("22. Plan par filtres : onglet et tableau");
            var tabControl = FindVisualChildren<TabControl>(host).FirstOrDefault();

            // Chaque bouton du panneau doit expliquer ce qu'il fait.
            void CheckTooltip(string label, string description) {
                var buttons = FindVisualChildren<Button>(host)
                    .Where(b => b.Content is string text && text == label)
                    .ToList();

                Check($"Info-bulle : {description}",
                    buttons.Count > 0 && buttons.All(b => b.ToolTip is string tip && tip.Trim().Length > 0),
                    "un bouton avec une info-bulle",
                    buttons.Count == 0 ? "<bouton absent>" : string.Join(" | ", buttons.Select(b => b.ToolTip as string ?? "<vide>")));
            }

            CheckTooltip(vm.Labels.NowButtonText, "bouton Now");
            CheckTooltip(vm.Labels.NightButtonText, "bouton Nuit astronomique");
            CheckTooltip(vm.Labels.CopyButtonText, "bouton Copier le resultat");

            // Le bouton « nuit astronomique » definit la fenetre (le delta d'heures) : il doit
            // rester dans le bloc fenetre, hors des onglets de calcul.
            var nightButton = FindVisualChildren<Button>(host)
                .FirstOrDefault(b => b.Content is string text && text == vm.Labels.NightButtonText);
            var insideTab = false;
            for (var parent = nightButton == null ? null : VisualTreeHelper.GetParent(nightButton);
                 parent != null;
                 parent = VisualTreeHelper.GetParent(parent)) {
                if (parent is TabControl) { insideTab = true; break; }
            }
            Check("Bouton nuit astronomique hors des onglets", nightButton != null && !insideTab,
                "un bouton dans le bloc fenetre",
                nightButton == null ? "<absent>" : (insideTab ? "dans un onglet" : "hors des onglets"));

            // Le bouton doit etre SOUS les informations de la fenetre (et non par-dessus) et
            // occuper la pleine largeur du bloc.
            if (nightButton != null) {
                var windowGroup = FindVisualChildren<GroupBox>(host).FirstOrDefault();
                var sessionLine = FindVisualChildren<System.Windows.Controls.StackPanel>(host)
                    .FirstOrDefault(p => p.ToolTip is string tip && tip == vm.Labels.SessionTooltip);

                if (windowGroup != null && sessionLine != null) {
                    var buttonBounds = nightButton.TransformToAncestor(windowGroup)
                        .TransformBounds(new Rect(0, 0, nightButton.ActualWidth, nightButton.ActualHeight));
                    var sessionBounds = sessionLine.TransformToAncestor(windowGroup)
                        .TransformBounds(new Rect(0, 0, sessionLine.ActualWidth, sessionLine.ActualHeight));

                    var overlap = Rect.Intersect(buttonBounds, sessionBounds);
                    Check("Bouton nuit sous les informations de la fenetre (aucun chevauchement)",
                        overlap.IsEmpty || overlap.Height <= 1,
                        "deux zones distinctes",
                        $"bouton y={buttonBounds.Y:0} h={buttonBounds.Height:0} | seance y={sessionBounds.Y:0} h={sessionBounds.Height:0}");

                    Check("Bouton nuit en pleine largeur",
                        buttonBounds.Width > 0 && Math.Abs(buttonBounds.Width - sessionBounds.Width) <= 12,
                        "meme largeur que la ligne de seance",
                        $"bouton {buttonBounds.Width:0} px / seance {sessionBounds.Width:0} px");
                }
            }
            Check("Le panneau est organise en trois onglets", tabControl != null && tabControl.Items.Count == 3,
                "3 onglets", tabControl == null ? "<aucun>" : tabControl.Items.Count.ToString());

            if (tabControl != null) {
                tabControl.SelectedIndex = 1;
                host.UpdateLayout();

                CheckTooltip(vm.Labels.FilterCheckAllButton, "bouton Tout cocher");
                CheckTooltip(vm.Labels.FilterDistributeButton, "bouton Repartir 100 %");
                CheckTooltip(vm.Labels.FilterResetButton, "bouton Reinitialiser");
                CheckTooltip(vm.Labels.FilterAddButton, "bouton + filtre");
                CheckTooltip(vm.Labels.FilterRemoveButton, "bouton - filtre");
                CheckTooltip(vm.Labels.FilterPlanCopyButton, "bouton Copier le plan");
                CheckTooltip(vm.Labels.FilterRemainderButton, "boutons de reliquat (une par filtre)");

                // On restreint la recherche au contenu de l'onglet (le premier onglet peut rester
                // dans l'arbre visuel selon le cycle de disposition).
                var planContent = (tabControl.Items[1] as TabItem)?.Content as FrameworkElement;
                Check("Contenu de l'onglet plan disponible", planContent != null, "un contenu", planContent == null ? "<aucun>" : planContent.GetType().Name);

                if (planContent != null) {
                    CheckEqual("Onglet plan : sept lignes de temps de pose", 7, FindVisualChildren<PrecisionStepperControl>(planContent).Count());
                    CheckEqual("Onglet plan : sept cases a cocher", 7, FindVisualChildren<CheckBox>(planContent).Count());
                    CheckEqual("Onglet plan : sept ascenseurs de pourcentage", 7, FindVisualChildren<IntStepperControl>(planContent).Count());

                    // Les noms des filtres doivent etre visibles (defaut constate : le style
                    // implicite de N.I.N.A. est un interrupteur qui n'affiche pas son contenu).
                    var planTexts = CollectTexts(planContent);
                    Check("Onglet plan : les noms des filtres sont affiches",
                        vm.FilterPlan.All(r => planTexts.Contains(r.Name)),
                        "L, R, G, B, S, H, O", string.Join(" | ", planTexts.Take(20)));

                    // Cases a cocher compactes : le style implicite de N.I.N.A. en fait 65 px de large.
                    var planCheckBoxes = FindVisualChildren<CheckBox>(planContent).ToList();
                    Check("Onglet plan : cases a cocher compactes",
                        planCheckBoxes.Count == 7 && planCheckBoxes.All(c => c.ActualWidth > 0 && c.ActualWidth <= 26),
                        "20 px de large",
                        string.Join(",", planCheckBoxes.Select(c => c.ActualWidth.ToString("0"))));

                    // Pas de 5 s pour les temps de pose du plan (l'onglet principal garde 15 s).
                    CheckEqual("Plan : pas de 5 s sur le temps de pose", 5d, vm.FilterPlan[0].ExposureStepSeconds);
                    var planPoseSteppers = FindVisualChildren<PrecisionStepperControl>(planContent).ToList();
                    Check("Onglet plan : les ascenseurs de pose utilisent le pas de 5 s",
                        planPoseSteppers.Count == 7 && planPoseSteppers.All(s => Math.Abs(s.StepSize - 5d) < 1e-9),
                        "pas de 5 s", string.Join(",", planPoseSteppers.Select(s => s.StepSize.ToString("0"))));

                    var planItems = FindVisualChildren<ItemsControl>(planContent).FirstOrDefault();
                    Check("Onglet plan : tableau des sept filtres", planItems != null && planItems.Items.Count == 7,
                        "7 lignes", planItems == null ? "<aucun tableau>" : planItems.Items.Count.ToString());

                    // Les elements d'une ligne ne doivent pas se chevaucher : la case a cocher de
                    // N.I.N.A. fait 65 px de large (style StandardCheckBox) et contient le nom du
                    // filtre — c'est ce defaut qui faisait superposer la case et le nom.
                    var rowGrid = FindVisualChildren<Grid>(planContent).FirstOrDefault(g => g.Children.OfType<CheckBox>().Any());
                    Check("Onglet plan : une ligne de filtre est construite", rowGrid != null, "une ligne", rowGrid == null ? "<aucune>" : "trouvee");

                    if (rowGrid != null) {
                        var bounds = new List<(string Name, Rect Bounds)>();
                        foreach (UIElement child in rowGrid.Children) {
                            try {
                                var transform = child.TransformToAncestor(rowGrid);
                                var rect = transform.TransformBounds(new Rect(0, 0, child.RenderSize.Width, child.RenderSize.Height));
                                bounds.Add((child.GetType().Name, rect));
                            } catch {
                                // element non encore realise : ignore
                            }
                        }

                        var overlapping = new List<string>();
                        for (var i = 0; i < bounds.Count; i++) {
                            for (var j = i + 1; j < bounds.Count; j++) {
                                var intersection = Rect.Intersect(bounds[i].Bounds, bounds[j].Bounds);
                                if (!intersection.IsEmpty && intersection.Width > 1 && intersection.Height > 1) {
                                    overlapping.Add($"{bounds[i].Name}/{bounds[j].Name}");
                                }
                            }
                        }

                        Check("Onglet plan : aucun chevauchement dans une ligne de filtre",
                            bounds.Count >= 4 && overlapping.Count == 0,
                            "4 elements mesures et aucun chevauchement",
                            string.Join(" | ", bounds.Select(x => $"{x.Name} x={x.Bounds.X:0} w={x.Bounds.Width:0}")));
                    }
                }
            }

            Section("23. Plan par filtres : reliquat, copie et gestion de la liste");

            // Etat connu : les sept filtres par defaut (100 %), puis 3 points laisses libres.
            vm.ResetFilterPlanCommand.Execute(null);
            vm.FilterPlan[6].Percent = 0;
            CheckEqual("Pourcentages non utilises", 3, 100 - vm.FilterPlan.Sum(r => r.Percent));
            Check("Le bouton de reliquat est actif sur les filtres eligibles",
                vm.FilterPlan[0].CanReceiveRemainder, "actif", "inactif");

            vm.FilterPlan[0].ReceiveRemainderCommand.Execute(null);
            CheckEqual("Reliquat attribue : total de 100 %", 100, vm.FilterPlan.Sum(r => r.Percent));
            Check("Plus de reliquat a attribuer", !vm.FilterPlan[0].CanReceiveRemainder, "inactif", "actif");

            // Copie du plan : une ligne par filtre actif, puis les totaux.
            var planText = vm.FilterPlanClipboardText;
            Check("Plan copie : une ligne par filtre actif",
                planText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Length >= 7,
                "7 lignes ou plus", planText);
            Check("Plan copie : filtre, poses et duree",
                planText.Contains(" : ", StringComparison.Ordinal) && planText.Contains("×", StringComparison.Ordinal),
                "*L : N × T s (duree)*", planText);
            Check("Plan valide : copie autorisee", vm.CanCopyFilterPlan, "autorisee", "refusee");

            vm.CopyFilterPlanCommand.Execute(null);
            Check("Bouton Copier le plan : retour affiche", vm.HasFilterPlanStatus, "un message", vm.FilterPlanStatusText);

            // Renommage d'un filtre qui participe au plan (le nom apparait alors dans la copie).
            vm.FilterPlan[3].Name = "Ha";
            CheckEqual("Renommage d'un filtre", "Ha", vm.FilterPlan[3].Name);
            Check("Le nom renomme apparait dans le plan copie",
                vm.FilterPlanClipboardText.Contains("Ha", StringComparison.Ordinal), "*Ha*", vm.FilterPlanClipboardText);

            // Ajout et suppression d'une ligne.
            var initialRows = vm.FilterPlan.Count;
            vm.AddFilterCommand.Execute(null);
            CheckEqual("Ajout d'un filtre", initialRows + 1, vm.FilterPlan.Count);
            CheckEqual("Le filtre ajoute part a 0 %", 0, vm.FilterPlan[initialRows].Percent);

            vm.RemoveLastFilterCommand.Execute(null);
            CheckEqual("Suppression du dernier filtre", initialRows, vm.FilterPlan.Count);

            // Un plan refuse n'est pas proposé à la copie.
            vm.FilterPlan[0].Percent = 100;
            Check("Plan refuse : copie desactivee", !vm.CanCopyFilterPlan, "refusee", "autorisee");
            vm.ResetFilterPlanCommand.Execute(null);

            Section("24. Surcharge par pose et temps a reserver");

            // Etat connu : 21:30 -> 03:15 (5 h 45), pose de 300 s, sans surcharge ni reserve.
            vm.StartHour = 21;
            vm.StartMinute = 30;
            vm.EndHour = 3;
            vm.EndMinute = 15;
            vm.ExposureSeconds = 300d;
            vm.ExposureOverheadSeconds = 0d;
            vm.FlipReserveMinutes = 0d;
            vm.FixedReserveMinutes = 0d;
            vm.AutofocusEvery = 0;
            vm.AutofocusDurationSeconds = 0d;

            CheckEqual("Sans surcharge : 69 poses", "69", vm.PoseCountText);
            Check("Aucune surcharge : pas d'indication", !vm.HasOverhead, "aucune", vm.OverheadInfoText);

            vm.ExposureOverheadSeconds = 5d;
            CheckEqual("Surcharge de 5 s : 68 poses", "68", vm.PoseCountText);
            Check("Surcharge : indication du cout par pose", vm.HasOverhead && !string.IsNullOrEmpty(vm.OverheadInfoText),
                "une indication", vm.OverheadInfoText);
            Check("Surcharge : le detail du calcul la mentionne",
                vm.ResultDetailText.Contains("overhead", StringComparison.Ordinal)
                || vm.ResultDetailText.Contains("surcharge", StringComparison.Ordinal),
                "*overhead*", vm.ResultDetailText);
            Check("Surcharge : rappel dans le plan", vm.HasPlanOverhead && !string.IsNullOrEmpty(vm.PlanOverheadHintText),
                "un rappel", vm.PlanOverheadHintText);

            // Le plan par filtres tient compte de la meme surcharge.
            var planWithOverhead = vm.FilterPlanClipboardText;
            vm.ExposureOverheadSeconds = 0d;
            var planWithoutOverhead = vm.FilterPlanClipboardText;
            Check("Plan : la surcharge change le nombre de poses", planWithOverhead != planWithoutOverhead,
                "un plan different", planWithoutOverhead);

            // Reserves : retournement de 10 min et marge de 5 min.
            vm.FlipReserveMinutes = 10d;
            vm.FixedReserveMinutes = 5d;
            Check("Reserves : signalees", vm.HasReserves, "des reserves", "aucune");
            CheckEqual("Reserves de 15 min : 66 poses", "66", vm.PoseCountText);
            Check("Reserves : resume affiche", !string.IsNullOrEmpty(vm.ReservesSummaryText), "un resume", vm.ReservesSummaryText);

            // Autofocus toutes les 10 poses : le nombre passe a 65 apres iteration.
            vm.AutofocusEvery = 10;
            vm.AutofocusDurationSeconds = 60d;
            CheckEqual("Autofocus : 65 poses", "65", vm.PoseCountText);

            // Reserves au-dela de la fenetre : avertissement.
            vm.FlipReserveMinutes = 400d;
            Check("Reserves > fenetre : avertissement", vm.HasWarning, "un avertissement", vm.WarningText);
            CheckEqual("Reserves > fenetre : 0 pose", "0", vm.PoseCountText);

            // Retour a l'etat sans reserve.
            vm.FlipReserveMinutes = 0d;
            vm.FixedReserveMinutes = 0d;
            vm.AutofocusEvery = 0;
            vm.AutofocusDurationSeconds = 0d;
            CheckEqual("Sans reserve : 69 poses", "69", vm.PoseCountText);

            // Onglet des reserves : les quatre reglages sont rendus.
            var reservesTab = tabControl != null && tabControl.Items.Count > 2 ? tabControl.Items[2] as TabItem : null;
            var reservesContent = reservesTab?.Content as FrameworkElement;
            Check("Onglet des reserves present", reservesContent != null, "un contenu", reservesContent == null ? "<aucun>" : reservesContent.GetType().Name);

            if (reservesContent != null) {
                tabControl.SelectedIndex = 2;
                host.UpdateLayout();
                var reserveSteppers = FindVisualChildren<PrecisionStepperControl>(reservesContent).Count()
                                    + FindVisualChildren<IntStepperControl>(reservesContent).Count();
                CheckEqual("Onglet des reserves : quatre reglages", 4, reserveSteppers);
            }

            if (settingsBackup != null) { File.WriteAllText(settingsPath, settingsBackup); } else { File.Delete(settingsPath); }

            return Report();
        }

        /// <summary>Ecrit un fichier de reglages deterministe avant une composition.</summary>
        private static void WriteSettings(string path, string language) {
            File.WriteAllLines(path, new[] {
                "settingsVersion=1",
                "language=" + language,
                "startHour=21",
                "startMinute=30",
                "endHour=3",
                "endMinute=15",
                "exposureSeconds=300"
            });
        }

        private static StubNighttimeCalculator GetCalculator(PluginImports imports) => imports.NighttimeCalculator;

        private static PluginImports ComposePluginExports(Assembly pluginAssembly) {
            try {
                var catalog = new AssemblyCatalog(pluginAssembly);
                var container = new CompositionContainer(catalog, CompositionOptions.DisableSilentRejection | CompositionOptions.IsThreadSafe);

                var nighttimeCalculator = new StubNighttimeCalculator();
                var profileService = new StubProfileService();

                container.ComposeExportedValue<IProfileService>(profileService);
                container.ComposeExportedValue<INighttimeCalculator>(nighttimeCalculator);

                var imports = new PluginImports {
                    NighttimeCalculator = nighttimeCalculator,
                    ProfileService = profileService
                };
                container.ComposeParts(imports);

                Console.WriteLine("Composition MEF reussie : aucun export manquant.");
                return imports;
            } catch (Exception ex) {
                failures++;
                Console.WriteLine($"[ECHEC] Composition MEF impossible : {ex.Message}");
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        private static List<string> CollectTexts(DependencyObject root) {
            var texts = new List<string>();

            foreach (var textBlock in FindVisualChildren<TextBlock>(root)) {
                if (!string.IsNullOrWhiteSpace(textBlock.Text)) { texts.Add(textBlock.Text); }
            }

            // Les champs de saisie portent aussi du texte visible (nom du filtre, valeurs).
            foreach (var textBox in FindVisualChildren<TextBox>(root)) {
                if (!string.IsNullOrWhiteSpace(textBox.Text)) { texts.Add(textBox.Text); }
            }

            foreach (var button in FindVisualChildren<Button>(root)) {
                if (button.Content is string content && !string.IsNullOrWhiteSpace(content)) { texts.Add(content); }
            }

            foreach (var groupBox in FindVisualChildren<GroupBox>(root)) {
                if (groupBox.Header is string header && !string.IsNullOrWhiteSpace(header)) { texts.Add(header); }
            }

            return texts;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject {
            if (root == null) { yield break; }
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++) {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) { yield return typed; }
                foreach (var descendant in FindVisualChildren<T>(child)) {
                    yield return descendant;
                }
            }
        }

        private static void Section(string title) {
            Console.WriteLine();
            Console.WriteLine(title);
        }

        private static int Report() {
            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? $"OK : {checks} verifications conformes."
                : $"ECHEC : {failures} anomalie(s) sur {checks} verifications.");
            return failures == 0 ? 0 : 1;
        }

        private static void Check(string label, bool condition, string expected = null, string actual = null) {
            checks++;
            if (condition) {
                Console.WriteLine($"  [ok]    {label}");
                return;
            }
            failures++;
            Console.WriteLine($"  [ECHEC] {label}" + (expected == null ? string.Empty : $" -- attendu {expected}, obtenu {actual ?? "<null>"}"));
        }

        private static void CheckText(IEnumerable<string> texts, string expected) {
            var list = texts.ToList();
            Check($"Libelle affiche : \"{expected}\"", list.Contains(expected), expected, string.Join(" | ", list));
        }

        private static void CheckEqual(string label, string expected, string actual) {
            Check(label, string.Equals(expected, actual, StringComparison.Ordinal), expected, actual);
        }

        private static void CheckEqual(string label, int expected, int actual) {
            Check(label, expected == actual, expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture));
        }

        private static void CheckEqual(string label, double expected, double actual) {
            Check(label, Math.Abs(expected - actual) < 1e-9, expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture));
        }

        private static void CheckEqual(string label, Type expected, Type actual) {
            Check(label, expected == actual, expected?.FullName ?? "null", actual?.FullName ?? "null");
        }

        private static void CheckEqual(string label, (double Min, double Max) expected, (double Min, double Max) actual) {
            Check(label,
                Math.Abs(expected.Min - actual.Min) < 1e-9 && Math.Abs(expected.Max - actual.Max) < 1e-9,
                $"({expected.Min} ; {expected.Max})",
                $"({actual.Min} ; {actual.Max})");
        }

        private static void CheckEqual(string label, (int Min, int Max) expected, (int Min, int Max) actual) {
            Check(label, expected == actual, $"({expected.Min} ; {expected.Max})", $"({actual.Min} ; {actual.Max})");
        }

        private sealed class PluginImports {

            [Import(typeof(IPluginManifest))]
            public IPluginManifest Manifest { get; set; }

            [ImportMany(typeof(ResourceDictionary))]
            public IEnumerable<ResourceDictionary> Dictionaries { get; set; }

            [ImportMany(typeof(IDockableVM))]
            public IEnumerable<IDockableVM> Dockables { get; set; }

            /// <summary>Renseignes par le harnais ; ce ne sont pas des imports MEF.</summary>
            public StubNighttimeCalculator NighttimeCalculator { get; set; }

            public StubProfileService ProfileService { get; set; }
        }
    }
}
