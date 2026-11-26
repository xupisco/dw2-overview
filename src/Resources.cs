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
    /// <summary>The stock level to maintain (the game's "Maintain" column); below it the game shows the stock in red.</summary>
    public double? Maintain { get; set; }
    public double? RateRequired { get; set; }
    public double? RateCurrent { get; set; }
    public double? RateBuilding { get; set; }
    public double? Known { get; set; }
    public double? PriceSell { get; set; }
    public double? PriceBuy { get; set; }
    public double? BaseCost { get; set; }
    public bool? RateMet { get; set; }
    public bool Critical { get; set; }
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
    public List<DepositResource> Resources { get; set; }
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
            });
        }

        // Resources: the empire's numbers per resource.
        var stock = Amounts(Safe(() => empire.CalculateTotalCargoLevels(empire.EmpireId)));
        var maintain = Amounts(Safe(() => empire.CalculateTotalStockLevels(false)));
        var required = Amounts(Safe(() => empire.ResourceRatesRequired));
        var current = Amounts(Safe(() => empire.ResourceRatesCurrent));
        var building = Amounts(Safe(() => empire.ResourceRatesUnderConstruction));
        var known = Amounts(Safe(() => empire.ResourcesKnown));
        var sell = Amounts(Safe(() => empire.ResourcePricesSell));
        var buy = Amounts(Safe(() => empire.ResourcePricesBuy));
        var critical = Amounts(Safe(() => empire.CriticalMissingResources));
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
                Maintain = Get(maintain, id),
                RateRequired = Get(required, id),
                RateCurrent = Get(current, id),
                RateBuilding = Get(building, id),
                Known = Get(known, id),
                PriceSell = Get(sell, id),
                PriceBuy = Get(buy, id),
                BaseCost = Num(res.BaseCost),
                RateMet = met != null && id >= 0 && id < met.Length ? met[id] : null,
                Critical = critical.ContainsKey(id),
                Deposits = depositCount,
                Mined = deposits.Count(d => d.MinedBy != null && d.Resources.Any(r => r.Id == id)),
            });
        }
        return (rows, deposits);
    }

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
