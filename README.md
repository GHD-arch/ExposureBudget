# Exposure Budget — plugin pour N.I.N.A.

Plugin pour **N.I.N.A. (Nighttime Imaging 'N' Astronomy)** qui calcule **combien de poses**
d'un temps donné il est possible de réaliser entre une heure de début **Hd** et une heure
de fin **Hf**, en tenant compte du **passage de minuit**.

> **Langue : le panneau suit celle de N.I.N.A.** (réglage `language=auto` par défaut) — donc
> français si N.I.N.A. est en français, anglais sinon. Le changement de langue dans
> Options → Langue est appliqué **immédiatement**, sans redémarrer N.I.N.A. La langue peut
> aussi être forcée via `language=en` ou `language=fr` — voir la section « Langue » plus bas.

> 📄 Ce README décrit l'installation et l'utilisation. Deux documents l'accompagnent :
> [`digest.md`](digest.md) récapitule tout le développement (décisions techniques, points
> d'intégration découverts dans N.I.N.A., pièges rencontrés et solutions, résultats des
> vérifications), et [`afaire.md`](afaire.md) liste les améliorations à venir avec leur
> priorité, leurs notes de mise en œuvre et les vérifications à ajouter.

Panneau livré par le plugin, affiché dans l'onglet **Imagerie** :

```
┌ Exposure Budget ────────────────────────────┐
│ Acquisition window                          │
│  Start  [−][ 21 h ][+] : [−][ 30 min ][+]   │
│  End    [−][ 03 h ][+] : [−][ 15 min ][+]   │
│  Available duration ............. 05:45:00  │
│  10/10/2026 21:30  →  11/10/2026 03:15      │
│ Exposure time                               │
│  Exposure [−][ 300  s ][+] = [ 5 min 00 s ] │
│                               i.e. 5 min    │
│  [     Astronomical night (N.I.N.A.)    ]   │
│ Number of exposures                         │
│                 69                          │
│  20,700 s ÷ 300 s = 69 → 69 exposures       │
│  The exposure count comes out exact: no     │
│  rounding is applied.                       │
│  Covered time: 05:45:00 (69 × 300 s) ·      │
│  Remaining: 00:00:00                        │
│  Last exposure ends: 11/10/2026 03:15:00    │
└─────────────────────────────────────────────┘
```

Le résultat est **recalculé en direct** à chaque modification d'une valeur (il n'y a pas de
bouton « calculer » : il serait redondant).

## Règles de calcul

