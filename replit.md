# Lanceur de Presets - Application WPF Windows

## Vue d'ensemble

Application de bureau Windows (.NET 8 WPF) permettant de créer et gérer des presets de lancement de programmes exécutables. L'utilisateur peut regrouper ses logiciels favoris en presets et les lancer en un clic.

## Fonctionnalités principales

- **Gestion de Presets** : Créer, modifier, dupliquer, supprimer des presets
- **Ajout d'exécutables** : Par sélection de fichier ou depuis les processus actifs
- **Lancement** : Exécuter tous les programmes d'un preset avec délais configurables
- **Options par exécutable** : Exécution en tant qu'administrateur, délai de lancement
- **Historique** : Suivi des exécutions avec date/heure/durée
- **Statistiques** : Programmes les plus utilisés, nombre de lancements
- **Import/Export** : Sauvegarder et partager des presets en JSON
- **Verrouillage** : Protection par mot de passe des presets sensibles
- **Multilingue** : Français et Anglais
- **Thèmes** : Clair, Sombre, ou automatique selon le système

## Structure du projet

```
PresetLauncher/
├── App.xaml                    # Point d'entrée de l'application
├── Models/                     # Modèles de données
│   ├── Preset.cs              # Modèle preset
│   ├── ExecutableItem.cs      # Modèle exécutable
│   ├── ExecutionHistory.cs    # Historique d'exécution
│   ├── AppSettings.cs         # Paramètres application
│   └── AppData.cs             # Données application
├── Services/                   # Services métier
│   ├── DataService.cs         # Gestion sauvegarde/chargement
│   ├── SettingsService.cs     # Gestion paramètres
│   ├── LauncherService.cs     # Lancement des programmes
│   ├── LocalizationService.cs # Traductions
│   ├── ThemeService.cs        # Gestion des thèmes
│   └── PasswordService.cs     # Hachage mots de passe
├── Views/                      # Interface utilisateur
│   ├── MainWindow.xaml        # Fenêtre principale
│   ├── Pages/                 # Pages de navigation
│   │   ├── PresetsPage.xaml   # Liste des presets
│   │   ├── HistoryPage.xaml   # Historique
│   │   ├── StatisticsPage.xaml# Statistiques
│   │   └── SettingsPage.xaml  # Paramètres
│   └── Dialogs/               # Dialogues modaux
│       ├── PresetEditorDialog.xaml
│       ├── ProcessPickerDialog.xaml
│       ├── PasswordDialog.xaml
│       └── ExecutionDialog.xaml
├── Themes/                     # Thèmes visuels
│   ├── LightTheme.xaml
│   ├── DarkTheme.xaml
│   └── BaseStyles.xaml
└── Converters/                 # Convertisseurs XAML
    └── Converters.cs
```

## Technologies

- .NET 8.0 Windows
- WPF (Windows Presentation Foundation)
- CommunityToolkit.Mvvm (pattern MVVM)
- Newtonsoft.Json (sérialisation)

## Compilation

```bash
cd PresetLauncher
dotnet restore
dotnet build
```

## Publication

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

## Sauvegarde des données

Les données sont stockées en JSON dans `%LOCALAPPDATA%\PresetLauncher\`:
- `settings.json` : Paramètres de l'application
- `data.json` : Presets et historique

## Notes de développement

- Interface en français par défaut, anglais disponible
- Thème automatique selon les paramètres Windows
- Pas d'inscription au démarrage automatique de Windows (comme demandé)
- Application portable, peut être copiée et exécutée directement
