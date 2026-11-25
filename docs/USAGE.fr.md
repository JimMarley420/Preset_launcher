# Guide d’utilisation

[English](USAGE.md) · [PresetLauncher](../README.fr.md)

## Premiers pas

Choisissez le programme d’installation adapté dans la [dernière version](https://github.com/JimMarley420/Preset_launcher/releases/latest). Le paquet inclut .NET et Windows App SDK.

| Processeur Windows | Programme d’installation |
| --- | --- |
| x64 | `PresetLauncher_Setup_<version>.exe` |
| x86 32 bits | `PresetLauncher_Setup_<version>_x86.exe` |
| ARM64 | `PresetLauncher_Setup_<version>_arm64.exe` |

Consultez **Paramètres → Système → Informations système → Type du système**. Utilisez une version de Windows prise en charge par Microsoft. Les paquets ARM64 sont compilés et inspectés par la CI x64. L’installation et le démarrage natifs ARM64 restent à valider sur du matériel ARM64. Les installations existantes conservent leur architecture lors des mises à jour, y compris en émulation. Pour changer d’architecture, désinstallez d’abord la version actuelle. La désinstallation conserve les préréglages, l’historique, les statistiques, les paramètres et la politique de mise à jour protégée.

Le programme d’installation exige au minimum le build 17763. L’application cible le build 19041. Les versions de Windows compatibles dépendent également de l’architecture et du cycle de support de Microsoft.

Le volet de navigation est réduit au démarrage. Les icônes ouvrent les presets, l’historique, les statistiques et les paramètres. Le bouton de menu affiche les libellés.

Dans **Presets**, sélectionnez **Nouveau preset** ou le bouton de modification d’une ligne. Saisissez un nom et ajoutez des programmes depuis des fichiers ou les processus en cours. Les flèches changent l’ordre de lancement. La roue dentée d’un programme ouvre ses options de délai et de droits administrateur. Les chemins introuvables ou inaccessibles restent dans la liste. **Annuler** ou **Retour** permet de quitter et demande une confirmation si le preset a été modifié. Windows détermine comment ouvrir les scripts et raccourcis.

Vous pouvez rechercher les presets par nom ou description. **Importer un preset** ajoute un preset JSON avec une nouvelle identité. Vérifiez les chemins, arguments et droits administrateur des presets importés avant de les lancer.

## Lancement des programmes

Choisissez un mode dans **Lancer**. Le réglage **Confirmer avant le lancement** détermine si le mode choisi demande une confirmation.

| Action | Comportement |
| --- | --- |
| Tout lancer | Envoie les demandes dans l’ordre du preset, applique les délais et enregistre le lancement dans l’historique. |
| Lancer les programmes arrêtés | Ignore les programmes détectés par leur chemin ou leur nom de fichier. Ce mode ignore les délais et n’ajoute aucune entrée à l’historique. |
| Fermer les processus | Demande la fermeture des processus correspondants et peut les arrêter de force après confirmation. Des programmes ouverts en dehors de PresetLauncher peuvent être concernés. Enregistrez d’abord votre travail. |

L’acceptation d’une demande par Windows ne signifie pas que le programme a fini de démarrer. Les deux modes comptabilisent les demandes acceptées dans les statistiques par programme.

## Historique, statistiques et apparence

L’historique affiche les lancements complets, leur durée et leur résultat. Les filtres sont **Tous**, **Réussites**, **Échecs** et **Aujourd’hui**. Le filtre **Échecs** inclut les lancements partiellement réussis. **Aujourd’hui** suit la date locale. **Effacer l’historique** conserve les presets et les statistiques.

Les statistiques affichent le nombre de presets, les lancements enregistrés pour les presets actuels, les chemins de programmes distincts ayant été utilisés et les dix chemins les plus utilisés.

Les paramètres proposent le français et l’anglais, les thèmes Clair, Sombre et Système et les arrière-plans Aucun, Mica et Acrylique. Les effets disponibles dépendent de Windows et de ses réglages d’accessibilité.

## Gestion des données

**Emplacement des données** affiche le fichier actif, généralement `%LocalAppData%\PresetLauncher\data.json`. Les fichiers de sauvegarde sont choisis séparément.

- **Exporter les données** crée une sauvegarde JSON versionnée et non chiffrée des presets, configurations, historiques et statistiques. Le fichier peut contenir des chemins d’exécutables, des arguments, des détails de lancement et des empreintes de mots de passe de presets. Conservez-le dans un emplacement privé. La sauvegarde exclut les programmes installés, les paramètres et la politique de mise à jour.
- **Importer les données** accepte les sauvegardes de version 1 et les anciens fichiers JSON de données jusqu’à 32 Mio. Vérifiez le récapitulatif avant de remplacer les données actuelles. La restauration conserve les identifiants, vérifie les chemins sur cet ordinateur et garde l’ancien fichier de données au même emplacement. Vous pouvez examiner les programmes introuvables après la restauration.
- **Examiner les références** affiche les exécutables introuvables par preset. Reconnectez les lecteurs ou réseaux indisponibles avant de confirmer. Le nettoyage retire les références examinées encore indisponibles et conserve les presets, l’historique, les statistiques et les fichiers exécutables.
- **Réinitialiser l’application** supprime les presets, l’historique des lancements et les statistiques, puis rétablit les paramètres par défaut. Les programmes installés, les fichiers exécutables externes et la version minimale protégée sont conservés. Exportez une sauvegarde si vous souhaitez récupérer les données plus tard.

Aucune commande ne permet de dupliquer ou d’exporter un seul preset, de modifier des arguments ou de verrouiller un preset par mot de passe. Les presets JSON importés peuvent contenir des arguments transmis à Windows lors du lancement.

## Mises à jour

PresetLauncher consulte GitHub Releases au démarrage et depuis les paramètres. Il vérifie la politique signée, la taille et l’empreinte SHA-256 de l’installeur avant de l’ouvrir.

- Les mises à jour facultatives peuvent être reportées ou ignorées.
- Les mises à jour recommandées peuvent être reportées de 24 heures.
- Les mises à jour obligatoires doivent être installées pour continuer à utiliser l’application.

Les versions minimales déjà connues restent imposées hors ligne. Les rappels des mises à jour recommandées sont vérifiés au démarrage. Les versions 1.2.6 et antérieures utilisent l’ancien serveur de mise à jour. Installez manuellement la dernière version si aucune mise à jour n’est proposée. Consultez la [politique de sécurité](../SECURITY.md).
