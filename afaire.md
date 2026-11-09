# Exposure Budget — améliorations à faire

Liste de travail des évolutions identifiées, avec l'effort, l'apport attendu et les notes de
mise en œuvre (fichiers et API concernés). Le contexte technique complet est dans
[`digest.md`](digest.md) ; ce fichier ne contient que **ce qui reste à faire**.

**Comment l'utiliser** : cocher `[x]` quand c'est fait, déplacer la ligne dans « Déjà réalisé »,
et ajouter un incrément de version dans le README (la version se change uniquement dans
`Properties\AssemblyInfo.cs`). Chaque tâche terminée doit s'accompagner des vérifications
correspondantes dans `tools\verify` et/ou `tools\smoketest`, puis d'un `.\tools\build.ps1 -Install`.

Légende : **Priorité** (1 = à faire en premier) · **Effort** (faible / moyen / élevé) ·
**Apport** (ce que l'utilisateur y gagne).

## Vue d'ensemble

Chaque amélioration en une ou deux lignes, avec sa priorité, son effort et son apport.
Le détail (mise en œuvre, vérifications, décisions) suit dans les sections numérotées.

- [ ] **1. Tableau comparatif des temps de pose** — affiche, pour la nuit saisie, ce que
  donneraient plusieurs durées d'exposition (120 s → 176 poses, 180 s → 117, 300 s → 70…).
  On choisit sa durée d'après le nombre de poses visé, au lieu de tâtonner. *(priorité 1 · effort faible · apport très élevé)*
- [x] **2. Surcharge par pose** — ajoute à chaque pose le temps qu'elle consomme réellement en
  plus (téléchargement, dither, stabilisation : souvent 2 à 8 s). Le calcul cesse de surestimer
  la nuit de 5 à 10 %. *(priorité 1 · effort faible · apport très élevé · **fait en 1.5.0.0**)*
- [x] **3. Temps à réserver** — déduit de la fenêtre les opérations inévitables : flip au
  méridien, autofocus toutes les N poses, marge fixe. Le nombre annoncé tient compte de ce qui
  ne prendra pas de poses. *(priorité 2 · effort moyen · apport élevé · **fait en 1.5.0.0**)*
- [ ] **4. Élément de séquence « Exposure Budget »** — une instruction dans le séquenceur qui
  calcule le nombre de poses à l'exécution (nuit et cible en cours) et alimente la prise de
  vues. Plus besoin de saisir un nombre à la main avant chaque nuit. *(priorité 2 · effort élevé · apport très élevé)*
- [ ] **5. Préremplissage depuis la séquence ou la caméra** — deux boutons qui vont chercher le
  temps de pose courant (caméra connectée, ou instruction de la cible active) pour éviter toute
  ressaisie. *(priorité 3 · effort moyen · apport élevé)*
- [ ] **6. Publier le résultat comme symbole de séquence** — le nombre de poses devient un
  symbole (`ExposureBudget_Count`…) réutilisable dans les expressions et conditions du
  séquenceur. Le calcul devient utilisable partout dans N.I.N.A. *(priorité 3 · effort faible · apport moyen)*
- [ ] **7. Option « ne jamais dépasser la fenêtre »** — une case à cocher qui arrondit à la pose
  inférieure au lieu du plus proche, pour être certain de finir avant l'heure de fin. La règle
  du 0,5 reste le comportement par défaut. *(priorité 3 · effort faible · apport moyen)*
- [ ] **8. Confort (reste)** — langues supplémentaires et remise à zéro des réglages. Le bouton
  « Maintenant » et la copie du résultat sont **faits** (1.2.0.0) ; le compte à rebours a été
  écarté. *(priorité 4 · effort faible · apport moyen)*
- [ ] **9. Diffusion** — publication sur un dépôt de plugins N.I.N.A. (dépôt GitHub, manifeste
  avec empreinte, paquet zip, tests rejoués en CI, captures d'écran). Utile seulement si le
  plugin est partagé. *(priorité 4 · effort moyen · apport élevé si partagé)*
- [ ] **10. Finitions** — image en 512 px pour les écrans à forte densité, variante transparente
  si vous fournissez un PNG avec canal alpha, option de sauvegarde des réglages à la
  désinstallation. *(priorité 5 · effort faible · apport faible)*
- [x] **11. Plan par filtres (caméra mono)** — un second onglet où l'on coche les filtres à
  utiliser (L, R, G, B, S, H, O), avec pour chacun un temps de pose et un **pourcentage de la
  nuit** ; le plugin donne le nombre de poses par filtre et **refuse** le plan si le total des
  poses arrondies dépasse la fenêtre. *(priorité 1 · **fait en 1.4.0.0**, les trois lots)
  reste le lot 3 : copier le plan, attribuer le reliquat, gérer la liste des filtres)*

---

## 1. Tableau comparatif des temps de pose

**Objectif** : pour la fenêtre courante, montrer côte à côte le nombre de poses obtenu avec
plusieurs temps de pose, afin de **choisir** sa durée d'exposition au lieu de la subir.

**Comportement** : sous le résultat principal, un tableau `temps de pose → poses → temps
couvert → reliquat`, la ligne du temps de pose courant mise en évidence. Durées candidates :
soit une liste fixe (30, 60, 120, 180, 240, 300, 600, 900 s), soit « les diviseurs utiles » de
la fenêtre. Tient compte de la surcharge (tâche 2) et des temps réservés (tâche 3) s'ils existent.

**Mise en œuvre** : nouvelle classe pure `Model\ExposureComparison.cs` (liste de lignes) →
`ExposureBudgetVM` expose `IReadOnlyList<ExposureComparisonRow> Comparison` → `ItemsControl`
dans `View\ExposureBudgetView.xaml` (styles N.I.N.A. implicites) → libellés à ajouter dans
`Localization\` (en-têtes de colonnes, 2 × ~5 chaînes).

**Vérifications** : cas dans `tools\verify` (candidats, arrondi, reliquat, fenêtre nulle) +
contrôle d'affichage dans `tools\smoketest` (le tableau est bien rendu, lignes non vides).

**Décisions** : liste fixe ou calculée ? Faut-il pouvoir cliquer une ligne pour adopter ce temps
de pose (recommandé : oui, un clic remplit le champ « Exposure ») ?

## 2. Surcharge par pose

**Objectif** : tenir compte du temps réellement consommé par pose en plus de la pose
elle-même : téléchargement, dither, stabilisation du guidage (typiquement 2 à 8 s).

**Comportement** : nouveau réglage « Overhead » (ascenseur, 0 à 60 s, pas de 1 s). Le calcul
devient `N = ΔT / (pose + surcharge)`. L'interface affiche la **durée effective par pose** et le
détail du calcul la mentionne ; le temps couvert reste exprimé en poses × durée effective.

**Mise en œuvre** : `ExposureBudgetCalculator.Compute(window, exposure, overhead)` +
`ExposureCountResult` enrichi (`ExposureSeconds`, `OverheadSeconds`, `EffectiveSeconds`) ;
réglage `overheadSeconds` dans `ExposureBudgetSettings` ; libellés dans `Localization\`.

**Vérifications** : `tools\verify` — nouveaux cas avec surcharge (dont un cas de bascule à
0,5 exact), non-régression des cas sans surcharge ; `tools\smoketest` — liaison de l'ascenseur.

**Décision** : la surcharge s'applique-t-elle à **toutes** les poses (y compris la dernière,
qui n'est souvent suivie d'aucun dither) ? Recommandé : oui pour la simplicité, en le
documentant.

## 3. Temps à réserver

**Objectif** : ne pas promettre des poses qui ne tiendront pas à cause des opérations
inévitables de la nuit.

**Comportement** : trois réglages facultatifs, dans une zone repliable :
flip au méridien (minutes), autofocus toutes les N poses (secondes par exécution), marge fixe
(minutes). La fenêtre **exploitable** = fenêtre − réserves (le nombre de poses est calculé sur
la fenêtre exploitable, et le tableau indique le temps réservé). Avertissement si les réserves
dépassent la fenêtre.

**Mise en œuvre** : `Model\ExposureBudgetReserves.cs` (calcul des réserves en fonction du nombre
de poses — itératif : les réserves dépendent du nombre d'autofocus, qui dépend du nombre de
poses) ; `View\ExposureBudgetView.xaml` — `Expander` (style `DownExpander` de N.I.N.A.) ;
réglages `flipReserveMinutes`, `autofocusEvery`, `autofocusDurationSeconds`, `fixedReserveMinutes`.

**Vérifications** : `tools\verify` — cas avec réserves (dont réserves > fenêtre, 0 autofocus,
valeurs négatives bornées).

## 4. Élément de séquence « Exposure Budget »

> **État : à faire.** Ce qui suit décrit précisément ce qui est prévu, pour pouvoir
> l'implémenter plus tard sans avoir à réétudier le séquenceur de N.I.N.A.

**En une phrase** : une instruction à insérer dans le séquenceur qui calcule le nombre de poses
**au moment où la séquence s'exécute** (nuit et cible en cours) et l'injecte dans la prise de
vues, au lieu de le saisir à la main avant chaque séance.

### 4.1 Le problème que ça résout

Le panneau actuel est une **calculette** : on lit un nombre, puis on le recopie dans
l'instruction « Take Exposures » de la séquence. Ce nombre est juste au moment où on le lit,
mais il dépend de la nuit : heure de départ réelle (mise en station, flip, nuages), heure de fin
réelle (crépuscule qui avance ou recule de plusieurs minutes par nuit), et temps de pose de la
cible. Résultat, deux dérives classiques :

- **trop peu de poses** : la séquence se termine avant l'aube et la nuit est perdue ;
- **trop de poses** : la séquence continue après le début du crépuscule, et les dernières poses
  sont inutilisables (ciel qui s'éclaircit), voire le pointage part sur un ciel déjà bleu.

### 4.2 Avant / après

| | Séquence | Nuit du 10/10 (21:30 → 03:15) |
|---|---|---|
| Aujourd'hui | `Take Exposures` 100 × 300 s | la séquence dépasse l'aube : ~13 poses perdues, il faut recalculer et ressaisir chaque nuit |
| Avec l'instruction | `Exposure Budget` → `Take Exposures` | l'instruction calcule **69** et la séquence s'arrête exactement à 03:15 |

### 4.3 Ce que fait l'instruction, concrètement

**Ce que l'utilisateur règle dans le bloc** (champs de la carte dans le séquenceur) :

| Champ | Valeurs | Défaut proposé |
|---|---|---|
| Fin de la fenêtre (Hf) | heure fixe `hh:mm`, ou **aube astronomique de N.I.N.A.**, ou fin de nuit nautique | aube astronomique (replis déjà écrits pour le panneau) |
| Début | « maintenant », ou heure fixe | maintenant |
| Temps de pose | depuis la cible (prise de vues suivante), ou saisi dans le bloc | depuis la cible |
| Réserves / surcharge | réutilise les tâches 2 et 3 (surcharge par pose, flip, autofocus) | aucun |
| Action | publier un symbole, renseigner la prise de vues suivante, ou seulement journaliser | publier un symbole **et** renseigner la prise de vues |
| Si 0 pose | arrêter la cible, ou continuer | arrêter la cible |

**À l'exécution**, le bloc :

1. lit l'heure courante (et la date de nuit de N.I.N.A. : une séance consultée le matin
   appartient à la nuit précédente) ;
2. résout Hf : heure fixe du bloc, ou crépuscule/aube calculés par `INighttimeCalculator` pour
   le profil d'observatoire actif ;
3. calcule la fenêtre `Hf − maintenant`, applique la surcharge et les réserves, puis la **même
   règle d'arrondi du 0,5** que le panneau (source unique : `ExposureBudgetCalculator`) ;
4. publie le résultat : symbole (`ExposureBudget_Count`) et/ou `ExposureCount` de la prise de
   vues suivante ;
5. écrit une ligne de statut dans le séquenceur et dans le journal de N.I.N.A.
   (« Exposure Budget : fenêtre 05:45:00, pose 300 s → 69 poses ») ;
6. si le résultat est 0 : arrête la cible avec un statut explicite (jamais une exception).

### 4.4 Mise en œuvre technique

- **L'entité** : `SequenceItem\ExposureBudgetInstruction.cs`, exportée en MEF comme les items de
  N.I.N.A. :
  `[Export(typeof(ISequenceItem))]` + `[ExportMetadata("Name"/"Description"/"Icon"/"Category", …)]`.
  Le plus court est de dériver de `NINA.Sequencer.SequenceItem.SequenceItem` (comme le fait
  `TakeExposure : SequenceItem, IExposureItem, IValidatable`), qui fournit déjà `Name`,
  `Description`, `Category`, `Icon`, `Status`, `Clone` et la gestion du parent.
- **Métadonnées** : `Name`/`Description`/`Category` sont passés à `Loc` s'ils commencent par
  `Lbl_`, sinon affichés tels quels (donc on peut fournir nos propres chaînes traduites) ;
  `Icon` doit être la **clé d'une géométrie présente dans le dictionnaire de ressources fusionné**
  du plugin — le chargeur fait `item.Icon = (GeometryGroup)resourceDictionary[icon]`, il faut donc
  ajouter cette géométrie (SVG converti) dans `DataTemplates.xaml`.
- **Constructeur sans paramètre** : indispensable, car la fabrique du séquenceur et
  `SequenceJsonConverter` instancient les items par réflexion ; les services passent par des
  imports MEF (`[ImportingConstructor]` sur un second constructeur, ou propriétés
  `[Import(AllowDefault = true)]`). Le chargeur compose déjà : `IProfileService`,
  `INighttimeCalculator`, `ISymbolBroker`, `ISequenceMediator`, `ICameraMediator`,
  `IApplicationStatusMediator`…
- **Écrire dans la prise de vues suivante** : le bloc est dans un conteneur séquentiel ; on
  retrouve l'instruction suivante via `Parent` / les items du conteneur (ou
  `ISequenceMediator`), et on renseigne `IExposureItem.ExposureCount`. Comme les propriétés de
  `TakeExposure` sont **à base d'expressions** en N.I.N.A. 3.x, la voie la plus robuste reste de
  publier un **symbole** et de laisser l'utilisateur écrire l'expression dans le champ — à
  trancher en relisant les sources du séquenceur au moment de l'implémentation.
- **Symbole** : `ISymbolBroker.RegisterSymbolProvider("ExposureBudget")` puis
  `ISymbolProvider.AddOrUpdateSymbol(token, value)` ; ces deux API sont **publiques** (vérifié),
  donc utilisables depuis un plugin.
- **Carte dans l'interface** : un `DataTemplate` dans le dictionnaire du plugin, construit comme
  les items de N.I.N.A. (`SequenceBlockView` + `SequenceBlockView.SequenceItemContent`, et
  `ExprControl` si l'on expose des expressions).
- **Sérialisation** : propriétés publiques get/set ; prévoir plus tard un
  `ISequenceEntityUpgrader` (également composé par le chargeur) si le schéma évolue.

### 4.5 Points de vigilance

- **Ne jamais lever d'exception** dans `Execute` : une exception arrête la séquence. Toute erreur
  (pas de nuit calculable, temps de pose introuvable) doit se traduire par un statut et un
  comportement dégradé.
- Le bloc se place **avant** l'acquisition de la cible ; exécuté en pleine séance, « maintenant »
  donne le temps restant — c'est voulu, à documenter dans l'infobulle.
- Si le temps de pose de la cible n'est pas résolvable, demander la valeur dans le bloc plutôt
  que de deviner.
- Les libellés de la palette sont lus au chargement du plugin : un changement de langue de
  N.I.N.A. demandera un redémarrage pour la palette (comme pour les items de N.I.N.A.
  eux-mêmes) — le panneau, lui, se retraduit à chaud.

### 4.6 Ordre de réalisation conseillé (pour limiter le risque)

1. **Version minimale** : le bloc calcule et **publie un symbole**, écrit un statut et journalise
   — aucun effet de bord sur la séquence, donc testable sans risque.
2. **Puis** « renseigner la prise de vues suivante » (écriture dans `IExposureItem`).
3. **Puis** les options avancées : surcharge et réserves (tâches 2 et 3), arrêt automatique si 0.

### 4.7 Critères d'acceptation

1. le bloc apparaît dans la palette, dans la bonne catégorie, avec un nom, une description et une
   icône **dans les deux langues** ;
2. inséré avant une prise de vues, il calcule à l'exécution et la prise de vues réalise
   **exactement** le nombre annoncé ;
3. Hf vaut par défaut l'**aube astronomique**, avec les replis nautique / civil / coucher du
   Soleil déjà écrits pour le panneau ;
4. 0 pose → la cible s'arrête avec un statut clair, sans exception ;
5. le nombre calculé et la fenêtre apparaissent dans le journal de N.I.N.A. et dans le statut du
   bloc ;
6. la séquence se sauvegarde, se recharge et se clone sans perte des réglages du bloc ;
7. le panneau reste utilisable seul, et **la règle de calcul reste unique** (le bloc appelle le
   même `ExposureBudgetCalculator` que le panneau).

### 4.8 Vérifications à ajouter

- `tools\smoketest` : ajouter `[ImportMany(typeof(ISequenceItem))]` aux imports et contrôler que
  l'instruction est composée par MEF, que `Name`/`Description`/`Category` sont renseignés, que la
  géométrie de l'icône existe dans `Application.Resources` et que le `DataTemplate` de la carte
  est présent ; contrôler que `Clone()` renvoie une instance distincte portant les mêmes
  réglages ; contrôler le chemin de calcul avec un faux `INighttimeCalculator` (fenêtre connue →
  nombre attendu).
- `tools\verify` : la règle elle-même est déjà couverte ; ajouter les cas propres au bloc s'il en
  introduit (par exemple « fenêtre négative → 0 », « surcharge supérieure à la fenêtre »).
- Test manuel : dans N.I.N.A., une séquence d'essai avec Hf = maintenant + 10 min, pour vérifier
  le nombre annoncé, le statut et l'arrêt propre quand il n'y a plus de place.

## 5. Préremplissage depuis la séquence ou la caméra

**Objectif** : éviter toute ressaisie du temps de pose.

**Comportement** : un bouton « Depuis la caméra » (temps de pose courant) et/ou
« Depuis la cible » (temps de pose de l'instruction `TakeExposure` de la cible active).

**Mise en œuvre** : `ICameraMediator` → `GetInfo().ExposureTime` (le chargeur de plugins
compose déjà `cameraMediator` ✓) ; pour la cible, `ISequenceMediator` (composé ✓) →
conteneur actif → recherche de l'`IExposureItem`. Prévoir un repli silencieux (message dans le
panneau) si l'information n'est pas disponible — le plugin doit rester utilisable hors séquence.

**Vérifications** : `tools\smoketest` avec un faux médiateur (temps de pose retourné, caméra
déconnectée → message d'information, pas d'exception).

## 6. Publier le résultat comme symbole de séquence

**Objectif** : rendre le résultat réutilisable partout dans le séquenceur (expressions,
conditions, autres instructions).

**Comportement** : publier par exemple `ExposureBudget_Count`, `ExposureBudget_Window`,
`ExposureBudget_Remaining` ; mis à jour à chaque recalcul du panneau.

**Mise en œuvre** : `ISymbolBroker.RegisterSymbolProvider("ExposureBudget")` (public) →
`ISymbolProvider.AddOrUpdateSymbol(token, value)` ; import optionnel
`[Import(AllowDefault = true)] public ISymbolBroker SymbolBroker` ; appeler dans
`Recalculate()` et lors du `Teardown()` retirer les symboles (`RemoveSymbol`).

**Vérifications** : `tools\smoketest` avec un faux `ISymbolBroker` (vérifier les jetons publiés
et leurs valeurs, y compris après un changement de langue ou d'heure).

## 7. Option « ne jamais dépasser la fenêtre »

**Objectif** : permettre d'être certain de terminer avant Hf, quitte à perdre une pose.

**Comportement** : case à cocher « Ne jamais dépasser la fenêtre » : au lieu de l'arrondi au
plus proche demandé (règle du 0,5), prendre le **plancher** (partie entière). Défaut : décochée,
la règle du 0,5 restant la référence.

**Mise en œuvre** : `ExposureBudgetCalculator.RoundToPoseCount(exact, neverExceed)` ; réglage
`neverExceedWindow` ; une case à cocher dans le panneau ; libellés dans `Localization\`.

**Vérifications** : `tools\verify` — mêmes cas que la règle du 0,5 mais en mode plancher (34,5
→ 34 ; 175,5 → 175 ; compte exact inchangé).

## 8. Confort

| Amélioration | État / note de mise en œuvre |
|---|---|
| Bouton « Maintenant » pour l'heure de début | **fait** (1.2.0.0) — remplit `StartHour`/`StartMinute` depuis `DateTime.Now` ; la date de référence passe au **jour courant** et non à la convention de nuit astronomique, sinon un clic en matinée plaçait la séance la veille |
| Copier le résultat en un clic | **fait** (1.2.0.0, affiné en 1.2.1.0) — `Clipboard.SetText` du **nombre de poses uniquement** (la valeur affichée en grand) + confirmation ; l'échec d'accès au presse-papiers est signalé, jamais par une exception |
| Compte à rebours des poses restantes | **écarté sur demande** — le développement (minuteur, compte des poses restantes, libellés, tests) a été retiré |
| Langues supplémentaires | à faire — une classe `IPluginStrings` de plus + une ligne dans `PluginStrings.Resolve` (l'architecture est prête) |
| Réinitialiser les réglages | à faire — bouton discret « valeurs par défaut » dans le panneau, ou suppression du fichier `ExposureBudget.settings` |

**Vérifications** : `tools\smoketest` — bornage du bouton « Maintenant » (heure courante), contenu
du texte copié, confirmation affichée puis effacée au calcul suivant, libellés français.

## 9. Diffusion (si le plugin est partagé)

1. **Dépôt GitHub** : code, `README.md`, `digest.md`, `afaire.md`, licence (MPL-2.0, comme
   N.I.N.A.), `.gitignore` déjà présent.
2. **Manifeste du dépôt de plugins N.I.N.A.** : entrée JSON conforme au schéma de
   `NINA.Plugin/ManifestDefinition/PluginManifest.cs` — `identifier` (le GUID actuel), `name`,
   `version`, `minimumApplicationVersion`, `author`, `descriptions`, `installer` (URL + type +
   **empreinte SHA-256**), `tags`, `homepage`, `repository`, `changelogURL`.
3. **Paquet** : zip contenant `NINA.Plugin.ExposureBudget.dll` (+ `.pdb`), ou le DLL seul
   (`InstallerType.DLL`) ; l'empreinte doit être régénérée à chaque version.
4. **Captures d'écran** : `ScreenshotURL` (hauteur affichée max **1000 px**) et
   `AltScreenshotURL` (**500 px**) — visibles uniquement sur la page « Plugins disponibles »,
   donc seulement une fois le plugin publié sur un dépôt.
5. **CI** (GitHub Actions) : `dotnet build` + les deux harnais (`tools\verify`,
   `tools\smoketest`) à chaque push ; les harnais renvoient un code de sortie exploitable.
6. **GUID** : le conserver (l'identité du plugin est le GUID ; le dossier d'installation suit
   le **nom**, qui ne doit pas être traduit).

## 10. Finitions

- **Image en 512 px** pour les écrans à forte densité :
  `dotnet run --project tools\makeicon -- --resize image.png NINA.Plugin.ExposureBudget\Resources\PluginImage.png 512`
  (le fichier passe de ~120 à ~450 Ko).
- **Variante transparente de l'image** : si un PNG **avec canal alpha** est fourni, il remplace
  simplement `Resources\PluginImage.png` (l'outil `makeicon` conserve la transparence).
- **`uninstall.ps1 -KeepSettings`** : sauvegarder `ExposureBudget.settings` avant suppression du
  dossier (aujourd'hui il est supprimé avec le dossier, et le panneau repart des valeurs par
  défaut).
- **Icône du panneau** : elle restera vectorielle (l'emplacement n'accepte qu'une géométrie) ;
  aujourd'hui c'est la calculatrice de N.I.N.A. — la remplacer impliquerait de fournir un
  `GeometryGroup` (SVG converti), à faire seulement si c'est demandé.

---

## 11. Plan par filtres (caméra mono)

> **État : fait.** Les trois lots sont réalisés (1.4.0.0) et le plan profite désormais de la
> surcharge par pose et des temps à réserver (1.5.0.0) : le temps consommé d'une pose du plan
> inclut la surcharge, et la fenêtre utilisée est la fenêtre exploitable.
> Origine : caméra mono, filtres L/R/G/B/S/H/O, un temps de pose et un pourcentage du temps
> disponible par filtre, avec vérification que le total tient dans la nuit.

**En une phrase** : un second onglet du panneau où l'on coche les filtres à utiliser, règle pour
chacun un temps de pose et un pourcentage de la nuit, et où le plugin donne le nombre de poses par
filtre en vérifiant que le total ne dépasse pas la fenêtre.

### 11.1 Décisions validées

| # | Sujet | Décision |
|---|---|---|
| 1 | Pourcentages | **entiers, pas de 5** |
| 2 | Σ % < 100 | **bouton « Attribuer le reliquat »** : les points de pourcentage non utilisés vont au filtre sélectionné |
| 3 | Dépassement dû aux arrondis | **refus** : aucun arbitrage automatique, le plan est déclaré invalide avec le dépassement chiffré |
| 4 | Arrondi par filtre | **règle du 0,5**, identique au calcul simple |
| 5 | Structure de l'interface | **deux onglets** : « Pose unique » (existant) et « Plan par filtres » (un troisième onglet, « Temps à réserver », est venu ensuite avec la tâche 3) |
| 6 | Liste des filtres | **liste fixe éditable** (pas de roue à filtres aujourd'hui) : noms modifiables, ajout et suppression de lignes |

### 11.2 Calcul, pour chaque filtre coché

| Étape | Formule |
|---|---|
| Temps alloué | `A = ΔT × p / 100` |
| Nombre exact | `N = A / s` |
| Nombre retenu | `C = règle du 0,5` (même code que l'onglet principal) |
| Temps couvert | `T = C × s` |

**Vérification bloquante** : `Σ T ≤ ΔT` ; sinon le plan est **refusé** avec un message chiffré
(« les poses arrondies dépassent la fenêtre de 00:00:40 — réduisez un pourcentage ou un temps de
pose »). Contrôle de saisie complémentaire : `Σ p ≤ 100 %`.

Exemple (ΔT = 4 h) : L 40 % / 120 s → 48 poses (1 h 36) ; R, G, B 15 % / 120 s → 18 poses chacun
(36 min) ; S 5 % / 300 s → 2 poses ; H 7 % / 300 s → 3 poses ; O 3 % / 300 s → 1 pose. Total :
108 poses, 3 h 54 couvertes, **6 min perdues** par les arrondis (2,40 → 2 et 1,44 → 1) : elles
sont affichées mais non redistribuables avec des pourcentages entiers.

### 11.3 Mise en page

Le bloc « fenêtre d'acquisition » (existant) reste en haut, toujours visible ; dessous, deux
onglets. Contenu de l'onglet « Plan par filtres » :

```
[Tout cocher]  [Répartir 100 %]  [Réinitialiser]
┌───┬──────────┬──────────────┬──────────┬───────┐
│ ✓ │ Filtre   │ Pose (s)     │    %     │ Poses │
├───┼──────────┼──────────────┼──────────┼───────┤
│[x]│ L        │ [−][120][+]  │[−][40][+]│  48   │
│[x]│ R        │ [−][120][+]  │[−][15][+]│  18   │
│[x]│ G        │ [−][120][+]  │[−][15][+]│  18   │
│[x]│ B        │ [−][120][+]  │[−][15][+]│  18   │
│[x]│ S        │ [−][300][+]  │[−][ 5][+]│   2   │
│[x]│ H        │ [−][300][+]  │[−][ 7][+]│   3   │
│[ ]│ O        │ [−][300][+]  │[−][ 3][+]│   –   │
└───┴──────────┴──────────────┴──────────┴───────┘
[+ filtre]  [− filtre]
Σ % : 100 %   ·   108 poses   ·   3 h 54 min
Reste : 6 min                        [Attribuer le reliquat au filtre sélectionné]
                                     [Copier le plan]
```

- % et temps de pose par **ascenseurs** natifs (0–100 pas de 5 ; pose pas de 15 s avec saisie
  libre) ; le nom du filtre est un **champ éditable** (liste fixe), avec `[+ filtre]` / `[− filtre]` ;
- la ligne **Σ** est la zone de validation : rouge si `Σ % > 100 %`, rouge si le temps couvert
  dépasse la fenêtre (refus, bouton de copie désactivé), reliquat toujours affiché ;
- messages **écrits**, pas seulement colorés ;
- un filtre décoché affiche « – » et sort de tous les calculs (ses valeurs restent mémorisées) ;
- « Répartir 100 % » répartit les points entiers entre les filtres cochés (le reste au premier) ;
- « Attribuer le reliquat » donne les points non utilisés (100 − Σ %) au filtre sélectionné ;
- « Copier le plan » copie une ligne par filtre (`L : 48 × 120 s (1 h 36 min)`), dans l'esprit du
  bouton de copie actuel (qui ne copie que le nombre).

### 11.4 Persistance (liste fixe, indexée)

```
plan.filter.count=7
plan.filter.0.name=L
plan.filter.0.enabled=true
plan.filter.0.exposure=120
plan.filter.0.percent=40
...
plan.rounding=half-up
```

Indexation par **position** et non par nom, pour qu'un renommage ne perde pas les réglages ;
`settingsVersion` incrémenté (les fichiers existants prennent les valeurs par défaut).

### 11.5 Architecture

- `Model\FilterPlanEntry.cs` + `Model\FilterPlanCalculator.cs` → `FilterPlanResult` (lignes,
  totaux, validations) : **pur, sans WPF ni N.I.N.A.**, testé dans `tools\verify` ;
- `ViewModel\FilterPlanRowVM.cs` (une ligne notifiante) + extension de `ExposureBudgetVM`
  (liste, totaux, commandes) ; recalcul branché au même endroit que le reste, donc les boutons
  **Now** et **Nuit** profitent immédiatement au plan ;
- `View\ExposureBudgetView.xaml` : `TabControl` (styles natifs disponibles) + onglet filtres ;
- ~12 chaînes dans `Localization\` (anglais/français), messages de validation compris ;
- **critère d'architecture** : un filtre unique à 100 % doit donner exactement le même nombre que
  l'onglet « Pose unique » (même `ExposureBudgetCalculator`, une seule source de vérité).

### 11.6 Lots

| Lot | Contenu | Effort | État |
|---|---|---|---|
| 1 | modèle pur, calcul par filtre, totaux, validations, onglet avec liste fixe | ~½ journée | **fait** (1.3.0.0) |
| 2 | édition (cases, ascenseurs de pose et de %), boutons, validations visibles, persistance | ~1 journée | **fait** (1.3.0.0) |
| 3 | « Copier le plan », « Attribuer le reliquat », renommer / ajouter / supprimer des filtres | ~½ journée | **fait** (1.4.0.0) |

Effort total ≈ 2 journées. Risque principal : densité de l'interface dans un panneau étroit
(atténué par les onglets et les ascenseurs) ; risque secondaire : le cas « refus » doit être
impossible à manquer.

---

## Décisions à prendre

| Sujet | Question | Recommandation |
|---|---|---|
| Surcharge | s'applique-t-elle à la dernière pose ? | oui, pour rester simple et prévisible |
| Arrondi | garder la règle du 0,5 par défaut et proposer le plancher en option ? | oui (tâche 7) |
| Comparatif | liste de durées fixe ou calculée ? | fixe au départ, filtrable plus tard |
| Séquence | écrire le résultat dans un symbole ou directement dans `TakeExposure.ExposureCount` ? | symbole d'abord (plus simple et réutilisable), puis intégration directe si utile |
| Nuit astronomique | quel crépuscule utiliser par défaut ? | astronomique (déjà le cas), avec repli indiqué |

## Déjà réalisé

- **1.5.0.0** — **réalisme du calcul** : **surcharge par pose** (temps consommé = exposition +
  surcharge, réglée de 1 s en 1 s, appliquée aussi au plan par filtres) et **temps à réserver**
  (retournement au méridien, autofocus toutes les N poses avec itération, marge fixe) déduits de
  la fenêtre, avec nouvel onglet « Temps à réserver », résumé « réservé · exploitable » et
  avertissement quand les réserves dépassent la fenêtre. 24 vérifications pures et 16 vérifications
  d'interface ajoutées.

- **1.4.0.0** — **plan par filtres, lot 3** : bouton **>** par ligne (attribue les points de
  pourcentage non utilisés au filtre), **nom de filtre modifiable** dans la ligne, **+ filtre** /
  **- filtre**, et **Copier le plan** (une ligne par filtre + totaux), désactivé tant que le plan
  est refusé. Quatorze vérifications ajoutées (reliquat, copie, renommage, ajout, suppression,
  copie bloquée sur plan refusé).
- **1.3.2.0** — onglet « Plan par filtres » lisible : les **noms des filtres** s'affichent de
  nouveau (le style implicite de N.I.N.A. est un interrupteur de 65 px qui n'affiche pas son
  contenu → **petite case à cocher de 20 px définie par le plugin**, nom dans sa propre colonne),
  colonnes 22 / 44 / 92 / 84 / reste, et **temps de pose réglé de 5 s en 5 s** dans le plan
  (l'onglet principal garde 15 s). Le harnais vérifie les noms affichés, la taille des cases, le
  pas de 5 s et l'absence de chevauchement.
- **1.3.1.0** — correctif d'affichage de l'onglet « Plan par filtres » : la case à cocher de
  N.I.N.A. mesure 65 × 25 px (style `StandardCheckBox`) et le nom du filtre se superposait à
  elle. Le nom est désormais **dans** la case à cocher (un seul contrôle, chevauchement
  impossible), colonnes rééquilibrées (74 / 92 / 84 / reste) et **contrôle automatique** ajouté
  au harnais : les éléments d'une ligne de filtre ne doivent pas se chevaucher.
- **1.3.0.0** — **plan par filtres** (lots 1 et 2) : second onglet « Plan par filtres », sept
  filtres par défaut (L, R, G, B, S, H, O), cases à cocher, temps de pose et pourcentages
  éditables, nombre de poses par filtre, totaux et reliquat, **refus** si `Σ % > 100 %` ou si les
  poses arrondies dépassent la fenêtre, boutons *Tout cocher* / *Répartir 100 %* / *Réinitialiser*,
  plan mémorisé dans les réglages. Logique pure testée (`FilterPlanCalculator`) et interface
  vérifiée (onglet, sept lignes, validations).
- **1.2.0.0 / 1.2.1.0** — confort : bouton **« Maintenant »** (heure de début = heure courante,
  date du jour) et bouton **« Copier le résultat »** (le **nombre de poses** dans le
  presse-papiers, avec confirmation). Vérifications ajoutées aux deux harnais.
- **1.1.0.0** — renommage en « Exposure Budget », fiche du plugin traduite **et corrigée**
  (visible par les liaisons WPF), image du plugin, langue lue dans le profil actif avec
  retraduction à chaud, migration des réglages (`settingsVersion`), journalisation de la langue.
- **1.0.0.0** — fenêtre d'acquisition `hh:mm` avec passage de minuit, temps de pose par pas de
  15 s avec équivalence en minutes, calcul en direct du nombre de poses (règle du 0,5), temps
  couvert / reliquat / dépassement, horodatage de la dernière pose, avertissements (fenêtre
  nulle, pose trop longue), bouton « Nuit astronomique » et ses replis, interface bilingue
  suivant N.I.N.A., mémorisation des réglages, 92 + 147 vérifications automatisées.

## Hors périmètre (écarté volontairement)

- **Compte à rebours des poses restantes** : développé (minuteur sur le dispatcher de
  l'application, compte des poses restantes, libellés, tests), puis **retiré à la demande**.
  Le calcul sous-jacent a aussi été supprimé du modèle et des harnais — à ne pas reproposer.
- **Planification multi-cibles** (répartir la fenêtre entre plusieurs cibles ou filtres) :
  c'est le métier de *Target Scheduler* ; le plugin reste un outil de calcul pour une cible.
- **Optimisation du temps de pose par le rapport signal/bruit** : nécessite des hypothèses
  photométriques hors du cadre de l'outil.
- **Prise en compte de la hauteur de la cible ou du méridien** : N.I.N.A. et les plugins de
  séquence le gèrent déjà ; la fenêtre saisie reste la seule référence.
- **Page d'options dédiée** : le panneau et le fichier de réglages suffisent.
