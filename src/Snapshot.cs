using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistantWorlds.Types;
using DistantWorlds2;
using HarmonyLib;

namespace DW2Overview;

/// <summary>One row of the construction list.</summary>
internal sealed class SiteResource
{
    public string Name { get; set; }
    public int Abundance { get; set; }
}

internal sealed class BuildItem
{
    /// <summary>ship, base or facility.</summary>
    public string Category { get; set; }
    /// <summary>building (in a yard), queued (waiting for a yard), outsideYard (under construction or repair, not found in any yard), facility, idle (a yard with nothing in it).</summary>
    public string State { get; set; }
    /// <summary>Construction, Repair, Retrofit, Retire (the game's ConstructionType), or Facility.</summary>
    public string Work { get; set; }
    public string Name { get; set; }
    public string Design { get; set; }
    public string Role { get; set; }
    /// <summary>The role as the game writes it (TextHelper.ResolveDescription): "Small Freighter", not FreighterSmall.</summary>
    public string RoleName { get; set; }
    /// <summary>The game's own value (ConstructionLevel or ConstructionProgress); scale still to be confirmed.</summary>
    public double? Progress { get; set; }
    public string Where { get; set; }
    public string WhereKind { get; set; }
    public string System { get; set; }
    public int? Queue { get; set; }
    public int? ShipId { get; set; }
    public int? OrbId { get; set; }
    /// <summary>The yard owner (or the facility's colony), for "go to": "ship" (ShipId) or "orb" (OrbId).</summary>
    public string WhereType { get; set; }
    public int? WhereId { get; set; }
    /// <summary>A base built in place: the planet, moon or asteroid it sits on (Ship.Parent).</summary>
    public string Site { get; set; }
    public int? SiteOrbId { get; set; }
    /// <summary>A mining base: the resources known at its site, most abundant first.</summary>
    public List<SiteResource> Extracts { get; set; }

    public BuildItem At(Where where)
    {
        Where = where.Name;
        WhereKind = where.Kind;
        System = where.System;
        WhereType = where.Type;
        WhereId = where.Id;
        return this;
    }
}

/// <summary>A yard owner or colony: its name, kind (ship, base, colony), system, and how to find it again (type + id).</summary>
internal readonly record struct Where(string Name, string Kind, string System, string Type, int Id);

/// <summary>
/// Reads the game's data on the game thread (DWGame.Update postfix), at most once a second and only while
/// someone has asked for it recently, and keeps the result as ready-made JSON for the server thread.
/// </summary>
internal static class Snapshot
{
    private const double IntervalSeconds = 1.0;
    private const double KeepCollectingSeconds = 10.0;

    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static readonly JsonSerializerOptions json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private static double nextCollect;
    private static double lastRequest = double.NegativeInfinity;
    private static bool dumped;
    private static int errors;

    public static volatile string InfoJson = "{\"loaded\":false}";
    public static volatile string ConstructionJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string ColoniesJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string ResearchJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string MessagesJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string IndexJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string ShipsJson = "{\"loaded\":false,\"items\":[]}";
    public static volatile string ResourcesJson = "{\"loaded\":false,\"items\":[],\"deposits\":[]}";
    private const double ResourcesIntervalSeconds = 3.0;
    private static double nextResources;
    private static double lastResourcesRequest = double.NegativeInfinity;
    private static bool resourcesLogged;

    /// <summary>The Resources tab asked: collect resources and deposits now and while it keeps asking.</summary>
    public static void TouchResources()
    {
        double now = clock.Elapsed.TotalSeconds;
        if (now - Volatile.Read(ref lastResourcesRequest) >= KeepCollectingSeconds)
        {
            nextResources = 0;
        }
        Volatile.Write(ref lastResourcesRequest, now);
        Touch();
    }
    private static double lastShipsRequest = double.NegativeInfinity;
    private static string lastPendingSig = "";
    private static bool shipsLogged;

    /// <summary>The Ships tab asked: collect the ship list from the next tick on (it is skipped otherwise).</summary>
    public static void TouchShips()
    {
        Volatile.Write(ref lastShipsRequest, clock.Elapsed.TotalSeconds);
        Touch();
    }

