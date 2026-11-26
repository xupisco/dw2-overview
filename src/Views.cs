using DistantWorlds.Types;
using static DW2Overview.Snapshot;

namespace DW2Overview;

internal sealed class CargoRow
{
    public string Name { get; set; }
    /// <summary>fuel, construction, luxury or other (the resource's own flags, as in /api/resources).</summary>
    public string Category { get; set; }
    public double Amount { get; set; }
}

/// <summary>A place a mission names (its primary and secondary targets), for a link in the mission's text.</summary>
internal sealed class PlaceRow
{
    public string Name { get; set; }
    public string Type { get; set; }
    public int Id { get; set; }
}

internal sealed class RaceRow
{
    public string Name { get; set; }
    public long Amount { get; set; }
    public double? Growth { get; set; }
}

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
    /// <summary>The planet, for a drawn thumbnail: its type (Continental, Ocean...) and the colours the game
    /// draws it with (OrbTypes.xml: atmosphere, clouds, gas), whether it has rings, its diameter.</summary>
    public string PlanetType { get; set; }
    public string Surface { get; set; }
    public string Atmosphere { get; set; }
    public string Clouds { get; set; }
    public string Gas { get; set; }
    public bool Rings { get; set; }
    public int Diameter { get; set; }
    /// <summary>Resources known at the planet, most abundant first.</summary>
    public List<SiteResource> Resources { get; set; }
    /// <summary>Each race living there: amount and its yearly growth (the game's Population at screen).</summary>
    public List<RaceRow> Races { get; set; }
    /// <summary>The whole population's yearly growth (Population.ReproductionRate of the list).</summary>
    public double? PopulationGrowth { get; set; }
    public int Troops { get; set; }
    /// <summary>Under attack: ships attacking it, or troops invading it.</summary>
    public int Attackers { get; set; }
    public int Invaders { get; set; }
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
    /// <summary>An advisor suggestion still waiting for a decision (from Empire.EvaluationQueue).</summary>
    public bool Pending { get; set; }
    /// <summary>The message the game shows in its dialog right now, with that dialog's buttons (game texts).</summary>
    public bool OnScreen { get; set; }
    public List<DialogButton> Buttons { get; set; }
}

/// <summary>A button of the game's message dialog: which one (yes, no, other, show) and its text.</summary>
internal sealed class DialogButton
{
    public string Id { get; set; }
    public string Text { get; set; }
}

/// <summary>One of the empire's own ships or bases, with what it is doing.</summary>
internal sealed class ShipRow
{
    /// <summary>The mission in the game's own words (TextHelper.ResolveMissionDescription), e.g. "Build MS-3
    /// (Large Mining Station) at Kintaris 2".</summary>
    public string MissionText { get; set; }
    /// <summary>Where the ship is heading right now (ShipMission.GetCurrentTarget), when it is not the mission's
    /// target: a construction ship first fetches its materials (Goma), then builds (Kintaris 2).</summary>
    public string Heading { get; set; }
    public string HeadingType { get; set; }
    public int? HeadingId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
    public string Kind { get; set; }
    /// <summary>The role as the game writes it ("Small Freighter"); Kind keeps the internal name for grouping.</summary>
    public string RoleName { get; set; }
    /// <summary>What a ship (not a base) carries, by resource, largest first.</summary>
    public List<CargoRow> Cargo { get; set; }
    /// <summary>The mission's places ("from Kintaris 10 Mining Station to Goma Spaceport"), to make them links.</summary>
    public List<PlaceRow> Places { get; set; }
    public bool IsBase { get; set; }
    public string Design { get; set; }
    public string System { get; set; }
    public string Fleet { get; set; }
    /// <summary>The current mission's type, or "Idle" when there is none.</summary>
    public string Mission { get; set; }
    public string Target { get; set; }
    public string TargetType { get; set; }
    public int? TargetId { get; set; }
    public int Queued { get; set; }
    public bool Manual { get; set; }
    /// <summary>How the ship gets its missions: FullyAutomate, Manual, AutoExplore, AutoPatrol...</summary>
    public string Automation { get; set; }
    public bool Docked { get; set; }
    public bool Building { get; set; }
    /// <summary>How much of the ship is built, 0..1, while it is under construction (the Construction tab's progress).</summary>
    public double? Progress { get; set; }
    public double? Damage { get; set; }
    public double? Fuel { get; set; }
    /// <summary>Reactor energy left, 0..1 of the storage capacity.</summary>
    public double? Energy { get; set; }
    /// <summary>Shield strength, 0..1 of the capacity (null for ships without shields).</summary>
    public double? Shields { get; set; }
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
    /// <summary>The role as the game writes it ("Small Freighter"); Kind keeps the internal name for grouping.</summary>
    public string RoleName { get; set; }
    public string System { get; set; }
    public bool Own { get; set; }
    /// <summary>Own ships only: the current mission, or "Idle".</summary>
    public string Mission { get; set; }
}

