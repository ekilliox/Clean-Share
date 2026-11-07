using System;
using Dalamud.Configuration;

namespace PartagePropre;

[Serializable]
public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>La fenêtre de partage est ouverte.</summary>
    public bool ShareEnabled { get; set; } = true;

    /// <summary>Fenêtre placée hors de l'écran.</summary>
    public bool OffScreen { get; set; } = true;

    /// <summary>Pas de bouton dans la barre des tâches.</summary>
    public bool HideTaskbar { get; set; } = true;
}
