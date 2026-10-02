using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JalaliTray;

public static class NtpClient
{
    const long Epoch1900Ticks = 599266080000000000L; // 1900-01-01 UTC in DateTime ticks

    /// <summary>Returns (server time - local UTC clock) using the standard 4-timestamp NTP formula.</summary>
    public static async Task<TimeSpan> GetOffsetAsync(string server, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var addresses = await Dns.GetHostAddressesAsync(server, cts.Token);
        var ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                 ?? throw new SocketException((int)SocketError.HostNotFound);

        var request = new byte[48];
        request[0] = 0x1B; // LI=0, VN=3, Mode=3 (client)

        using var udp = new UdpClient(ip.AddressFamily);
        udp.Connect(ip, 123);

        var t1 = DateTime.UtcNow;
        await udp.SendAsync(request, cts.Token);
        var result = await udp.ReceiveAsync(cts.Token);
        var t4 = DateTime.UtcNow;

        var data = result.Buffer;
        if (data.Length < 48) throw new InvalidDataException("Short NTP reply");

        var t2 = ReadTimestamp(data, 32);
        var t3 = ReadTimestamp(data, 40);
        return ((t2 - t1) + (t3 - t4)) / 2;
    }

    static DateTime ReadTimestamp(byte[] b, int offset)
    {
        ulong seconds = ((ulong)b[offset] << 24) | ((ulong)b[offset + 1] << 16) | ((ulong)b[offset + 2] << 8) | b[offset + 3];
        ulong fraction = ((ulong)b[offset + 4] << 24) | ((ulong)b[offset + 5] << 16) | ((ulong)b[offset + 6] << 8) | b[offset + 7];
        // MSB clear means the 32-bit seconds counter has wrapped past 2036.
        long era = (b[offset] & 0x80) == 0 ? 1L << 32 : 0;
        long ticks = (long)((seconds + (ulong)era) * TimeSpan.TicksPerSecond + fraction * TimeSpan.TicksPerSecond / 0x100000000UL);
        return new DateTime(Epoch1900Ticks + ticks, DateTimeKind.Utc);
    }
}

/// <summary>
/// The app's notion of "now". With NTP enabled it applies an offset to the Windows clock
/// without touching the system time.
/// </summary>
public static class TimeService
{
    public static TimeSpan Offset { get; private set; }
    public static DateTime? LastSyncUtc { get; private set; }

    public static DateTime Now => (DateTime.UtcNow + Offset).ToLocalTime();
    public static DateTime Today => Now.Date;

    public static async Task<bool> SyncAsync(string server)
    {
        try
        {
            Offset = await NtpClient.GetOffsetAsync(server);
            LastSyncUtc = DateTime.UtcNow;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Reset()
    {
        Offset = TimeSpan.Zero;
        LastSyncUtc = null;
    }
}
