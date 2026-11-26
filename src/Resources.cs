using DistantWorlds.Types;
using static DW2Overview.Snapshot;

namespace DW2Overview;

/// <summary>One resource the empire knows, with the game's own numbers (meaning and scale still to be checked
/// against the game's Resources screen: stock, rate per second, price, "0 of 2 sources").</summary>
internal sealed class ResourceRow
{
    public short Id { get; set; }
    public string Name { get; set; }
    /// <summary>fuel, construction, luxury or other.</summary>
    public string Category { get; set; }
    public double? Stock { get; set; }
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
        var minedByStation = new HashSet<int>();
        foreach (var ship in Copy(empire.Ships))
        {
            if (ship.IsBase && ship.Role.ToString().Contains("Mining") && ship.Parent.Type == ItemType.Orb)
            {
                minedByStation.Add(ship.Parent.Id);
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
                Resources = found,
            });
        }

        // Resources: the empire's numbers per resource.
        var stock = Amounts(Safe(() => empire.CalculateTotalCargoLevels(empire.EmpireId)));
        var required = Amounts(Safe(() => empire.ResourceRatesRequired));
        var current = Amounts(Safe(() => empire.ResourceRatesCurrent));
        var building = Amounts(Safe(() => empire.ResourceRatesUnderConstruction));
        var known = Amounts(Safe(() => empire.ResourcesKnown));
        var sell = Amounts(Safe(() => empire.ResourcePricesSell));
        var buy = Amounts(Safe(() => empire.ResourcePricesBuy));
        var critical = Amounts(Safe(() => empire.CriticalMissingResources));
        var met = Safe(() => empire.ResourceRateRequirementsMet);
        var rows = new List<ResourceRow>();
        foreach (var res in Copy(Safe(() => galaxy.Resources)))
        {
            short id = res.ResourceId;
            int depositCount = deposits.Count(d => d.Resources.Any(r => r.Id == id));
            bool relevant = known.ContainsKey(id) || stock.ContainsKey(id) || required.ContainsKey(id) || depositCount > 0;
            if (!relevant)
            {
                continue;
            }
            rows.Add(new ResourceRow
            {
                Id = id,
                Name = Safe(() => res.Name),
                Category = Safe(() => res.IsFuel) ? "fuel" : Safe(() => res.IsConstruction) ? "construction" : Safe(() => res.IsLuxury) ? "luxury" : "other",
                Stock = Get(stock, id),
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
