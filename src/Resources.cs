using DistantWorlds.Types;
using static DW2Overview.Snapshot;

namespace DW2Overview;

/// <summary>One resource the empire knows, with the game's own numbers (meaning and scale still to be checked
/// against the game's Resources screen: stock, rate per second, price, "0 of 2 sources").</summary>
internal sealed class ResourceRow
{
    public short Id { get; set; }
    public string Name { get; set; }
    /// <summary>fuel, construction, luxury or other: the first that applies, for display.</summary>
    public string Category { get; set; }
    /// <summary>Every category the resource belongs to: the game counts e.g. Caslon both as fuel and as construction.</summary>
    public List<string> Categories { get; set; }
    public double? Stock { get; set; }
    /// <summary>Held at colonies and bases (what the game seems to call On Hand), and still on ships.</summary>
    public double? OnHand { get; set; }
    public double? OnShips { get; set; }
    /// <summary>The stock level to maintain (the game's "Maintain" column); below it the game shows the stock in red.</summary>
    public double? Maintain { get; set; }
    public double? RateRequired { get; set; }
    /// <summary>Empire.ResourceRatesRequiredComplete: a fuller demand; a candidate for the game's supply table
    /// (Carbonite was critical there while the plain demand gave it years, 2026-11-26).</summary>
    public double? RateRequiredComplete { get; set; }
    /// <summary>Empire.CalculateResourceRateSurplus(id, false / true): the game's own production surplus.</summary>
    public double? Surplus { get; set; }
    public double? SurplusWithBuilding { get; set; }
    public double? RateCurrent { get; set; }
    public double? RateBuilding { get; set; }
    public double? Known { get; set; }
    public double? PriceSell { get; set; }
    public double? PriceBuy { get; set; }
    public double? BaseCost { get; set; }
    public bool? RateMet { get; set; }
    public bool Critical { get; set; }
    /// <summary>In the game's critical shortages (Empire.DetermineCriticalResourceShortages).</summary>
    public bool Shortage { get; set; }
    /// <summary>Known deposits of this resource, and how many of them the empire mines.</summary>
    public int Deposits { get; set; }
    public int Mined { get; set; }
}

/// <summary>A known place with resources: a planet, moon or asteroid, mined or not.</summary>
internal sealed class DepositRow
{
    public int OrbId { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
    public string System { get; set; }
    /// <summary>colony (own), station (own mining station there), or null when not mined by the empire.</summary>
    public string MinedBy { get; set; }
    /// <summary>An own mining station still being built there (not mining yet): its construction, 0..1.</summary>
    public double? StationBuilding { get; set; }
    /// <summary>The game's order for new mining locations (Empire.MiningTargets: 1 = first), when listed there.</summary>
    public int? Priority { get; set; }
    /// <summary>A mining station is ordered here and waits for a construction ship (Empire.QueuedMiningTargets).</summary>
    public bool Queued { get; set; }
    public List<DepositResource> Resources { get; set; }
    /// <summary>
    /// Mining ships mining here right now (mission Mine/MineLoad on this orb): ships mine without a station,
    /// so the game still lists the place under New Mining Locations.
    /// </summary>
    public List<MiningShipRow> Ships { get; set; }
}

internal sealed class MiningShipRow
{
    public int Id { get; set; }
    public string Name { get; set; }
    /// <summary>The resources of this place it carries now (empty on its first trip: then it is any of them).</summary>
    public List<short> Carrying { get; set; }
}

internal sealed class DepositResource
{
    public short Id { get; set; }
    public string Name { get; set; }
    public int Abundance { get; set; }
}

internal static class ResourcesView
{

