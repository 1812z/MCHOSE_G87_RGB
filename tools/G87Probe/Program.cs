using HidSharp;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Artemis.Plugins.MchoseG87;

if (args.Contains("--verify"))
{
    foreach (int percent in new[] { -1, 0, 1, 25, 50, 75, 99, 100, 101 })
    {
        byte[] levels = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        byte[] scaled = G87Protocol.ScaleBrightness(levels, percent);
        int clamped = Math.Clamp(percent, 0, 100);
        for (int i = 0; i < levels.Length; i++)
            if (scaled[i] != (byte)Math.Round(i * clamped / 100.0, MidpointRounding.AwayFromZero))
                throw new InvalidDataException("Brightness rounding/clamping failed");
    }
    Console.WriteLine("PASS: brightness 0–100%, endpoints, rounding and bounds.");
    string capture = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "captured-frames.json");
    using var document = JsonDocument.Parse(File.ReadAllText(capture));
    int capturedCount = 0;
    foreach (var frame in document.RootElement.EnumerateArray())
    {
        var reports = frame.GetProperty("reports").EnumerateArray()
            .Select(r => Convert.FromHexString(r.GetString()!)).ToArray();
        var payload = Unpack(reports, true);
        if (!payload.SequenceEqual(Unpack(G87Protocol.Packetize(payload), false)))
            throw new InvalidDataException("Captured payload did not survive packetization");
        capturedCount++;
    }
    var random = new Random(87);
    for (int test = 0; test < 100; test++)
    {
        byte[] rgb = new byte[G87Protocol.SlotCount * 3];
        random.NextBytes(rgb);
        if (test == 0) Array.Clear(rgb);
        var wireless = G87Protocol.EncodeWireless(rgb);
        if (wireless.Count > 8) throw new InvalidDataException("Wireless packet budget exceeded");
        var compact = Unpack(wireless, false);
        if (test != 0)
        {
            var assigned = new HashSet<byte>();
            int cursor = 0, groups = 0;
            while (cursor < compact.Length)
            {
                cursor += 3;
                int count = compact[cursor++];
                groups++;
                for (int j = 0; j < count; j++)
                {
                    byte slot = compact[cursor++];
                    if (!G87Protocol.ActiveSlots.Contains((int)slot) || !assigned.Add(slot))
                        throw new InvalidDataException("Wireless slot invalid or repeated");
                }
            }
            if (groups > 6 || assigned.Count != 87) throw new InvalidDataException("Wireless frame coverage failed");
        }
        var payload = Unpack(G87Protocol.Encode(rgb), false);
        byte[] decoded = new byte[rgb.Length];
        var seen = new HashSet<byte>();
        for (int cursor = 0; cursor < payload.Length;)
        {
            byte r = payload[cursor++], g = payload[cursor++], b = payload[cursor++];
            int count = payload[cursor++];
            for (int i = 0; i < count; i++)
            {
                byte key = payload[cursor++];
                if (key >= G87Protocol.SlotCount || !seen.Add(key)) throw new InvalidDataException("Invalid/repeated slot");
                decoded[key * 3] = r;
                decoded[key * 3 + 1] = g;
                decoded[key * 3 + 2] = b;
            }
        }
        if (seen.Count != G87Protocol.SlotCount || !rgb.SequenceEqual(decoded))
            throw new InvalidDataException("RGB round-trip failed");
    }
    Console.WriteLine($"PASS: {capturedCount} real captured payloads repacketized; 100 RGB round-trips covering all 108 slots, checksums, lengths, ordering and padding.");
    Console.WriteLine("PASS: compact wireless frames contain all 87 active keys, at most six color groups and eight reports.");
    return;
}

