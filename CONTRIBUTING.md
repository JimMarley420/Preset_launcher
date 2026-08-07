# Contribuer a PresetLauncher

Merci de votre interet pour PresetLauncher ! Voici comment contribuer.

## Signaler un bug

1. Verifiez les [issues existantes](https://github.com/Wailroth/PresetLauncher/issues) pour eviter les doublons
2. Ouvrez une nouvelle issue avec le template "Bug report"
3. Fournissez un maximum de details (version, OS, etapes pour reproduire)

## Proposer une amelioreation

1. Ouvrez une issue avec le template "Feature request"
2. Decrivez le probleme et la solution souhaitee

## Contribuer au code

1. Fork le depot
2. Creez une branche pour votre modification :
   ```bash
   git checkout -b feature/nom-de-la-fonctionnalite
   ```
3. Faites vos changements
4. Testez votre modification
5. Committez avec un message clair :
   ```bash
   git commit -m "Ajouter: description du changement"
   ```
6. Push sur votre fork :
   ```bash
   git push origin feature/nom-de-la-fonctionnalite
   ```
7. Ouvrez une Pull Request

## Guidelines

- Suivez le style de code existant
- Utilisez les conventions C# et WinUI 3
- Testez vos modifications avant de soumettre
- Decrivez vos changements dans la Pull Request

## Build et developpement

```bash
# Compiler
dotnet build PresetLauncherWinUI3\PresetLauncherWinUI3.csproj

# Lancer en mode developpement
dotnet run --project PresetLauncherWinUI3\PresetLauncherWinUI3.csproj
```

## Licence

En contribuant, vous acceptez que vos contributions soient licencies sous la [MIT License](LICENSE).
