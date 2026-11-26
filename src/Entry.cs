using System.Text.Json;
using DistantWorlds2;
using HarmonyLib;

namespace DW2Overview;

/// <summary>Entry point for the game's own mod loader (mod.json mainClass).</summary>
public class Mod
{
    public Mod(DWGame game) => Core.Init("game mod loader", null);
}

/// <summary>Entry point for the DW2 Mod Launcher, which passes the settings.schema.json values.</summary>
public static class Entry
{
    public static void InitWithOptions(string json) => Core.Init("mod launcher", json);

    public static void Init() => Core.Init("mod launcher", null);
}

/// <summary>
/// Settings: the launcher's (settings.schema.json: Port), then the optional overview.json in the mod folder,
/// whose keys win over the launcher's:
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

    public static void Init(string via, string launcherJson)
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

        ApplyLauncherOptions(launcherJson);
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

    /// <summary>The launcher's settings (settings.schema.json). Its number fields are floats: "Port": 4800.0.</summary>
    private static void ApplyLauncherOptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryPort(doc.RootElement, "Port", out int port))
            {
                Config.Port = port;
            }
            Log.Info($"Launcher settings: port {Config.Port}");
        }
        catch (Exception e)
        {
            Log.Error("Launcher settings could not be read, using defaults: " + e.Message);
        }
    }

    /// <summary>overview.json, for development: only the keys it has replace the launcher's values.</summary>
    private static void LoadConfig()
    {
        string path = Path.Combine(ModDir, "overview.json");
        if (!File.Exists(path))
        {
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Name.Equals("port", StringComparison.OrdinalIgnoreCase) && TryPort(doc.RootElement, p.Name, out int port))
                {
                    Config.Port = port;
                }
                else if (p.Name.Equals("webRoot", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                {
                    Config.WebRoot = p.Value.GetString();
                }
            }
            Log.Info($"overview.json: port {Config.Port}, webRoot {Config.WebRoot ?? "(mod folder)"}");
        }
        catch (Exception e)
        {
            Log.Error("overview.json could not be read: " + e.Message);
        }
    }

    /// <summary>A port from a number (or a numeric string): a whole number from 1024 to 65535, else ignored (logged).</summary>
    private static bool TryPort(JsonElement root, string key, out int port)
    {
        port = 0;
        if (!root.TryGetProperty(key, out var v))
        {
            return false;
        }
        double value = v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double s) => s,
            _ => double.NaN,
        };
        if (double.IsNaN(value) || value < 1024 || value > 65535)
        {
            Log.Error($"Port {v} is not a number from 1024 to 65535; keeping {Config.Port}.");
            return false;
        }
        port = (int)Math.Round(value);
        return true;
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