    private const int MessageLimit = 200;
    private const double IndexIntervalSeconds = 10.0;
    private static double nextIndex;
    private static double lastIndexRequest = double.NegativeInfinity;
    private static bool indexLogged;

    /// <summary>Called by the server on each API request, so collection runs only while a page is open.</summary>
    public static void Touch() => Volatile.Write(ref lastRequest, clock.Elapsed.TotalSeconds);

    /// <summary>The palette asked for the index: keep it fresh, and build it right away if it is stale.</summary>
    public static void TouchIndex()
    {
        double now = clock.Elapsed.TotalSeconds;
        if (now - Volatile.Read(ref lastIndexRequest) >= KeepCollectingSeconds)
        {
            nextIndex = 0;
        }
        Volatile.Write(ref lastIndexRequest, now);
        Touch();
    }

    public static void Install(Harmony h)
    {
        h.Patch(AccessTools.Method(typeof(DWGame), "Update"),
            postfix: new HarmonyMethod(typeof(Snapshot), nameof(UpdatePostfix)));
    }

    private static void UpdatePostfix(DWGame __instance, Stride.Games.GameTime gameTime)
    {
        Commands.Run(__instance, gameTime);
        Audio.Tick(__instance);
        Hotkey.Tick(__instance, __instance.Galaxy != null && (__instance.Renderer as ScaledRenderer)?.PlayerEmpire != null);
        double now = clock.Elapsed.TotalSeconds;
        if (now < nextCollect)
        {
            return;
        }
        nextCollect = now + IntervalSeconds;
        try
        {
            var galaxy = __instance.Galaxy;
            var empire = (__instance.Renderer as ScaledRenderer)?.PlayerEmpire;
            bool loaded = galaxy != null && empire != null;
            if (!loaded)
            {
                InfoJson = Serialize(new { api = 1, mod = Core.Version, loaded = false, token = Commands.Token });
                ConstructionJson = ColoniesJson = ResearchJson = MessagesJson = IndexJson = ShipsJson = ResourcesJson = "{\"loaded\":false,\"items\":[]}";
                indexLogged = false;
                nextIndex = 0;
                return;
            }

            bool wanted = now - Volatile.Read(ref lastRequest) < KeepCollectingSeconds || !dumped;
            if (!wanted)
            {
                return;
            }

            var sw = Stopwatch.StartNew();
            var stats = new Dictionary<string, int>();
            var items = Collect(galaxy, empire, stats);
            double constructionMs = sw.Elapsed.TotalMilliseconds;
            var colonies = Views.Colonies(galaxy, empire);
            double coloniesMs = sw.Elapsed.TotalMilliseconds - constructionMs;
            var (research, concurrent) = Views.Research(empire);
            double researchMs = sw.Elapsed.TotalMilliseconds - constructionMs - coloniesMs;
            double mark = sw.Elapsed.TotalMilliseconds;
            var messages = Views.Messages(galaxy, empire, MessageLimit);
            double messagesMs = sw.Elapsed.TotalMilliseconds - mark;

            // Resources and deposits: only while the Resources tab asks, and at most every few seconds
            // (it walks every orb the empire knows).
            double resourcesMs = 0;
            if (now >= nextResources && now - Volatile.Read(ref lastResourcesRequest) < KeepCollectingSeconds)
            {
                nextResources = now + ResourcesIntervalSeconds;
                mark = sw.Elapsed.TotalMilliseconds;
                var (resources, deposits) = ResourcesView.Collect(galaxy, empire);
                resourcesMs = sw.Elapsed.TotalMilliseconds - mark;
                ResourcesJson = Serialize(new { loaded = true, collectedAt = DateTime.Now.ToString("HH:mm:ss"), ms = Math.Round(resourcesMs, 2), items = resources, deposits });
                if (!resourcesLogged)
                {
                    resourcesLogged = true;
                    Log.Info($"First resources: {resources.Count} resources, {deposits.Count} deposits in {resourcesMs:0.00} ms");
                    foreach (var r in resources)
                    {
                        Log.Info($"  {r.Name,-18} {r.Category,-12} stock={r.Stock} maintain={r.Maintain} req={r.RateRequired} cur={r.RateCurrent} bld={r.RateBuilding} known={r.Known} sell={r.PriceSell} buy={r.PriceBuy} base={r.BaseCost} met={r.RateMet} critical={r.Critical} deposits={r.Mined}/{r.Deposits}");
                    }
                    foreach (var d in deposits.Take(15))
                    {
                        Log.Info($"  deposit {d.Name} ({d.Kind}, {d.System}) mined={d.MinedBy}: " + string.Join(", ", d.Resources.Select(x => $"{x.Name} {x.Abundance}")));
                    }
                }
            }

            // Every own ship with its mission: only while the Ships tab asks for it.
            double shipsMs = 0;
            if (now - Volatile.Read(ref lastShipsRequest) < KeepCollectingSeconds)
            {
                mark = sw.Elapsed.TotalMilliseconds;
                var ships = Views.Ships(galaxy, empire);
                shipsMs = sw.Elapsed.TotalMilliseconds - mark;
                var abandoned = Views.Abandoned(galaxy, empire);
                ShipsJson = Serialize(new { loaded = true, collectedAt = DateTime.Now.ToString("HH:mm:ss"), items = ships, abandoned });
                if (!shipsLogged)
                {
                    shipsLogged = true;
                    Log.Info($"First ships list: {ships.Count} in {shipsMs:0.00} ms; missions: "
                        + string.Join(", ", ships.GroupBy(s => s.Mission).Select(g => $"{g.Key}={g.Count()}"))
                        + "; automation: " + string.Join(", ", ships.GroupBy(s => s.Automation).Select(g => $"{g.Key}={g.Count()}"))
                        + "; sample: " + string.Join(" | ", ships.Take(6).Select(s => $"{s.Name} {s.Mission}->{s.Target} dmg={s.Damage} fuel={s.Fuel} docked={s.Docked}")));
                }
            }

            // The palette's index is bigger and changes slowly: rebuilt at most every IndexIntervalSeconds,
            // and only while a page asked for it recently.
            double indexMs = 0;
            if (now >= nextIndex && now - Volatile.Read(ref lastIndexRequest) < KeepCollectingSeconds)
            {
                nextIndex = now + IndexIntervalSeconds;
                mark = sw.Elapsed.TotalMilliseconds;
                var index = Views.Index(galaxy, empire, out int knownCount);
                indexMs = sw.Elapsed.TotalMilliseconds - mark;
                IndexJson = Serialize(new { loaded = true, collectedAt = DateTime.Now.ToString("HH:mm:ss"), knownCount, ms = Math.Round(indexMs, 2), items = index });
                if (!indexLogged)
                {
                    indexLogged = true;
                    Log.Info($"First index: {index.Count} items ({knownCount} from ResolveKnownItems) in {indexMs:0.00} ms; kinds: "
                        + string.Join(", ", index.GroupBy(i => i.Kind).Select(g => $"{g.Key}={g.Count()}")));
                }
            }
            sw.Stop();

            var collectedAt = DateTime.Now.ToString("HH:mm:ss");
            InfoJson = Serialize(new
            {
                api = 1,
                mod = Core.Version,
                loaded = true,
                empire = Safe(() => empire.Name),
                starDate = Safe(() => galaxy.StarDate),
                starDateText = Safe(() => TextHelper.ResolveStarDateDescription(galaxy.StarDate)),
                token = Commands.Token,
                economy = Views.Economy(empire),
                // What each tab of the page shows next to its name, from data already collected in this tick.
                counts = new
                {
                    construction = items.Count(i => i.State != "idle"),
                    ships = Copy(empire.Ships).Count,
                    colonies = colonies.Count,
                    research = research.Count(r => r.State == "active" || r.State == "queued"),
                    messages = messages.Count(m => !m.Pending),
                    pending = messages.Count(m => m.Pending),
                },
                lastAnswer = Commands.LastAnswer,
                audio = new { muted = Audio.Muted, music = Audio.Music, musicRestore = Audio.MusicRestore },
                paused = Safe(() => __instance.GetGamePaused()),
                // Paused by the game itself (an event or a decision waiting), not by the player.
                autoPaused = Safe(() => __instance.AutoPaused),
                speed = Safe(() => __instance.GetGameSpeed()),
                collectedAt,
                collectMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                timings = new
                {
                    construction = Math.Round(constructionMs, 2),
                    colonies = Math.Round(coloniesMs, 2),
                    research = Math.Round(researchMs, 2),
                    messages = Math.Round(messagesMs, 2),
                    ships = Math.Round(shipsMs, 2),
                    resources = Math.Round(resourcesMs, 2),
                    index = Math.Round(indexMs, 2),
                },
                stats,
            });
            ConstructionJson = Serialize(new { loaded = true, collectedAt, items });
            ColoniesJson = Serialize(new { loaded = true, collectedAt, items = colonies });
            ResearchJson = Serialize(new { loaded = true, collectedAt, concurrent, items = research });
            MessagesJson = Serialize(new { loaded = true, collectedAt, items = messages });

            // Advisor suggestions waiting for a decision: logged whenever the set changes, to learn their shape.
            string pendingSig = string.Join(";", messages.Where(m => m.Pending).Select(m => m.Key));
            if (pendingSig != lastPendingSig)
            {
                lastPendingSig = pendingSig;
                foreach (var m in messages.Where(m => m.Pending))
                {
                    Log.Info($"Pending suggestion: [{m.Priority} {m.Type}/{m.SubType} advisor={m.Advisor}] {m.Title} | {m.Text?.Replace('\n', ' ')} -> {m.RelatedName} ({m.RelatedType} {m.RelatedId}) task={m.Task}");
                }
            }

            if (!dumped)
            {
                dumped = true;
                Dump(items, stats, sw.Elapsed.TotalMilliseconds);
                Log.Info($"Colonies ({coloniesMs:0.00} ms): {ColoniesJson}");
                Log.Info($"Research ({researchMs:0.00} ms): {ResearchJson}");
                Log.Info($"Messages ({messagesMs:0.00} ms): {messages.Count} shown of {Copy(Safe(() => empire.Messages)).Count} " +
                    $"(RecentMessages {Copy(Safe(() => empire.RecentMessages)).Count}); newest: " +
                    string.Join(" | ", messages.Take(5).Select(m => $"[{m.Priority} {m.Type}/{m.SubType}{(m.Advisor ? " advisor" : "")}] {m.Title} -> {m.RelatedName} task={m.Task}")));
            }
        }
        catch (Exception e)
        {
            if (errors++ < 20)
            {
                Log.Error("Collect: " + e);
            }
        }
    }

