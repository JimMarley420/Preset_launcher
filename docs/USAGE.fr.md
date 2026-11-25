# Guide d'utilisation

[English](USAGE.md) · [PresetLauncher](../README.fr.md)

## Premiers pas

Installez la [dernière version](https://github.com/JimMarley420/Preset_launcher/releases/latest) sous Windows x64. L'installeur déclare le build 17763 de Windows 10 comme minimum. Le projet cible le build 19041 et la compatibilité avec les builds plus anciens n'a pas été vérifiée. Les environnements .NET et Windows App SDK sont inclus.

Créez un preset, donnez-lui un nom et ajoutez des programmes depuis le sélecteur de fichiers ou la liste des processus en cours. Chaque entrée peut avoir un délai et demander des droits administrateur. Windows détermine comment ouvrir les scripts et raccourcis. Vous pouvez rechercher les presets par nom ou description, les modifier, les importer depuis un fichier JSON ou les supprimer.

## Lancement des programmes

| Action | Comportement |
| --- | --- |
| Lancer tout | Lance les programmes dans l'ordre du preset, applique les délais et enregistre le lancement. |
| Lancer les manquants | Ignore les programmes détectés par leur chemin ou nom de fichier. Aucun délai ni entrée dans l'historique. |
| Fermer les processus | Demande la fermeture des processus correspondants et peut les arrêter de force après confirmation. |

Enregistrez votre travail avant de fermer les processus. Les programmes correspondants ouverts en dehors de PresetLauncher sont également concernés.

## Historique et apparence

L'historique enregistre les lancements complets, leur durée et les demandes de démarrage réussies. Une demande acceptée ne signifie pas que l'application a fini de charger. Les statistiques indiquent le nombre de presets, les lancements enregistrés et les dix chemins de programmes les plus utilisés.

Les paramètres proposent le français et l'anglais, les thèmes clair, sombre et système, Mica ou Acrylique sur les systèmes compatibles, le nettoyage des entrées invalides et la réinitialisation des données.

## Mises à jour

L'application recherche les mises à jour sur GitHub Releases au démarrage et depuis les paramètres. Elle vérifie la taille et l'empreinte SHA-256 de l'installeur avant de l'ouvrir.

* Les mises à jour facultatives peuvent être reportées ou ignorées.
* Les mises à jour recommandées peuvent être reportées de 24 heures, mais pas ignorées définitivement.
* Les mises à jour obligatoires doivent être installées pour continuer à utiliser l'application.

Les versions minimales déjà connues restent imposées hors ligne. Le rappel des mises à jour recommandées est vérifié au démarrage, pas pendant que l'application reste ouverte. Consultez la [politique de sécurité](../SECURITY.md) pour les détails de vérification.

Les versions 1.2.6 et antérieures utilisent l'ancien serveur de mise à jour. Si la mise à jour n'est pas proposée, installez manuellement la dernière version. Les presets et préférences sont conservés.

## Sauvegarde des données

Par défaut, les données sont stockées dans `%LocalAppData%\PresetLauncher\`.

| Fichier | Contenu |
| --- | --- |
| `data.json` | Presets, historique et statistiques |
| `settings.json` | Préférences de l'application |

La désinstallation conserve ces fichiers. Fermez l'application avant de copier les deux fichiers en lieu sûr ou de les restaurer sur un autre PC. Faites une sauvegarde avant de réinitialiser les données. Le bouton Parcourir dans les paramètres ne déplace pas le stockage actif et ne doit pas servir de sauvegarde.

Vérifiez les chemins, arguments et droits administrateur de tout preset importé avant son lancement.

## Limitations actuelles

Les filtres d'historique et la préférence de confirmation de lancement ne modifient pas encore le comportement. La duplication, l'export individuel, la modification des arguments et le verrouillage par mot de passe ne sont pas accessibles via des commandes fonctionnelles. Les presets JSON importés peuvent contenir des arguments transmis à Windows.
