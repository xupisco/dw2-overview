using System.Globalization;
using System.Text;
using System.Text.Json;
using DistantWorlds.Types;
using static DW2Overview.Snapshot;

namespace DW2Overview;

/// <summary>
/// The empire over time, for the page's charts: one point every game month (Constants.YearDateUnits / 12 of
/// Galaxy.StarDate, 50 s at speed 1), recorded whether or not a page is open, so the series has no gaps.
///
/// One JSON Lines file per game in data\Logs\DW2Overview\history, named after the galaxy's RandomSeed and the
/// player's empire id. Loading an earlier save of the same game cuts the points after its date: that future
/// did not happen in the game now being played.
///
/// A point: {"d": star date, "cash", "flow" (cashflow a year), "priv" (private sector cash), "pop", "cols",
///           "r": {"resource id": [stock, rate in /s, rate used /s, level to maintain, sell price]}}
/// The page works out each resource's supply state from those, with the same rule as the Resources tab.
/// </summary>
internal static class History
{
    private const long StepUnits = Constants.YearDateUnits / 12;

    private static readonly object gate = new();
    private static readonly List<(long date, string json)> points = new();
    private static string key;
    private static string path;
    private static string resourcesJson = "[]";
    private static int epoch;
    private static long lastDate = long.MinValue;
    private static int errors;

    /// <summary>On the game thread, every collection tick (about once a second) while a game is loaded.</summary>
    public static void Tick(Galaxy galaxy, Empire empire)
    {
        try
        {
            long date = galaxy.StarDate;
            string gameKey = $"{Safe(() => galaxy.RandomSeed)}_{empire.EmpireId}";
            if (gameKey != key)
            {
                Open(galaxy, empire, gameKey);
            }
            if (lastDate != long.MinValue && date < lastDate)
            {
                Rewind(date);
            }
            if (lastDate == long.MinValue || date >= lastDate + StepUnits)
            {
                Record(galaxy, empire, date);
            }
        }
        catch (Exception e)
        {
            if (errors++ < 10)
            {
                Log.Error("History: " + e);
            }
        }
    }

    /// <summary>A game was loaded (or another one): read its file, and the resource names for the page.</summary>
    private static void Open(Galaxy galaxy, Empire empire, string gameKey)
    {
        string dir = Path.Combine(Core.OutDir, "history");
        Directory.CreateDirectory(dir);
        var loaded = new List<(long, string)>();
        string file = Path.Combine(dir, gameKey + ".jsonl");
        if (File.Exists(file))
        {
            foreach (string line in File.ReadLines(file))
            {
                if (TryDate(line, out long d))
                {
                    loaded.Add((d, line));
                }
            }
            loaded.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }
        var resources = new List<object>();
        foreach (var res in Copy(Safe(() => galaxy.Resources)))
        {
            var cats = new[] { ("fuel", Safe(() => res.IsFuel)), ("construction", Safe(() => res.IsConstruction)), ("luxury", Safe(() => res.IsLuxury)) }
                .Where(c => c.Item2).Select(c => c.Item1).ToList();
            resources.Add(new { id = res.ResourceId, name = Safe(() => res.Name), category = cats.FirstOrDefault() ?? "other", categories = cats });
        }
        lock (gate)
        {
            key = gameKey;
            path = file;
            points.Clear();
            points.AddRange(loaded);
            lastDate = loaded.Count > 0 ? loaded[^1].Item1 : long.MinValue;
            resourcesJson = JsonSerializer.Serialize(resources);
            epoch++;
        }
        Log.Info($"History: {Safe(() => empire.Name)}, game {gameKey}: {loaded.Count} points in {file}");
    }

    /// <summary>An earlier save of this game: forget the points after its date, in memory and in the file.</summary>
    private static void Rewind(long date)
    {
        int before;
        lock (gate)
        {
            before = points.Count;
            points.RemoveAll(p => p.date > date);
            lastDate = points.Count > 0 ? points[^1].date : long.MinValue;
            epoch++;
            File.WriteAllLines(path, points.Select(p => p.json));
        }
        Log.Info($"History: back to {date} (an earlier save), {before - points.Count} later points dropped");
    }