internal static class Views
{
    /// <summary>
    /// The message log plus the advisor's pending suggestions. Suggestions ("Assign Mission: Build MS-1 at
    /// Kintaris 4", with Assign Mission / Decline) are not in Empire.Messages but in Empire.EvaluationQueue
    /// (IsAdvisorSuggestion, the action in GameTask, no RelatedObject) and leave it once accepted, declined
    /// or expired; they come first, flagged Pending.
    /// </summary>
    public static List<MessageRow> Messages(Empire empire, int limit)
    {
        var log = Copy(Safe(() => empire.Messages));
        // Every message passes through EvaluationQueue for a few seconds on its way to the log (seen in game,
        // 2026-11-26), so only the advisor's suggestions count as pending; the rest would show up twice.
        var pending = Copy(Safe(() => empire.EvaluationQueue)).Where(m => m.IsAdvisorSuggestion).ToList();
        // When the game shows a suggestion in its dialog it leaves the queue (seen 2026-11-26 with a contract
        // offer), yet it still waits for an answer: the one on screen counts as pending too.
        var shown = Safe(() => DistantWorlds.UI.UserInterfaceController.EmpireMessageDialogGetActiveMessage());
        if (shown != null && shown.IsAdvisorSuggestion && !pending.Contains(shown))
        {
            pending.Insert(0, shown);
        }
        var rows = new List<MessageRow>();
        foreach (var (m, isPending) in pending.Select(m => (m, true)).Concat(log.OrderByDescending(m => m.StarDate).Take(limit).Select(m => (m, false))))
        {
            var (relatedType, relatedId) = Target(Safe(() => m.RelatedObject));
            rows.Add(new MessageRow
            {
                Pending = isPending,
                OnScreen = shown != null && ReferenceEquals(m, shown),
                Buttons = shown != null && ReferenceEquals(m, shown) ? DialogButtons() : null,
                Key = MessageKey(m, isPending),
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
                RoleName = obj is Ship rs ? Safe(() => TextHelper.ResolveDescription(rs.Role)) : null,
                System = SystemName(galaxy, obj),
                Own = obj is Ship ship ? ship.EmpireId == empire.EmpireId : kind == "Colony",
                Mission = obj is Ship own && own.EmpireId == empire.EmpireId ? MissionName(own) : null,
            });
        }
    }

    /// <summary>
    /// The current mission's type; without one: "UnderConstruction" or "InRepair" while in a yard (a queued
    /// ship has no mission yet but is not idle), null for a base (bases get no missions), "Idle" otherwise.
    /// </summary>
    public static string MissionName(Ship ship)
    {
        var mission = Safe(() => ship.Mission);
        if (mission != null && mission.Type != ShipMissionType.Undefined)
        {
            return mission.Type.ToString();
        }
        if (Safe(() => ship.IsUnderConstructionOrRepair))
        {
            return Safe(() => ship.ConstructionLevel) < 1f ? "UnderConstruction" : "InRepair";
        }
        return ship.IsBase ? null : "Idle";
    }

