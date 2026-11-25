using System.Collections.ObjectModel;
using System.Globalization;

namespace PresetLauncher.Services;

public class LocalizationService
{
    private static readonly (string Key, string English, string French)[] Translations =
    [
        ("app.title", "PresetLauncher", "PresetLauncher"),
        ("cleanup.confirm", "References to review: {0:N0}. The files could be on a disconnected drive or network location. Removing a reference keeps the preset and any files.", "Références à vérifier : {0:N0}. Les fichiers peuvent se trouver sur un lecteur déconnecté ou sur le réseau. La suppression d’une référence conserve le preset et les fichiers."),
        ("cleanup.none", "All executable references are currently available.", "Tous les exécutables référencés sont accessibles."),
        ("cleanup.remove", "Remove references", "Supprimer les références"),
        ("cleanup.removed", "References removed: {0:N0}.", "Références supprimées : {0:N0}."),
        ("data.backupScope", "Export a backup", "Exporter une sauvegarde"),
        ("data.export", "Export data", "Exporter les données"),
        ("data.exported", "Backup exported.", "Sauvegarde exportée."),
        ("data.exportScope", "Includes presets, launch history and usage statistics. Application preferences and installed programs are kept separately.", "Inclut les presets, l’historique et les statistiques. Les paramètres et les programmes installés sont conservés séparément."),
        ("data.fileType", "PresetLauncher backup (JSON)", "Sauvegarde PresetLauncher (JSON)"),
        ("data.import", "Import data", "Importer les données"),
        ("data.imported", "Backup restored.", "Sauvegarde restaurée."),
        ("data.importedWithCopy", "Backup restored. The previous data file was kept alongside it as {0}.", "Sauvegarde restaurée. Le fichier précédent a été conservé au même emplacement sous le nom {0}."),
        ("data.importReplace", "This backup will replace all current presets, launch history and usage statistics.", "Cette sauvegarde remplacera tous les presets, l’historique des lancements et les statistiques actuels."),
        ("data.importSummary", "Presets: {0:N0}\nHistory entries: {1:N0}\nUsage entries: {2:N0}", "Presets : {0:N0}\nEntrées d’historique : {1:N0}\nEntrées de statistiques : {2:N0}"),
        ("data.invalidBackup", "This file is invalid, too large or uses an unsupported backup format.", "Ce fichier est invalide, trop volumineux ou utilise un format de sauvegarde non pris en charge."),
        ("data.loadFailed", "Application data could not be read. The original file was kept. Restore a backup in Settings.", "Impossible de lire les données. Le fichier d’origine a été conservé. Restaurez une sauvegarde dans les paramètres."),
        ("data.replace", "Replace data", "Remplacer les données"),
        ("data.restoreFailed", "The backup could not be restored.", "Impossible de restaurer la sauvegarde."),
        ("data.restoreScope", "Replaces current presets, history and usage statistics.", "Remplace les presets, l’historique et les statistiques actuels."),
        ("data.restoreTitle", "Restore a backup", "Restaurer une sauvegarde"),
        ("data.sensitivity", "This JSON backup is unencrypted. It can contain executable paths, launch arguments, usage history and preset password hashes.", "Cette sauvegarde JSON n’est pas chiffrée. Elle peut contenir des chemins d’exécutables, des arguments, l’historique d’utilisation et des empreintes de mots de passe de presets."),
        ("data.writeFailed", "The change could not be saved.", "Impossible d’enregistrer la modification."),
        ("dialog.cancel", "Cancel", "Annuler"),
        ("dialog.close.message", "Close processes matching “{0}”? This may force-close programs started elsewhere. Save any open work first.", "Fermer les processus associés à « {0} » ? Des programmes lancés ailleurs peuvent être fermés de force. Enregistrez d’abord votre travail."),
        ("dialog.close.title", "Close processes", "Fermer les processus"),
        ("dialog.delete.message", "Delete “{0}”? Launch history and usage statistics will be kept.", "Supprimer « {0} » ? L’historique et les statistiques seront conservés."),
        ("dialog.delete.title", "Delete preset", "Supprimer le preset"),
        ("dialog.error", "Something went wrong.", "Une erreur est survenue."),
        ("dialog.launch.message", "Send start requests for “{0}”?", "Envoyer les demandes de lancement pour « {0} » ?"),
        ("dialog.ok", "OK", "OK"),
        ("dialog.preset.nameRequired", "Enter a preset name.", "Saisissez un nom de preset."),
        ("editor.addFailed", "The selected files could not be added.", "Impossible d’ajouter les fichiers sélectionnés."),
        ("editor.back", "Back to presets", "Retour aux presets"),
        ("editor.create", "Create preset", "Créer le preset"),
        ("editor.delayError", "Enter a whole number from 0 to {0:N0} seconds.", "Saisissez un nombre entier de 0 à {0:N0} secondes."),
        ("editor.discard", "Discard changes?", "Abandonner les modifications ?"),
        ("editor.discardAction", "Discard changes", "Abandonner"),
        ("editor.discardMessage", "Changes to this preset will be lost.", "Les modifications apportées à ce preset seront perdues."),
        ("editor.empty", "No programs added.", "Aucun programme ajouté."),
        ("editor.options", "Launch options", "Options de lancement"),
        ("editor.optionsNamed", "Launch options for {0}", "Options de lancement pour {0}"),
        ("editor.save", "Save changes", "Enregistrer"),
        ("editor.saveFailed", "The preset could not be saved. Your changes are still in the editor.", "Impossible d’enregistrer le preset. Vos modifications sont toujours dans l’éditeur."),
        ("exe.addFromFile", "Add from file", "Ajouter un fichier"),
        ("exe.addFromProcess", "From running processes", "Depuis les processus actifs"),
        ("exe.delay", "Delay (seconds)", "Délai (secondes)"),
        ("exe.executables", "Programs", "Programmes"),
        ("exe.invalid", "Executable unavailable", "Fichier exécutable introuvable"),
        ("exe.moveDown", "Move down", "Descendre"),
        ("exe.moveUp", "Move up", "Monter"),
        ("exe.programMany", "{0:N0} programs", "{0:N0} programmes"),
        ("exe.programOne", "{0:N0} program", "{0:N0} programme"),
        ("exe.remove", "Remove program", "Retirer le programme"),
        ("exe.runAsAdmin", "Run as administrator", "Exécuter en tant qu’administrateur"),
        ("execution.closeFailed", "The processes could not be closed.", "Impossible de fermer les processus."),
        ("execution.inProgress", "Sending start requests…", "Envoi des demandes de lancement…"),
        ("execution.launching", "Sending a start request for {0}…", "Demande de lancement pour {0}…"),
        ("execution.processesClosed", "Processes closed: {0:N0}.", "Processus fermés : {0:N0}."),
        ("execution.results", "Start requests accepted: {0:N0}.", "Demandes de lancement acceptées : {0:N0}."),
        ("execution.resultsFailed", "Start requests accepted: {0:N0}. Failed: {1:N0}.", "Demandes de lancement acceptées : {0:N0}. Échecs : {1:N0}."),
        ("execution.saveFailed", "The launch could not be completed or recorded. Some programs may already have started.", "Le lancement ou son enregistrement a échoué. Certains programmes ont peut-être déjà démarré."),
        ("history.clear", "Clear history", "Effacer l’historique"),
        ("history.clearConfirm", "Delete all launch history? Presets and usage statistics will be kept.", "Effacer tout l’historique des lancements ? Les presets et les statistiques seront conservés."),
        ("history.cleared", "Launch history cleared.", "Historique des lancements effacé."),
        ("history.empty", "No launch history.", "Aucun lancement dans l’historique."),
        ("history.failedPrograms", "Failed programs", "Programmes en échec"),
        ("history.failure", "{0:N0}/{1:N0} start requests accepted · Failed: {2:N0}", "{0:N0}/{1:N0} demandes de lancement acceptées · Échecs : {2:N0}"),
        ("history.filter", "Filter history", "Filtrer l’historique"),
        ("history.filter.all", "All", "Tous"),
        ("history.filter.failures", "Failed", "Échecs"),
        ("history.filter.success", "Successful", "Réussites"),
        ("history.filter.today", "Today", "Aujourd’hui"),
        ("history.noMatches", "No launches match this filter.", "Aucun lancement ne correspond à ce filtre."),
        ("history.success", "{0:N0}/{1:N0} start requests accepted", "{0:N0}/{1:N0} demandes de lancement acceptées"),
        ("history.timing", "{0} · {1:N1} s", "{0} · {1:N1} s"),
        ("history.title", "History", "Historique"),
        ("language.en", "English", "English"),
        ("language.fr", "Français", "Français"),
        ("menu.closeProcesses", "Close processes", "Fermer les processus"),
        ("menu.delete", "Delete preset", "Supprimer le preset"),
        ("menu.more", "More actions for {0}", "Autres actions pour {0}"),
        ("nav.history", "History", "Historique"),
        ("nav.presets", "Presets", "Presets"),
        ("nav.settings", "Settings", "Paramètres"),
        ("nav.statistics", "Statistics", "Statistiques"),
        ("preset.deleted", "Preset deleted.", "Preset supprimé."),
        ("preset.description", "Description", "Description"),
        ("preset.edit", "Edit preset", "Modifier le preset"),
        ("preset.editNamed", "Edit {0}", "Modifier {0}"),
        ("preset.import", "Import preset", "Importer un preset"),
        ("preset.imported", "Preset imported.", "Preset importé."),
        ("preset.importError", "This file could not be read as a preset. Use Settings to restore application backups.", "Ce fichier ne peut pas être lu comme un preset. Pour restaurer une sauvegarde, ouvrez les paramètres."),
        ("preset.importWriteFailed", "The preset could not be saved.", "Impossible d’enregistrer le preset."),
        ("preset.launch", "Launch", "Lancer"),
        ("preset.launchAll", "Launch all", "Tout lancer"),
        ("preset.launchMissing", "Launch missing only", "Lancer les programmes arrêtés"),
        ("preset.launchNamed", "Launch {0}", "Lancer {0}"),
        ("preset.name", "Preset name", "Nom du preset"),
        ("preset.new", "New preset", "Nouveau preset"),
        ("preset.noMatches", "No presets match your search.", "Aucun preset ne correspond à la recherche."),
        ("preset.nopresets", "No presets yet.", "Aucun preset."),
        ("preset.programSummary", "Unavailable: {0:N0}", "Introuvables : {0:N0}"),
        ("preset.title", "Presets", "Presets"),
        ("process.empty", "No accessible executable paths were found.", "Aucun chemin d’exécutable accessible."),
        ("process.error", "The running processes could not be read.", "Impossible de lire les processus en cours."),
        ("process.loading", "Reading running processes…", "Recherche des processus en cours…"),
        ("process.noMatches", "No matching processes.", "Aucun processus ne correspond à la recherche."),
        ("process.refresh", "Refresh", "Actualiser"),
        ("process.search", "Search name or path", "Rechercher par nom ou chemin"),
        ("process.select", "Add program", "Ajouter le programme"),
        ("process.title", "Running processes", "Processus en cours"),
        ("search.placeholder", "Search presets", "Rechercher des presets"),
        ("settings.about", "About", "À propos"),
        ("settings.appearance", "Appearance", "Apparence"),
        ("settings.backdrop", "Backdrop", "Arrière-plan"),
        ("settings.backdrop.acrylic", "Acrylic", "Acrylique"),
        ("settings.backdrop.mica", "Mica", "Mica"),
        ("settings.backdrop.none", "None", "Aucun"),
        ("settings.behavior", "Behavior", "Comportement"),
        ("settings.checkUpdate", "Check for updates", "Vérifier les mises à jour"),
        ("settings.cleanup", "Missing executables", "Exécutables introuvables"),
        ("settings.cleanup.action", "Review references", "Examiner les références"),
        ("settings.cleanup.subtitle", "Remove references to executable files that could not be found.", "Supprimer les références aux fichiers exécutables introuvables."),
        ("settings.confirmLaunch", "Confirm before launch", "Confirmer avant le lancement"),
        ("settings.data", "Data management", "Gestion des données"),
        ("settings.dataPath", "Data location", "Emplacement des données"),
        ("settings.export.error", "The backup could not be written. Choose a writable location outside protected application files.", "Impossible d’enregistrer la sauvegarde. Choisissez un emplacement accessible en dehors des fichiers protégés de l’application."),
        ("settings.language", "Language", "Langue"),
        ("settings.off", "Off", "Désactivé"),
        ("settings.on", "On", "Activé"),
        ("settings.reset.action", "Reset application", "Réinitialiser l’application"),
        ("settings.reset.confirm", "Delete all presets, launch history and usage statistics, and restore default settings. Installed programs and external executable files will be kept. Export your data first if you may need it later.", "Supprimer tous les presets, l’historique et les statistiques, puis rétablir les paramètres par défaut. Les programmes installés et les fichiers exécutables externes seront conservés. Exportez vos données si vous souhaitez pouvoir les récupérer."),
        ("settings.reset.failed", "The application could not be fully reset. Review the current settings and try again.", "La réinitialisation n’a pas abouti. Vérifiez les paramètres actuels avant de réessayer."),
        ("settings.reset.section", "Reset", "Réinitialisation"),
        ("settings.reset.subtitle", "Delete all presets, launch history and usage statistics, and restore default settings.", "Supprimer tous les presets, l’historique et les statistiques, puis rétablir les paramètres par défaut."),
        ("settings.reset.success", "Application reset.", "Application réinitialisée."),
        ("settings.reset.title", "Reset application", "Réinitialiser l’application"),
        ("settings.saveFailed", "The preference could not be saved.", "Impossible d’enregistrer le paramètre."),
        ("settings.theme", "Theme", "Thème"),
        ("settings.theme.dark", "Dark", "Sombre"),
        ("settings.theme.light", "Light", "Clair"),
        ("settings.theme.system", "System", "Système"),
        ("settings.title", "Settings", "Paramètres"),
        ("settings.updateAvailable", "Update available", "Mise à jour disponible"),
        ("settings.updateCheckFailed", "Unable to check for updates", "Impossible de vérifier les mises à jour"),
        ("settings.updateDownloadError", "Unable to download the update. Please try again later.", "Impossible de télécharger la mise à jour. Veuillez rééssayer plus tard."),
        ("settings.updateDownloadingInstalling", "Update", "Mise à jour"),
        ("settings.updatePreparing", "Preparing update...", "Préparation de la mise à jour en cours..."),
        ("settings.updateProgress", "Downloading update… {0:P0}", "Téléchargement de la mise à jour… {0:P0}"),
        ("settings.updateUpToDate", "You are already running the latest version", "Vous disposez déjà de la dernière version"),
        ("settings.version", "Installed version", "Version installée"),
        ("stats.empty", "No program usage recorded.", "Aucune utilisation enregistrée."),
        ("stats.mostUsed", "Most-used programs", "Programmes les plus utilisés"),
        ("stats.once", "{0:N0} time", "{0:N0} fois"),
        ("stats.presetsCount", "Presets", "Presets"),
        ("stats.times", "{0:N0} times", "{0:N0} fois"),
        ("stats.title", "Statistics", "Statistiques"),
        ("stats.totalLaunches", "Recorded launches", "Lancements enregistrés"),
        ("stats.uniquePrograms", "Unique programs", "Programmes distincts"),
        ("update.exit", "Exit application", "Quitter l'application"),
        ("update.install", "Install update", "Installer"),
        ("update.later", "Later", "Plus tard"),
        ("update.optionalMessage", "Installed version: {0}\nNew version: {1}", "Version installée : {0}\nNouvelle version : {1}"),
        ("update.policyErrorMessage", "The saved update policy could not be read. Close other instances of PresetLauncher. If the problem persists, see the recovery instructions in the project documentation.", "Impossible de lire la politique de mise à jour enregistrée. Fermez les autres instances de PresetLauncher. Si le problème persiste, consultez le guide de récupération dans la documentation du projet."),
        ("update.policyErrorTitle", "Update state unavailable", "État des mises à jour inaccessible"),
        ("update.recommendedMessage", "Installed version: {0}\nNew version: {1}\n\nWe recommend installing this update.", "Version installée : {0}\nNouvelle version : {1}\n\nNous vous recommandons cette mise à jour."),
        ("update.recommendedTitle", "Update recommended", "Mise à jour recommandée"),
        ("update.remindTomorrow", "Remind me tomorrow", "Me rappeler demain"),
        ("update.requiredMessage", "Your version ({0}) is no longer supported. Install version {1} to continue.", "Votre version ({0}) n'est plus prise en charge. Installez la version {1} pour continuer."),
        ("update.requiredRetry", "An update is required to continue. Retry or exit the application.", "Une mise à jour est nécessaire pour continuer. Réessayez ou quittez l'application."),
        ("update.requiredTitle", "Update required", "Mise à jour obligatoire"),
        ("update.retry", "Retry", "Réessayer"),
        ("update.skip", "Skip this version", "Ignorer cette version"),
        ("update.sourceBuild", "Automatic updates are disabled for unversioned source builds.", "Les mises à jour automatiques sont désactivées pour les builds non versionnés."),
    ];