    private static void Record(Galaxy galaxy, Empire empire, long date)
    {
        var stock = Amounts(Safe(() => empire.CalculateTotalCargoLevels(empire.EmpireId)));
        var current = Amounts(Safe(() => empire.ResourceRatesCurrent));
        var required = Amounts(Safe(() => empire.ResourceRatesRequired));
        var maintain = Amounts(Safe(() => empire.CalculateTotalStockLevels(false)));
        var sell = Amounts(Safe(() => empire.ResourcePricesSell));

        var w = new StringBuilder(2048);
        w.Append("{\"d\":").Append(date);
        Field(w, "cash", Safe(() => empire.Economy.Money), 0);
        float flow = Safe(() => empire.CalculateCashflowActual(1f, true));
        if (!(Math.Abs(flow) < 1e12f) || flow == 0f)
        {
            // Right after a load the cached value is float.MaxValue, or 0 (seen 2026-11-27 on the first point): not computed yet.
            flow = Safe(() => empire.CalculateCashflowActual(1f, false));
        }
        Field(w, "flow", flow, 1);
        Field(w, "priv", Safe(() => empire.Economy.PrivateMoney), 0);
        long pop = 0;
        int cols = 0;
        foreach (var orb in Copy(empire.Colonies))
        {
            cols++;
            pop += Safe(() => (long?)orb.Colony?.Population?.TotalAmount) ?? 0;
        }
        w.Append(",\"pop\":").Append(pop).Append(",\"cols\":").Append(cols);
        w.Append(",\"r\":{");
        bool first = true;
        foreach (var res in Copy(Safe(() => galaxy.Resources)))
        {
            short id = res.ResourceId;
            float s = stock.GetValueOrDefault(id), c = current.GetValueOrDefault(id), q = required.GetValueOrDefault(id), m = maintain.GetValueOrDefault(id);
            if (s <= 0 && c <= 0 && q <= 0 && m <= 0 && !Safe(() => res.IsConstruction) && !Safe(() => res.IsFuel))
            {
                continue;
            }
            w.Append(first ? "" : ",").Append('"').Append(id).Append("\":[");
            w.Append(Num(s, 0)).Append(',').Append(Num(c, 3)).Append(',').Append(Num(q, 3)).Append(',').Append(Num(m, 0)).Append(',').Append(Num(sell.GetValueOrDefault(id), 1)).Append(']');
            first = false;
        }
        w.Append("}}");
        string json = w.ToString();
        lock (gate)
        {
            points.Add((date, json));
            lastDate = date;
            File.AppendAllText(path, json + "\n");
        }
    }

    /// <summary>
    /// GET /api/history: the points after "since" (a star date) when the page already has the earlier ones of this
    /// epoch; all of them otherwise (another game, or an earlier save loaded since).
    /// </summary>
    public static string Json(long? since, int? pageEpoch)
    {
        lock (gate)
        {
            if (key == null)
            {
                return "{\"loaded\":false,\"points\":[]}";
            }
            bool incremental = since != null && pageEpoch == epoch;
            var w = new StringBuilder(points.Count * 1600 + 512);
            w.Append("{\"loaded\":true,\"game\":\"").Append(key).Append("\",\"epoch\":").Append(epoch)
                .Append(",\"incremental\":").Append(incremental ? "true" : "false")
                .Append(",\"yearUnits\":").Append(Constants.YearDateUnits)
                .Append(",\"startStarDate\":").Append(Constants.StartStarDate)
                .Append(",\"step\":").Append(StepUnits)
                .Append(",\"resources\":").Append(resourcesJson)
                .Append(",\"points\":[");
            bool first = true;
            foreach (var (date, json) in points)
            {
                if (incremental && date <= since)
                {
                    continue;
                }
                w.Append(first ? "" : ",").Append(json);
                first = false;
            }
            return w.Append("]}").ToString();
        }
    }

    private static bool TryDate(string line, out long date)
    {
        date = 0;
        try
        {
            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.TryGetProperty("d", out var d) && d.TryGetInt64(out date);
        }
        catch (JsonException)
        {
            return false;   // a line cut short when the game closed mid-write
        }
    }

    private static void Field(StringBuilder w, string name, float value, int digits)
    {
        if (float.IsFinite(value) && Math.Abs(value) < 1e12f)
        {
            w.Append(",\"").Append(name).Append("\":").Append(Num(value, digits));
        }
    }

    private static string Num(float value, int digits) =>
        float.IsFinite(value) ? Math.Round((double)value, digits).ToString(CultureInfo.InvariantCulture) : "0";

    private static Dictionary<short, float> Amounts(ResourceQuantityList list)
    {
        var amounts = new Dictionary<short, float>();
        foreach (var q in Copy(list))
        {
            amounts[q.ResourceId] = amounts.GetValueOrDefault(q.ResourceId) + q.Amount;
        }
        return amounts;
    }
}