    public static List<ShipRow> Ships(Galaxy galaxy, Empire empire)
    {
        var rows = new List<ShipRow>();
        foreach (var ship in Copy(empire.Ships))
        {
            var mission = Safe(() => ship.Mission);
            bool idle = mission == null || mission.Type == ShipMissionType.Undefined;
            var target = idle ? null : Safe(() => mission.PrimaryTarget.Resolve(galaxy)) ?? Safe(() => (IDw2Item)ship.CurrentTarget);
            var (targetType, targetId) = Target(target as StellarObject);
            float range = Safe(() => ship.CurrentFuelRange()), maxRange = Safe(() => ship.MaximumFuelRange());
            rows.Add(new ShipRow
            {
                Id = ship.ShipId,
                Name = Safe(() => ship.Name),
                Kind = ship.IsBase ? "Base " + ship.Role : ship.Role.ToString(),
                RoleName = Safe(() => TextHelper.ResolveDescription(ship.Role)),
                IsBase = ship.IsBase,
                Design = Safe(() => ship.Design?.Name),
                System = SystemName(galaxy, ship),
                Fleet = Safe(() => ship.Fleet?.Name),
                Mission = MissionName(ship),
                MissionText = idle ? null : Safe(() => TextHelper.ResolveMissionDescription(galaxy, empire, ship, mission)),
                Target = Safe(() => (target as INamed)?.Name),
                TargetType = targetType,
                TargetId = targetId,
                Queued = Safe(() => ship.NextMissions?.Count ?? 0),
                Manual = !idle && mission.IsManuallyAssigned,
                Automation = ship.MissionAssignmentType.ToString(),
                Docked = Safe(() => ship.IsDocked),
                Building = Safe(() => ship.IsUnderConstructionOrRepair),
                Progress = Safe(() => ship.IsUnderConstructionOrRepair) && Safe(() => ship.ConstructionLevel) < 1f ? Num(Safe(() => ship.ConstructionLevel)) : null,
                Damage = Num(ship.HullDamageLevel),
                Fuel = maxRange > 0 ? Num(range / maxRange) : null,
                Energy = Ratio(Safe(() => (float)ship.Energy), Safe(() => ship.Summary.ReactorEnergyStorageCapacity)),
                Shields = Ratio(Safe(() => (float)ship.Shields), Safe(() => ship.Summary.ShieldCapacity)),
            });
            var row = rows[^1];
            if (!ship.IsBase)
            {
                var cargo = Copy(Safe(() => ship.Cargo)).Where(c => (float)c.Amount >= 1f)
                    .GroupBy(c => c.ResourceId)
                    .Select(g =>
                    {
                        var res = Safe(() => galaxy.Resources.GetById(g.Key));
                        return new CargoRow
                        {
                            Name = Safe(() => res?.Name),
                            Category = res == null ? null : Safe(() => res.IsFuel) ? "fuel" : Safe(() => res.IsConstruction) ? "construction" : Safe(() => res.IsLuxury) ? "luxury" : "other",
                            Amount = Math.Round(g.Sum(c => (double)(float)c.Amount)),
                        };
                    })
                    .OrderByDescending(c => c.Amount)
                    .ToList();
                row.Cargo = cargo.Count > 0 ? cargo : null;
            }
            if (!idle)
            {
                var places = new List<PlaceRow>();
                foreach (var item in new[] { mission.PrimaryTarget, mission.SecondaryTarget })
                {
                    var place = Safe(() => item.Resolve(galaxy)) as StellarObject;
                    var (placeType, placeId) = Target(place);
                    string placeName = Safe(() => (place as INamed)?.Name);
                    if (placeId != null && !string.IsNullOrEmpty(placeName) && places.All(x => x.Name != placeName))
                    {
                        places.Add(new PlaceRow { Name = placeName, Type = placeType, Id = placeId.Value });
                    }
                }
                row.Places = places.Count > 0 ? places : null;
            }
            if (!idle)
            {
                var now = Safe(() => mission.GetCurrentTarget(galaxy));
                if (now != null)
                {
                    var (nowType, nowId) = Target(now);
                    row.Heading = Safe(() => (now as INamed)?.Name);
                    row.HeadingType = nowType;
                    row.HeadingId = nowId;
                }
                if (mission.Type == ShipMissionType.Build && buildLogged.Add(ship.ShipId))
                {
                    Log.Info($"Build mission of {row.Name}: '{row.MissionText}'; primary {Safe(() => (mission.PrimaryTarget.Resolve(galaxy) as INamed)?.Name)}, " +
                        $"secondary {Safe(() => (mission.SecondaryTarget.Resolve(galaxy) as INamed)?.Name)}, now {row.Heading ?? row.Target}, design {mission.BuildDesignId}.");
                }
            }
        }
        return rows;
    }

