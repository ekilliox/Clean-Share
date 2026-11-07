namespace PartagePropre;

/// <summary>Textes en français ou en anglais, selon la langue de Dalamud.</summary>
internal static class Loc
{
    public static bool Fr { get; set; }

    private static string T(string fr, string en) => Fr ? fr : en;

    public static string WindowTitle => T("FFXIV Partage (sans menus)", "FFXIV Share (no menus)");
    public static string SettingsTitle => T("Partage propre", "Clean Share");
    public static string HowTo => T(
        $"Dans Discord : Partager l'écran > Applications > « {WindowTitle} ».",
        $"In Discord: Share Your Screen > Applications > \"{WindowTitle}\".");
    public static string Enabled => T("Fenêtre de partage active", "Share window enabled");
    public static string OffScreen => T("Cacher la fenêtre hors de l'écran", "Hide the window off-screen");
    public static string OffScreenHint => T("Décoche si l'image est noire ou figée dans Discord.", "Uncheck if the picture is black or frozen in Discord.");
    public static string Taskbar => T("Cacher le bouton dans la barre des tâches", "Hide the taskbar button");
    public static string TaskbarHint => T("Décoche si Discord ne trouve plus la fenêtre.", "Uncheck if Discord can't find the window.");
    public static string ErrorPrefix => T("[Partage propre] Problème : ", "[Clean Share] Problem: ");
    public static string Failed(string what, int hr) => T($"{what} a échoué (0x{hr:X8})", $"{what} failed (0x{hr:X8})");
}
