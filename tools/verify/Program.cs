using NINA.Plugin.ExposureBudget.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NINA.Plugin.ExposureBudget.Verify {

    /// <summary>
    /// Verification de la logique de calcul du plugin : fenetre entre Hd et Hf avec
    /// passage de minuit, regle d'arrondi du 0,5, cas limites.
    /// Code de sortie 0 si tout est conforme, 1 sinon.
    /// </summary>
    internal static class Program {

        private static int failures;
        private static int checks;

        private static int Main() {
            Console.WriteLine("== Exposure Budget : verification de la logique de calcul ==");
            Console.WriteLine();

            Section("1. Duree disponible entre Hd et Hf");
            CheckWindow("Exemple de la demande : 21:30 -> 03:15 (le lendemain)", 21, 30, 3, 15, TimeSpan.FromMinutes(345));
            CheckWindow("Session entierement le soir : 19:00 -> 23:00", 19, 0, 23, 0, TimeSpan.FromHours(4));
            CheckWindow("Passage de minuit sur une minute : 23:59 -> 00:01", 23, 59, 0, 1, TimeSpan.FromMinutes(2));
            CheckWindow("Nuit complete : 18:00 -> 06:00", 18, 0, 6, 0, TimeSpan.FromHours(12));
            CheckWindow("Nuit longue : 16:00 -> 08:00", 16, 0, 8, 0, TimeSpan.FromHours(16));
            CheckWindow("Heures identiques : 21:00 -> 21:00 (fenetre nulle)", 21, 0, 21, 0, TimeSpan.Zero);
            CheckWindow("Une minute : 22:00 -> 22:01", 22, 0, 22, 1, TimeSpan.FromMinutes(1));
            CheckWindow("Fin juste avant minuit : 21:00 -> 23:59", 21, 0, 23, 59, TimeSpan.FromMinutes(179));

            Section("2. Regle d'arrondi demandee : partie decimale < 0,5 -> pose inferieure, sinon une pose de plus");
            CheckRound("2,4 -> 2", 2.4m, 2);
            CheckRound("2,499 -> 2", 2.499m, 2);
            CheckRound("2,5 -> 3 (0,5 atteint : une pose en plus)", 2.5m, 3);
            CheckRound("2,6 -> 3", 2.6m, 3);
            CheckRound("0,49 -> 0", 0.49m, 0);
            CheckRound("0,5 -> 1", 0.5m, 1);
            CheckRound("69 -> 69 (aucun arrondi)", 69m, 69);
            CheckRound("0 -> 0", 0m, 0);
            CheckRound("175,5 -> 176", 175.5m, 176);

            Section("3. Exemple de la demande : 21:30 -> 03:15, soit 20 700 s disponibles");
            CheckCount("300 s (5 min) : 69,00 -> 69 poses, aucun reliquat", 21, 30, 3, 15, 300d, 69, 20700m, 0m);
            CheckCount("600 s (10 min) : 34,5 -> 35 poses, depassement de 300 s", 21, 30, 3, 15, 600d, 35, 21000m, -300m);
            CheckCount("900 s (15 min) : 23,00 -> 23 poses", 21, 30, 3, 15, 900d, 23, 20700m, 0m);
            CheckCount("1200 s (20 min) : 17,25 -> 17 poses", 21, 30, 3, 15, 1200d, 17, 20400m, 300m);
            CheckCount("1350 s (22 min 30 s) : 15,333 -> 15 poses", 21, 30, 3, 15, 1350d, 15, 20250m, 450m);
            CheckCount("1380 s (23 min) : 15,00 -> 15 poses", 21, 30, 3, 15, 1380d, 15, 20700m, 0m);
            CheckCount("0,5 s : 41 400 poses couvrant exactement la fenetre", 21, 30, 3, 15, 0.5d, 41400, 20700m, 0m);

            Section("4. Fenetre 21:30 -> 03:21, soit 21 060 s (cas x,5 et x,2)");
            CheckCount("300 s : 70,2 -> 70 poses, reliquat de 60 s", 21, 30, 3, 21, 300d, 70, 21000m, 60m);
            CheckCount("120 s : 175,5 -> 176 poses, depassement de 60 s", 21, 30, 3, 21, 120d, 176, 21120m, -60m);
            CheckCount("1170 s : 18,00 -> 18 poses", 21, 30, 3, 21, 1170d, 18, 21060m, 0m);

            Section("5. Autres cas limites");
            CheckCount("Fenetre de 120 s / 300 s = 0,4 -> aucune pose", 22, 0, 22, 2, 300d, 0, 0m, 120m);
            CheckCount("Fenetre de 180 s / 360 s = 0,5 -> 1 pose, depassement de 180 s", 22, 0, 22, 3, 360d, 1, 360m, -180m);
            CheckCount("Nuit de 16 h / 900 s = 64,00 -> 64 poses", 16, 0, 8, 0, 900d, 64, 57600m, 0m);
            CheckCount("Nuit de 16 h / 950 s = 60,63 -> 61 poses", 16, 0, 8, 0, 950d, 61, 57950m, -350m);
            CheckCount("Nuit de 12 h / 300 s = 144 poses", 18, 0, 6, 0, 300d, 144, 43200m, 0m);
            CheckCount("Deux heures (7 200 s) / 1 600 s : 4,5 -> 5 poses", 22, 0, 0, 0, 1600d, 5, 8000m, -800m);

            Section("6. Indicateurs de resultat");
            var exact = ExposureBudgetCalculator.Compute(TimeSpan.FromMinutes(345), 300d);
            CheckEqual("69 x 300 s ne depasse pas la fenetre", false, exact.Overflows);
            CheckEqual("Fenetre non vide", false, exact.EmptyWindow);
            CheckEqual("Nombre exact = 69", 69m, exact.ExactCount);
            CheckEqual("Pas d'arrondi vers le haut", false, exact.RoundedUp);
            CheckEqual("Pas d'arrondi vers le bas", false, exact.RoundedDown);
            CheckEqual("Reliquat nul", 0m, exact.RemainingSeconds);

            var up = ExposureBudgetCalculator.Compute(TimeSpan.FromMinutes(345), 600d);
            CheckEqual("35 x 600 s depasse la fenetre", true, up.Overflows);
            CheckEqual("Reliquat negatif de 300 s", -300m, up.RemainingSeconds);
            CheckEqual("Arrondi vers le haut detecte (34,5)", true, up.RoundedUp);
            CheckEqual("Partie decimale = 0,5", 0.5m, up.Fraction);
            CheckEqual("Nombre exact = 34,5", 34.5m, up.ExactCount);

            var down = ExposureBudgetCalculator.Compute(TimeSpan.FromMinutes(345), 1200d);
            CheckEqual("Arrondi vers le bas detecte (17,25)", true, down.RoundedDown);
            CheckEqual("Partie decimale = 0,25", 0.25m, down.Fraction);

            var empty = ExposureBudgetCalculator.Compute(TimeSpan.Zero, 300d);
            CheckEqual("Fenetre nulle signalee", true, empty.EmptyWindow);
            CheckEqual("Aucune pose pour une fenetre nulle", 0, empty.Count);

            Section("7. Bornes et saisie");
            CheckEqual("Pas de l'ascenseur = 15 s", 15, ExposureBudgetCalculator.ExposureStepSeconds);
            CheckEqual("300 s reste 300 s", 300d, ExposureBudgetCalculator.SnapToExposureStep(300d));
            CheckEqual("301 s est ramene a 300 s", 300d, ExposureBudgetCalculator.SnapToExposureStep(301d));
            CheckEqual("308 s est ramene a 315 s", 315d, ExposureBudgetCalculator.SnapToExposureStep(308d));
            CheckEqual("Heure bornee a 23", 23, ExposureBudgetCalculator.ClampHour(31));
            CheckEqual("Heure bornee a 0", 0, ExposureBudgetCalculator.ClampHour(-2));
            CheckEqual("Minute bornee a 59", 59, ExposureBudgetCalculator.ClampMinute(90));
            CheckEqual("Minute bornee a 0", 0, ExposureBudgetCalculator.ClampMinute(-5));

            Section("8. Temps de pose invalide");
            CheckThrows("Temps de pose 0 s refuse", 0d);
            CheckThrows("Temps de pose negatif refuse", -15d);
            CheckThrows("Temps de pose NaN refuse", double.NaN);
            CheckThrows("Temps de pose infini refuse", double.PositiveInfinity);

            Section("9. Plan par filtres : repartition, totaux et controles");
            var quatreHeures = TimeSpan.FromHours(4);
            var plan = new List<FilterPlanEntry> {
                new FilterPlanEntry("L", true, 120d, 40),
                new FilterPlanEntry("R", true, 120d, 15),
                new FilterPlanEntry("G", true, 120d, 15),
                new FilterPlanEntry("B", true, 120d, 15),
                new FilterPlanEntry("S", true, 300d, 5),
                new FilterPlanEntry("H", true, 300d, 7),
                new FilterPlanEntry("O", true, 300d, 3)
            };
            var resultat = FilterPlanCalculator.Compute(quatreHeures, plan);
            CheckEqual("Plan : L a 40 % de 4 h avec 120 s -> 48 poses", 48, resultat.Lines[0].Count);
            CheckEqual("Plan : R a 15 % avec 120 s -> 18 poses", 18, resultat.Lines[1].Count);
            CheckEqual("Plan : S a 5 % avec 300 s (2,40) -> 2 poses", 2, resultat.Lines[4].Count);
            CheckEqual("Plan : H a 7 % avec 300 s (3,36) -> 3 poses", 3, resultat.Lines[5].Count);
            CheckEqual("Plan : O a 3 % avec 300 s (1,44) -> 1 pose", 1, resultat.Lines[6].Count);
            CheckEqual("Plan : total des pourcentages", 100, resultat.TotalPercent);
            CheckEqual("Plan : total des poses", 108, resultat.TotalCount);
            CheckEqual("Plan : temps couvert", 14040d, resultat.TotalCovered.TotalSeconds);
            CheckEqual("Plan : reliquat de 6 min perdu par les arrondis", 360d, resultat.Remaining.TotalSeconds);
            CheckEqual("Plan : aucun depassement de pourcentage", false, resultat.PercentOverflow);
            CheckEqual("Plan : aucun depassement de fenetre", false, resultat.CoverageOverflow);
            CheckEqual("Plan : plan valide", true, resultat.IsValid);
            CheckEqual("Plan : aucun point de pourcentage inutilise", 0, resultat.UnusedPercent);

            // Un seul filtre a 100 % doit donner exactement le calcul simple : une seule regle.
            var fenetre = TimeSpan.FromMinutes(345);
            var seul = FilterPlanCalculator.Compute(fenetre, new[] { new FilterPlanEntry("L", true, 300d, 100) });
            var simple = ExposureBudgetCalculator.Compute(fenetre, 300d);
            CheckEqual("Plan : un filtre a 100 % donne le meme nombre que le calcul simple", simple.Count, seul.Lines[0].Count);
            CheckEqual("Plan : et le meme temps couvert", simple.CoveredSeconds, (decimal)seul.TotalCovered.TotalSeconds);

            // Pourcentages > 100 : refus, mais les nombres restent affiches.
            var depassementPourcent = FilterPlanCalculator.Compute(quatreHeures, new[] {
                new FilterPlanEntry("L", true, 120d, 60),
                new FilterPlanEntry("R", true, 120d, 60)
            });
            CheckEqual("Plan : 60 % + 60 % -> depassement de pourcentage", true, depassementPourcent.PercentOverflow);
            CheckEqual("Plan : plan invalide", false, depassementPourcent.IsValid);
            CheckEqual("Plan : poses tout de meme calculees", 2, depassementPourcent.Lines.Count(x => x.Count > 0));

            // Arrondi qui fait depasser la fenetre : refus avec le depassement chiffre.
            var depassementTemps = FilterPlanCalculator.Compute(TimeSpan.FromSeconds(1000), new[] {
                new FilterPlanEntry("L", true, 600d, 100)
            });
            CheckEqual("Plan : 1,667 pose arrondie a 2 -> depassement de fenetre", true, depassementTemps.CoverageOverflow);
            CheckEqual("Plan : depassement de 200 s", 200d, depassementTemps.Overrun.TotalSeconds);
            CheckEqual("Plan : depassement -> plan invalide", false, depassementTemps.IsValid);

            // Filtres decoches : exclus des totaux, affiches a zero.
            var partiel = FilterPlanCalculator.Compute(quatreHeures, new[] {
                new FilterPlanEntry("L", true, 120d, 50),
                new FilterPlanEntry("R", false, 120d, 50)
            });
            CheckEqual("Plan : seuls les filtres coches comptent", 50, partiel.TotalPercent);
            CheckEqual("Plan : un seul filtre actif", 1, partiel.EnabledCount);
            CheckEqual("Plan : filtre decoche -> 0 pose", 0, partiel.Lines[1].Count);
            CheckEqual("Plan : points de pourcentage inutilises", 50, partiel.UnusedPercent);

            var vide = FilterPlanCalculator.Compute(quatreHeures, new[] { new FilterPlanEntry("L", false, 120d, 40) });
            CheckEqual("Plan : aucun filtre actif signale", true, vide.IsEmpty);
            CheckEqual("Plan : aucun filtre actif -> invalide", false, vide.IsValid);

            var fenetreNulle = FilterPlanCalculator.Compute(TimeSpan.Zero, new[] { new FilterPlanEntry("L", true, 120d, 100) });
            CheckEqual("Plan : fenetre nulle -> 0 pose", 0, fenetreNulle.TotalCount);

            var poseNulle = FilterPlanCalculator.Compute(quatreHeures, new[] { new FilterPlanEntry("L", true, 0d, 100) });
            CheckEqual("Plan : temps de pose nul -> 0 pose", 0, poseNulle.TotalCount);
            CheckEqual("Plan : temps de pose nul signale", false, poseNulle.Lines[0].HasValidExposure);

            Section("11. Surcharge par pose et temps a reserver");
            var fenetre345 = TimeSpan.FromMinutes(345);   // 5 h 45 = 20 700 s
            var sansSurcharge = ExposureBudgetCalculator.Compute(fenetre345, 300d);
            var avecSurcharge = ExposureBudgetCalculator.Compute(fenetre345, 300d, 5d, null);
            CheckEqual("Surcharge : sans surcharge -> 69 poses", 69, sansSurcharge.Count);
            CheckEqual("Surcharge : 20 700 / (300 + 5) = 67,87 -> 68 poses", 68, avecSurcharge.Count);
            CheckEqual("Surcharge : cout par pose", 305m, avecSurcharge.PerPoseSeconds);
            CheckEqual("Surcharge : temps couvert (expositions seules)", 20400m, avecSurcharge.CoveredSeconds);
            CheckEqual("Surcharge : temps consomme", 20740m, avecSurcharge.ConsumedSeconds);
            CheckEqual("Surcharge : arrondi au-dessus -> depassement de 40 s", true, avecSurcharge.Overflows);

            var propre = ExposureBudgetCalculator.Compute(fenetre345, 250d, 2d, null);
            CheckEqual("Surcharge : 20 700 / 252 = 82,14 -> 82 poses", 82, propre.Count);
            CheckEqual("Surcharge : 82 x 252 = 20 664 consommees", 20664m, propre.ConsumedSeconds);
            CheckEqual("Surcharge : reliquat de 36 s", 36m, propre.RemainingSeconds);
            CheckEqual("Surcharge nulle = calcul d'origine", 69, ExposureBudgetCalculator.Compute(fenetre345, 300d, 0d, null).Count);

            var reserves = new NightReserves { FlipMinutes = 10d, FixedMinutes = 5d };
            var avecReserves = ExposureBudgetCalculator.Compute(fenetre345, 300d, 0d, reserves);
            CheckEqual("Reserves : 15 min reservees", 900d, avecReserves.ReservedSeconds.TotalSeconds);
            CheckEqual("Reserves : fenetre exploitable de 5 h 30", 19800d, avecReserves.UsableWindow.TotalSeconds);
            CheckEqual("Reserves : 19 800 / 300 = 66 poses", 66, avecReserves.Count);

            // Autofocus toutes les 10 poses : le nombre de passages depend du nombre de poses.
            var reservesAutofocus = new NightReserves { FlipMinutes = 10d, FixedMinutes = 5d, AutofocusEvery = 10, AutofocusSeconds = 60d };
            var avecAutofocus = ExposureBudgetCalculator.Compute(fenetre345, 300d, 0d, reservesAutofocus);
            CheckEqual("Autofocus : 65 poses apres iteration", 65, avecAutofocus.Count);
            CheckEqual("Autofocus : 6 passages -> 21 min reservees", 1260d, avecAutofocus.ReservedSeconds.TotalSeconds);
            CheckEqual("Autofocus : fenetre exploitable de 5 h 24", 19440d, avecAutofocus.UsableWindow.TotalSeconds);

            var reservesTrop = new NightReserves { FlipMinutes = 400d };
            var avecTrop = ExposureBudgetCalculator.Compute(fenetre345, 300d, 0d, reservesTrop);
            CheckEqual("Reserves > fenetre : 0 pose", 0, avecTrop.Count);
            CheckEqual("Reserves > fenetre signale", true, avecTrop.ReservesExceedWindow);

            var autofocusSeul = new NightReserves { AutofocusEvery = 10, AutofocusSeconds = 60d };
            CheckEqual("Autofocus : 25 poses -> 2 passages", 2, autofocusSeul.AutofocusCount(25));
            CheckEqual("Autofocus : total de 2 x 60 s", 120d, autofocusSeul.Total(25).TotalSeconds);
            CheckEqual("Aucune reserve par defaut", true, new NightReserves().IsEmpty);

            var planAvecSurcharge = FilterPlanCalculator.Compute(fenetre345,
                new[] { new FilterPlanEntry("L", true, 300d, 100) }, 5d);
            CheckEqual("Plan : surcharge -> 68 poses", 68, planAvecSurcharge.Lines[0].Count);
            CheckEqual("Plan : temps consomme avec surcharge", 20740d, planAvecSurcharge.TotalConsumed.TotalSeconds);
            CheckEqual("Plan : depassement detecte sur le temps consomme", true, planAvecSurcharge.CoverageOverflow);

            Section("12. Plan par filtres : repartition automatique");
            var aRepartir = new List<FilterPlanEntry> {
                new FilterPlanEntry("L", true, 120d, 0),
                new FilterPlanEntry("R", true, 120d, 0),
                new FilterPlanEntry("G", true, 120d, 0),
                new FilterPlanEntry("B", true, 120d, 0)
            };
            FilterPlanCalculator.DistributeEvenly(aRepartir);
            CheckEqual("Repartition : 4 filtres -> 25 % chacun", 100, aRepartir.Sum(x => x.Percent));
            CheckEqual("Repartition : chaque filtre a 25 %", 25, aRepartir[0].Percent);

            var sept = new List<FilterPlanEntry> {
                new FilterPlanEntry("L", true, 120d, 0), new FilterPlanEntry("R", true, 120d, 0),
                new FilterPlanEntry("G", true, 120d, 0), new FilterPlanEntry("B", true, 120d, 0),
                new FilterPlanEntry("S", true, 300d, 0), new FilterPlanEntry("H", true, 300d, 0),
                new FilterPlanEntry("O", true, 300d, 0)
            };
            FilterPlanCalculator.DistributeEvenly(sept);
            CheckEqual("Repartition : 7 filtres totalisent 100 %", 100, sept.Sum(x => x.Percent));
            CheckEqual("Repartition : les premiers filtres prennent le reliquat", 15, sept[0].Percent);
            CheckEqual("Repartition : le dernier recoit la part de base", 10, sept[6].Percent);

            var avecDecoches = new List<FilterPlanEntry> {
                new FilterPlanEntry("L", true, 120d, 40),
                new FilterPlanEntry("R", false, 120d, 30),
                new FilterPlanEntry("G", true, 120d, 0)
            };
            FilterPlanCalculator.DistributeEvenly(avecDecoches);
            CheckEqual("Repartition : filtres decoches remis a 0 %", 0, avecDecoches[1].Percent);
            CheckEqual("Repartition : 2 filtres actifs -> 50 % pour le premier", 50, avecDecoches[0].Percent);
            CheckEqual("Repartition : 2 filtres actifs -> 50 % pour le second", 50, avecDecoches[2].Percent);

            Console.WriteLine();
            Console.WriteLine(failures == 0
                ? $"OK : {checks} verifications conformes."
                : $"ECHEC : {failures} anomalie(s) sur {checks} verifications.");
            return failures == 0 ? 0 : 1;
        }

        private static void Section(string title) {
            Console.WriteLine();
            Console.WriteLine(title);
        }

        private static void CheckWindow(string label, int hd, int md, int hf, int mf, TimeSpan expected) {
            var actual = ExposureBudgetCalculator.GetWindow(hd, md, hf, mf);
            Check(label, expected, actual, FormatDuration(expected), FormatDuration(actual));
        }

        private static void CheckRound(string label, decimal exact, int expected) {
            var actual = ExposureBudgetCalculator.RoundToPoseCount(exact);
            Check(label, expected, actual,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture));
        }

        private static void CheckCount(string label, int hd, int md, int hf, int mf, double exposureSeconds, int expectedCount, decimal expectedCovered, decimal expectedRemaining) {
            var window = ExposureBudgetCalculator.GetWindow(hd, md, hf, mf);
            var result = ExposureBudgetCalculator.Compute(window, exposureSeconds);

            Check($"{label} : nombre de poses", expectedCount, result.Count,
                expectedCount.ToString(CultureInfo.InvariantCulture),
                result.Count.ToString(CultureInfo.InvariantCulture));
            Check($"{label} : temps couvert", expectedCovered, result.CoveredSeconds,
                FormatSeconds(expectedCovered), FormatSeconds(result.CoveredSeconds));
            Check($"{label} : reliquat", expectedRemaining, result.RemainingSeconds,
                FormatSeconds(expectedRemaining), FormatSeconds(result.RemainingSeconds));
        }

        private static void CheckThrows(string label, double exposureSeconds) {
            checks++;
            try {
                ExposureBudgetCalculator.Compute(TimeSpan.FromHours(1), exposureSeconds);
                failures++;
                Console.WriteLine($"  [ECHEC] {label} (aucune exception levee)");
            } catch (ArgumentOutOfRangeException) {
                Console.WriteLine($"  [ok]    {label}");
            }
        }

        private static void CheckEqual(string label, bool expected, bool actual) {
            Check(label, expected, actual, expected.ToString(), actual.ToString());
        }

        private static void CheckEqual(string label, int expected, int actual) {
            Check(label, expected, actual,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture));
        }

        private static void CheckEqual(string label, decimal expected, decimal actual) {
            Check(label, expected, actual, FormatSeconds(expected), FormatSeconds(actual));
        }

        private static void CheckEqual(string label, double expected, double actual) {
            Check(label, expected, actual,
                expected.ToString("0.###", CultureInfo.InvariantCulture),
                actual.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static void Check<T>(string label, T expected, T actual, string expectedText, string actualText) {
            checks++;
            var ok = EqualityComparer<T>.Default.Equals(expected, actual);
            if (!ok) { failures++; }
            Console.WriteLine($"  [{(ok ? "ok" : "ECHEC")}] {label}{(ok ? string.Empty : $" -- attendu {expectedText}, obtenu {actualText}")}");
        }

        private static string FormatDuration(TimeSpan value) {
            var sign = value < TimeSpan.Zero ? "-" : string.Empty;
            if (value < TimeSpan.Zero) { value = value.Negate(); }
            return $"{sign}{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
        }

        private static string FormatSeconds(decimal seconds) => seconds.ToString("0.###", CultureInfo.InvariantCulture) + " s";
    }
}