if (args.Contains("--review-mapping"))
{
    string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "g87-key-calibration.json"));
    var mapping = JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(output))!;
    string backup = output + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
    File.Copy(output, backup, false);
    Console.WriteLine("Disable Artemis G87 plugin and exit MCHOSE HUB first.");
    Console.WriteLine("Review ambiguous slots only. Do NOT press the illuminated physical key.");
    Console.WriteLine("Type its NAME then Enter; '-' = no LED; 'flash' = multiple LEDs/flash; /quit = save and quit. q means the Q key.");
    Console.WriteLine("Each slot is sent 3 times; wait for the prompt, then observe the steady red key.");
    Console.WriteLine($"Original backed up: {backup}");
    Console.WriteLine("Press Enter when ready.");
    Console.ReadLine();
    var hid = DeviceList.Local.GetHidDevices(0x41e4, 0x2001).Single(G87Transport.Matches);
    using var transport = new G87Transport(hid);
    try
    {
        int start = 0;
        int startArg = Array.IndexOf(args, "--start");
        if (startArg >= 0 && (startArg + 1 >= args.Length || !int.TryParse(args[startArg + 1], out start) || start < 0 || start >= G87Protocol.SlotCount))
            throw new ArgumentException("--start must be between 0 and 107");
        foreach (int slot in Enumerable.Range(0, 19).Concat(Enumerable.Range(84, 24)).Where(slot => slot >= start))
        {
            byte[] rgb = new byte[G87Protocol.SlotCount * 3];
            rgb[slot * 3] = 80;
            for (int repeat = 0; repeat < 3; repeat++)
            {
                transport.Send(G87Protocol.Encode(rgb));
                Thread.Sleep(150);
            }
            Console.Write($"Hardware {slot,3} [previous: {mapping.GetValueOrDefault(slot, "unrecorded")}] - key NAME: ");
            string? answer = Console.ReadLine()?.Trim();
            if (answer == null || answer.Equals("/quit", StringComparison.OrdinalIgnoreCase)) break;
            if (answer.Length == 0) continue;
            mapping[slot] = answer == "-" ? "" : answer;
            File.WriteAllText(output, JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    finally { transport.Send(G87Protocol.Packetize([0, 0, 0, 1])); }
    Console.WriteLine("Review saved. No physical-key presses were needed.");
    return;
}

if (args.Contains("--calibrate-press"))
{
    string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "g87-key-calibration.json"));
    var mapping = File.Exists(output)
        ? JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(output))!
        : new Dictionary<int, string>();
    Console.WriteLine("Close MCHOSE HUB and disable the Artemis G87 plugin.");
    Console.WriteLine("Press the physical key lit red; modifiers are detected separately.");
    Console.WriteLine("Mouse LEFT = no visible LED / skip; RIGHT = Fn; MIDDLE = save and quit.");
    Console.WriteLine("For an unreported key other than Fn, use the text calibration mode.");
    Console.WriteLine("Avoid clicking other applications: keep this window foreground.");
    Console.WriteLine($"Saves after each key: {output}");
    Console.WriteLine("Press Enter to start (existing answers are preserved).");
    Console.ReadLine();
    bool stop = false;
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop = true; };
    Console.CancelKeyPress += cancel;
    var hid = DeviceList.Local.GetHidDevices(0x41e4, 0x2001).Single(G87Transport.Matches);
    using var transport = new G87Transport(hid);
    try
    {
        int[] watched = Enumerable.Range(1, 254)
            .Where(v => v is not (3 or 5 or 6 or 16 or 17 or 18)).ToArray();
        bool Down(int v) => (Native.GetAsyncKeyState(v) & 0x8000) != 0;
        foreach (int slot in Enumerable.Range(0, G87Protocol.SlotCount))
        {
            if (mapping.ContainsKey(slot)) continue;
            // Wait for release so a held key cannot answer multiple slots.
            while (!stop && watched.Any(Down)) Thread.Sleep(15);
            if (stop) break;
            Thread.Sleep(120);
            byte[] rgb = new byte[G87Protocol.SlotCount * 3];
            rgb[slot * 3] = 80;
            transport.Send(G87Protocol.Encode(rgb));
            Console.Write($"Hardware {slot,3}/107 - press illuminated key: ");
            int pressed = 0;
            while (!stop && pressed == 0)
            {
                pressed = watched.FirstOrDefault(Down);
                Thread.Sleep(10);
            }
            if (stop || pressed == 4) break;
            string name = pressed == 1 ? "" : pressed == 2 ? "Function" : VirtualKeyName(pressed);
            // Do not silently assign two slots to the same physical key.
            if (name.Length > 0 && mapping.Values.Contains(name))
            {
                Console.WriteLine($"{name}: already recorded. Stopped; check the saved mapping before continuing.");
                break;
            }
            mapping[slot] = name;
            File.WriteAllText(output, JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(name.Length == 0 ? "(empty)" : name);
        }
        Console.WriteLine("Saved. Middle mouse button / Ctrl+C stops safely; rerun to resume.");
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
        transport.Send(G87Protocol.Packetize([0, 0, 0, 1]));
    }
    return;
}

