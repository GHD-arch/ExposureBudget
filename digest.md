# Exposure Budget — digest du développement

Récapitulatif complet de ce qui a été conçu, développé, vérifié et documenté pour ce plugin
N.I.N.A. Document destiné à être lu seul : il reprend les décisions, les pièges rencontrés et
les résultats des vérifications, sans avoir à parcourir l'historique du projet.

- **Plugin** : Exposure Budget (ex-« Number To Hours »), version **1.2.1.0**
- **Auteur** : Gerard Hurtaud
- **Application cible** : N.I.N.A. 3.2.0.9001 installée (`net8.0-windows`), version minimale déclarée `3.0.0.0`
- **Dossier de travail** : `E:\DEV\NTH - NumberToHours`
- **Installation** : `%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExposureBudget\`

---

## 1. Le besoin et la réponse apportée

En astrophotographie, une séance s'étend d'une heure de début à une heure de fin — souvent
**à cheval sur minuit** (21:30 → 03:15 le lendemain). Le plugin répond à une question simple :
**combien de poses d'une durée donnée tiennent dans ce créneau ?**

Règles de calcul implémentées (arithmétique **décimale**, donc sans imprécision flottante sur
les cas limites) :

| Règle | Détail |
|---|---|
| Fenêtre | `ΔT = Hf − Hd`, **+ 24 h si Hf ≤ Hd** (la fin appartient au lendemain) |
| Nombre exact | `N = ΔT / temps de pose` |
| Arrondi | partie décimale **< 0,5 → pose inférieure** ; **≥ 0,5 → une pose de plus** |
| Heures identiques | fenêtre nulle → 0 pose, avec avertissement |

Exemples vérifiés automatiquement : `21:30 → 03:15` (20 700 s) donne **69 poses** à 300 s,
**35** à 600 s (34,5 → dépassement signalé), **17** à 1 200 s (17,25) ; `21:30 → 03:21`
donne **70** à 300 s (70,2) et **176** à 120 s (175,5, cas du 0,5).

L'interface affiche systématiquement le **nombre exact avec décimales**, la **règle d'arrondi
appliquée**, le **temps couvert**, le **reliquat** (ou le **dépassement**) et l'**horodatage
de la dernière pose**. Le calcul se fait **en direct** : il n'y a pas de bouton « calculer »
(il serait redondant).

## 2. Le panneau

- **Start / End** : ascenseurs `hh` (0–23) et `mm` (0–59) — contrôles **natifs** de N.I.N.A.
  (`IntStepperControl`), saisie clavier possible.
- **Available duration** en direct, avec reconstitution de la séance
  (`10/10/2026 21:30 → 11/10/2026 03:15`) : le passage au lendemain est visible.
- **Exposure** : `StepperControl` réglé **de 15 s en 15 s**, bornes 0,1 s → 24 h.
- **Équivalence en minutes** à côté : `5 min 00 s` et `i.e. 5 min` (300 s = 5 min).
- **Astronomical night (N.I.N.A.)** : remplit début et fin avec le **crépuscule et l'aube
  astronomiques** calculés par N.I.N.A. pour le profil d'observatoire actif — avec replis
  successifs **nautique → civil → coucher/lever du Soleil** si le crépuscule astronomique
  n'existe pas (nuit blanche), et mention du repli utilisé.
- **Number of exposures** en grand, avec détail, arrondi, couverture, reliquat et
  avertissements (fenêtre nulle, temps de pose plus long que la fenêtre, dépassement de Hf).
- **Now** : remplit l'heure de début avec l'heure courante (la date de référence passe au jour
  courant, pour qu'un clic en matinée ne place pas la séance la veille).
- **Copy result** : copie **le nombre de poses** dans le presse-papiers (uniquement la valeur
  affichée en grand) avec une confirmation ; un presse-papiers inaccessible est signalé dans le
  panneau, jamais par une exception.

## 3. Architecture

```
NINA.Plugin.ExposureBudget\
  ExposureBudgetPlugin.cs            manifeste ([Export(typeof(IPluginManifest))] : PluginBase)
  DataTemplates.xaml(.cs)            dictionnaire exporté, clé « <VM>_Dockable »
  ViewModel\ExposureBudgetVM.cs      panneau ancrable ([Export(typeof(IDockableVM))] : DockableVM)
  View\ExposureBudgetView.xaml       interface (styles et contrôles N.I.N.A. d'origine)
  Localization\                      IPluginStrings + EnglishStrings + FrenchStrings + PluginLanguage
  Model\ExposureBudgetCalculator.cs  logique de calcul pure (sans WPF, sans N.I.N.A.)
  Model\ExposureCountResult.cs       résultat détaillé (exact, retenu, couvert, reliquat…)
  Model\ExposureBudgetSettings.cs    réglages (fichier texte, sans dépendance)
  Resources\PluginImage.png          image du plugin (256 × 256, intégrée à l'assembly)
  Properties\AssemblyInfo.cs         nom, auteur, version, GUID, métadonnées du manifeste
tools\
  build.ps1                          construction + vérifications + installation
  install.ps1 / uninstall.ps1        installation / désinstallation (avec migration)
  makeicon\                          préparation et analyse d'images pour la fiche du plugin
  verify\                            harnais de calcul (92 vérifications)
  smoketest\                         harnais de chargement façon N.I.N.A. (157 vérifications)
ExposureBudget.sln, README.md, digest.md
```

Le plugin s'appuie sur trois exports MEF, exactement comme le chargeur de N.I.N.A. les attend :

1. `IPluginManifest` → nom, auteur, version, textes de la fiche, compatibilité ;
2. `ResourceDictionary` → le `DataTemplate` fusionné dans `Application.Current.Resources` ;
3. `IDockableVM` → le panneau de l'onglet Imagerie.

**Point clé** : la clé du `DataTemplate` doit être **`<Type complet du ViewModel>_Dockable`**
(`NINA.Plugin.ExposureBudget.ViewModel.ExposureBudgetVM_Dockable`), car c'est ainsi que
`NINA.Utility.AvalonDock.PaneTemplateSelector` recherche le template d'un panneau de plugin.

## 4. N.I.N.A. : points d'intégration et règles découvertes dans les sources

| Découverte | Conséquence pour le plugin |
|---|---|
| Le chargeur scanne **tous les `.dll`** du dossier de plugin (racine et sous-dossiers) | ne **jamais** copier une assembly `N.I.N.A.*` dans la sortie (`<Private>false</Private>`) |
| `Constants.UserExtensionsFolder` = `Plugins\<PluginMinimumApplicationVersion de N.I.N.A.>` | pour 3.2.0.9001 : `Plugins\3.0.0` (valeur lue dans `NINA.Plugin.dll`) |
| `PluginLoader.ParsePluginVersion` refuse une version sans `Build`/`Revision` | la version doit garder **quatre composants** (`1.1.0.0`) |
| Les plugins sont chargés depuis un `Task.Run`, dans un `AssemblyLoadContext` dédié | les URI `pack://` sont résolues via le `AssemblyResolve` de N.I.N.A. ; c'est pourquoi le XAML du plugin fonctionne |
| `PluginInstaller.GetDestinationFolderFromManifest` construit le dossier à partir de `manifest.Name` | le **nom** du plugin ne doit pas être traduit |
| La langue est portée par le **profil** (`ActiveProfile.ApplicationSettings.Language`), pas par la culture du fil | lire le profil, pas `CurrentUICulture` |
| `ProfileService.LocaleChanged` est déclenché sur le dispatcher UI | retraduction à chaud possible, sans redémarrage |
| `LayoutInitializer` insère les nouveaux panneaux dans « ToolsPane » | le panneau apparaît tout seul dans l'onglet Imagerie |
| La disposition AvalonDock enregistre `ContentId` (+ un titre informatif) | le titre d'un panneau peut changer sans casser la disposition |
| `INighttimeCalculator.Calculate()` → `NighttimeData` (`TwilightRiseAndSet`, nautique, civil, Soleil) | bouton « Nuit astronomique » et ses replis |
| Les styles sont **implicites** (`TextBlock`, `Button`, `TextBox`, `GroupBox`, ascenseurs) | aucun style à redéclarer : le rendu est celui de N.I.N.A., thème et couleurs du profil compris |
| La fiche du plugin est affichée par **liaison WPF** (`Key.Descriptions.ShortDescription`) et l'image par `Image.Source` ← `FeaturedImageURL`, via un convertisseur qui laisse passer la valeur | la propriété doit être **publique** ; l'image peut être un **pack URI** |

## 5. Pièges rencontrés et solutions

| Problème | Cause | Solution retenue |
|---|---|---|
| Le plugin ne compilait pas : `System.ComponentModel.Composition` introuvable | MEF n'est pas dans les frameworks de référence, c'est un **package** | référence directe à `System.ComponentModel.Composition.dll` de l'installation N.I.N.A. (`Private=false`) |
| La sérialisation JSON des réglages échouait dans N.I.N.A. | `System.Text.Json` de l'hôte dépend d'une autre version de `System.Text.Encodings.Web` | réglages dans un **fichier texte `clé=valeur`**, sans aucune dépendance |
| Le plugin restait en anglais malgré N.I.N.A. en français | défaut `language=en`, mais surtout détection basée sur `CurrentUICulture`, alors que N.I.N.A. stocke la langue **dans le profil** | résolution via `ActiveProfile.ApplicationSettings.Language`, + `LocaleChanged`, + `language=auto` par défaut |
| La fiche du plugin restait en anglais | `Descriptions` écrit en **implémentation d'interface explicite** → invisible pour la réflexion WPF, qui retombait sur la propriété de `PluginBase` (attributs d'assembly, anglais) | propriété **`public new IPluginDescription Descriptions`** + vérification par **liaison WPF** dans le harnais |
| Un ancien `language=en` figé dans le fichier de réglages annulait la correction | réglages écrits par une version antérieure | clé `settingsVersion` et migration : un fichier sans marqueur reprend la valeur par défaut courante (heures conservées) |
| Impossible de remplacer le DLL pendant que N.I.N.A. tourne | Windows verrouille une assembly chargée | `install.ps1` détecte le verrou, explique la marche à suivre et sort proprement (code 2) ; `build.ps1` le signale sans échec bloquant |
| Deux panneaux identiques après le renommage | ancien dossier `NumberToHours` encore présent | `install.ps1` migre les réglages puis supprime l'ancien dossier ; `uninstall.ps1` nettoie les deux noms |
| Un `--` dans un commentaire XML interrompait la construction | règle XML | commentaires reformulés |

## 6. Langue (i18n)

| Réglage | Effet |
|---|---|
| `language=auto` | **défaut** : suit la langue de N.I.N.A. |
| `language=en` | anglais forcé |
| `language=fr` | français forcé |

- Langue lue dans le **profil actif** ; repli sur la culture d'interface si le profil n'est pas
  encore chargé.
- **Retraduction à chaud** : `profileService.LocaleChanged` → libellés du panneau **et** fiche
  du plugin.
- La culture d'affichage des nombres suit la langue (`20,700 s` / `20 700 s`), les dates
  restent en 24 h `jj/mm/aaaa hh:mm` (cohérent avec les ascenseurs `hh`/`mm`).
- **Ne sont pas traduits** : le nom du plugin et le titre du panneau (« Exposure Budget »),
  qui sont l'identité du plugin — N.I.N.A. en dérive le nom du dossier d'installation.
- Ajouter une langue = écrire une classe `IPluginStrings` de plus et une ligne dans
  `PluginStrings.Resolve`.

## 7. Réglages et migration

Fichier `ExposureBudget.settings`, à côté du DLL, modifiable à la main :

```
# Exposure Budget - plugin settings, editable by hand.
# language : auto (default, follows the N.I.N.A. user interface language), en, or fr.
# The window times use the 24 hour clock, the exposure time is in seconds.
settingsVersion=1
language=auto
startHour=21
startMinute=30
endHour=3
endMinute=15
exposureSeconds=300
```

Lecture tolérante (clés inconnues ignorées, valeurs invalides ignorées, nombre accepté en
format invariant ou local). `settingsVersion` permet de faire évoluer les valeurs par défaut :
un fichier sans marqueur voit sa langue ramenée au défaut courant, les heures saisies restant
conservées.

## 8. Image du plugin

- `Resources\PluginImage.png` — **256 × 256**, carrée, intégrée à l'assembly, exposée par un
  pack URI dans `Descriptions.FeaturedImageURL`.
- Emplacements d'affichage : **25 × 25** dans la liste des plugins, jusqu'à **150 × 150** dans
  le volet de détail (à côté de l'auteur et de la version).
- Préparée par `tools\makeicon` (réduction de haute qualité par demiages successifs, cadrage
  carré centré) depuis l'image fournie par l'auteur (1024 × 1024, sombre et opaque — adaptée
  au thème sombre de N.I.N.A.).
- L'onglet du panneau conserve, lui, l'icône **calculatrice vectorielle** de N.I.N.A.
  (ressource `CalculatorSVG`).
- L'outil sait aussi analyser une image (`--inspect`) et régénérer l'icône calculatrice de
  N.I.N.A. en PNG, pour revenir en arrière.

## 9. Vérifications automatiques

| Harnais | Portée | Résultat |
|---|---|---|
| `tools\verify` | logique de calcul pure : passage de minuit, durées, règle du 0,5, cas limites, bornes, refus des temps de pose invalides | **92 vérifications** |
| `tools\smoketest` | chargement façon N.I.N.A. : composition **MEF réelle**, métadonnées et compatibilité, clé du `DataTemplate` dans `Application.Resources`, chargement de la vue depuis le BAML, **pose (layout) sans erreur**, **libellés effectivement affichés** dans le bon langage, liaisons des ascenseurs, recalcul en direct, bouton « Maintenant », contenu du texte copié et sa confirmation, avertissements, bouton *Nuit astronomique* et ses replis, réglages et migration, **fiche du plugin traduite par les deux chemins (appel C# et liaison WPF)**, **image chargée par le pack URI et affichée par la liaison** | **157 vérifications** |

Ces harnais ont réellement trouvé des défauts pendant le développement : un écart de
l'arrondi affiché au-delà de la fenêtre, la migration des réglages manquante, la fiche du
plugin non traduite pour WPF (implémentation explicite), et un test de liaison WPF trop strict.
Ils tournent à chaque `build.ps1`.

## 10. Installation et utilisation

```powershell
cd "E:\DEV\NTH - NumberToHours"

.\tools\build.ps1 -Install     # construction + vérifications + installation
.\tools\install.ps1            # installation seule (le dll doit être construit)
.\tools\uninstall.ps1          # désinstallation
.\tools\build.ps1 -NoVerify    # construction rapide, sans harnais
```

> **N.I.N.A. doit être fermé** pour installer une **mise à jour** (Windows verrouille le DLL
> chargé) ; pour une **première** installation, N.I.N.A. peut rester ouvert, le plugin sera
> pris en compte au démarrage suivant.

Installation manuelle : copier `NINA.Plugin.ExposureBudget.dll` (et le `.deps.json`) dans
`%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExposureBudget\` puis redémarrer N.I.N.A.
Le panneau apparaît dans le volet des outils de l'onglet **Imagerie**.

## 11. Métadonnées et versions

| Élément | Valeur |
|---|---|
| Nom affiché | `Exposure Budget` |
| Auteur | `Gerard Hurtaud` |
| Version | **`1.2.1.0`** (quatre composants obligatoires) |
| Version minimale de N.I.N.A. | `3.0.0.0` |
| Identifiant (GUID) | `8F3A1C64-2D7E-4B91-A3F5-6C0D9E7B4A28` |
| Licence | MPL-2.0 |
| Cible de compilation | `net8.0-windows` (installation stable) ou `net10.0-windows` via `-p:NinaTarget=Nightly` |

Historique : **1.0.0.0** = première version fonctionnelle (« Number To Hours ») ;
**1.1.0.0** = renommage, fiche du plugin traduite et corrigée, image du plugin, langue lue
dans le profil, migration des réglages, journalisation ; **1.2.0.0** = confort (bouton
« Maintenant », copie du résultat) ; **1.2.1.0** = la copie ne dépose que le nombre de poses.

La version se met à jour à un seul endroit : `Properties\AssemblyInfo.cs`.

## 12. Journalisation utile au diagnostic

Au démarrage, le plugin écrit deux lignes dans `%LOCALAPPDATA%\NINA\Logs\*.log` :

```
ExposureBudget: panel language 'fr' (setting 'auto', N.I.N.A. language 'fr-FR'), plugin 1.2.1.0 loaded from ...
ExposureBudget: plugin sheet language 'fr' (N.I.N.A. language 'fr-FR')
```

Elles indiquent la langue retenue, la valeur du réglage, la langue de N.I.N.A. et le fichier
réellement chargé — de quoi lever tout doute sur une question de langue.

## 13. Limites connues et pistes d'amélioration

Aucune n'est implémentée à ce jour. **La liste de travail détaillée — objectifs, notes de mise
en œuvre, vérifications à ajouter et décisions à prendre — est dans [`afaire.md`](afaire.md)** ;
voici le résumé, classé par rapport valeur/effort.

1. **Tableau comparatif des temps de pose** : afficher côte à côte, pour la fenêtre courante,
   120 s → 176 poses, 180 s → 117, 300 s → 70… avec le reliquat de chaque ligne. Transforme
   la calculette en outil de décision.
2. **Surcharge par pose** (téléchargement, dither, stabilisation) : `pose + surcharge`. C'est
   la première cause d'écart entre le calcul et la nuit réelle.
3. **Temps à réserver** : flip au méridien, autofocus toutes les N poses, déduits de la fenêtre.
4. **Élément de séquence « Exposure Budget »** : instruction qui calcule le nombre de poses à
   l'exécution et alimente une prise de vues — planification dynamique par nuit et par cible.
5. **Préremplissage depuis la séquence** (pose de la cible active) ou depuis la caméra.
6. **Résultat exposé via le `SymbolBroker`**, réutilisable dans les expressions de séquence.
7. **Option « ne jamais dépasser la fenêtre »** (plancher au lieu de l'arrondi au plus proche).
8. **Confort** : bouton « Maintenant » pour le début, compte à rebours des poses restantes,
   copie du résultat, langues supplémentaires.
9. **Diffusion** : dépôt GitHub, manifeste du dépôt de plugins N.I.N.A. (URL + empreinte),
   captures d'écran (`ScreenshotURL` / `AltScreenshotURL`, affichées seulement pour les plugins
   installés depuis un dépôt), tests rejoués en CI.

## 14. Inventaire des fichiers

| Fichier | Rôle |
|---|---|
| `ExposureBudget.sln` | solution (plugin + 2 harnais + outil d'image) |
| `README.md` | documentation utilisateur (installation, règles, langue, structure, versions) |
| `digest.md` | ce récapitulatif de développement |
| `afaire.md` | liste de travail des améliorations (priorités, notes de mise en œuvre, vérifications) |
| `NINA.Plugin.ExposureBudget\**` | le plugin (voir section 3) |
| `tools\build.ps1` | construction, vérification, installation |
| `tools\install.ps1` / `uninstall.ps1` | installation / désinstallation, avec migration depuis l'ancien nom |
| `tools\verify\Program.cs` | harnais de calcul (92 vérifications) |
| `tools\smoketest\Program.cs` + `Stubs.cs` | harnais de chargement (157 vérifications) |
| `tools\makeicon\Program.cs` | préparation / analyse d'images, génération de l'icône calculatrice |
| `image.png` | image source fournie par l'auteur (1024 × 1024) ; le plugin embarque la version préparée |