    public static (List<ResourceRow> resources, List<DepositRow> deposits) Collect(Galaxy galaxy, Empire empire)
    {
        // Deposits: every orb the empire has explored, with the resources it has discovered there.
        var minedByColony = new HashSet<int>(Copy(empire.Colonies).Select(o => o.OrbId));
        // A mining station is a base from the moment its construction starts (seen 2026-11-26: Kintaris 10 counted
        // as mined while the game said "building Mining Station here"); it mines once ConstructionLevel reaches 1.
        var minedByStation = new HashSet<int>();
        var stationBuilding = new Dictionary<int, double>();
        foreach (var ship in Copy(empire.Ships))
        {
            if (ship.IsBase && ship.Role.ToString().Contains("Mining") && ship.Parent.Type == ItemType.Orb)
            {
                float level = Safe(() => ship.ConstructionLevel);
                if (float.IsFinite(level) && level < 1f)
                {
                    stationBuilding[ship.Parent.Id] = Math.Max(stationBuilding.GetValueOrDefault(ship.Parent.Id), Math.Round(Math.Max(level, 0f), 3));
                }
                else
                {
                    minedByStation.Add(ship.Parent.Id);
                }
            }
        }
        var priority = new Dictionary<int, int>();
        int rank = 0;
        foreach (var t in Copy(Safe(() => empire.MiningTargets)))
        {
            rank++;
            if (Safe(() => t.StellarObject) is Orb po && !priority.ContainsKey(po.OrbId))
            {
                priority[po.OrbId] = rank;
            }
        }
        var queued = new HashSet<int>(Copy(Safe(() => empire.QueuedMiningTargets)).Where(o => o != null).Select(o => o.OrbId));
        // Mining ships at work, by the orb they mine (the mission's primary target).
        var shipMining = new Dictionary<int, List<(Ship ship, HashSet<short> cargo)>>();
        foreach (var ship in Copy(empire.Ships))
        {
            var mission = Safe(() => ship.Mission);
            if (ship.IsBase || mission == null || (mission.Type != ShipMissionType.Mine && mission.Type != ShipMissionType.MineLoad))
            {
                continue;
            }
            if (Safe(() => mission.PrimaryTarget.Resolve(galaxy)) is not Orb mined)
            {
                continue;
            }
            var cargo = new HashSet<short>(Copy(Safe(() => ship.Cargo)).Where(c => c.EmpireId == empire.EmpireId && (float)c.Amount > 0).Select(c => c.ResourceId));
            if (!shipMining.TryGetValue(mined.OrbId, out var list))
            {
                shipMining[mined.OrbId] = list = new();
            }
            list.Add((ship, cargo));
        }
        string miningSig = string.Join(";", shipMining.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + string.Join(",", kv.Value.Select(v => v.ship.ShipId))));
        if (miningSig != lastShipMining)
        {
            lastShipMining = miningSig;
            Log.Info($"Mining ships at work: {(shipMining.Count == 0 ? "none" : string.Join("; ", shipMining.Select(kv => $"{Safe(() => galaxy.Orbs.GetById(kv.Key)?.Name)}: {string.Join(", ", kv.Value.Select(v => v.ship.Name))}")))}");
        }
        var deposits = new List<DepositRow>();
        var map = Safe(() => empire.ExplorationMap);
        foreach (var orb in Copy(Safe(() => map?.GetAllKnownOrbs(galaxy, empire, 1))))
        {
            byte level = Safe(() => map.CheckExplorationLevel(orb));
            var found = new List<DepositResource>();
            foreach (var r in Copy(Safe(() => orb.Resources)))
            {
                if (r.IsEmpty || r.DiscoveryLevel > level)
                {
                    continue;
                }
                found.Add(new DepositResource { Id = r.ResourceId, Name = Safe(() => r.Name), Abundance = r.Abundance });
            }
            if (found.Count == 0)
            {
                continue;
            }
            deposits.Add(new DepositRow
            {
                OrbId = orb.OrbId,
                Name = Safe(() => orb.Name),
                Kind = orb.Category.ToString(),
                System = SystemName(galaxy, orb),
                MinedBy = minedByColony.Contains(orb.OrbId) ? "colony" : minedByStation.Contains(orb.OrbId) ? "station" : null,
                StationBuilding = !minedByColony.Contains(orb.OrbId) && !minedByStation.Contains(orb.OrbId) && stationBuilding.TryGetValue(orb.OrbId, out double b) ? b : null,
                Resources = found,
                Priority = priority.TryGetValue(orb.OrbId, out int pr) ? pr : null,
                Queued = queued.Contains(orb.OrbId),
                Ships = shipMining.TryGetValue(orb.OrbId, out var miners)
                    ? miners.Select(m => new MiningShipRow
                    {
                        Id = m.ship.ShipId,
                        Name = Safe(() => m.ship.Name),
                        Carrying = found.Where(f => m.cargo.Contains(f.Id)).Select(f => f.Id).ToList(),
                    }).ToList()
                    : null,
            });
        }

