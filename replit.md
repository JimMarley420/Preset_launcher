# Lanceur de Presets - Application WinUI 3 Windows

## Vue d'ensemble

Application de bureau Windows moderne utilisant Windows UI Library 3 (WinUI 3) et le Windows App SDK. L'utilisateur peut regrouper ses logiciels favoris en presets et les lancer en un clic.

## Fonctionnalites principales

- **Gestion de Presets** : Creer, modifier, dupliquer, supprimer des presets
- **Ajout d'executables** : Par selection de fichier ou depuis les processus actifs
- **Lancement** : Executer tous les programmes d'un preset avec delais configurables
- **Options par executable** : Execution en tant qu'administrateur, delai de lancement
- **Historique** : Suivi des executions avec date/heure/duree
- **Statistiques** : Programmes les plus utilises, nombre de lancements
- **Import/Export** : Sauvegarder et partager des presets en JSON
- **Verrouillage** : Protection par mot de passe des presets sensibles
- **Multilingue** : Francais et Anglais
- **Themes** : Clair, Sombre, ou automatique selon le systeme

## Structure du projet

```
PresetLauncherWinUI3/
├── App.xaml                    # Point d'entree de l'application
├── MainWindow.xaml             # Fenetre principale avec NavigationView
├── Models/                     # Modeles de donnees
│   ├── Preset.cs              # Modele preset
│   ├── ExecutableItem.cs      # Modele executable
│   ├── ExecutionHistory.cs    # Historique d'execution
│   ├── AppSettings.cs         # Parametres application
│   └── AppData.cs             # Donnees application
├── Services/                   # Services metier
│   ├── DataService.cs         # Gestion sauvegarde/chargement
│   ├── SettingsService.cs     # Gestion parametres
│   ├── LauncherService.cs     # Lancement des programmes
│   ├── LocalizationService.cs # Traductions
│   ├── ThemeService.cs        # Gestion des themes
│   └── PasswordService.cs     # Hachage mots de passe
├── Views/                      # Interface utilisateur
│   ├── Pages/                 # Pages de navigation
│   │   ├── PresetsPage.xaml   # Liste des presets
│   │   ├── HistoryPage.xaml   # Historique
│   │   ├── StatisticsPage.xaml# Statistiques
│   │   └── SettingsPage.xaml  # Parametres
│   └── Dialogs/               # Dialogues modaux
│       └── PresetEditorDialog.xaml
└── app.manifest               # Manifest Windows
```

## Technologies

- .NET 8.0 Windows
- WinUI 3 (Windows UI Library 3)
- Windows App SDK 1.5
- Newtonsoft.Json (serialisation)

## Compilation (sur Windows uniquement)

WinUI 3 ne peut etre compile que sur Windows. Telechargez le dossier PresetLauncherWinUI3/ et executez:

```bash
cd PresetLauncherWinUI3
dotnet restore
dotnet build -c Release
```

L'executable sera dans `bin/Release/net8.0-windows10.0.19041.0/`

## Publication

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## Sauvegarde des donnees

Les donnees sont stockees en JSON dans `%LOCALAPPDATA%\PresetLauncher\`:
- `settings.json` : Parametres de l'application
- `data.json` : Presets et historique

## Notes de developpement

- Interface native Windows 11 avec NavigationView
- Composants WinUI 3 natifs (ToggleSwitch, ContentDialog, etc.)
- Interface en francais par defaut, anglais disponible
- Theme automatique selon les parametres Windows
- Application non-packagée (pas de MSIX)

## Limitations Replit

Ce projet WinUI 3 ne peut PAS etre compile sur Replit car:
- Le compilateur XAML (XamlCompiler.exe) est un executable Windows
- Le Windows App SDK necessite un environnement Windows

Pour compiler: telechargez le projet et compilez-le sur votre machine Windows avec Visual Studio 2022 ou le .NET SDK 8.