    private static List<BuildItem> Collect(Galaxy galaxy, Empire empire, Dictionary<string, int> stats)
    {
        var items = new List<BuildItem>();
        var seenShips = new HashSet<int>();

        // Yards on ships: spaceports, other bases with construction yards, construction ships.
        foreach (var owner in Copy(empire.Ships))
        {
            var cs = Safe(() => owner.ConstructionSystem);
            if (cs != null)
            {
                Count(stats, "yardOwnerShips");
                var where = new Where(Safe(() => owner.Name), owner.IsBase ? "base" : "ship", SystemName(galaxy, owner), "ship", owner.ShipId);
                ReadYards(galaxy, empire, cs, where, items, seenShips, stats);
            }
        }

        // Yards on colonies, and facilities under construction.
        foreach (var orb in Copy(empire.Colonies))
        {
            Count(stats, "colonies");
            var where = new Where(Safe(() => orb.Name), "colony", SystemName(galaxy, orb), "orb", orb.OrbId);
            var cs = Safe(() => orb.Colony?.ConstructionSystem);
            if (cs != null)
            {
                Count(stats, "yardOwnerColonies");
                ReadYards(galaxy, empire, cs, where, items, seenShips, stats);
            }
            foreach (var facility in Copy(Safe(() => orb.Facilities)))
            {
                Count(stats, "facilities");
                float progress = Safe(() => facility.ConstructionProgress);
                if (progress >= 1f)
                {
                    continue;
                }
                items.Add(new BuildItem
                {
                    Category = "facility",
                    State = "facility",
                    Work = "Facility",
                    Name = Safe(() => facility.Name),
                    Progress = Clean(progress),
                    OrbId = orb.OrbId,
                }.At(where));
            }
        }

        // Ships the game says are under construction or repair but that no yard listed (e.g. bases being built in place).
        foreach (var ship in Copy(empire.Ships))
        {
            if (seenShips.Contains(ship.ShipId) || !Safe(() => ship.IsUnderConstructionOrRepair))
            {
                continue;
            }
            Count(stats, "outsideYard");
            items.Add(AddSite(galaxy, empire, ship, new BuildItem
            {
                Category = ship.IsBase ? "base" : "ship",
                State = "outsideYard",
                Work = "ConstructionOrRepair",
                Name = Safe(() => ship.Name),
                Design = Safe(() => ship.Design?.Name),
                Role = Safe(() => ship.Role.ToString()),
                RoleName = Safe(() => TextHelper.ResolveDescription(ship.Role)),
                Progress = Clean(Safe(() => ship.ConstructionLevel)),
                System = SystemName(galaxy, ship),
                ShipId = ship.ShipId,
                WhereType = "ship",
                WhereId = ship.ShipId,
            }));
        }
        AddQueuedTargets(galaxy, empire, items, stats);
        return items;
    }

