using System.Diagnostics;
using DistantWorlds.Types;
using DistantWorlds2;
using Stride.Input;

namespace DW2Overview;

/// <summary>
/// Ctrl+Alt+O in the game opens the page in the default browser. The first time a game is loaded in a
/// session, the game's brief on-screen message says so. The game's own shortcuts ignore modifiers
/// (Ctrl+Alt+letter also fires the letter), so at the first load the log says whether the player's key
/// mappings use O too.
/// </summary>
internal static class Hotkey
{
    public const string Name = "Ctrl+Alt+O";
    private static bool hinted;
    private static bool checkedMappings;

    public static string Url => $"http://localhost:{Core.Config.Port}/";

    /// <summary>Called every frame from the DWGame.Update postfix.</summary>
    public static void Tick(DWGame game, bool loaded)
    {
        try
        {
            var input = game.Input;
            if (input != null && input.HasKeyboard
                && (input.IsKeyDown(Keys.LeftCtrl) || input.IsKeyDown(Keys.RightCtrl))
                && (input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt))
                && input.IsKeyPressed(Keys.O))
            {
                OpenPage();
                Brief(game, "Empire Overview: opening " + Url, 2.5f);
            }
            if (loaded && !hinted)
            {
                hinted = true;
                Brief(game, $"Empire Overview: {Name} opens {Url}", 5f);
                CheckMappings(game);
            }
        }
        catch (Exception e)
        {
            Log.Error("Hotkey: " + e.Message);
        }
    }

    /// <summary>The default browser, off the game thread (starting it can take a moment).</summary>
    public static void OpenPage()
    {
        string url = Url;
        Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Log.Info("Opened the page in the browser: " + url);
            }
            catch (Exception e)
            {
                Log.Error("Could not open the browser: " + e.Message);
            }
        });
    }

    private static void Brief(DWGame game, string text, float seconds)
    {
        if (game.Renderer is ScaledRenderer renderer)
        {
            renderer.SetBriefMessage(text, seconds);
        }
    }

    /// <summary>Logs the player's key mappings on O: they would fire together with Ctrl+Alt+O.</summary>
    private static void CheckMappings(DWGame game)
    {
        if (checkedMappings)
        {
            return;
        }
        checkedMappings = true;
        var mappings = game.DWSettings?.KeyMappings;
        if (mappings == null)
        {
            return;
        }
        var onO = mappings.Where(m => m != null && m.Key.ToString() == "O")
            .Select(m => (m.ShiftActive ? "Shift+" : "") + "O: " + (m.TaskType != GameTaskType.Undefined ? m.TaskType.ToString() : m.UiSection.ToString()))
            .ToList();
        Log.Info(onO.Count == 0
            ? $"{Name}: no game shortcut uses O."
            : $"{Name}: the game also uses O ({string.Join(", ", onO)}); it fires together with the page shortcut.");
    }
}
