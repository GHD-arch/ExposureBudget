using NINA.Plugin.ExposureBudget.Model;
using System;
using System.Collections.Generic;
using System.Globalization;

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
