using RetroMultiTools.Models;
using RetroMultiTools.Services;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RetroMultiTools.Utilities;

/// <summary>
/// Provides Discord Rich Presence integration using the Discord IPC protocol.
/// Updates the user's Discord status to show the current ROM/game being played.
/// </summary>
public static class DiscordRichPresence
{
    private const string ApplicationId = "1482627815391105105";
    private const int IpcVersion = 1;

    private static readonly object Lock = new();
    private static bool _connected;
    private static Stream? _pipeStream;
    private static long _startTimestamp;

    /// <summary>
    /// Returns a display-friendly system name for Rich Presence details.
    /// </summary>
    private static string GetSystemDisplayName(RomSystem system) => system switch
    {
        RomSystem.NES => "Nintendo Entertainment System",
        RomSystem.SNES => "Super Nintendo",
        RomSystem.N64 => "Nintendo 64",
        RomSystem.GameBoy => "Game Boy",
        RomSystem.GameBoyColor => "Game Boy Color",
        RomSystem.GameBoyAdvance => "Game Boy Advance",
        RomSystem.VirtualBoy => "Virtual Boy",
        RomSystem.SegaMasterSystem => "Sega Master System",
        RomSystem.MegaDrive => "Sega Genesis / Mega Drive",
        RomSystem.SegaCD => "Sega CD",
        RomSystem.Sega32X => "Sega 32X",
        RomSystem.GameGear => "Game Gear",
        RomSystem.Atari2600 => "Atari 2600",
        RomSystem.Atari5200 => "Atari 5200",
        RomSystem.Atari7800 => "Atari 7800",
        RomSystem.AtariJaguar => "Atari Jaguar",
        RomSystem.AtariLynx => "Atari Lynx",
        RomSystem.PCEngine => "PC Engine / TurboGrafx-16",
        RomSystem.NeoGeoPocket => "Neo Geo Pocket",
        RomSystem.ColecoVision => "ColecoVision",
        RomSystem.Intellivision => "Intellivision",
        RomSystem.MSX => "MSX",
        RomSystem.MSX2 => "MSX2",
        RomSystem.AmstradCPC => "Amstrad CPC",
        RomSystem.Oric => "Oric",
        RomSystem.ThomsonMO5 => "Thomson MO5",
        RomSystem.WataraSupervision => "Watara Supervision",
        RomSystem.ColorComputer => "TRS-80 Color Computer",
        RomSystem.Panasonic3DO => "3DO",
        RomSystem.AmigaCD32 => "Amiga CD32",
        RomSystem.SegaSaturn => "Sega Saturn",
        RomSystem.SegaDreamcast => "Sega Dreamcast",
        RomSystem.GameCube => "Nintendo GameCube",
        RomSystem.Wii => "Nintendo Wii",
        RomSystem.Arcade => "Arcade",
        RomSystem.Atari800 => "Atari 800",
        RomSystem.NECPC88 => "NEC PC-88",
        RomSystem.N64DD => "Nintendo 64DD",
        RomSystem.NintendoDS => "Nintendo DS",
        RomSystem.Nintendo3DS => "Nintendo 3DS",
        RomSystem.NeoGeo => "Neo Geo",
        RomSystem.NeoGeoCD => "Neo Geo CD",
        RomSystem.PhilipsCDi => "Philips CD-i",
        RomSystem.FairchildChannelF => "Fairchild Channel F",
        RomSystem.TigerGameCom => "Tiger Game Com",
        RomSystem.MemotechMTX => "Memotech MTX",
        _ => "Retro Gaming"
    };

    /// <summary>
    /// Returns a lowercase image key derived from the system enum name for use as
    /// a Discord Rich Presence asset key. Asset images should be uploaded to the
    /// Discord Developer Portal with matching names.
    /// </summary>
    private static string GetSystemImageKey(RomSystem system) =>
        system.ToString().ToLowerInvariant();