if (args.Contains("--calibrate"))
{
    string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "g87-key-calibration.json"));
    var mapping = File.Exists(output)
        ? JsonSerializer.Deserialize<Dictionary<int, string>>(File.ReadAllText(output))!
        : new Dictionary<int, string>();
    Console.WriteLine("Close MCHOSE HUB and DISABLE the Artemis G87 plugin first.");
    Console.WriteLine("Each hardware index lights red. Type the physical key name (e.g. Esc, F1, A, RightCtrl).");
    Console.WriteLine("Enter = no visible key. q = save and quit. r = repeat current index.");
    Console.WriteLine("Results save after every answer; restarting resumes at the next index.");
    Console.WriteLine($"Output: {output}");
    Console.WriteLine("Press Enter when ready. Ctrl+C cancels.");
    Console.ReadLine();
    var hid = DeviceList.Local.GetHidDevices(0x41e4, 0x2001).Single(G87Transport.Matches);
    using var transport = new G87Transport(hid);
    try
    {
        for (int slot = 0; slot < G87Protocol.SlotCount; slot++)
        {
            if (mapping.ContainsKey(slot)) continue;
            while (true)
            {
                byte[] rgb = new byte[G87Protocol.SlotCount * 3];
                rgb[slot * 3] = 80;
                transport.Send(G87Protocol.Encode(rgb));
                Console.Write($"Hardware {slot,3}/107 - visible key: ");
                string? name = Console.ReadLine();
                if (name == null || name.Equals("q", StringComparison.OrdinalIgnoreCase)) return;
                if (name.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;
                mapping[slot] = name.Trim();
                File.WriteAllText(output, JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true }));
                break;
            }
        }
        Console.WriteLine("Calibration complete. Share the result file for mapping correction.");
    }
    finally { transport.Send(G87Protocol.Packetize([0, 0, 0, 1])); }
    return;
}

if (args.Contains("--test-brightness") || args.Contains("--scan"))
{
    Console.WriteLine("Close MCHOSE HUB and disable the Artemis G87 plugin before this test.");
    Console.WriteLine("Press Enter to start, or Ctrl+C to cancel.");
    Console.ReadLine();
    var hid = DeviceList.Local.GetHidDevices(0x41e4, 0x2001).Single(G87Transport.Matches);
    using var transport = new G87Transport(hid);
    try
    {
        if (args.Contains("--test-brightness"))
        {
            byte[] white = Enumerable.Repeat((byte)255, G87Protocol.SlotCount * 3).ToArray();
            foreach (int percent in new[] { 0, 10, 25, 50, 75, 100, 50, 10, 0 })
            {
                Console.WriteLine($"Brightness: {percent}%");
                transport.Send(G87Protocol.Encode(G87Protocol.ScaleBrightness(white, percent)));
                Thread.Sleep(2000);
            }
        }
        else
        {
            Console.WriteLine("Enter hardware index 0–107 to light one red LED; q to finish.");
            while (true)
            {
                Console.Write("Index: ");
                string? input = Console.ReadLine();
                if (input == null || input.Equals("q", StringComparison.OrdinalIgnoreCase)) break;
                if (!int.TryParse(input, out int slot) || slot < 0 || slot >= G87Protocol.SlotCount) continue;
                byte[] rgb = new byte[G87Protocol.SlotCount * 3];
                rgb[slot * 3] = 64;
                transport.Send(G87Protocol.Encode(rgb));
            }
        }
    }
    finally { transport.Send(G87Protocol.Packetize([0, 0, 0, 1])); }
    return;
}

if (args.Contains("--test-generated"))
{
    var hid = DeviceList.Local.GetHidDevices(0x41e4, 0x2001).Single(G87Transport.Matches);
    using var transport = new G87Transport(hid);
    try
    {
        foreach (var color in new[] { (48, 0, 0), (0, 48, 0), (0, 0, 48) })
        {
            byte[] rgb = new byte[G87Protocol.SlotCount * 3];
            for (int slot = 0; slot < G87Protocol.SlotCount; slot++)
            {
                rgb[slot * 3] = (byte)color.Item1;
                rgb[slot * 3 + 1] = (byte)color.Item2;
                rgb[slot * 3 + 2] = (byte)color.Item3;
            }
            transport.Send(G87Protocol.Encode(rgb));
            Thread.Sleep(1500);
        }
    }
    finally { transport.Send(G87Protocol.Packetize([0, 0, 0, 1])); }
    Console.WriteLine("Generated red/green/blue test sent, followed by captured black marker. Visual verification required.");
    return;
}