    /// <summary>
    /// The game's own "Search Known Items" for one query: it returns nothing for an empty query, so the
    /// palette asks per search. Runs on the game thread (through Commands.OnGameThread).
    /// </summary>
    public static List<IndexRow> Search(Galaxy galaxy, Empire empire, string query, int limit)
    {
        var rows = new List<IndexRow>();
        empire.ResolveKnownItems(query, true, true, true, out var found);
        foreach (var obj in Copy(found))
        {
            var (type, id) = Target(obj);
            if (type == null)
            {
                continue;
            }
            rows.Add(new IndexRow
            {
                Type = type,
                Id = id.Value,
                Name = Safe(() => (obj as INamed)?.Name),
                Kind = obj switch
                {
                    Ship s => s.IsBase ? "Base " + s.Role : s.Role.ToString(),
                    Orb o => Safe(() => o.Colony != null && o.Colony.Empire.Id == empire.EmpireId) ? "Colony" : o.Category.ToString(),
                    _ => null,
                },
                System = SystemName(galaxy, obj),
                Own = obj is Ship ship ? ship.EmpireId == empire.EmpireId : Safe(() => (obj as Orb)?.Colony?.Empire.Id == empire.EmpireId),
            });
            if (rows.Count >= limit)
            {
                break;
            }
        }
        return rows;
    }

    /// <summary>The visible, enabled buttons of the game's message dialog, with the game's own texts.</summary>
    public static List<DialogButton> DialogButtons()
    {
        var dialog = DistantWorlds.UI.UserInterfaceController.EmpireMessageDialog;
        var list = new List<DialogButton>();
        if (dialog == null)
        {
            return list;
        }
        foreach (var (id, button) in DialogButtonsById(dialog))
        {
            if (button != null && button.Visible && button.Enabled && !string.IsNullOrWhiteSpace(Safe(() => button.Text)))
            {
                list.Add(new DialogButton { Id = id, Text = button.Text });
            }
        }
        return list;
    }

    public static IEnumerable<(string id, DistantWorlds.UI.DWButton button)> DialogButtonsById(DistantWorlds.UI.EmpireMessageDialog dialog)
    {
        yield return ("yes", dialog.YesButton);
        yield return ("no", dialog.NoButton);
        yield return ("other", dialog.OtherButton);
        yield return ("show", dialog.ShowButton);
    }