    /// <summary>
    /// Bases the player (or the advisor) ordered and no construction ship has taken yet: the empire's queued
    /// targets (Empire.QueuedMiningTargets and the like; seen 2026-11-26: a mining station ordered at Kintaris 3
    /// shows in the game's New Mining Locations with a cancel button and was nowhere in Construction). A place
    /// a ship is already on its way to, or building at, is left out.
    /// </summary>
    private static void AddQueuedTargets(Galaxy galaxy, Empire empire, List<BuildItem> items, Dictionary<string, int> stats)
    {
        var taken = new HashSet<int>(items.Where(i => i.SiteOrbId != null).Select(i => i.SiteOrbId.Value));
        var lists = new (string role, string name, OrbList orbs)[]
        {
            ("MiningStation", "Mining Station", Safe(() => empire.QueuedMiningTargets)),
            ("ResearchStation", "Research Station", Safe(() => empire.QueuedResearchTargets)),
            ("SpaceportSmall", "Spaceport", Safe(() => empire.QueuedSpaceportTargets)),
            ("MonitoringStation", "Monitoring Station", Safe(() => empire.QueuedMonitoringTargets)),
            ("ResortBase", "Resort", Safe(() => empire.QueuedResortTargets)),
            ("Outpost", "Outpost", Safe(() => empire.QueuedOutpostTargets)),
            ("ColonyShip", "Colony", Safe(() => empire.QueuedColonizationTargets)),
        };
        var sig = new List<string>();
        foreach (var (role, name, orbs) in lists)
        {
            foreach (var orb in Copy(orbs))
            {
                if (orb == null || !taken.Add(orb.OrbId))
                {
                    continue;
                }
                Count(stats, "queuedTargets");
                var item = new BuildItem
                {
                    Category = role == "ColonyShip" ? "ship" : "base",
                    State = "planned",
                    Work = "Build",
                    Name = name,
                    Role = role,
                    RoleName = name,
                    System = SystemName(galaxy, orb),
                    OrbId = orb.OrbId,
                    WhereType = "orb",
                    WhereId = orb.OrbId,
                };
                OrbSite(empire, orb, item);
                items.Add(item);
                sig.Add($"{name} at {item.Site}");
            }
        }
        string text = string.Join(", ", sig);
        if (text != lastQueued)
        {
            lastQueued = text;
            Log.Info($"Queued targets waiting for a ship: {(sig.Count == 0 ? "none" : text)}.");
        }
    }

