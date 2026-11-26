using System.Collections.Concurrent;
using System.Security.Cryptography;
using DistantWorlds.Types;
using DistantWorlds2;

namespace DW2Overview;

/// <summary>
/// Commands from the page. The server thread only queues them; they run on the game thread, in the
/// DWGame.Update postfix, so game objects are never touched from another thread.
/// </summary>
internal static class Commands
{
    /// <summary>New on every game start; the page reads it from /api/info and sends it back with each command.</summary>
    public static readonly string Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static readonly ConcurrentQueue<(string type, int id)> pending = new();
    private static int logged;

    public static void GoTo(string type, int id) => pending.Enqueue((type, id));

    private static readonly ConcurrentQueue<Action<DWGame>> jobs = new();

    /// <summary>
    /// Runs a read on the game thread and waits for its result (from a server thread). Returns default on
    /// timeout, e.g. while the game is loading or minimized long enough that Update does not run.
    /// </summary>
    public static T OnGameThread<T>(Func<DWGame, T> read, int timeoutMs = 1000)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        jobs.Enqueue(game =>
        {
            try
            {
                done.TrySetResult(read(game));
            }
            catch (Exception e)
            {
                done.TrySetException(e);
            }
        });
        return done.Task.Wait(timeoutMs) ? done.Task.Result : default;
    }

    // ---- The game's own hotkeys as commands ----
    // The palette lists the player's key mappings (DWSettings.KeyMappings, as remapped in the options) and
    // runs one the same way a key press does: InputSystem.HandleKeyPress, on the game thread.

    private static readonly ConcurrentQueue<int> keyPresses = new();

    public static void PressKey(int index) => keyPresses.Enqueue(index);

    /// <summary>One key mapping, for the palette. Runs on the game thread (through OnGameThread).</summary>
    public static List<object> ListKeyMappings(DWGame game)
    {
        var rows = new List<object>();
        var mappings = game.DWSettings?.KeyMappings;
        if (mappings == null)
        {
            return rows;
        }
        for (int i = 0; i < mappings.Count; i++)
        {
            var m = mappings[i];
            if (m == null || m.CockpitTaskType != CockpitTaskType.Undefined)
            {
                continue;   // cockpit-mode controls only make sense in that mode
            }
            string action = m.TaskType != GameTaskType.Undefined ? m.TaskType.ToString()
                : m.UiSection != DrawDetailType.Undefined ? "Open" + m.UiSection : null;
            if (action == null)
            {
                continue;
            }
            rows.Add(new
            {
                index = i,
                action,
                description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description,
                key = (m.ShiftActive ? "Shift+" : "") + (string.IsNullOrEmpty(m.KeyName) ? m.Key.ToString() : m.KeyName),
                role = m.QueueBaseRole.ToString() == "Undefined" ? null : m.QueueBaseRole.ToString(),
            });
        }
        return rows;
    }

    // ---- The game's message dialog: Accept / Decline, Dismiss, Take me there... ----
    // Done the way the player does it: the message is opened in the game's message dialog (if it is not on
    // screen already) and that dialog's own button is clicked, so the game's own checks and costs apply.
    // Advisor suggestions: Yes = "Assign Mission" / "Accept Contract...", No = "Decline". Other messages: Yes is
    // often "Dismiss", Show "Take me to...". If the dialog does not show that message, or the button is hidden
    // or disabled, nothing is clicked.

    private static readonly ConcurrentQueue<(string key, string button)> answers = new();

    /// <summary>What happened to the last dialog button pressed from the page, for /api/info.</summary>
    public static volatile string LastAnswer;

    public static void AnswerSuggestion(string key, bool accept) => answers.Enqueue((key, accept ? "yes" : "no"));

    public static void PressDialogButton(string key, string button) => answers.Enqueue((key, button));

    private static void RunAnswers(DWGame game)
    {
        while (answers.TryDequeue(out var a))
        {
            string what = a.button;
            try
            {
                if (game.Renderer is not ScaledRenderer renderer || renderer.PlayerEmpire is not Empire empire)
                {
                    LastAnswer = "no game loaded";
                    continue;
                }
                // The message is a suggestion in the queue, or the one on screen in the game's dialog.
                var onScreen = DistantWorlds.UI.UserInterfaceController.EmpireMessageDialogGetActiveMessage();
                var message = Snapshot.Copy(empire.EvaluationQueue).Append(onScreen)
                    .FirstOrDefault(m => m != null && (Views.MessageKey(m, true) == a.key || Views.MessageKey(m, false) == a.key));
                if (message == null)
                {
                    LastAnswer = "that message is no longer waiting (answered, closed or expired)";
                    Log.Warn($"Dialog {a.key}: not on screen nor in the queue.");
                    continue;
                }
                if (!ReferenceEquals(onScreen, message))
                {
                    renderer.OpenMessage(message, true, false);
                }
                var dialog = DistantWorlds.UI.UserInterfaceController.EmpireMessageDialog;
                var button = dialog == null ? null : Views.DialogButtonsById(dialog).FirstOrDefault(b => b.id == a.button).button;
                bool shown = dialog != null && ReferenceEquals(dialog.Message, message);
                what = button?.Text ?? a.button;
                Log.Info($"Dialog '{message.Title}' → {a.button} ('{button?.Text}'). Dialog shows it: {shown}; buttons: " +
                    (dialog == null ? "none" : string.Join(", ", Views.DialogButtonsById(dialog).Select(b => $"{b.id}='{b.button?.Text}' visible={b.button?.Visible} enabled={b.button?.Enabled}"))));
                if (!shown || button == null)
                {
                    LastAnswer = $"{what}: the game did not open that message; nothing done";
                    continue;
                }
                if (!button.Visible || !button.Enabled)
                {
                    LastAnswer = $"{what}: the game's button is not available now";
                    continue;
                }
                var args = new DWEventArgs(button.ExtraClickData);
                button.PreClickEvent?.Invoke(button, args);
                button.ClickEvent?.Invoke(button, args);
                // Short: the button, and the message's title when it says more (the task text carries markup).
                string title = message.Title;
                LastAnswer = string.IsNullOrEmpty(title) || what.Contains(title, StringComparison.OrdinalIgnoreCase) || title.Contains(what, StringComparison.OrdinalIgnoreCase)
                    ? (title != null && title.Length > what.Length ? title : what) : $"{what} · {title}";
                Log.Info($"Dialog button pressed: {a.button} ('{what}').");
            }
            catch (Exception e)
            {
                LastAnswer = $"{what}: failed ({e.Message})";
                Log.Error($"Dialog {a.key}: {e}");
            }
        }
    }

    // ---- Research: the game's research screen, scrolled to one project ----
    // Opened with the game's own shortcut (the key mapping whose UiSection is the research screen); from the
    // next frame on, the tree (UserInterfaceController.ResearchScreen) is scrolled so the project sits in the
    // middle and shows as hovered. Positions are logged: the tree's coordinates are still to be confirmed.
    private static int researchTarget = -1, researchFrames;   // -1: nothing to open (0 is a real project id)

    public static void OpenResearch(int projectId) => jobs.Enqueue(_ => { researchTarget = projectId; researchFrames = 0; });

    private static void RunResearch(DWGame game)
    {
        if (researchTarget < 0)
        {
            return;
        }
        researchFrames++;
        try
        {
            var screen = DistantWorlds.UI.UserInterfaceController.ResearchScreen;
            if (researchFrames == 1 && (screen == null || !screen.Visible))
            {
                var mappings = game.DWSettings?.KeyMappings;
                int index = -1;
                for (int i = 0; mappings != null && i < mappings.Count; i++)
                {
                    if (mappings[i] != null && mappings[i].CockpitTaskType == CockpitTaskType.Undefined && mappings[i].UiSection.ToString().Contains("Research"))
                    {
                        index = i;
                        break;
                    }
                }
                Log.Info($"Research {researchTarget}: opening the research screen (key mapping {index}).");
                if (index >= 0)
                {
                    keyPresses.Enqueue(index);
                }
                return;
            }
            if (screen == null || !screen.Visible)
            {
                if (researchFrames > 60)
                {
                    Log.Warn($"Research {researchTarget}: the research screen did not open.");
                    researchTarget = -1;
                }
                return;
            }
            var empire = (game.Renderer as ScaledRenderer)?.PlayerEmpire;
            var project = Snapshot.Copy(empire?.Research?.Projects).FirstOrDefault(p => p.ResearchProjectId == researchTarget);
            if (project == null)
            {
                Log.Warn($"Research {researchTarget}: no such project for this empire.");
                researchTarget = -1;
                return;
            }
            var before = screen.ScrollOffset;
            var posBefore = screen.CalculateProjectPosition(project);
            screen.ScrollOffset = Stride.Core.Mathematics.Vector2.Zero;
            var pos = screen.CalculateProjectPosition(project);
            var rect = screen.DisplayRectangle;
            var size = screen.ProjectDrawSizeScaled;
            var max = screen.MaximumScrollOffset;
            var target = new Stride.Core.Mathematics.Vector2(pos.X + size.X / 2 - rect.Width / 2, pos.Y + size.Y / 2 - rect.Height / 2);
            target = Stride.Core.Mathematics.Vector2.Clamp(target, Stride.Core.Mathematics.Vector2.Zero, max);
            screen.ScrollOffset = target;
            screen.HoveredProject = project;
            Log.Info($"Research '{project.Definition?.Name}': position {posBefore} at scroll {before}, {pos} at scroll 0; project size {size}; view {rect}; " +
                $"scroll set to {target} (now {screen.ScrollOffset}, max {max}); after: {screen.CalculateProjectPosition(project)}.");
        }
        catch (Exception e)
        {
            Log.Warn($"Research {researchTarget}: {e.Message}");
        }
        researchTarget = -1;
    }

    public static void Run(DWGame game, Stride.Games.GameTime gameTime)
    {
        RunResearch(game);
        RunAnswers(game);
        while (jobs.TryDequeue(out var job))
        {
            job(game);
        }
        while (keyPresses.TryDequeue(out int index))
        {
            try
            {
                var mappings = game.DWSettings?.KeyMappings;
                if (game.Galaxy == null || game.InputSystem == null || mappings == null || index < 0 || index >= mappings.Count)
                {
                    Log.Warn($"Key {index}: no game loaded or no such mapping.");
                    continue;
                }
                var m = mappings[index];
                float dt = (float)gameTime.Elapsed.TotalSeconds;
                game.InputSystem.HandleKeyPress(game.Galaxy, m, gameTime, dt, (float)gameTime.Total.TotalSeconds, dt);
                if (logged++ < 50)
                {
                    Log.Info($"Key {index}: {m.TaskType}/{m.UiSection} ({m.KeyName}) {m.Description}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"Key {index}: {e}");
            }
        }
        while (pending.TryDequeue(out var command))
        {
            try
            {
                var galaxy = game.Galaxy;
                if (galaxy == null || game.Renderer is not ScaledRenderer renderer)
                {
                    Log.Warn($"GoTo {command.type} {command.id}: no game loaded.");
                    continue;
                }
                if (command.type == "fleet")
                {
                    var empire = renderer.PlayerEmpire;
                    var fleet = empire?.Fleets?.FirstOrDefault(f => f.FleetId == command.id);
                    var lead = fleet?.LeadShip;
                    if (fleet == null)
                    {
                        Log.Warn($"GoTo fleet {command.id}: not found.");
                        continue;
                    }
                    renderer.SetSelectedObject(fleet);
                    if (lead != null)
                    {
                        renderer.MoveViewNoZoom(lead);
                    }
                    if (logged++ < 50)
                    {
                        Log.Info($"GoTo fleet {command.id}: {fleet.Name} (lead {lead?.Name ?? "none"})");
                    }
                    continue;
                }
                StellarObject target = command.type switch
                {
                    "ship" => galaxy.Ships.GetById(command.id),
                    "orb" => galaxy.Orbs.GetById(command.id),
                    _ => null,
                };
                if (target == null)
                {
                    Log.Warn($"GoTo {command.type} {command.id}: not found.");
                    continue;
                }
                renderer.SetSelectedObject(target);
                renderer.MoveViewNoZoom(target);
                if (logged++ < 50)
                {
                    Log.Info($"GoTo {command.type} {command.id}: {(target as INamed)?.Name}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"GoTo {command.type} {command.id}: {e}");
            }
        }
    }
}