    /// <summary>Tells messages apart between snapshots (the game gives them no id); also how the page names a
    /// suggestion it wants answered.</summary>
    public static string MessageKey(EmpireMessage m, bool pending)
    {
        var (_, relatedId) = Target(Safe(() => m.RelatedObject));
        return $"{(pending ? "pending|" : "")}{m.StarDate}|{m.Type}|{m.SubType}|{m.Title}|{relatedId}";
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
            }.WithPlanet(galaxy, empire, orb, colony));
        }
        return rows;
    }

    /// <summary>The planet and its people, after the game's colony panel (type, colours, resources, races, threats).</summary>
    private static ColonyRow WithPlanet(this ColonyRow row, Galaxy galaxy, Empire empire, Orb orb, Colony colony)
    {
        var type = Safe(() => galaxy.OrbTypes.GetById(orb.TypeId));
        row.PlanetType = Safe(() => type?.Name);
        row.Surface = Safe(() => type?.SurfaceDrawType.ToString());
        row.Atmosphere = Safe(() => type == null ? null : Hex(type.AtmosphereColor));
        row.Clouds = Safe(() => type == null ? null : Hex(type.CloudColor));
        row.Gas = Safe(() => type == null ? null : Hex(type.GasGradientPrimaryColor));
        row.Rings = orb.HasRings;
        row.Diameter = orb.Diameter;
        byte level = Safe(() => empire.ExplorationMap.CheckExplorationLevel(orb));
        row.Resources = Copy(Safe(() => orb.Resources))
            .Where(r => !r.IsEmpty && r.DiscoveryLevel <= level)
            .OrderByDescending(r => r.Abundance)
            .Select(r => new SiteResource { Name = Safe(() => r.Name), Abundance = r.Abundance })
            .ToList();
        var people = Safe(() => colony.Population);
        row.Races = Copy(people)
            .OrderByDescending(x => x.Amount)
            .Select(x => new RaceRow { Name = Safe(() => x.Race?.Name), Amount = x.Amount, Growth = Num(x.ReproductionRate) })
            .ToList();
        row.PopulationGrowth = Num(Safe(() => people?.ReproductionRate ?? float.NaN));
        row.Troops = Copy(Safe(() => colony.Troops)).Count;
        row.Attackers = Copy(Safe(() => colony.Attackers)).Count;
        row.Invaders = Copy(Safe(() => colony.TroopsInvading)).Count;
        if (colonyLogged.Add(orb.OrbId))
        {
            // To see which growth matches the game's Population screen (+4.4% at Goma on 2026-11-26).
            Log.Info($"Colony {row.Name}: {row.PlanetType} ({row.Surface}, atmosphere {row.Atmosphere}, gas {row.Gas}, rings {row.Rings}); growth CalculateGrowthRate={row.Growth} ReproductionRate={row.PopulationGrowth}; races {string.Join(", ", row.Races.Select(x => $"{x.Name} {x.Amount} {x.Growth}"))}; troops {row.Troops}");
        }
        return row;
    }

    private static readonly HashSet<int> colonyLogged = new();
    private static readonly HashSet<int> buildLogged = new();

    private static string Hex(Stride.Core.Mathematics.Color c) => c.A == 0 ? null : $"#{c.R:x2}{c.G:x2}{c.B:x2}";

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
        cashflowState = Sane(Safe(() => empire.CalculateCashflowState(1f, true))),
        cashflowActual = Sane(Safe(() => empire.CalculateCashflowActual(1f, true)) is var cached && Math.Abs(cached) < 1e12f
            ? cached : Safe(() => empire.CalculateCashflowActual(1f, false))),
        warWeariness = Num(Safe(() => empire.WarWeariness)),
    };

    /// <summary>
    /// Right after a save loads, the cached cashflow comes back as float.MaxValue (3.4e38 a year); a value that
    /// large means "not computed yet", so the uncached one is used, and null if that is absurd too.
    /// </summary>
    private static double? Sane(float value) => Math.Abs(value) < 1e12f ? Num(value) : null;

    /// <summary>value / capacity, clamped to 0..1; null when there is no capacity.</summary>
    private static double? Ratio(float value, float capacity) => capacity > 0 && float.IsFinite(value) ? Math.Round(Math.Clamp(value / capacity, 0f, 1f), 4) : null;

    private static double? Num(float value) => float.IsFinite(value) ? Math.Round(value, 4) : null;
}
