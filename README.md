<img src="PartagePropre/images/icon.png" width="96" align="right">

# Partage propre

Plugin Dalamud pour **FINAL FANTASY XIV** : partage le jeu sur **Discord** **sans les fenêtres des plugins** (Dalamud / ImGui), avec le son du jeu.

## Comment ça marche

Le plugin ouvre une fenêtre « **FFXIV Partage (sans menus)** » qui affiche l'image du jeu *avant* que Dalamud y dessine ses menus (comme la « Capture de jeu » d'OBS).
Cette fenêtre appartient au jeu, donc Discord prend aussi le son du jeu quand tu la partages.

- Même résolution que le jeu.
- Cachée hors de l'écran et sans bouton dans la barre des tâches : tu ne la vois pas, Discord si.
- Ce que le **jeu** dessine lui-même (chat, barres de vie…) reste visible. Seules les fenêtres des plugins disparaissent.

## Installation

1. Dans le jeu, tape `/xlsettings` → onglet **Expérimental**.
2. Dans **Custom Plugin Repositories** (dépôts de plugins personnalisés), colle ce lien :

   ```
   https://raw.githubusercontent.com/ekilliox/PartagePropre/main/repo.json
   ```

   coche la case **Activé**, clique sur **+** puis **Sauvegarder et fermer**.
3. Tape `/xlplugins`, cherche **Partage propre** et installe-le.

## Utilisation

1. Discord → **Partager l'écran** → onglet **Applications** → **FFXIV Partage (sans menus)**.
2. Réglages : `/xlplugins` → Partage propre → bouton **Open** :
   - **Fenêtre de partage active**
   - **Cacher la fenêtre hors de l'écran** (décoche si l'image est noire ou figée dans Discord)
   - **Cacher le bouton dans la barre des tâches** (décoche si Discord ne trouve pas la fenêtre)

## À savoir

- Comme tout plugin Dalamud, c'est un outil tiers non autorisé par Square Enix. Ce plugin ne modifie pas le jeu : il copie seulement l'image.
- Fait pour Dalamud API 15. Après une grosse mise à jour de Dalamud, il faudra peut-être une nouvelle version.

## Compiler soi-même

Besoin du SDK .NET 10 et de Dalamud installé (XIVLauncher) :

```
cd PartagePropre
dotnet build -c Release
```

Le paquet est créé dans `PartagePropre/bin/Release/PartagePropre/latest.zip`.
