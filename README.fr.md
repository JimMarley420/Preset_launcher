# PresetLauncher

Lancez ensemble les applications Windows que vous utilisez régulièrement, sans les ouvrir une par une.

[English](README.md) · **Français**

[![Version](https://img.shields.io/github/v/release/JimMarley420/Preset_launcher?style=flat-square)](https://github.com/JimMarley420/Preset_launcher/releases/latest) [![Licence MIT](https://img.shields.io/badge/licence-MIT-lightgrey?style=flat-square)](LICENSE)

[Télécharger pour Windows](https://github.com/JimMarley420/Preset_launcher/releases/latest) · [Signaler un problème](https://github.com/JimMarley420/Preset_launcher/issues/new/choose) · [Compiler le projet](#compiler-le-projet)

![Fenêtre de PresetLauncher](https://github.com/user-attachments/assets/4bc92444-c889-46ad-932b-5ef402787a71)

## Un preset, plusieurs applications

Un preset est une liste de programmes enregistrée. Créez-en un pour le travail, un autre pour jouer, ou pour les outils que vous ouvrez chaque matin. Lancez toute la liste ou uniquement les programmes qui ne sont pas déjà ouverts.

| | Fonctionnalités |
| --- | --- |
| Presets | Créer, dupliquer, importer et exporter des groupes de programmes au format JSON. |
| Options de lancement | Définir les arguments, délais et droits administrateur de chaque programme. |
| Applications ouvertes | Détecter les processus, lancer les programmes manquants et demander leur fermeture. |
| Historique | Consulter les résultats, durées et statistiques d'utilisation. |
| Apparence | Choisir un thème clair, sombre ou système et passer du français à l'anglais. |

Un preset peut être verrouillé par mot de passe dans l'interface. Ce verrouillage ne chiffre pas les fichiers JSON et ne restreint pas l'accès aux exécutables Windows.

## Installation

Utilisez un PC Windows 10 ou 11 compatible avec l'installeur x64. Windows 10 version 2004 ou ultérieure est recommandé.

1. Ouvrez la page de la [dernière version](https://github.com/JimMarley420/Preset_launcher/releases/latest).
2. Téléchargez `PresetLauncher_Setup_<version>.exe` et exécutez-le.
3. Ouvrez PresetLauncher, créez un preset et ajoutez les exécutables souhaités.

L'installeur inclut l'environnement d'exécution. Les mises à jour intégrées proviennent de `version.multichat.cloud`, et non directement de GitHub Releases.

## Données locales

Par défaut, les fichiers se trouvent dans `%LocalAppData%\PresetLauncher\` :

- `data.json` — presets, historique et statistiques.
- `settings.json` — préférences de l'application.

La désinstallation conserve ces données. Sauvegardez-les avant de changer de PC. Avant de lancer un preset importé, vérifiez les chemins, arguments et demandes de droits administrateur.

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