        // Resources: the empire's numbers per resource.
        var stock = Amounts(Safe(() => empire.CalculateTotalCargoLevels(empire.EmpireId)));
        // The game's "On Hand" is less than CalculateTotalCargoLevels (Carbonite 488 against 1,014 on 2026-11-26):
        // the total must count cargo still on ships. Stock held at colonies and at bases, apart, to compare.
        var atColonies = new Dictionary<short, float>();
        var atBases = new Dictionary<short, float>();
        var onShips = new Dictionary<short, float>();
        void AddCargo(Dictionary<short, float> into, CargoList cargo)
        {
            foreach (var c in Copy(cargo))
            {
                if (c.EmpireId == empire.EmpireId)
                {
                    into[c.ResourceId] = into.GetValueOrDefault(c.ResourceId) + (float)c.Amount;
                }
            }
        }
        foreach (var orb in Copy(empire.Colonies))
        {
            AddCargo(atColonies, Safe(() => orb.Colony?.Cargo));
        }
        foreach (var ship in Copy(empire.Ships))
        {
            AddCargo(ship.IsBase ? atBases : onShips, Safe(() => ship.Cargo));
        }
        if (!stockLogged)
        {
            stockLogged = true;
            foreach (var id in stock.Keys.OrderBy(k => k))
            {
                Log.Info($"Stock {Safe(() => galaxy.Resources.GetById(id)?.Name) ?? id.ToString()}: total {stock[id]:0}, colonies {atColonies.GetValueOrDefault(id):0}, bases {atBases.GetValueOrDefault(id):0}, ships {onShips.GetValueOrDefault(id):0}.");
            }
        }
        var maintain = Amounts(Safe(() => empire.CalculateTotalStockLevels(false)));
        var required = Amounts(Safe(() => empire.ResourceRatesRequired));
        var requiredComplete = Amounts(Safe(() => empire.ResourceRatesRequiredComplete));
        var current = Amounts(Safe(() => empire.ResourceRatesCurrent));
        var building = Amounts(Safe(() => empire.ResourceRatesUnderConstruction));
        var known = Amounts(Safe(() => empire.ResourcesKnown));
        var sell = Amounts(Safe(() => empire.ResourcePricesSell));
        var buy = Amounts(Safe(() => empire.ResourcePricesBuy));
        var critical = Amounts(Safe(() => empire.CriticalMissingResources));
        // The game's own list of critical shortages; a candidate for the one critical construction resource the
        // supply table showed (15/0/1) while every stock lasted years (2026-11-26). Logged when it changes.
        var shortages = Amounts(Safe(() => empire.DetermineCriticalResourceShortages()));
        string sig = string.Join(",", shortages.Keys.OrderBy(k => k));
        if (sig != lastShortages)
        {
            lastShortages = sig;
            Log.Info($"Critical resource shortages (DetermineCriticalResourceShortages): {(shortages.Count == 0 ? "none" : string.Join(", ", shortages.Select(kv => $"{Safe(() => galaxy.Resources.GetById(kv.Key)?.Name) ?? kv.Key.ToString()} {kv.Value:0.#}")))}; CriticalMissingResources: {critical.Count}.");
        }
        var met = Safe(() => empire.ResourceRateRequirementsMet);
        // Categories are the resource's own flags (checked 2026-11-26 against the game's supply table: its
        // Construction row is every IsConstruction resource, 16 in the game, even those the empire has none of;
        // Luxury only the ones in demand or in stock; Fuel the fuel resources).
        var rows = new List<ResourceRow>();
        foreach (var res in Copy(Safe(() => galaxy.Resources)))
        {
            short id = res.ResourceId;
            int depositCount = deposits.Count(d => d.Resources.Any(r => r.Id == id));
            // The game keeps a zero entry for every resource in the rate lists, so only real amounts count.
            bool relevant = known.GetValueOrDefault(id) > 0 || stock.GetValueOrDefault(id) > 0 || required.GetValueOrDefault(id) > 0
                || current.GetValueOrDefault(id) > 0 || maintain.GetValueOrDefault(id) > 0 || depositCount > 0
                || Safe(() => res.IsConstruction) || Safe(() => res.IsFuel);   // listed by the game even with none in stock
            if (!relevant)
            {
                continue;
            }
            rows.Add(new ResourceRow
            {
                Id = id,
                Name = Safe(() => res.Name),
                Category = Safe(() => res.IsFuel) ? "fuel" : Safe(() => res.IsConstruction) ? "construction" : Safe(() => res.IsLuxury) ? "luxury" : "other",
                Categories = new[] { ("fuel", Safe(() => res.IsFuel)), ("construction", Safe(() => res.IsConstruction)), ("luxury", Safe(() => res.IsLuxury)) }
                    .Where(c => c.Item2).Select(c => c.Item1).ToList(),
                Stock = Get(stock, id),
                OnHand = Num(atColonies.GetValueOrDefault(id) + atBases.GetValueOrDefault(id)),
                OnShips = Num(onShips.GetValueOrDefault(id)),
                Maintain = Get(maintain, id),
                RateRequired = Get(required, id),
                RateRequiredComplete = Get(requiredComplete, id),
                Surplus = Num(Safe(() => empire.CalculateResourceRateSurplus(id, false))),
                SurplusWithBuilding = Num(Safe(() => empire.CalculateResourceRateSurplus(id, true))),
                RateCurrent = Get(current, id),
                RateBuilding = Get(building, id),
                Known = Get(known, id),
                PriceSell = Get(sell, id),
                PriceBuy = Get(buy, id),
                BaseCost = Num(res.BaseCost),
                RateMet = met != null && id >= 0 && id < met.Length ? met[id] : null,
                Critical = critical.ContainsKey(id),
                Shortage = shortages.ContainsKey(id),
                Deposits = depositCount,
                Mined = deposits.Count(d => d.MinedBy != null && d.Resources.Any(r => r.Id == id)),
            });
        }
        return (rows, deposits);
    }

    private static string lastShortages;
    private static string lastShipMining;
    private static bool stockLogged;

    private static Dictionary<short, float> Amounts(ResourceQuantityList list)
    {
        var amounts = new Dictionary<short, float>();
        foreach (var q in Copy(list))
        {
            amounts[q.ResourceId] = amounts.GetValueOrDefault(q.ResourceId) + q.Amount;
        }
        return amounts;
    }

    private static double? Get(Dictionary<short, float> amounts, short id) => amounts.TryGetValue(id, out float v) ? Num(v) : null;

    private static double? Num(float value) => float.IsFinite(value) ? Math.Round(value, 4) : null;
}
