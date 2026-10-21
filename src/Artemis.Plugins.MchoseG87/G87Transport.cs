using System.ComponentModel;
using System.Runtime.InteropServices;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace Artemis.Plugins.MchoseG87;

public sealed class G87Transport : IDisposable
{
    private readonly SafeFileHandle _handle;
    public G87Transport(HidDevice device)
    {
        _handle = CreateFile(device.DevicePath, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (_handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            _handle.Dispose();
            throw new Win32Exception(error);
        }
    }

    public static bool Matches(HidDevice hid) => hid.VendorID == 0x41e4 && hid.ProductID == 0x2001 &&
        hid.DevicePath.Contains("mi_01&col01", StringComparison.OrdinalIgnoreCase) &&
        hid.GetMaxOutputReportLength() == 20 && hid.GetReportDescriptor().DeviceItems
            .Any(item => item.Usages.GetAllValues().Contains(0xff020002u));

    public void Send(IEnumerable<byte[]> reports)
    {
        foreach (var report in reports)
        {
            if (report.Length != 20 || report[0] != 0x13 || report[1] != 0x88)
                throw new ArgumentException("Unexpected G87 report");
            if (!HidD_SetOutputReport(_handle, report, report.Length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Thread.Sleep(4);
        }
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetOutputReport(SafeFileHandle handle, byte[] report, int length);
}
