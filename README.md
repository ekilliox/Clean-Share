<img src="PartagePropre/images/icon.png" width="96" align="right">

# Partage propre (Clean Share)

[English](#english) · [Français](#français)

## English

Dalamud plugin for **FINAL FANTASY XIV**: share the game on **Discord** **without plugin windows** (Dalamud / ImGui), with game audio.

### How it works

The plugin opens a "**FFXIV Share (no menus)**" window showing the game picture *before* Dalamud draws its menus on it (like OBS "Game Capture").
That window belongs to the game process, so Discord also picks up the game audio when you share it.

- Same resolution as the game.
- Hidden off-screen and without a taskbar button: you don't see it, Discord does.
- What the **game** draws itself (chat, HP bars…) stays visible. Only plugin windows are removed.

### Install

1. In game, type `/xlsettings` → **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste:

   ```
   https://raw.githubusercontent.com/ekilliox/PartagePropre/main/repo.json
   ```

   tick **Enabled**, click **+** then **Save and Close**.
3. Type `/xlplugins`, search **Partage propre (Clean Share)** and install it.

### Usage

1. Discord → **Share Your Screen** → **Applications** tab → **FFXIV Share (no menus)**.
2. Settings: `/xlplugins` → Clean Share → **Open** button:
   - **Share window enabled**
   - **Hide the window off-screen** (uncheck if the picture is black or frozen in Discord)
   - **Hide the taskbar button** (uncheck if Discord can't find the window)

The plugin follows Dalamud's language (French or English).

### Good to know

- Like every Dalamud plugin, this is a third-party tool not allowed by Square Enix. It does not modify the game: it only copies the picture.
- Built for Dalamud API 15. A big Dalamud update may need a new version.

### Build it yourself

Needs the .NET 10 SDK and Dalamud installed (XIVLauncher):

```
cd PartagePropre
dotnet build -c Release
```

The package is created in `PartagePropre/bin/Release/PartagePropre/latest.zip`.

## Français

Plugin Dalamud pour **FINAL FANTASY XIV** : partage le jeu sur **Discord** **sans les fenêtres des plugins** (Dalamud / ImGui), avec le son du jeu.

### Comment ça marche

Le plugin ouvre une fenêtre « **FFXIV Partage (sans menus)** » qui affiche l'image du jeu *avant* que Dalamud y dessine ses menus (comme la « Capture de jeu » d'OBS).
Cette fenêtre appartient au jeu, donc Discord prend aussi le son du jeu quand tu la partages.

- Même résolution que le jeu.
- Cachée hors de l'écran et sans bouton dans la barre des tâches : tu ne la vois pas, Discord si.
- Ce que le **jeu** dessine lui-même (chat, barres de vie…) reste visible. Seules les fenêtres des plugins disparaissent.

### Installation

1. Dans le jeu, tape `/xlsettings` → onglet **Expérimental**.
2. Dans **Custom Plugin Repositories** (dépôts de plugins personnalisés), colle ce lien :

   ```
   https://raw.githubusercontent.com/ekilliox/PartagePropre/main/repo.json
   ```

   coche la case **Activé**, clique sur **+** puis **Sauvegarder et fermer**.
3. Tape `/xlplugins`, cherche **Partage propre (Clean Share)** et installe-le.

### Utilisation

1. Discord → **Partager l'écran** → onglet **Applications** → **FFXIV Partage (sans menus)**.
2. Réglages : `/xlplugins` → Partage propre → bouton **Open** :
   - **Fenêtre de partage active**
   - **Cacher la fenêtre hors de l'écran** (décoche si l'image est noire ou figée dans Discord)
   - **Cacher le bouton dans la barre des tâches** (décoche si Discord ne trouve pas la fenêtre)

Le plugin suit la langue de Dalamud (français ou anglais).

### À savoir

- Comme tout plugin Dalamud, c'est un outil tiers non autorisé par Square Enix. Ce plugin ne modifie pas le jeu : il copie seulement l'image.
- Fait pour Dalamud API 15. Après une grosse mise à jour de Dalamud, il faudra peut-être une nouvelle version.

### Compiler soi-même

Besoin du SDK .NET 10 et de Dalamud installé (XIVLauncher) :

```
cd PartagePropre
dotnet build -c Release
```

Le paquet est créé dans `PartagePropre/bin/Release/PartagePropre/latest.zip`.
