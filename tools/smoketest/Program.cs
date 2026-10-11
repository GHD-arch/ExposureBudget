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
            CheckEqual("Version", "1.2.1.0", manifest.Version.ToString());
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
            CheckEqual("Fiche du plugin, description courte en anglais",
                "Computes how many exposures of a given duration fit between a start time and an end time, taking the midnight rollover into account.",
                manifest.Descriptions.ShortDescription);
            Check("Fiche du plugin, description longue en anglais",
                manifest.Descriptions.LongDescription.StartsWith("Exposure Budget works out", StringComparison.Ordinal),
                "commence par \"Exposure Budget works out\"", manifest.Descriptions.LongDescription);
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
            CheckEqual("Information de nuit astronomique", "Astronomical night on 10/10/2026: 19:42 → 05:32 on 11/10/2026.", vm.NightInfoText);
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
            Check("Repli signale dans l'information", vm.NightInfoText.StartsWith("Nautical night", StringComparison.Ordinal), "*Nautical night*", vm.NightInfoText);

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
                && frenchImports.Manifest.Descriptions.ShortDescription.StartsWith("Calcule combien de poses", StringComparison.Ordinal),
                "commence par \"Calcule combien de poses\"",
                frenchImports?.Manifest.Descriptions.ShortDescription);

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
                    sheetProbe.Text.StartsWith("Calcule combien de poses", StringComparison.Ordinal),
                    "commence par \"Calcule combien de poses\"", sheetProbe.Text);
                Check("Liste des plugins (DataContext Key) en francais",
                    sheetListProbe.Text.StartsWith("Calcule combien de poses", StringComparison.Ordinal),
                    "commence par \"Calcule combien de poses\"", sheetListProbe.Text);
            }
            Check("Description longue en francais",
                frenchImports != null && frenchImports.Manifest.Descriptions.LongDescription.StartsWith("Exposure Budget détermine", StringComparison.Ordinal),
                "commence par \"Exposure Budget détermine\"",
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