    private static string lastQueued;

    private static void ReadYards(Galaxy galaxy, Empire empire, ConstructionSystem cs, Where where,
        List<BuildItem> items, HashSet<int> seenShips, Dictionary<string, int> stats)
    {
        foreach (var yard in Copy(Safe(() => cs.Yards)))
        {
            Count(stats, "yards");
            // IsEmpty said false for idle yards in the first test (2026-11-26), so judge by the content instead.
            if (yard.ShipUnderConstructionId <= 0 && yard.ConstructionType == ConstructionType.Undefined)
            {
                // A construction ship on its way to build a base has an empty yard but is not idle (seen 2026-11-26:
                // Lone Rebel, "Build MS-3 (Large Mining Station) at Kintaris 2", counted as an idle yard).
                var enRoute = EnRoute(galaxy, empire, where);
                if (enRoute != null)
                {
                    Count(stats, "yardsEnRoute");
                    items.Add(enRoute.At(where));
                    continue;
                }
                Count(stats, "yardsIdle");
                items.Add(new BuildItem { Category = "yard", State = "idle" }.At(where));
                continue;
            }
            Count(stats, "yardsBusy");
            items.Add(Describe(galaxy, empire, yard.ShipUnderConstructionId, yard.DesignId, yard.ConstructionType, "building", null, seenShips).At(where));
        }
        int position = 0;
        foreach (var wait in Copy(Safe(() => cs.WaitQueue)))
        {
            position++;
            Count(stats, "queued");
            items.Add(Describe(galaxy, empire, wait.ShipUnderConstructionId, wait.DesignId, wait.ConstructionType, "queued", position, seenShips).At(where));
        }
    }

