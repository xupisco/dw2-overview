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

    public static void Run(DWGame game)
    {
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
