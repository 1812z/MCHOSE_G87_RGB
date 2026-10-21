namespace Artemis.Plugins.MchoseG87;

public static class G87Protocol
{
    public const int ReportLength = 20;
    public const int SlotCount = 108;
    public static readonly int[] ActiveSlots = [0,1,2,3,4,5,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,24,25,26,27,28,30,31,32,33,34,35,36,37,38,39,40,42,43,44,45,46,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,72,73,74,78,79,80,81,82,83,84,85,86,89,90,91,92,94,95,96,97,98,101];

    // Conservative compatibility path: official capture never exceeded eight
    // packets/frame. 87 keys + six RGB group headers = 111 bytes (8 reports).
    // Omit black keys, as official GIF frames do, and never address empty slots.
    public static List<byte[]> EncodeWireless(ReadOnlySpan<byte> rgb)
    {
        if (rgb.Length != SlotCount * 3) throw new ArgumentException("Expected 108 RGB slots", nameof(rgb));
        var groups = new List<(double R, double G, double B, List<byte> Keys)>();
        foreach (int slot in ActiveSlots)
        {
            int r = rgb[slot * 3], g = rgb[slot * 3 + 1], b = rgb[slot * 3 + 2];
            if ((r | g | b) == 0) continue;
            int match = groups.FindIndex(c => c.R == r && c.G == g && c.B == b);
            if (match < 0) groups.Add((r, g, b, [(byte)slot]));
            else groups[match].Keys.Add((byte)slot);
        }
        while (groups.Count > 6)
        {
            int a = 0, b = 1;
            double best = double.MaxValue;
            for (int i = 0; i < groups.Count; i++)
                for (int j = i + 1; j < groups.Count; j++)
                {
                    var x = groups[i]; var y = groups[j];
                    double distance = (Math.Pow(x.R-y.R, 2) + Math.Pow(x.G-y.G, 2) + Math.Pow(x.B-y.B, 2)) * x.Keys.Count * y.Keys.Count / (x.Keys.Count + y.Keys.Count);
                    if (distance < best) { best = distance; a = i; b = j; }
                }
            var first = groups[a]; var second = groups[b];
            double n = first.Keys.Count, m = second.Keys.Count;
            var merged = ((first.R*n+second.R*m)/(n+m), (first.G*n+second.G*m)/(n+m), (first.B*n+second.B*m)/(n+m), first.Keys);
            first.Keys.AddRange(second.Keys);
            groups[a] = merged;
            groups.RemoveAt(b);
        }
        if (groups.Count == 0) return Packetize([0, 0, 0, 1]);
        var payload = new List<byte>();
        foreach (var group in groups)
        {
            payload.Add((byte)Math.Round(group.R));
            payload.Add((byte)Math.Round(group.G));
            payload.Add((byte)Math.Round(group.B));
            payload.Add((byte)group.Keys.Count);
            payload.AddRange(group.Keys);
        }
        return Packetize(payload.ToArray());
    }

    public static byte[] ScaleBrightness(ReadOnlySpan<byte> rgb, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        byte[] scaled = new byte[rgb.Length];
        for (int i = 0; i < rgb.Length; i++) scaled[i] = (byte)((rgb[i] * percent + 50) / 100);
        return scaled;
    }

    // Observed GIF format: RGB, number of keys, key indices. Unlisted keys
    // are black in official frames. Always include every controlled slot.
    public static List<byte[]> Encode(ReadOnlySpan<byte> rgb)
    {
        if (rgb.Length != SlotCount * 3) throw new ArgumentException("Expected 108 RGB slots", nameof(rgb));
        var groups = new Dictionary<int, List<byte>>();
        for (int key = 0; key < SlotCount; key++)
        {
            int offset = key * 3;
            int color = (rgb[offset] << 16) | (rgb[offset + 1] << 8) | rgb[offset + 2];
            if (!groups.TryGetValue(color, out var keys)) groups[color] = keys = [];
            keys.Add((byte)key);
        }
        var payload = new List<byte>();
        foreach (var (color, keys) in groups)
        {
            payload.Add((byte)(color >> 16));
            payload.Add((byte)(color >> 8));
            payload.Add((byte)color);
            payload.Add((byte)keys.Count);
            payload.AddRange(keys);
        }
        return Packetize(payload.ToArray());
    }

    public static List<byte[]> Packetize(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty || payload.Length > 255 * 14) throw new ArgumentOutOfRangeException(nameof(payload));
        int count = (payload.Length + 13) / 14;
        var reports = new List<byte[]>(count);
        for (int i = 0; i < count; i++)
        {
            int size = Math.Min(14, payload.Length - i * 14);
            byte[] report = new byte[ReportLength];
            report[0] = 0x13;
            report[1] = 0x88;
            report[2] = (byte)count;
            report[3] = (byte)i;
            report[4] = (byte)(0x10 + size);
            payload.Slice(i * 14, size).CopyTo(report.AsSpan(5));
            // Non-final captured reports sometimes understate their size by
            // one byte; their payload is nevertheless 14 bytes. New reports
            // use the unambiguous full-length value seen in most packets.
            report[19] = (byte)(report.Take(19).Sum(b => (int)b) & 255);
            reports.Add(report);
        }
        return reports;
    }
}