if (args.Contains("--replay"))
{
    var candidates = DeviceList.Local.GetHidDevices(0x41e4, 0x2001)
        .Where(h => h.DevicePath.Contains("mi_01&col01", StringComparison.OrdinalIgnoreCase)
                    && h.GetMaxOutputReportLength() == 20).ToArray();
    if (candidates.Length != 1) throw new InvalidOperationException($"Expected one receiver output endpoint, found {candidates.Length}");
    string capture = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "captured-frames.json");
    using var document = JsonDocument.Parse(File.ReadAllText(capture));
    // Use the observed Windows SET_REPORT(Output), not interrupt-OUT writes.
    using var handle = Native.CreateFile(candidates[0].DevicePath, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
    if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    int frames = 0, reports = 0;
    foreach (var frame in document.RootElement.EnumerateArray().Take(48))
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var report in frame.GetProperty("reports").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(report.GetString()!);
            if (bytes.Length != 20 || bytes[0] != 0x13 || bytes[1] != 0x88 ||
                (bytes.Take(19).Sum(b => (int)b) & 255) != bytes[19])
                throw new InvalidDataException("Unexpected captured report");
            if (!Native.HidD_SetOutputReport(handle, bytes, bytes.Length))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            reports++;
            Thread.Sleep(4);
        }
        frames++;
        int delay = 106 - (int)clock.ElapsedMilliseconds;
        if (delay > 0) Thread.Sleep(delay);
    }
    Console.WriteLine($"Replay completed: {frames} captured frames, {reports} output reports. Visual confirmation required.");
    return;
}

// Descriptor-only inventory. No HID stream is opened and no commands are sent.
foreach (HidDevice hid in DeviceList.Local.GetHidDevices()
             .Where(h => h.VendorID is 0x41e4 or 0x3837))
{
    Console.WriteLine(hid.DevicePath);
    try
    {
        Console.WriteLine($"  VID={hid.VendorID:X4} PID={hid.ProductID:X4} product={hid.GetProductName()} manufacturer={hid.GetManufacturer()}");
        Console.WriteLine($"  input={hid.GetMaxInputReportLength()} output={hid.GetMaxOutputReportLength()} feature={hid.GetMaxFeatureReportLength()}");
        var descriptor = hid.GetReportDescriptor();
        foreach (var item in descriptor.DeviceItems)
            Console.WriteLine($"  usages={string.Join(",", item.Usages.GetAllValues().Select(u => u.ToString("X8")))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Descriptor unavailable: {ex.Message}");
    }
}

static string VirtualKeyName(int key)
{
    if (key is >= 0x41 and <= 0x5A || key is >= 0x30 and <= 0x39) return ((char)key).ToString();
    if (key is >= 0x70 and <= 0x87) return $"F{key - 0x6F}";
    return key switch
    {
        8 => "Backspace", 9 => "Tab", 13 => "Enter", 19 => "PauseBreak", 20 => "CapsLock", 27 => "Escape",
        32 => "Space", 33 => "PageUp", 34 => "PageDown", 35 => "End", 36 => "Home",
        37 => "ArrowLeft", 38 => "ArrowUp", 39 => "ArrowRight", 40 => "ArrowDown",
        44 => "PrintScreen", 45 => "Insert", 46 => "Delete", 91 => "LeftGui", 92 => "RightGui", 93 => "Application",
        144 => "NumLock", 145 => "ScrollLock", 160 => "LeftShift", 161 => "RightShift",
        162 => "LeftCtrl", 163 => "RightCtrl", 164 => "LeftAlt", 165 => "RightAlt",
        186 => "SemicolonAndColon", 187 => "EqualsAndPlus", 188 => "CommaAndLessThan", 189 => "MinusAndUnderscore",
        190 => "PeriodAndBiggerThan", 191 => "SlashAndQuestionMark", 192 => "GraveAccentAndTilde",
        219 => "BracketLeft", 220 => "Backslash", 221 => "BracketRight", 222 => "ApostropheAndDoubleQuote",
        _ => $"VK_{key:X2}"
    };
}

static byte[] Unpack(IEnumerable<byte[]> input, bool captured)
{
    var reports = input.ToArray();
    var payload = new List<byte>();
    for (int i = 0; i < reports.Length; i++)
    {
        var r = reports[i];
        if (r.Length != 20 || r[0] != 0x13 || r[1] != 0x88 || r[2] != reports.Length || r[3] != i ||
            (r.Take(19).Sum(b => (int)b) & 255) != r[19]) throw new InvalidDataException("Invalid framing/checksum");
        int size = captured && i + 1 < reports.Length ? 14 : r[4] - 0x10;
        if (size < 1 || size > 14) throw new InvalidDataException("Invalid length");
        if (!captured && r.Skip(5 + size).Take(14 - size).Any(b => b != 0))
            throw new InvalidDataException("Invalid padding");
        payload.AddRange(r.Skip(5).Take(size));
    }
    return payload.ToArray();
}

static class Native
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_SetOutputReport(SafeFileHandle handle, byte[] report, int length);
}
