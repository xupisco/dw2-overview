using DistantWorlds.Types;
using static DW2Overview.Snapshot;

namespace DW2Overview;

/// <summary>One colony. Values are the game's own; scales are still to be checked against the game's screens.</summary>
internal sealed class ColonyRow
{
    public string Name { get; set; }
    public string System { get; set; }
    public int OrbId { get; set; }
    public string Race { get; set; }
    public long? Population { get; set; }
    public long? MaxPopulation { get; set; }
    public double? Growth { get; set; }
    public double? Development { get; set; }
    public double? Approval { get; set; }
    public double? Happiness { get; set; }
    public double? Unrest { get; set; }
    public double? Corruption { get; set; }
    public double? TaxRate { get; set; }
    public double? Revenue { get; set; }
    public double? Quality { get; set; }
    public bool Rebelling { get; set; }
    public int Facilities { get; set; }
    public bool AutoTax { get; set; }
    public bool AutoPopulation { get; set; }
}

/// <summary>One research project: queued, recently finished, or available next.</summary>
internal sealed class ResearchRow
{
    /// <summary>active (within the first ConcurrentResearchProjects of the queue), queued, latest (just researched), next (available, not queued).</summary>
    public string State { get; set; }
    public int? Order { get; set; }
    public string Name { get; set; }
    public int Id { get; set; }
    public double? Progress { get; set; }
    public double? Size { get; set; }
    public double? Speed { get; set; }
    public bool Rushing { get; set; }
    public bool AtStation { get; set; }
}

/// <summary>One empire message (the game's message log).</summary>
internal sealed class MessageRow
{
    /// <summary>Stable enough to tell messages apart between snapshots (the game gives them no id).</summary>
    public string Key { get; set; }
    public long StarDate { get; set; }
    public string Date { get; set; }
    public string Type { get; set; }
    public string SubType { get; set; }
    public string Priority { get; set; }
    public bool Advisor { get; set; }
    public string Title { get; set; }
    public string Text { get; set; }
    public string RelatedName { get; set; }
    public string RelatedType { get; set; }
    public int? RelatedId { get; set; }
    public string Task { get; set; }
}

/// <summary>Something the command palette can jump to.</summary>
internal sealed class IndexRow
{
    /// <summary>ship, orb or fleet: what /api/goto takes.</summary>
    public string Type { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
    /// <summary>Planet, Moon, Star, Asteroid, a ship role, or a fleet role.</summary>
    public string Kind { get; set; }
    public string System { get; set; }
    public bool Own { get; set; }
}

internal static class Views
{
    public static List<MessageRow> Messages(Empire empire, int limit)
    {
        var all = Copy(Safe(() => empire.Messages));
        var rows = new List<MessageRow>();
        foreach (var m in all.OrderByDescending(m => m.StarDate).Take(limit))
        {
            var (relatedType, relatedId) = Target(Safe(() => m.RelatedObject));
            rows.Add(new MessageRow
            {
                Key = $"{m.StarDate}|{m.Type}|{m.SubType}|{m.Title}",
                StarDate = m.StarDate,
                Date = Safe(() => m.StarDateDescription),
                Type = m.Type.ToString(),
                SubType = m.SubType.ToString(),
                Priority = m.Priority.ToString(),
                Advisor = m.IsAdvisorSuggestion,
                Title = m.Title,
                Text = m.Text,
                RelatedName = Safe(() => (m.RelatedObject as INamed)?.Name),
                RelatedType = relatedType,
                RelatedId = relatedId,
                Task = Safe(() => m.GameTask == null || m.GameTask.IsEmpty ? null : m.GameTask.Text ?? m.GameTask.Type.ToString()),
            });
        }
        return rows;
    }

    /// <summary>
    /// What the empire knows, through the game's own "Search Known Items" (so nothing unexplored leaks out),
    /// plus the empire's own ships, colonies and fleets.
    /// </summary>
    public static List<IndexRow> Index(Galaxy galaxy, Empire empire, out int knownCount)
    {
        var rows = new List<IndexRow>();
        var seen = new HashSet<string>();
        knownCount = 0;
        StellarObjectList known = null;
        try
        {
            empire.ResolveKnownItems("", true, true, true, out known);
        }
        catch (Exception e)
        {
            Log.Warn("ResolveKnownItems: " + e.Message);
        }
        foreach (var obj in Copy(known))
        {
            knownCount++;
            Add(obj);
        }
        foreach (var ship in Copy(empire.Ships))
        {
            Add(ship);
        }
        foreach (var orb in Copy(empire.Colonies))
        {
            Add(orb);
        }
        foreach (var fleet in Copy(empire.Fleets))
        {
            var lead = Safe(() => fleet.LeadShip);
            rows.Add(new IndexRow
            {
                Type = "fleet",
                Id = fleet.FleetId,
                Name = fleet.Name,
                Kind = "Fleet " + fleet.Role,
                System = lead != null ? SystemName(galaxy, lead) : null,
                Own = true,
            });
        }
        return rows;

        void Add(StellarObject obj)
        {
            var (type, id) = Target(obj);
            if (type == null || !seen.Add(type + id))
            {
                return;
            }
            string kind = obj switch
            {
                Ship s => s.IsBase ? "Base " + s.Role : s.Role.ToString(),
                Orb o => o.Colony != null && o.Colony.Empire.Id == empire.EmpireId ? "Colony" : o.Category.ToString(),
                _ => null,
            };
            rows.Add(new IndexRow
            {
                Type = type,
                Id = id.Value,
                Name = Safe(() => (obj as INamed)?.Name),
                Kind = Safe(() => kind),
                System = SystemName(galaxy, obj),
                Own = obj is Ship ship ? ship.EmpireId == empire.EmpireId : kind == "Colony",
            });
        }
    }