| Règle | Détail |
|---|---|
| Fenêtre | `ΔT = Hf − Hd` ; **+ 24 h si Hf ≤ Hd** (la fin appartient au lendemain) |
| Fenêtre exploitable | `ΔT − réserves` (retournement au méridien, autofocus toutes les N poses, marge fixe) |
| Temps consommé par une pose | `temps de pose + surcharge` (téléchargement, dither, stabilisation) |
| Nombre exact | `N = fenêtre exploitable / temps consommé par une pose` |
| Arrondi | partie décimale **< 0,5 → pose inférieure** ; **≥ 0,5 → une pose de plus** |
| Temps couvert | `poses retenues × temps de pose` (la surcharge n'entre pas dans l'intégration) |
| Fin de la dernière pose | `début + temps consommé − surcharge` (la surcharge s'applique **entre** les poses) |
| Plan par filtres | `temps alloué = ΔT × % / 100`, puis la même règle d'arrondi ; **refus** si les pourcentages dépassent 100 % ou si le temps consommé arrondi dépasse `ΔT` |
| Calcul de précision | arithmétique décimale : les cas limites type `34,5` sont exacts, sans imprécision flottante |

(Hd = heure de début, Hf = heure de fin ; dans le panneau ces deux valeurs sont simplement
libellées **Start** et **End**. Sans surcharge ni réserve, les formules se réduisent au calcul
d'origine `ΔT / temps de pose`.)

Exemples vérifiés par les harnais du dépôt :

| Hd → Hf | ΔT | Pose | Surcharge | N exact | Poses retenues | Remarque |
|---|---|---|---|---|---|---|
| 21:30 → 03:15 | 05:45:00 | 300 s | 0 | 69,00 | **69** | aucun reliquat |
| 21:30 → 03:15 | 05:45:00 | 600 s | 0 | 34,50 | **35** | dépasse la fenêtre de 5 min (signalé) |
| 21:30 → 03:15 | 05:45:00 | 1 200 s | 0 | 17,25 | **17** | 5 min inutilisées |
| 21:30 → 03:21 | 05:51:00 | 300 s | 0 | 70,20 | **70** | 1 min inutilisée |
| 21:30 → 03:21 | 05:51:00 | 120 s | 0 | 175,50 | **176** | cas du 0,5 → pose en plus |
| 21:30 → 03:15 | 05:45:00 | 300 s | **5 s** | 67,87 | **68** | 20 700 ÷ 305 ; dépasse de 40 s (arrondi au-dessus) |
| 21:30 → 03:15 | 05:45:00 | 250 s | **2 s** | 82,14 | **82** | 82 × 252 = 20 664 s, 36 s libres |
| 21:30 → 03:15 | 05:45:00 | 300 s | 0 + **15 min réservées** | 66,00 | **66** | 19 800 ÷ 300 |
| 21:30 → 03:15 | 05:45:00 | 300 s | 0 + **21 min réservées** (autofocus ×6) | 65,70 | **65** | itération : 65 poses → 6 passages |
| 22:00 → 22:02 | 00:02:00 | 300 s | 0 | 0,40 | **0** | temps de pose trop long (signalé) |
| 21:00 → 21:00 | 00:00:00 | 300 s | 0 | 0,00 | **0** | heures identiques (signalé) |

L'interface affiche systématiquement le **nombre exact avec ses décimales**, la règle
d'arrondi appliquée, le **temps couvert**, le **reliquat** (ou le **dépassement**) et
l'**horodatage de la dernière pose**.

## Contenu du panneau

Le bloc **fenêtre d'acquisition** est toujours visible en haut (heures, bouton *Nuit
astronomique*, durée et réserves) ; le reste est réparti en trois onglets : **Pose unique**
(ci-dessous), **Plan par filtres** et **Temps à réserver**.

- **Start** et **End** : deux ascenseurs `hh` (0–23) et `mm` (0–59) — contrôles
  natifs `IntStepperControl` de N.I.N.A., donc style et comportement identiques au reste de
  l'application (saisie au clavier possible dans la zone de texte).
- **Astronomical night (N.I.N.A.)** : remplit l'heure de début et l'heure de fin avec le
  crépuscule et l'aube **astronomiques** calculés par N.I.N.A. pour le profil d'observatoire
  actif. Comme il ne définit que la **fenêtre** (le delta d'heures), il se trouve dans ce bloc,
  sous les informations de la fenêtre, et non dans les onglets de calcul. Si le crépuscule
  astronomique n'existe pas (nuit blanche aux hautes latitudes), le plugin replie
  successivement sur le crépuscule **nautique**, le crépuscule **civil** puis le coucher/lever
  du Soleil, et l'indique — les horaires ne sont pas répétés : ils sont déjà affichés dans les
  heures de début et de fin.
- **Available duration** calculée en direct, avec reconstitution de la séance
  (`10/10/2026 21:30 → 11/10/2026 03:15`), ce qui montre explicitement le passage au lendemain.
- **Exposure** : ascenseur `StepperControl` réglé **de 15 s en 15 s** (saisie libre au clavier),
  bornes 0,1 s à 24 h.
- **Champ d'information en minutes** à côté : `5 min 00 s` et `i.e. 5 min` (300 s = 5 min).
- **Number of exposures** en grand, avec détails du calcul, arrondi, reliquat et
  avertissements (fenêtre nulle, temps de pose trop long, dépassement de la fenêtre).
  Tout est recalculé à chaque modification d'une valeur : aucun bouton n'est nécessaire.
- **Now** : remplit l'heure de début avec l'heure courante (pratique pour calculer ce qu'il
  reste à faire en cours de séance).
- **Copy result** : copie **le nombre de poses** dans le presse-papiers (uniquement la valeur,
  par exemple `69`) et affiche une confirmation.
- **Overhead** (surcharge) : temps consommé par chaque pose en plus de l'exposition elle-même
  (téléchargement, dither, stabilisation), réglé de 1 s en 1 s. Le temps consommé par une pose
  devient `exposition + surcharge` ; le temps couvert reste la somme des expositions. La
  surcharge s'applique aussi au plan par filtres.

### Onglet « Temps à réserver »

Déduit de la fenêtre les opérations qui ne prendront pas de poses : **retournement au méridien**
(minutes), **autofocus toutes les N poses** avec sa durée, et **marge fixe** (minutes). Le nombre
de poses est calculé sur la fenêtre **exploitable** ; comme les autofocus dépendent du nombre de
poses, le calcul itère jusqu'à cohérence. Le résumé « réservé · exploitable » est rappelé en haut
du panneau, et un avertissement s'affiche si les réserves dépassent la fenêtre.

### Onglet « Plan par filtres » (caméra mono)

Pour une caméra monochrome et une roue à filtres : une ligne par filtre (par défaut
**L, R, G, B, S, H, O**), avec pour chacun une **case à cocher**, un **temps de pose** et un
**pourcentage du temps disponible**. Le plugin affiche le nombre de poses par filtre et refuse
le plan si les entrées ne tiennent pas :

- temps alloué = `fenêtre × % / 100`, nombre de poses = règle du **0,5** (même règle que
  l'onglet principal — un seul filtre à 100 % donne exactement le même résultat) ;
- **refus** si `Σ % > 100 %` ;
- **refus** si le **temps réellement couvert** par les poses arrondies dépasse la fenêtre
  (exemple : 4 h, une pose de 9 600 s à 100 % → 1,5 → 2 poses = 5 h 20 → refusé) ;
- totaux en bas (`100 % · 108 poses · 3 h 54`) et **reliquat** affiché (les secondes perdues par
  les arrondis ne sont pas redistribuables avec des pourcentages entiers) ;
- boutons **Tout cocher**, **Répartir 100 %** (par pas de 5 points) et **Réinitialiser** ;
- chaque ligne a un **bouton >** qui donne au filtre les points de pourcentage encore libres
  (100 − total), et le **nom du filtre est modifiable** directement dans la ligne ;
- **+ filtre** / **- filtre** ajoutent ou retirent une ligne en fin de liste (temps de pose par
  pas de **5 s** dans le plan) ;
- **Copier le plan** met une ligne par filtre dans le presse-papiers
  (`L : 48 × 120 s (01:36:00)`), suivie des totaux — désactivé tant que le plan est refusé.

Les derniers réglages (langue, heures, temps de pose et plan par filtres) sont mémorisés dans
`ExposureBudget.settings`, un petit fichier texte à côté du plugin, modifiable à la main
(clés `overheadSeconds`, `flipReserveMinutes`, `fixedReserveMinutes`, `autofocusEvery`,
`autofocusDurationSeconds` et le bloc `plan.filter.*` du plan par filtres).

## Langue

| Réglage | Effet |
|---|---|
| `language=auto` | **valeur par défaut** : suit la langue de l'interface de N.I.N.A. (français si N.I.N.A. est en français, anglais sinon) |
| `language=en` | anglais, quelle que soit la langue de N.I.N.A. |
| `language=fr` | français, quelle que soit la langue de N.I.N.A. |

Le plugin lit la langue de N.I.N.A. **dans le profil actif**
(`ActiveProfile.ApplicationSettings.Language`, la valeur réglée par Options → Langue) et
s'abonne à l'événement `LocaleChanged` : changer la langue de N.I.N.A. retraduit le panneau
**immédiatement**, sans redémarrage. Si le profil n'est pas encore chargé (au tout début du
démarrage), le plugin se replie sur la culture d'interface du fil courant.

La **fiche du plugin dans Options → Plugins** (description courte et description longue) suit
la même langue : `ExposureBudgetPlugin` fournit ses propres `Descriptions` à partir de
`Localization\`, là où `PluginBase` ne saurait lire que les attributs d'assembly figés en
anglais.

> ⚠️ Cette propriété est déclarée **`public new IPluginDescription Descriptions`** — et non en
> implémentation d'interface explicite. C'est indispensable : la page des plugins de N.I.N.A.
> affiche ces textes par **liaison WPF**, qui passe par la réflexion sur les membres
> **publics** du type réel. Une implémentation explicite (`IPluginManifest.Descriptions`)
> fonctionne pour les appels C# mais reste invisible pour WPF, qui retombe alors sur la
> propriété publique de `PluginBase`… et affiche l'anglais. Le harnais `tools\smoketest`
> vérifie les **deux chemins** (appel direct et liaison `Key.Descriptions.ShortDescription`,
> comme dans `PluginsView.xaml`).

Deux éléments **ne sont jamais traduits** : le **nom du plugin** et le **titre du panneau**
(l'onglet ancrable). « Exposure Budget » est l'identité du plugin, pas un libellé
d'interface — et N.I.N.A. en dérive le nom du dossier d'installation
(`PluginInstaller.GetDestinationFolderFromManifest` → `Plugins\<version>\<Nom>`), un nom
traduit créerait donc un dossier différent selon la langue du poste. Seuls les libellés du
panneau et les textes de la fiche du plugin suivent la langue.

Pour forcer une langue, éditez `ExposureBudget.settings` (situé à côté du `.dll`, dans
`%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExposureBudget\`) et remplacez la valeur de `language` ;
le fichier n'est lu qu'au chargement du plugin, donc **redémarrez N.I.N.A.** après avoir forcé
la langue :

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

La ligne `settingsVersion` est écrite par le plugin : elle sert à faire évoluer les valeurs
par défaut dans le temps. Un fichier qui n'en contient pas provient d'une version antérieure
du plugin, et **la langue y est alors ramenée à la valeur par défaut courante** (les heures et
le temps de pose saisis sont conservés).

La **culture d'affichage des nombres et des dates suit la langue retenue** (donc `20,700 s`
en anglais et `20 700 s` en français), afin que le panneau reste cohérent ; les dates
utilisent toujours le format 24 h `jj/mm/aaaa hh:mm`, en accord avec les ascenseurs `hh`/`mm`.
Tous les libellés, infobulles et messages sont centralisés dans `Localization\` : ajouter une
langue consiste à écrire une classe `IPluginStrings` de plus et une ligne dans
`PluginStrings.Resolve`.

## Prérequis et compatibilité

- **N.I.N.A. 3.x** installé. Le plugin est compilé en **`net8.0-windows`**, comme la version
  stable actuelle de N.I.N.A. (**3.2.0.9001** installée sur ce poste). Il déclare
  `MinimumApplicationVersion = 3.0.0.0`, donc N.I.N.A. le range dans le dossier
  `%LOCALAPPDATA%\NINA\Plugins\3.0.0\`.
- Les assemblies `NINA.*` sont prises **dans le dossier d'installation de N.I.N.A.**
  (voir `NinaSdkPath`) et ne sont **jamais** copiées dans la sortie du plugin : le chargeur
  de N.I.N.A. scanne tous les `.dll` du dossier du plugin, une copie d'une assembly
  `NINA.*` casserait le chargement des plugins.
- ⚠️ Les sources `E:\DEV\nina` (branche nightly 3.3.0) sont en `net10.0-windows` : un plugin
  compilé pour ces sources **ne se chargerait pas** dans la NINA 3.2 installée. Une seconde
  cible de compilation existe pour ce cas :

  ```powershell
  # Construire d'abord l'application depuis les sources, puis :
  dotnet build NINA.Plugin.ExposureBudget\NINA.Plugin.ExposureBudget.csproj -p:NinaTarget=Nightly
  # ou en indiquant explicitement un dossier contenant toutes les assemblies N.I.N.A. :
  dotnet build NINA.Plugin.ExposureBudget\NINA.Plugin.ExposureBudget.csproj `
      -p:NinaTarget=Nightly -p:NinaSdkPath="C:\chemin\vers\nina"
  ```

  La cible `Nightly` compile en `net10.0-windows` contre
  `E:\DEV\nina\NINA\bin\Debug\net10.0-windows` (sortie de l'application) ; le build s'arrête
  avec un message explicite si ce dossier ne contient pas l'ensemble des assemblies
  (`System.ComponentModel.Composition.dll`, `CommunityToolkit.Mvvm.dll`).

## Construction et installation

```powershell
# Construction + vérifications de la logique et du chargement
.\tools\build.ps1

# Construction + vérifications + installation dans N.I.N.A.
.\tools\build.ps1 -Install

# Installation seule (le dll doit déjà être construit)
.\tools\install.ps1

# Désinstallation
.\tools\uninstall.ps1
```

Installation manuelle : copier `NINA.Plugin.ExposureBudget.dll` (et éventuellement le
`.deps.json` et le `.pdb`) dans
`%LOCALAPPDATA%\NINA\Plugins\3.0.0\ExposureBudget\`, puis redémarrer N.I.N.A.

Une fois N.I.N.A. relancé :

1. Onglet **Imagerie** : le panneau *Exposure Budget* est ajouté au volet des outils
   (le `LayoutInitializer` de N.I.N.A. insère les nouveaux panneaux automatiquement).
2. S'il n'apparaît pas, utilisez le **menu des panneaux** de l'onglet Imagerie (visibilité
   des volets / réinitialisation de la disposition).
3. **Options → Plugins** permet de vérifier que le plugin est chargé : nom, auteur, version,
   **image du plugin** et description (traduite selon la langue de N.I.N.A.).

## Structure du dépôt

```
NINA.Plugin.ExposureBudget\
  ExposureBudgetPlugin.cs         manifeste du plugin ([Export(typeof(IPluginManifest))] : PluginBase)
  DataTemplates.xaml(.cs)         dictionnaire exporté, contient la clé <VM>_Dockable
  ViewModel\ExposureBudgetVM.cs   panneau ancrable ([Export(typeof(IDockableVM))] : DockableVM)
  View\ExposureBudgetView.xaml    interface, style N.I.N.A. d'origine, libellés liés à Labels
  Localization\                   langues du panneau (suit N.I.N.A., en/fr forçables)
  Model\ExposureBudgetCalculator.cs  logique de calcul pure (testable sans WPF)
  Model\ExposureCountResult.cs    résultat détaillé du calcul
  Model\ExposureBudgetSettings.cs mémorisation des réglages (fichier texte sans dépendance)
  Resources\PluginImage.png      image du plugin (fiche Options → Plugins), intégrée à l'assembly
  Properties\AssemblyInfo.cs      nom, auteur, version, GUID, version minimale de N.I.N.A.
tools\
  build.ps1                       construction + vérifications (+ installation en option)
  install.ps1 / uninstall.ps1     installation et désinstallation dans N.I.N.A.
  makeicon\                       prépare l'image du plugin (redimensionnement, analyse, icône calculatrice)
  verify\                         harnais de vérification de la logique de calcul (154 vérifications)
  smoketest\                      harnais de chargement du plugin façon N.I.N.A. (238 vérifications)
```

L'image de la fiche du plugin est `Resources\PluginImage.png` (256 × 256, carrée, intégrée à
l'assembly). Elle a été préparée par `tools\makeicon`, qui sait faire trois choses :

```powershell
# 1. préparer une image fournie (réduction de haute qualité, cadrage carré centré)
dotnet run --project tools\makeicon -- --resize image.png NINA.Plugin.ExposureBudget\Resources\PluginImage.png 256

# 2. analyser une image sans la modifier (dimensions, transparence, luminosité, coins)
dotnet run --project tools\makeicon -- --inspect image.png

# 3. régénérer l'icône calculatrice de N.I.N.A. (ressource CalculatorSVG), pour revenir en arrière
dotnet run --project tools\makeicon -- NINA.Plugin.ExposureBudget\Resources\Calculator.png
```

N.I.N.A. affiche cette image en **25 × 25** dans la liste des plugins et jusqu'à **150 × 150**
dans le volet de détail (à côté de l'auteur et de la version) : 256 px couvre jusqu'à 150 % de
mise à l'échelle d'écran ; utilisez `512` en dernier argument si vous voulez plus de marge
(le fichier passe alors de ~120 à ~450 Ko). L'onglet du panneau, lui, conserve l'icône
calculatrice **vectorielle** de N.I.N.A. (ressource `CalculatorSVG` de l'application).

Le dossier de travail s'appelle `ExposureBudget` ; il peut être renommé ou déplacé librement,
aucun fichier du projet ne contient de chemin absolu vers lui (les scripts calculent leurs
chemins depuis leur propre emplacement).

## Vérifications automatiques

| Harnais | Ce qu'il valide | Commande |
|---|---|---|
| `tools\verify` | passage de minuit, durées, règle du 0,5, cas limites, bornes, refus des temps de pose invalides, **surcharge par pose** (temps consommé, arrondi, dépassement), **temps à réserver** (flip, autofocus avec itération, réserves > fenêtre), **plan par filtres** (allocation, totaux, refus, temps consommé, répartition 100 %) — sans WPF ni N.I.N.A. | `dotnet run --project tools\verify` |
| `tools\smoketest` | composition MEF réelle (exports `IPluginManifest`, `ResourceDictionary`, `IDockableVM`), métadonnées, compatibilité, clé `…ExposureBudgetVM_Dockable` dans `Application.Resources`, chargement de la vue depuis le BAML, pose (layout) sans erreur, **libellés effectivement affichés**, absence de bouton de calcul, liaisons des ascenseurs, valeurs affichées, **recalcul en direct**, avertissements, **trois onglets**, bouton *Nuit astronomique* (replis, hors des onglets, sous les informations, pleine largeur), **plan par filtres** (calcul, refus, reliquat, copie, renommage, ajout/suppression, lignes rendues sans chevauchement), **surcharge et réserves** (compteur, résumé, avertissement), mémorisation et migration des réglages, **fiche du plugin traduite et complète (cinq rubriques, deux langues)**, **icône du plugin chargée par le pack URI et affichée par la liaison** | `dotnet run --project tools\smoketest` |

## Versions et métadonnées

| Version | Contenu |
|---|---|
| **1.5.4.0** | Version courante. **Info-bulles sur toutes les commandes** : *Tout cocher*, *Répartir 100 %* et *Réinitialiser* en avaient (elles expliquent maintenant l'effet et ce qui est conservé), et les info-bulles existantes (*Now*, *Copier*, *Copier le plan*, bouton de reliquat, surcharge, filtres) ont été précisées. Dix contrôles automatiques vérifient que chaque bouton du panneau porte une info-bulle. |
| 1.5.3.0 | **Fiche du plugin réécrite** (Options → Plugins) : description courte et longue complètes, en anglais et en français, organisées en cinq rubriques — *fenêtre de séance*, *nombre de poses*, *nuit réaliste*, *caméra mono et filtres*, *réglages et langue* — pour qu'un utilisateur comprenne le panneau sans lire le README. Dix contrôles ajoutés sur le contenu de la fiche. |
| 1.5.2.0 | **Bloc fenêtre réorganisé** : le bouton *Astronomical night* passe **sous** les informations de la fenêtre (pleine largeur, plus de chevauchement) et son message ne répète plus les horaires déjà affichés — il ne signale plus qu'un **repli** (pas de crépuscule astronomique cette nuit). Deux contrôles de placement ajoutés au harnais. |
| 1.5.1.0 | Le bouton **Astronomical night** est déplacé dans le bloc **fenêtre d'acquisition** : il ne définit que le delta d'heures, il n'appartient à aucun onglet de calcul. Un contrôle automatique vérifie qu'il reste hors des onglets. |
| 1.5.0.0 | **Réalisme du calcul** : **surcharge par pose** (téléchargement, dither, stabilisation — le temps consommé devient `exposition + surcharge`) et **temps à réserver** (retournement au méridien, autofocus périodiques, marge fixe) déduits de la fenêtre, avec itération sur les autofocus. Nouvel onglet « Temps à réserver » ; la surcharge s'applique aussi au plan par filtres. |
| 1.4.0.0 | **Plan par filtres, lot 3** : bouton **>** qui attribue les pourcentages non utilisés au filtre de la ligne, **nom du filtre modifiable** dans la ligne, boutons **+ filtre** / **- filtre**, et **Copier le plan** (une ligne par filtre avec ses poses et le temps couvert, désactivé tant que le plan est refusé). |
| 1.3.2.0 | **Plan par filtres, lignes lisibles** : les noms des filtres réapparaissent (le style implicite de N.I.N.A. est un interrupteur qui n'affiche pas son contenu) avec une **petite case à cocher de 20 px** propre au plugin, et **temps de pose réglé de 5 s en 5 s** dans le plan (l'onglet « Pose unique » garde 15 s). |
| 1.3.1.0 | **Correctif d'affichage** de l'onglet « Plan par filtres » : la case à cocher de N.I.N.A. mesure 65 px (style `StandardCheckBox`) et le nom du filtre se superposait à elle — colonnes rééquilibrées et contrôle anti-chevauchement ajouté au harnais. |
| 1.3.0.0 | **Plan par filtres** (caméra mono) : second onglet du panneau, une ligne par filtre (L, R, G, B, S, H, O) avec case à cocher, temps de pose et pourcentage du temps disponible ; nombre de poses par filtre, totaux, reliquat, et **refus** si les pourcentages dépassent 100 % ou si les poses arrondies dépassent la fenêtre. Boutons *Tout cocher*, *Répartir 100 %* et *Réinitialiser* ; plan mémorisé dans les réglages. |
| 1.2.1.0 | Le bouton **Copy result** ne dépose plus que **le nombre de poses** dans le presse-papiers (et non tout le détail du calcul). |
| 1.2.0.0 | **Confort** : bouton **Now** (heure de début = heure courante, date du jour) et bouton **Copy result** (copie du résultat, avec confirmation). |
| 1.1.0.0 | **Renommage en « Exposure Budget »** (ex-« Number To Hours » : dossier de plugin `ExposureBudget`, réglages et heures conservés). **Fiche du plugin traduite** (description courte et longue) comme le panneau, et correction de son affichage (`public new Descriptions` : une implémentation d'interface explicite était invisible pour les liaisons WPF). **Image du plugin** dans la fiche. Détermination de la langue via **le profil actif de N.I.N.A.** avec retraduction à chaud. Migration des réglages (`settingsVersion`) et journalisation de la langue retenue. |
| 1.0.0.0 | Première version fonctionnelle (nommée « Number To Hours ») : fenêtre d'acquisition `hh`/`mm`, temps de pose par pas de 15 s, équivalence en minutes, calcul en direct du nombre de poses avec la règle du 0,5, passage de minuit, heures identiques signalées, bouton *Nuit astronomique* (replis nautique/civil/Soleil), interface bilingue suivant N.I.N.A. (`en`/`fr` forçables), mémorisation des réglages. |

Métadonnées affichées par N.I.N.A. dans **Options → Plugins** : nom `Exposure Budget`,
auteur **Gerard Hurtaud**, version `1.5.4.0`, version minimale de N.I.N.A. `3.0.0.0`.

La version se met à jour **à un seul endroit** : `AssemblyVersion` et `AssemblyFileVersion`
dans `NINA.Plugin.ExposureBudget\Properties\AssemblyInfo.cs` (c'est la convention des plugins
N.I.N.A., qui lisent ces attributs d'assembly). N'oubliez pas d'ajouter la ligne
correspondante au tableau ci-dessus.

> ⚠️ La version doit garder **quatre composants** (`majeure.mineure.correctif.revision`) :
> `PluginLoader.ParsePluginVersion` de N.I.N.A. refuse toute version dont `Build` ou
> `Revision` vaut `-1` (donc `"1.0.0"` serait rejeté), et `PluginCompatibilityMap` construit
> un `System.Version` à quatre composants à partir de ces valeurs. Le harnais de chargement
> vérifie ce point à chaque exécution.

## Personnalisation

- **Auteur / nom / description** : `NINA.Plugin.ExposureBudget\Properties\AssemblyInfo.cs`
  (`AssemblyCompany` = auteur affiché par N.I.N.A., `AssemblyTitle` = nom du plugin,
  `AssemblyDescription` / `LongDescription` = textes affichés dans Options → Plugins).
- **GUID du plugin** : à changer si le plugin est publié sur un dépôt de plugins.
- **Libellés et langues** : tout est dans `Localization\` (`IPluginStrings` + une classe par
  langue). Les vues ne contiennent aucun texte en dur : elles sont liées à `Labels`.
- **Clé du DataTemplate** : `DataTemplates.xaml` doit garder la clé
  `NINA.Plugin.ExposureBudget.ViewModel.ExposureBudgetVM_Dockable` — c'est la convention
  `<Type complet du ViewModel>_Dockable` recherchée par `NINA.Utility.AvalonDock.PaneTemplateSelector`.
