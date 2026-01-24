using System.Collections.Generic;

namespace PresetLauncher.Services;

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
            ["nav.settings"] = "Parametres",
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
            ["preset.exported"] = "Preset exporte avec succes!",
            ["preset.imported"] = "Preset importe avec succes!",
            ["preset.importError"] = "Erreur lors de l'import du preset.",
            ["exe.add"] = "Ajouter un executable",
            ["exe.addFromFile"] = "Depuis un fichier...",
            ["exe.addFromProcess"] = "Depuis les processus actifs...",
            ["exe.remove"] = "Supprimer",
            ["exe.name"] = "Nom",
            ["exe.path"] = "Chemin",
            ["exe.arguments"] = "Arguments",
            ["exe.runAsAdmin"] = "Executer en tant qu'administrateur",
            ["exe.delay"] = "Delai (secondes)",
            ["exe.invalid"] = "Chemin invalide",
            ["exe.moveUp"] = "Monter",
            ["exe.moveDown"] = "Descendre",
            ["exe.executables"] = "Executables",
            ["exe.programs"] = "programmes",
            ["history.title"] = "Historique d'execution",
            ["history.date"] = "Date",
            ["history.preset"] = "Preset",
            ["history.status"] = "Statut",
            ["history.duration"] = "Duree",
            ["history.clear"] = "Effacer l'historique",
            ["history.empty"] = "Aucun historique d'execution.",
            ["history.clearConfirm"] = "Voulez-vous vraiment effacer tout l'historique?",
            ["history.failures"] = "{0} echec(s) sur {1}",
            ["stats.title"] = "Statistiques d'utilisation",
            ["stats.mostUsed"] = "Programmes les plus utilises",
            ["stats.totalLaunches"] = "Lancements totaux",
            ["stats.presetsCount"] = "Nombre de presets",
            ["stats.uniquePrograms"] = "Programmes uniques",
            ["stats.times"] = "{0} fois",
            ["settings.title"] = "Parametres",
            ["settings.language"] = "Langue",
            ["settings.theme"] = "Theme",
            ["settings.theme.light"] = "Clair",
            ["settings.theme.dark"] = "Sombre",
            ["settings.theme.system"] = "Systeme",
            ["settings.dataPath"] = "Emplacement des donnees",
            ["settings.browse"] = "Parcourir...",
            ["settings.confirmLaunch"] = "Confirmer avant le lancement",
            ["settings.notifications"] = "Afficher les notifications",
            ["settings.options"] = "Options",
            ["settings.data"] = "Donnees",
            ["settings.cleanup"] = "Nettoyer les executables invalides",
            ["settings.cleanupDone"] = "Nettoyage termine!",
            ["dialog.confirm"] = "Confirmer",
            ["dialog.cancel"] = "Annuler",
            ["dialog.yes"] = "Oui",
            ["dialog.no"] = "Non",
            ["dialog.ok"] = "OK",
            ["dialog.save"] = "Enregistrer",
            ["dialog.error"] = "Erreur",
            ["dialog.export"] = "Export",
            ["dialog.import"] = "Import",
            ["dialog.delete.title"] = "Confirmer la suppression",
            ["dialog.delete.message"] = "Voulez-vous vraiment supprimer ce preset ?",
            ["dialog.launch.title"] = "Lancer le preset",
            ["dialog.launch.message"] = "Voulez-vous lancer tous les programmes de ce preset ?",
            ["dialog.password"] = "Mot de passe",
            ["dialog.password.enter"] = "Entrez le mot de passe",
            ["dialog.password.set"] = "Definir un mot de passe",
            ["dialog.password.wrong"] = "Mot de passe incorrect",
            ["dialog.password.confirm"] = "Confirmez le mot de passe",
            ["dialog.password.mismatch"] = "Les mots de passe ne correspondent pas.",
            ["dialog.password.empty"] = "Le mot de passe ne peut pas etre vide.",
            ["dialog.preset.nameRequired"] = "Veuillez entrer un nom pour le preset.",
            ["search.placeholder"] = "Rechercher...",
            ["execution.starting"] = "Demarrage en cours...",
            ["execution.success"] = "Execution reussie",
            ["execution.failed"] = "Echec de l'execution",
            ["execution.launching"] = "Lancement de {0}...",
            ["execution.inProgress"] = "Lancement du preset...",
            ["execution.preparing"] = "Preparation...",
            ["lock.preset"] = "Verrouiller le preset",
            ["unlock.preset"] = "Deverrouiller le preset",
            ["process.title"] = "Processus en cours d'execution",
            ["process.select"] = "Selectionner"
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
            ["preset.exported"] = "Preset exported successfully!",
            ["preset.imported"] = "Preset imported successfully!",
            ["preset.importError"] = "Error importing preset.",
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
            ["exe.executables"] = "Executables",
            ["exe.programs"] = "programs",
            ["history.title"] = "Execution History",
            ["history.date"] = "Date",
            ["history.preset"] = "Preset",
            ["history.status"] = "Status",
            ["history.duration"] = "Duration",
            ["history.clear"] = "Clear history",
            ["history.empty"] = "No execution history.",
            ["history.clearConfirm"] = "Are you sure you want to clear all history?",
            ["history.failures"] = "{0} failure(s) out of {1}",
            ["stats.title"] = "Usage Statistics",
            ["stats.mostUsed"] = "Most used programs",
            ["stats.totalLaunches"] = "Total launches",
            ["stats.presetsCount"] = "Number of presets",
            ["stats.uniquePrograms"] = "Unique programs",
            ["stats.times"] = "{0} times",
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
            ["settings.options"] = "Options",
            ["settings.data"] = "Data",
            ["settings.cleanup"] = "Clean up invalid executables",
            ["settings.cleanupDone"] = "Cleanup completed!",
            ["dialog.confirm"] = "Confirm",
            ["dialog.cancel"] = "Cancel",
            ["dialog.yes"] = "Yes",
            ["dialog.no"] = "No",
            ["dialog.ok"] = "OK",
            ["dialog.save"] = "Save",
            ["dialog.error"] = "Error",
            ["dialog.export"] = "Export",
            ["dialog.import"] = "Import",
            ["dialog.delete.title"] = "Confirm deletion",
            ["dialog.delete.message"] = "Are you sure you want to delete this preset?",
            ["dialog.launch.title"] = "Launch preset",
            ["dialog.launch.message"] = "Do you want to launch all programs in this preset?",
            ["dialog.password"] = "Password",
            ["dialog.password.enter"] = "Enter password",
            ["dialog.password.set"] = "Set password",
            ["dialog.password.wrong"] = "Incorrect password",
            ["dialog.password.confirm"] = "Confirm password",
            ["dialog.password.mismatch"] = "Passwords do not match.",
            ["dialog.password.empty"] = "Password cannot be empty.",
            ["dialog.preset.nameRequired"] = "Please enter a name for the preset.",
            ["search.placeholder"] = "Search...",
            ["execution.starting"] = "Starting...",
            ["execution.success"] = "Execution successful",
            ["execution.failed"] = "Execution failed",
            ["execution.launching"] = "Launching {0}...",
            ["execution.inProgress"] = "Launching preset...",
            ["execution.preparing"] = "Preparing...",
            ["lock.preset"] = "Lock preset",
            ["unlock.preset"] = "Unlock preset",
            ["process.title"] = "Running processes",
            ["process.select"] = "Select"
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
