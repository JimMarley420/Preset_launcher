<div align="center">

# PresetLauncher

**Lanceur de presets pour Windows**

Organisez vos programmes en groupes et lancez-les en un clic.

[![GitHub release](https://img.shields.io/github/v/release/Wailroth/PresetLauncher?style=flat-square&logo=github)](https://github.com/Wailroth/PresetLauncher/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/Windows-10%2B-blue?style=flat-square&logo=windows)](https://www.microsoft.com/windows/)

</div>

---

## A propos

**PresetLauncher** est une application Windows moderne construite avec WinUI 3 qui permet de grouper vos executables favoris en "presets" et de les lancer tous en une seule action. Fini de lancer une par une vos applications au demarrage !

## Capture d'ecran

<img width="856" height="628" alt="image" src="https://github.com/user-attachments/assets/4bc92444-c889-46ad-932b-5ef402787a71" />


## Fonctionnalites

### Gestion de presets
- Creer, modifier, dupliquer et supprimer des presets
- Ajouter des executables via le selecteur de fichier ou depuis les processus en cours
- Definir des arguments de lancement, des delais entre les programmes, et l'execution en tant qu'admin
- Importer et exporter des presets en JSON
- Verrouiller un preset avec un mot de passe

### Lancement en masse
- **Lancer tout** : demarrer tous les executables d'un preset
- **Lancer les manquants** : ignorer les programmes deja en cours d'execution
- Detection automatique des processus en cours via les API Windows (`EnumProcesses`, `QueryFullProcessImageName`)
- Fermeture gracieuse des processus d'un preset

### Historique et statistiques
- Historique complet de chaque lancement avec succes/echecs et duree
- Filtres : Tous, Succes, Echecs, Aujourd'hui
- Tableau de bord avec KPI et classement des programmes les plus utilises

### Interface moderne
- Design Windows 11 avec support Mica et Acrylic
- Barre de titre personnalisee
- Themes : Clair, Sombre, Systeme
- Localisation Francais et Anglais

### Mise a jour automatique
- Verification automatique des mises a jour au demarrage
- Telechargement et installation directe depuis l'application

## Pre-requis

- **Windows 10** version 2004 (build 19041) ou superieure
- **.NET 8 Desktop Runtime** (inclus dans l'installation autonome)

## Installation

### Via l'installeur (recommande)

1. Telecharger la derniere version depuis la page [Releases](https://github.com/Wailroth/PresetLauncher/releases/latest)
2. Lancer l'installateur `PresetLauncher-x.x.x-setup.exe`
3. Suivre les instructions d'installation

### Compilation depuis les sources

```bash
# Cloner le depot
git clone https://github.com/Wailroth/PresetLauncher.git
cd PresetLauncher

# Compiler
dotnet build PresetLauncherWinUI3\PresetLauncherWinUI3.csproj --configuration Release

# Publier (autonome, pret a executer)
dotnet publish PresetLauncherWinUI3\PresetLauncherWinUI3.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:PublishReadyToRun=true `
    --output publish/win-x64
```

### Creer l'installeur

```powershell
# Necessite Inno Setup 6 installe
.\Installer\build.ps1 -Version "x.x.x"
```

## Structure du projet

```
PresetLauncher/
├── PresetLauncherWinUI3/          # Application principale (WinUI 3)
│   ├── Models/                    # Modeles de donnees
│   ├── Services/                  # Logique metier
│   ├── Views/
│   │   ├── Dialogs/               # Editeur de preset
│   │   └── Pages/                 # Presets, Historique, Statistiques, Reglages
│   └── App.xaml                   # Point d'entree et ressources globales
├── Installer/                     # Scripts Inno Setup et build
│   ├── PresetLauncher.iss
│   └── build.ps1
└── PresetLauncher.RealeaseMaker/  # Outil de gestion des releases
```

## Technologies

| Composant | Technologie |
|-----------|-------------|
| UI | WinUI 3 (Windows App SDK 1.8) |
| Framework | .NET 8.0 |
| Langage | C# 12 |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| Serialisation | Newtonsoft.Json 13.0 |
| Installeur | Inno Setup 6 |
| Mode de deploiement | Autonome (sans MSIX) |

## Donnees utilisateur

Les donnees sont stockees dans `%LocalAppData%\PresetLauncher\` :
- `settings.json` : preferences utilisateur
- `data.json` : presets, historique, statistiques

## Contribuer

Les contributions sont bienvenues ! Veuillez ouvrir une [issue](https://github.com/Wailroth/PresetLauncher/issues) pour signaler un bug ou suggerer une amelioration.

1. Fork le depot
2. Creer une branche (`git checkout -b feature/nouvelle-fonctionnalite`)
3. Commiter vos changements (`git commit -m 'Ajouter nouvelle fonctionnalite'`)
4. Pusher (`git push origin feature/nouvelle-fonctionnalite`)
5. Ouvrir une Pull Request

## Bugs et suggestions

Si vous rencontrez un bug ou avez une idee d'amelioreation, n'hesitez pas a ouvrir une issue sur le [depot GitHub](https://github.com/Wailroth/PresetLauncher/issues).

## Licence

Ce projet est sous licence MIT. Voir le fichier [LICENSE](LICENSE) pour plus de details.