    private static readonly IReadOnlyDictionary<string, string> English =
        new ReadOnlyDictionary<string, string>(Translations.ToDictionary(row => row.Key, row => row.English));
    private static readonly IReadOnlyDictionary<string, string> French =
        new ReadOnlyDictionary<string, string>(Translations.ToDictionary(row => row.Key, row => row.French));

    public static LocalizationService Instance { get; private set; } = new();
    public string Language { get; private set; } = "fr";
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("fr-FR");

    public LocalizationService() => Instance = this;

    public void SetLanguage(string? language)
    {
        var code = language?.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        Language = string.Equals(code, "fr", StringComparison.OrdinalIgnoreCase) ? "fr" : "en";
        Culture = CultureInfo.GetCultureInfo(Language == "fr" ? "fr-FR" : "en-US");
    }

    public string Get(string key) => GetTranslations(Language).GetValueOrDefault(key) ?? key;

    public string Get(string key, params object[] args)
    {
        var template = Get(key);
        if (args.Length == 0) return template;
        try { return string.Format(Culture, template, args); }
        catch (FormatException) { return template; }
    }

    public static IReadOnlyDictionary<string, string> GetTranslations(string language) => language == "fr" ? French : English;

    public IEnumerable<string> AvailableLanguages => ["fr", "en"];
}
