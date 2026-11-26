using System.Diagnostics;
using System.Runtime.InteropServices;
using DistantWorlds2;

namespace DW2Overview;

/// <summary>
/// Sound controls for the page: mute the whole game, and the music volume.
///
/// - Mute uses this process's audio sessions in Windows (the volume mixer's switch), like Window Fit's
///   "mute when unfocused": music and effects stop together and the game's saved settings never change.
///   The page's mute is the player's wish, so it holds: if something unmutes the game (Window Fit when the
///   game gets the focus back), it is muted again within a couple of seconds. Unmuting unmutes every session
///   of the game. Closing the game normally undoes the mute (Windows would remember it).
/// - Music is the game's own setting (<c>DWSettings.MusicVolume</c>), the one its options screen changes;
///   the music system reads it as it plays. Mute music remembers the volume to go back to.
/// All of it runs on the game thread.
/// </summary>
internal static class Audio
{
    private static readonly uint pid = (uint)Environment.ProcessId;
    private static Guid eventContext = Guid.NewGuid();
    private static readonly Stopwatch sinceCheck = Stopwatch.StartNew();
    private static bool wantMuted;
    private static bool failed;
    private static float musicBeforeMute = 0.5f;

    /// <summary>Last state seen: every game session muted (null before the first check or on failure).</summary>
    public static bool? Muted { get; private set; }
    public static float? Music { get; private set; }

    static Audio()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            if (wantMuted)
            {
                try { SetSessionsMute(false); } catch { }
            }
        };
    }

    /// <summary>Called every frame from the game's Update; checks (and keeps the page's mute) every 2 s.</summary>
    public static void Tick(DWGame game)
    {
        if (sinceCheck.Elapsed.TotalSeconds < 2)
        {
            return;
        }
        sinceCheck.Restart();
        Music = Snapshot.Safe(() => game.DWSettings?.MusicVolume);
        if (failed)
        {
            return;
        }
        try
        {
            var (count, muted) = ReadSessions();
            if (wantMuted && count > muted)
            {
                int n = SetSessionsMute(true);
                Log.Info($"Audio: the game was unmuted by something else; muted {n} session(s) again.");
                muted = count;
            }
            Muted = count == 0 ? null : muted == count;
        }
        catch (Exception e)
        {
            failed = true;
            Muted = null;
            Log.Warn("Audio sessions unavailable, mute turned off: " + e.Message);
        }
    }

    /// <summary>Applies the page's request: mute (true/false) and/or music volume (0..1). Returns the new state.</summary>
    public static object Set(DWGame game, bool? mute, float? music)
    {
        if (mute != null && !failed)
        {
            wantMuted = mute.Value;
            int n = SetSessionsMute(mute.Value);
            Log.Info($"Audio: {(mute.Value ? "muted" : "unmuted")} {n} session(s) from the page.");
            var (count, muted) = ReadSessions();
            Muted = count == 0 ? null : muted == count;
        }
        if (music != null && game.DWSettings != null)
        {
            float before = game.DWSettings.MusicVolume;
            // A negative volume means "unmute music": back to the volume it had before going to 0.
            float value = music.Value < 0f ? musicBeforeMute : Math.Clamp(music.Value, 0f, 1f);
            if (value == 0f && before > 0f)
            {
                musicBeforeMute = before;
            }
            game.DWSettings.MusicVolume = value;
            Music = value;
            Log.Info($"Audio: music volume {before:0.##} -> {value:0.##}; music system volume now {Snapshot.Safe(() => game.MusicSystem?.Volume):0.##}.");
        }
        return new { muted = Muted, music = Music, musicRestore = musicBeforeMute };
    }

    /// <summary>The volume "unmute music" goes back to.</summary>
    public static float MusicRestore => musicBeforeMute;

    private static (int count, int muted) ReadSessions()
    {
        int count = 0, muted = 0;
        ForEachSession(v =>
        {
            v.GetMute(out int m);
            count++;
            if (m != 0)
            {
                muted++;
            }
        });
        return (count, muted);
    }

    private static int SetSessionsMute(bool mute)
    {
        int changed = 0;
        ForEachSession(v =>
        {
            v.GetMute(out int m);
            if ((m != 0) != mute)
            {
                v.SetMute(mute ? 1 : 0, ref eventContext);
                changed++;
            }
        });
        return changed;
    }

    /// <summary>This process's audio sessions on every active output device.</summary>
    private static void ForEachSession(Action<ISimpleAudioVolume> act)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out var devices);
        devices.GetCount(out uint deviceCount);
        for (uint d = 0; d < deviceCount; d++)
        {
            devices.Item(d, out var device);
            var iid = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out object o);
            var manager = (IAudioSessionManager2)o;
            manager.GetSessionEnumerator(out var sessions);
            sessions.GetCount(out int count);
            for (int i = 0; i < count; i++)
            {
                sessions.GetSession(i, out var session);
                if (session.GetProcessId(out uint sessionPid) < 0 || sessionPid != pid)
                {
                    continue;
                }
                act((ISimpleAudioVolume)session);
            }
        }
    }

    // ── Windows Core Audio (mmdeviceapi.h, audiopolicy.h), as in Window Fit ──

    private const uint DeviceStateActive = 1;
    private const int ClsctxAll = 23;

    private enum EDataFlow { Render = 0 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, int role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client);
        void UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint count);
        void Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        void OpenPropertyStore(int access, out IntPtr properties);
        void GetId(out IntPtr id);
        void GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void GetAudioSessionControl(IntPtr sessionGuid, uint flags, out IntPtr control);
        void GetSimpleAudioVolume(IntPtr sessionGuid, uint flags, out IntPtr volume);
        void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        void RegisterSessionNotification(IntPtr notification);
        void UnregisterSessionNotification(IntPtr notification);
        void RegisterDuckNotification(IntPtr sessionId, IntPtr notification);
        void UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int count);
        void GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl
        void GetState(out int state);
        void GetDisplayName(out IntPtr name);
        void SetDisplayName(IntPtr name, IntPtr eventContext);
        void GetIconPath(out IntPtr path);
        void SetIconPath(IntPtr path, IntPtr eventContext);
        void GetGroupingParam(out Guid grouping);
        void SetGroupingParam(IntPtr grouping, IntPtr eventContext);
        void RegisterAudioSessionNotification(IntPtr notification);
        void UnregisterAudioSessionNotification(IntPtr notification);
        // IAudioSessionControl2
        void GetSessionIdentifier(out IntPtr id);
        void GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        void SetDuckingPreference(int optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        void SetMasterVolume(float level, ref Guid eventContext);
        void GetMasterVolume(out float level);
        void SetMute(int mute, ref Guid eventContext);
        void GetMute(out int mute);
    }
}
