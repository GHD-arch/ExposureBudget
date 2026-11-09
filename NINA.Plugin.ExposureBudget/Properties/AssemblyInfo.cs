using System.Reflection;
using System.Runtime.InteropServices;

// --- Metadonnees lues par NINA.PluginBase -------------------------------------
// PluginBase.Name            <- AssemblyTitle
// PluginBase.Author          <- AssemblyCompany
// PluginBase.Version         <- AssemblyFileVersion
// PluginBase.Identifier      <- GuidAttribute
// PluginBase.MinimumApplicationVersion <- AssemblyMetadata "MinimumApplicationVersion"
//
// Les textes affiches dans Options > Plugins sont traduits a l'execution :
// ExposureBudgetPlugin reimplemente IPluginManifest.Descriptions et fournit
// Localization\<langue>Strings.PluginShortDescription / PluginLongDescription.
// Les attributs ci-dessous restent la version de reference (anglais) : NINA les lit
// aussi pour signaler un plugin qui n'a pas pu etre charge.
//
// Version : c'est ici qu'elle se met a jour (source unique, comme pour les plugins N.I.N.A.).
// ATTENTION : la version doit comporter QUATRE composants (majeure.mineure.correctif.revision).
// NINA.Plugin.PluginLoader.ParsePluginVersion refuse toute version dont Build ou Revision vaut -1
// (donc "1.0.0" serait rejete) et PluginCompatibilityMap construit un System.Version a quatre
// composants a partir de ces valeurs.
[assembly: AssemblyTitle("Exposure Budget")]
[assembly: AssemblyDescription("Computes how many exposures of a given duration fit between a start time and an end time, taking the midnight rollover into account.")]
[assembly: AssemblyCompany("Gerard Hurtaud")]
[assembly: AssemblyProduct("N.I.N.A. - Exposure Budget plugin")]
[assembly: AssemblyCopyright("Copyright © 2026 Gerard Hurtaud")]
[assembly: ComVisible(false)]
[assembly: Guid("8F3A1C64-2D7E-4B91-A3F5-6C0D9E7B4A28")]
[assembly: AssemblyVersion("1.5.3.0")]
[assembly: AssemblyFileVersion("1.5.3.0")]

[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.0")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/")]
[assembly: AssemblyMetadata("Tags", "Exposure,Time,Calculator,Planning")]
[assembly: AssemblyMetadata("LongDescription",
    "Exposure Budget works out how many exposures of a given duration can be captured between " +
    "the start and the end of a session, including sessions that run past midnight. " +
    "The acquisition window can be filled in automatically from the astronomical night that " +
    "N.I.N.A. computes for your observatory profile. The panel and the plugin description " +
    "follow the N.I.N.A. user interface language (English or French).")]