    private static BuildItem Describe(Galaxy galaxy, Empire empire, int shipId, int designId, ConstructionType type, string state, int? queue,
        HashSet<int> seenShips)
    {
        var ship = shipId > 0 ? Safe(() => galaxy.Ships.GetById(shipId)) : null;
        if (ship != null)
        {
            seenShips.Add(ship.ShipId);
        }
        var design = Safe(() => empire.Designs.GetById(designId)) ?? Safe(() => galaxy.Designs.GetById(designId)) ?? Safe(() => ship?.Design);
        return AddSite(galaxy, empire, ship, new BuildItem
        {
            Category = ship != null && ship.IsBase ? "base" : "ship",
            State = state,
            Work = type.ToString(),
            Name = ship != null ? Safe(() => ship.Name) : null,
            Design = Safe(() => design?.Name),
            Role = ship != null ? Safe(() => ship.Role.ToString()) : null,
            RoleName = ship != null ? Safe(() => TextHelper.ResolveDescription(ship.Role)) : null,
            Progress = ship != null ? Clean(Safe(() => ship.ConstructionLevel)) : null,
            Queue = queue,
            ShipId = shipId > 0 ? shipId : null,
        });
    }

    /// <summary>
    /// The base a construction ship is on its way to build (ShipMissionType.Build: BuildDesignId, PrimaryTarget),
    /// or null when the yard's owner is not a ship on such a mission.
    /// </summary>
    private static BuildItem EnRoute(Galaxy galaxy, Empire empire, Where where)
    {
        if (where.Type != "ship")
        {
            return null;
        }
        var owner = Safe(() => galaxy.Ships.GetById(where.Id));
        var mission = Safe(() => owner?.Mission);
        if (mission == null || mission.Type != ShipMissionType.Build)
        {
            return null;
        }
        var design = Safe(() => empire.Designs.GetById(mission.BuildDesignId)) ?? Safe(() => galaxy.Designs.GetById(mission.BuildDesignId));
        var item = new BuildItem
        {
            Category = "base",
            State = "enroute",
            Work = "Build",
            Name = Safe(() => design?.Name),
            Design = Safe(() => design?.Name),
            Role = Safe(() => design?.GetRole(galaxy).ToString()),
            RoleName = Safe(() => design == null ? null : TextHelper.ResolveDescription(design.GetRole(galaxy))),
        };
        // The mission has two targets (where the materials come from, where to build); the site is the one the
        // game's description names ("Build MS-3 (Large Mining Station) at Kintaris 2"), else the secondary, else the primary.
        string text = Safe(() => TextHelper.ResolveMissionDescription(galaxy, empire, owner, mission)) ?? "";
        var first = Safe(() => mission.PrimaryTarget.Resolve(galaxy)) as Orb;
        var second = Safe(() => mission.SecondaryTarget.Resolve(galaxy)) as Orb;
        bool Named(Orb o) => o != null && !string.IsNullOrEmpty(o.Name) && text.Contains("at " + o.Name, StringComparison.OrdinalIgnoreCase);
        var orb = Named(second) ? second : Named(first) ? first : second ?? first;
        if (orb != null)
        {
            OrbSite(empire, orb, item);
        }
        if (!string.IsNullOrEmpty(text))
        {
            item.Name = text.StartsWith("Build ", StringComparison.OrdinalIgnoreCase) && text.Contains(" at ")
                ? text[6..text.LastIndexOf(" at ", StringComparison.Ordinal)]   // "MS-3 (Large Mining Station)"
                : item.Name;
        }
        return item;
    }

