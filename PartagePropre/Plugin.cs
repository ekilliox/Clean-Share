using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KernelDevice = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device;

namespace PartagePropre;

/// <summary>
/// Partage propre : une fenêtre "FFXIV Partage (sans menus)" montre le jeu SANS les fenêtres
/// Dalamud/plugins. On partage cette fenêtre dans Discord ; comme elle appartient au processus
/// du jeu, Discord prend le son du jeu avec. Réglages : bouton "Open" dans /xlplugins.
/// </summary>
public sealed unsafe class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly Config config;
    private readonly ShareWindow window;
    private readonly Mirror mirror = new();
    private readonly object gate = new();
    private bool disposed;
    private bool settingsOpen;
    private string? lastLoggedError;

    public Plugin(IDalamudPluginInterface pi, IChatGui chat, IPluginLog log)
    {
        this.pi = pi;
        this.chat = chat;
        this.log = log;
        this.config = pi.GetPluginConfig() as Config ?? new Config();
        Loc.Fr = (pi.UiLanguage ?? string.Empty).StartsWith("fr", StringComparison.OrdinalIgnoreCase);

        this.window = new ShareWindow
        {
            OffScreen = this.config.OffScreen,
            HideTaskbarButton = this.config.HideTaskbar,
        };

        // Notre Draw doit tourner même quand Dalamud cache les menus (cinématiques, gpose, interface cachée),
        // sinon l'image partagée se figerait.
        pi.UiBuilder.DisableAutomaticUiHide = true;
        pi.UiBuilder.DisableCutsceneUiHide = true;
        pi.UiBuilder.DisableGposeUiHide = true;
        pi.UiBuilder.DisableUserUiHide = true;

        pi.UiBuilder.Draw += this.OnDraw;
        pi.UiBuilder.OpenMainUi += this.OpenSettings;
        pi.UiBuilder.OpenConfigUi += this.OpenSettings;

        if (this.config.ShareEnabled) this.window.Show();
    }

    private void OpenSettings() => this.settingsOpen = true;

    private void Save() => this.pi.SavePluginConfig(this.config);

    // Appelé par Dalamud pendant qu'il prépare ses menus, AVANT de les dessiner sur l'image du jeu :
    // à ce moment-là l'image du jeu est encore "propre".
    private void OnDraw()
    {
        lock (this.gate)
        {
            if (this.disposed) return;
            this.CopyFrame();
        }

        // Fenêtre de réglages (elle-même invisible dans le partage, comme tous les menus Dalamud).
        if (this.settingsOpen) this.DrawSettings();
    }

    private void CopyFrame()
    {
        try
        {
            if (!this.window.IsVisible) return;

            var dev = KernelDevice.Instance();
            if (dev == null || dev->SwapChain == null || dev->SwapChain->DXGISwapChain == null) return;

            this.mirror.Frame((void*)dev->SwapChain->DXGISwapChain, this.window.Handle);
            this.window.EnsureSize(this.mirror.GameWidth, this.mirror.GameHeight);

            if (this.mirror.LastError is { } err && err != this.lastLoggedError)
            {
                this.lastLoggedError = err;
                this.log.Error($"PartagePropre: {err}");
                this.chat.PrintError(Loc.ErrorPrefix + err);
            }
        }
        catch (Exception e)
        {
            this.log.Error(e, "PartagePropre: error while copying the frame");
        }
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.FirstUseEver);
        if (ImGui.Begin(Loc.SettingsTitle + "###PartagePropreSettings", ref this.settingsOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(Loc.HowTo);
            ImGui.Separator();

            var enabled = this.config.ShareEnabled;
            if (ImGui.Checkbox(Loc.Enabled, ref enabled))
            {
                this.config.ShareEnabled = enabled;
                this.mirror.ClearError();
                if (enabled) this.window.Show();
                else this.window.Hide();
                this.Save();
            }

            var off = this.config.OffScreen;
            if (ImGui.Checkbox(Loc.OffScreen, ref off))
            {
                this.config.OffScreen = off;
                this.window.OffScreen = off;
                this.Save();
            }

            ImGui.TextDisabled(Loc.OffScreenHint);

            var tb = this.config.HideTaskbar;
            if (ImGui.Checkbox(Loc.Taskbar, ref tb))
            {
                this.config.HideTaskbar = tb;
                this.window.HideTaskbarButton = tb;
                this.Save();
            }

            ImGui.TextDisabled(Loc.TaskbarHint);
        }

        ImGui.End();
    }

    public void Dispose()
    {
        this.pi.UiBuilder.Draw -= this.OnDraw;
        this.pi.UiBuilder.OpenMainUi -= this.OpenSettings;
        this.pi.UiBuilder.OpenConfigUi -= this.OpenSettings;
        lock (this.gate)
        {
            this.disposed = true;
            this.mirror.Dispose();
        }

        this.window.Dispose();
    }
}
