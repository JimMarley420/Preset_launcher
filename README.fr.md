# PresetLauncher

Lancez ensemble les applications Windows que vous utilisez régulièrement, sans les ouvrir une par une.

[English](README.md) · **Français**

[![Version](https://img.shields.io/github/v/release/JimMarley420/Preset_launcher?style=flat-square)](https://github.com/JimMarley420/Preset_launcher/releases/latest) [![Licence MIT](https://img.shields.io/badge/licence-MIT-lightgrey?style=flat-square)](LICENSE)

[Télécharger pour Windows](https://github.com/JimMarley420/Preset_launcher/releases/latest) · [Signaler un problème](https://github.com/JimMarley420/Preset_launcher/issues/new/choose) · [Compiler le projet](#compiler-le-projet)

![Fenêtre de PresetLauncher](https://github.com/user-attachments/assets/4bc92444-c889-46ad-932b-5ef402787a71)

## Un preset, plusieurs applications

Un preset est une liste de programmes nommée. Créez-en un pour le travail, un autre pour jouer ou pour les outils que vous ouvrez chaque matin.

- Ajoutez des programmes via le sélecteur de fichiers ou en choisissant un processus Windows existant. Le sélecteur accepte les exécutables, scripts et raccourcis ; Windows détermine comment ouvrir le fichier.
- Recherchez les presets par nom ou description, modifiez leur contenu, importez un preset JSON ou supprimez-le.
- Choisissez un délai et une demande éventuelle de droits administrateur pour chaque entrée.

### Options de lancement

| Action | Comportement |
| --- | --- |
| Lancer tout | Lance les entrées dans leur ordre enregistré, applique chaque délai avant le lancement et enregistre le résultat. |
| Lancer les manquants | Ignore les programmes détectés par leur chemin ou nom de fichier. Ce mode n'applique pas les délais et ne crée pas d'entrée dans l'historique. |
| Fermer les processus | Après confirmation, tente de fermer les processus correspondants, puis peut les arrêter de force. Enregistrez votre travail : cela peut aussi fermer des programmes ouverts hors de PresetLauncher. |

### Historique et réglages

L'historique affiche les dates, durées et nombres de démarrages réussis des lancements complets. Il peut être effacé. Un démarrage réussi signifie que Windows a accepté la demande, pas que l'application a fini de charger.

Les statistiques indiquent le nombre de presets, les lancements complets enregistrés et les dix chemins de programmes les plus utilisés. Les réglages proposent le français et l'anglais, les thèmes clair/sombre/système, Mica ou Acrylique selon la compatibilité, le nettoyage des entrées invalides, la réinitialisation et la recherche de mises à jour.

### Limitations actuelles

Les filtres d'historique et la préférence de confirmation de lancement sont présents mais ne modifient pas encore le comportement. La duplication, l'export individuel, la modification des arguments et le verrouillage par mot de passe ne sont pas accessibles via des commandes fonctionnelles dans cette version. Un preset JSON importé peut contenir des arguments transmis à Windows lors du lancement.

## Installation

Utilisez un PC Windows 10 ou 11 compatible avec l'installeur x64. L'installeur déclare Windows 10 build 17763 comme minimum ; le projet cible le build 19041. La compatibilité avec les anciens builds n'a pas été vérifiée.

1. Ouvrez la page de la [dernière version](https://github.com/JimMarley420/Preset_launcher/releases/latest).
2. Téléchargez `PresetLauncher_Setup_<version>.exe` et exécutez-le.
3. Ouvrez PresetLauncher, créez un preset et ajoutez les exécutables souhaités.

L'installeur inclut l'environnement d'exécution. L'application recherche les mises à jour au démarrage et dans les réglages ; leur installation nécessite une confirmation. Elles proviennent de `version.multichat.cloud`, et non directement de GitHub Releases.

## Données locales

Par défaut, les fichiers se trouvent dans `%LocalAppData%\PresetLauncher\` :

- `data.json` — presets, historique et statistiques.
- `settings.json` — préférences de l'application.

La désinstallation conserve ces données. Sauvegardez manuellement les deux fichiers, application fermée, avant de changer de PC ou de réinitialiser les données. Le bouton Parcourir tente de copier les données mais ne déplace pas le stockage actif ; ne comptez pas dessus pour une sauvegarde. Avant de lancer un preset importé, vérifiez les chemins, arguments et demandes de droits administrateur.

## Compiler le projet

Sous Windows, installez le SDK .NET 8 et Visual Studio 2022 avec les outils de développement WinUI / Windows App SDK. Dans PowerShell :

```powershell
git clone https://github.com/JimMarley420/Preset_launcher.git
cd Preset_launcher
dotnet build .\PresetLauncherWinUI3\PresetLauncherWinUI3.csproj -c Release -p:Platform=x64
```

Pour créer l'installeur autonome x64, installez Inno Setup 6 puis lancez :

```powershell
.\Installer\build.ps1
```

L'installeur est généré dans `Installer/Output/`. Les [notes de développement](docs/DEVELOPMENT.md) décrivent l'organisation du code et les vérifications manuelles.

## Contribuer

Pour signaler un bug ou proposer une modification, consultez [CONTRIBUTING.md](CONTRIBUTING.md). Pour une vulnérabilité, suivez plutôt les instructions de [SECURITY.md](SECURITY.md).

PresetLauncher est distribué sous [licence MIT](LICENSE).