    /// <summary>How /api/goto finds an object again.</summary>
    private static (string type, int? id) Target(StellarObject obj) => obj switch
    {
        Ship s => ("ship", s.ShipId),
        Orb o => ("orb", o.OrbId),
        _ => (null, null),
    };

    public static List<ColonyRow> Colonies(Galaxy galaxy, Empire empire)
    {
        var rows = new List<ColonyRow>();
        foreach (var orb in Copy(empire.Colonies))
        {
            var colony = Safe(() => orb.Colony);
            if (colony == null)
            {
                continue;
            }
            rows.Add(new ColonyRow
            {
                Name = Safe(() => orb.Name),
                System = SystemName(galaxy, orb),
                OrbId = orb.OrbId,
                Race = Safe(() => colony.Population?.DominantRace?.Name),
                Population = Safe(() => (long?)colony.Population?.TotalAmount),
                MaxPopulation = colony.MaximumPopulation,
                Growth = Num(Safe(() => colony.CalculateGrowthRate(orb, empire))),
                Development = Num(Safe(() => colony.DevelopmentLevel)),
                Approval = Num(colony.EmpireApprovalRating),
                Happiness = Num(colony.RacialHappiness),
                Unrest = Num(colony.Unrest),
                Corruption = Num(colony.Corruption),
                TaxRate = Num(colony.TaxRate),
                // Without support costs this should match the empire panel's "Private Colony Revenue, Projected"
                // (with them it came out exactly 1,000 lower in the first test).
                Revenue = Num(Safe(() => colony.CalculateAnnualRevenue(orb, empire, colony.Corruption, false))),
                Quality = Num(Safe(() => orb.Quality)),
                Rebelling = colony.IsRebelling,
                Facilities = Copy(Safe(() => orb.Facilities)).Count,
                AutoTax = colony.AutomateTaxRates,
                AutoPopulation = colony.AutomatePopulationPolicy,
            });
        }
        return rows;
    }

    public static (List<ResearchRow> rows, int concurrent) Research(Empire empire)
    {
        var rows = new List<ResearchRow>();
        var research = Safe(() => empire.Research);
        int concurrent = Safe(() => empire.ConcurrentResearchProjects);
        if (research == null)
        {
            return (rows, concurrent);
        }
        var listed = new HashSet<int>();
        int order = 0;
        foreach (var p in Copy(Safe(() => research.ResearchQueue)))
        {
            order++;
            rows.Add(Row(p, order <= Math.Max(1, concurrent) ? "active" : "queued", order));
            listed.Add(p.ResearchProjectId);
        }
        foreach (var p in Copy(Safe(() => research.LatestProjects)))
        {
            if (listed.Add(p.ResearchProjectId))
            {
                rows.Add(Row(p, "latest", null));
            }
        }
        foreach (var p in Copy(Safe(() => research.NextProjects)))
        {
            if (listed.Add(p.ResearchProjectId))
            {
                rows.Add(Row(p, "next", null));
            }
        }
        return (rows, concurrent);
    }

    private static ResearchRow Row(ResearchProject p, string state, int? order) => new()
    {
        State = state,
        Order = order,
        Name = Safe(() => p.Definition?.Name),
        Id = p.ResearchProjectId,
        Progress = Num(Safe(() => p.ProgressRatio)),
        Size = Num(Safe(() => p.Definition?.Size ?? float.NaN)),
        Speed = Num(p.Speed),
        Rushing = p.Rushing,
        AtStation = p.AssignedResearchStationId > 0,
    };

    /// <summary>
    /// The empire panel's top numbers. Which cashflow method matches the panel's "Cashflow" is not known yet,
    /// so both candidates go out and get compared with the game.
    /// </summary>
    public static object Economy(Empire empire) => new
    {
        cash = Num(Safe(() => empire.Economy.Money)),
        privateCash = Num(Safe(() => empire.Economy.PrivateMoney)),
        moneyAvailable = Num(Safe(() => empire.MoneyAvailable)),
        cashflowState = Num(Safe(() => empire.CalculateCashflowState(1f, true))),
        cashflowActual = Num(Safe(() => empire.CalculateCashflowActual(1f, true))),
        warWeariness = Num(Safe(() => empire.WarWeariness)),
    };

    private static double? Num(float value) => float.IsFinite(value) ? Math.Round(value, 4) : null;
}