    /// <summary>
    /// Where a base being built in place will stand (a construction ship builds it on a planet, moon or
    /// asteroid: the base already exists with Ship.Parent there), and for a mining base what that site holds.
    /// </summary>
    private static BuildItem AddSite(Galaxy galaxy, Empire empire, Ship ship, BuildItem item)
    {
        if (ship == null || !ship.IsBase)
        {
            return item;
        }
        var parent = Safe(() => ship.Parent);   // a struct: the type and id of what the base sits on
        var orb = parent.Type == ItemType.Orb ? Safe(() => galaxy.Orbs.GetById(parent.Id)) : null;
        if (orb == null)
        {
            return item;
        }
        OrbSite(empire, orb, item);
        return item;
    }

    /// <summary>The planet, moon or asteroid a base stands on, and for a mining base what it holds.</summary>
    private static void OrbSite(Empire empire, Orb orb, BuildItem item)
    {
        item.Site = Safe(() => orb.Name);
        item.SiteOrbId = orb.OrbId;
        if ((item.Role ?? "").Contains("Mining"))
        {
            byte level = Safe(() => empire.ExplorationMap.CheckExplorationLevel(orb));
            item.Extracts = Copy(Safe(() => orb.Resources))
                .Where(r => !r.IsEmpty && r.DiscoveryLevel <= level)
                .OrderByDescending(r => r.Abundance)
                .Select(r => new SiteResource { Name = Safe(() => r.Name), Abundance = r.Abundance })
                .ToList();
        }
    }

    internal static string SystemName(Galaxy galaxy, StellarObject obj)
    {
        return Safe(() =>
        {
            int id = obj.GetSystemId(galaxy);
            return galaxy.Systems.TryGetValue(id, out var system) ? system.Name : null;
        });
    }

    private static void Dump(List<BuildItem> items, Dictionary<string, int> stats, double ms)
    {
        Log.Info($"First snapshot: {items.Count} items in {ms:0.0} ms; " + string.Join(", ", stats.Select(kv => $"{kv.Key}={kv.Value}")));
        foreach (var i in items.Take(200))
        {
            Log.Info($"  {i.State,-11} {i.Category,-8} {i.Work,-12} {i.Progress?.ToString("0.###") ?? "-",6}  {i.Name ?? "(no ship yet)"} [{i.Design}] {i.Role} @ {i.Where} ({i.WhereKind}) in {i.System} q={i.Queue} ship={i.ShipId} orb={i.OrbId}");
        }
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, json);

    private static double? Clean(float value) => float.IsFinite(value) ? Math.Round(value, 4) : null;

    private static void Count(Dictionary<string, int> stats, string key) => stats[key] = stats.GetValueOrDefault(key) + 1;

    /// <summary>Copies a game list; the game may change it while we read, so a failure just ends the copy early.</summary>
    internal static List<T> Copy<T>(IEnumerable<T> source)
    {
        var list = new List<T>();
        if (source == null)
        {
            return list;
        }
        try
        {
            foreach (var item in source)
            {
                if (item != null)
                {
                    list.Add(item);
                }
            }
        }
        catch (Exception e)
        {
            if (errors++ < 20)
            {
                Log.Warn($"Copy of {typeof(T).Name} list stopped early: {e.Message}");
            }
        }
        return list;
    }

    internal static T Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }
}
