using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace PresetLauncher.Services
{
    public class LocalizationService
    {
        private Dictionary<string, string> _currentTranslations = new();
        private string _currentLanguage = "fr";

        private static readonly Dictionary<string, Dictionary<string, string>> EmbeddedTranslations = new()
        {
            ["fr"] = new Dictionary<string, string>
            {
                ["app.title"] = "Lanceur de Presets",
                ["nav.presets"] = "Presets",
                ["nav.history"] = "Historique",
                ["nav.statistics"] = "Statistiques",
                ["nav.settings"] = "Paramètres",
                ["preset.new"] = "Nouveau Preset",
                ["preset.edit"] = "Modifier",
                ["preset.delete"] = "Supprimer",
                ["preset.duplicate"] = "Dupliquer",
                ["preset.launch"] = "Lancer",
                ["preset.export"] = "Exporter",
                ["preset.import"] = "Importer",
                ["preset.name"] = "Nom du preset",
                ["preset.description"] = "Description",
                ["preset.nopresets"] = "Aucun preset. Cliquez sur 'Nouveau Preset' pour commencer.",
                ["exe.add"] = "Ajouter un exécutable",
                ["exe.addFromFile"] = "Depuis un fichier...",
                ["exe.addFromProcess"] = "Depuis les processus actifs...",
                ["exe.remove"] = "Supprimer",
                ["exe.name"] = "Nom",
                ["exe.path"] = "Chemin",
                ["exe.arguments"] = "Arguments",
                ["exe.runAsAdmin"] = "Exécuter en tant qu'administrateur",
                ["exe.delay"] = "Délai (secondes)",
                ["exe.invalid"] = "Chemin invalide",
                ["exe.moveUp"] = "Monter",
                ["exe.moveDown"] = "Descendre",
                ["history.title"] = "Historique d'exécution",
                ["history.date"] = "Date",
                ["history.preset"] = "Preset",
                ["history.status"] = "Statut",
                ["history.duration"] = "Durée",
                ["history.clear"] = "Effacer l'historique",
                ["history.empty"] = "Aucun historique d'exécution.",
                ["stats.title"] = "Statistiques d'utilisation",
                ["stats.mostUsed"] = "Programmes les plus utilisés",
                ["stats.totalLaunches"] = "Lancements totaux",
                ["stats.presetsCount"] = "Nombre de presets",
                ["settings.title"] = "Paramètres",
                ["settings.language"] = "Langue",
                ["settings.theme"] = "Thème",
                ["settings.theme.light"] = "Clair",
                ["settings.theme.dark"] = "Sombre",
                ["settings.theme.system"] = "Système",
                ["settings.dataPath"] = "Emplacement des données",
                ["settings.browse"] = "Parcourir...",
                ["settings.confirmLaunch"] = "Confirmer avant le lancement",
                ["settings.notifications"] = "Afficher les notifications",
                ["dialog.confirm"] = "Confirmer",
                ["dialog.cancel"] = "Annuler",
                ["dialog.yes"] = "Oui",
                ["dialog.no"] = "Non",
                ["dialog.ok"] = "OK",
                ["dialog.save"] = "Enregistrer",
                ["dialog.delete.title"] = "Confirmer la suppression",
                ["dialog.delete.message"] = "Voulez-vous vraiment supprimer ce preset ?",
                ["dialog.launch.title"] = "Lancer le preset",
                ["dialog.launch.message"] = "Voulez-vous lancer tous les programmes de ce preset ?",
                ["dialog.password"] = "Mot de passe",
                ["dialog.password.enter"] = "Entrez le mot de passe",
                ["dialog.password.set"] = "Définir un mot de passe",
                ["dialog.password.wrong"] = "Mot de passe incorrect",
                ["search.placeholder"] = "Rechercher...",
                ["execution.starting"] = "Démarrage en cours...",
                ["execution.success"] = "Exécution réussie",
                ["execution.failed"] = "Échec de l'exécution",
                ["execution.launching"] = "Lancement de {0}...",
                ["lock.preset"] = "Verrouiller le preset",
                ["unlock.preset"] = "Déverrouiller le preset"
            },
            ["en"] = new Dictionary<string, string>
            {
                ["app.title"] = "Preset Launcher",
                ["nav.presets"] = "Presets",
                ["nav.history"] = "History",
                ["nav.statistics"] = "Statistics",
                ["nav.settings"] = "Settings",
                ["preset.new"] = "New Preset",
                ["preset.edit"] = "Edit",
                ["preset.delete"] = "Delete",
                ["preset.duplicate"] = "Duplicate",
                ["preset.launch"] = "Launch",
                ["preset.export"] = "Export",
                ["preset.import"] = "Import",
                ["preset.name"] = "Preset name",
                ["preset.description"] = "Description",
                ["preset.nopresets"] = "No presets. Click 'New Preset' to get started.",
                ["exe.add"] = "Add executable",
                ["exe.addFromFile"] = "From file...",
                ["exe.addFromProcess"] = "From running processes...",
                ["exe.remove"] = "Remove",
                ["exe.name"] = "Name",
                ["exe.path"] = "Path",
                ["exe.arguments"] = "Arguments",
                ["exe.runAsAdmin"] = "Run as administrator",
                ["exe.delay"] = "Delay (seconds)",
                ["exe.invalid"] = "Invalid path",
                ["exe.moveUp"] = "Move up",
                ["exe.moveDown"] = "Move down",
                ["history.title"] = "Execution History",
                ["history.date"] = "Date",
                ["history.preset"] = "Preset",
                ["history.status"] = "Status",
                ["history.duration"] = "Duration",
                ["history.clear"] = "Clear history",
                ["history.empty"] = "No execution history.",
                ["stats.title"] = "Usage Statistics",
                ["stats.mostUsed"] = "Most used programs",
                ["stats.totalLaunches"] = "Total launches",
                ["stats.presetsCount"] = "Number of presets",
                ["settings.title"] = "Settings",
                ["settings.language"] = "Language",
                ["settings.theme"] = "Theme",
                ["settings.theme.light"] = "Light",
                ["settings.theme.dark"] = "Dark",
                ["settings.theme.system"] = "System",
                ["settings.dataPath"] = "Data location",
                ["settings.browse"] = "Browse...",
                ["settings.confirmLaunch"] = "Confirm before launch",
                ["settings.notifications"] = "Show notifications",
                ["dialog.confirm"] = "Confirm",
                ["dialog.cancel"] = "Cancel",
                ["dialog.yes"] = "Yes",
                ["dialog.no"] = "No",
                ["dialog.ok"] = "OK",
                ["dialog.save"] = "Save",
                ["dialog.delete.title"] = "Confirm deletion",
                ["dialog.delete.message"] = "Are you sure you want to delete this preset?",
                ["dialog.launch.title"] = "Launch preset",
                ["dialog.launch.message"] = "Do you want to launch all programs in this preset?",
                ["dialog.password"] = "Password",
                ["dialog.password.enter"] = "Enter password",
                ["dialog.password.set"] = "Set password",
                ["dialog.password.wrong"] = "Incorrect password",
                ["search.placeholder"] = "Search...",
                ["execution.starting"] = "Starting...",
                ["execution.success"] = "Execution successful",
                ["execution.failed"] = "Execution failed",
                ["execution.launching"] = "Launching {0}...",
                ["lock.preset"] = "Lock preset",
                ["unlock.preset"] = "Unlock preset"
            }
        };

        public void SetLanguage(string language)
        {
            _currentLanguage = language;
            if (EmbeddedTranslations.ContainsKey(language))
            {
                _currentTranslations = EmbeddedTranslations[language];
            }
            else
            {
                _currentTranslations = EmbeddedTranslations["en"];
            }
        }

        public string Get(string key)
        {
            if (_currentTranslations.TryGetValue(key, out var value))
            {
                return value;
            }
            return key;
        }

        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public string CurrentLanguage => _currentLanguage;

        public IEnumerable<string> AvailableLanguages => EmbeddedTranslations.Keys;
    }
}