    /// <summary>
    /// Updates Discord Rich Presence to show the current game being played.
    /// Call this when launching a ROM via RetroArch.
    /// Runs on a background thread to avoid blocking the UI.
    /// </summary>
    public static void UpdatePresence(string romFileName, RomSystem system)
    {
        if (!AppSettings.Instance.DiscordRichPresenceEnabled)
            return;

        // Capture values for the background thread
        string gameName = Path.GetFileNameWithoutExtension(romFileName);
        string systemName = GetSystemDisplayName(system);
        string imageKey = GetSystemImageKey(system);

        _ = Task.Run(() =>
        {
            try
            {
                lock (Lock)
                {
                    if (!_connected)
                        Connect();

                    if (!_connected)
                        return;

                    _startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    var payload = new
                    {
                        cmd = "SET_ACTIVITY",
                        args = new
                        {
                            pid = Environment.ProcessId,
                            activity = new
                            {
                                state = $"Playing on {systemName}",
                                details = gameName,
                                timestamps = new
                                {
                                    start = _startTimestamp
                                },
                                assets = new
                                {
                                    large_image = imageKey,
                                    large_text = systemName,
                                    small_image = "retromultitools",
                                    small_text = "Retro Multi Tools"
                                }
                            }
                        },
                        nonce = Guid.NewGuid().ToString()
                    };

                    SendPayload(1, payload);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException) { }
        });
    }

    /// <summary>
    /// Clears the current Discord Rich Presence status.
    /// Call this when a game is closed or the user stops playing.
    /// </summary>
    public static void ClearPresence()
    {
        try
        {
            lock (Lock)
            {
                if (!_connected)
                    return;

                var payload = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = Environment.ProcessId,
                        activity = (object?)null
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                SendPayload(1, payload);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException) { }
    }

    /// <summary>
    /// Disconnects from the Discord IPC pipe and cleans up resources.
    /// </summary>
    public static void Shutdown()
    {
        lock (Lock)
        {
            try
            {
                _pipeStream?.Dispose();
            }
            catch (IOException) { }
            _pipeStream = null;
            _connected = false;
        }
    }

    private static void Connect()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                string pipeName = $"discord-ipc-{i}";
                var pipe = new System.IO.Pipes.NamedPipeClientStream(
                    ".", pipeName, System.IO.Pipes.PipeDirection.InOut,
                    System.IO.Pipes.PipeOptions.Asynchronous);

                try
                {
                    pipe.Connect(timeout: 1000);
                    _pipeStream = pipe;

                    // Send handshake
                    var handshake = new { v = IpcVersion, client_id = ApplicationId };
                    SendPayload(0, handshake);

                    // Read handshake response
                    ReadResponse();

                    _connected = true;
                    return;
                }
                catch
                {
                    // Dispose the pipe if handshake fails so the handle is not leaked.
                    if (_pipeStream == pipe)
                        _pipeStream = null;
                    pipe.Dispose();
                    throw;
                }
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or SocketException or InvalidOperationException) { continue; }
        }
    }

    private static void SendPayload(int opcode, object payload)
    {
        try
        {
            if (_pipeStream == null || !_pipeStream.CanWrite)
                return;

            string json = JsonSerializer.Serialize(payload);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

            byte[] header = new byte[8];
            BitConverter.TryWriteBytes(header.AsSpan(0, 4), opcode);
            BitConverter.TryWriteBytes(header.AsSpan(4, 4), jsonBytes.Length);

            _pipeStream.Write(header, 0, header.Length);
            _pipeStream.Write(jsonBytes, 0, jsonBytes.Length);
            _pipeStream.Flush();
        }
        catch (ObjectDisposedException) { }
    }

    private static void ReadResponse()
    {
        try
        {
            if (_pipeStream == null || !_pipeStream.CanRead)
                return;

            byte[] header = new byte[8];
            int bytesRead = 0;
            while (bytesRead < 8)
            {
                int read = _pipeStream.Read(header, bytesRead, 8 - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }

            if (bytesRead < 8) return;

            int length = BitConverter.ToInt32(header, 4);
            if (length <= 0 || length > 65536) return;

            byte[] body = new byte[length];
            bytesRead = 0;
            while (bytesRead < length)
            {
                int read = _pipeStream.Read(body, bytesRead, length - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }
        }
        catch (ObjectDisposedException) { }
    }
}
