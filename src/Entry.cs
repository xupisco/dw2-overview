using System.Text.Json;
using DistantWorlds2;
using HarmonyLib;

namespace DW2Overview;

/// <summary>Entry point for the game's own mod loader (mod.json mainClass).</summary>
public class Mod
{
    public Mod(DWGame game) => Core.Init("game mod loader");
}

/// <summary>Entry point for the DW2 Mod Launcher.</summary>
public static class Entry
{
    public static void InitWithOptions(string json) => Core.Init("mod launcher");

    public static void Init() => Core.Init("mod launcher");
}

/// <summary>
/// Optional overview.json in the mod folder:
/// { "port": 4722, "webRoot": "D:\\path\\to\\Overview\\web" }
/// webRoot lets the page be edited in place while the game runs (F5 in the browser picks it up).
/// </summary>
internal sealed class Config
{
    public int Port { get; set; } = 4722;
    public string WebRoot { get; set; }
}

internal static class Core
{
    private static bool initialized;

    public static string ModDir { get; private set; }
    public static string OutDir { get; private set; }
    public static Config Config { get; private set; } = new();
    public static string Version => typeof(Core).Assembly.GetName().Version?.ToString(3) ?? "?";

    public static void Init(string via)
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        string gameDir = Path.GetDirectoryName(typeof(DWGame).Assembly.Location) ?? AppContext.BaseDirectory;
        ModDir = Path.GetDirectoryName(typeof(Core).Assembly.Location) ?? gameDir;
        OutDir = Path.Combine(gameDir, "data", "Logs", "DW2Overview");
        Directory.CreateDirectory(OutDir);
        Log.Start(Path.Combine(OutDir, "DW2Overview.log"));
        Log.Info($"DW2 Overview {Version} starting (via {via}). Mod folder: {ModDir}");

        LoadConfig();
        Install("Snapshot", Snapshot.Install);
        try
        {
            string webRoot = string.IsNullOrWhiteSpace(Config.WebRoot) ? Path.Combine(ModDir, "web") : Config.WebRoot;
            Server.Start(Config.Port, webRoot);
        }
        catch (Exception e)
        {
            Log.Error("Server failed to start: " + e);
        }
    }

    private static void LoadConfig()
    {
        string path = Path.Combine(ModDir, "overview.json");
        if (!File.Exists(path))
        {
            return;
        }
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            Config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path), options) ?? new Config();
            Log.Info($"Config: port {Config.Port}, webRoot {Config.WebRoot ?? "(mod folder)"}");
        }
        catch (Exception e)
        {
            Log.Error("overview.json could not be read, using defaults: " + e.Message);
        }
    }

    private static void Install(string feature, Action<Harmony> patch)
    {
        try
        {
            patch(new Harmony("dw2overview." + feature.Replace(' ', '-').ToLowerInvariant()));
            Log.Info("Installed: " + feature);
        }
        catch (Exception e)
        {
            Log.Error($"{feature} failed to install: {e}");
        }
    }
}
