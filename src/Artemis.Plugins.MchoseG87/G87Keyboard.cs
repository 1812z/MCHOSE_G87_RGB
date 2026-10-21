using RGB.NET.Core;

namespace Artemis.Plugins.MchoseG87;

internal sealed class G87Keyboard : AbstractRGBDevice<G87Info>
{
    public G87Keyboard(IUpdateQueue queue) : base(new G87Info(), queue)
    {
        // G87 mapping physically calibrated by the user, 2026-10-21.
        // Hardware indices are column-major (six slots per matrix column).
        Row(0, ["Escape","F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12","PrintScreen","ScrollLock","PauseBreak"],
            [0,12,18,24,30,36,42,48,54,60,66,72,78,84,90,96], [0,2,3,4,5,6.5,7.5,8.5,9.5,11,12,13,14,15.5,16.5,17.5]);
        Row(1.5, ["GraveAccentAndTilde","1","2","3","4","5","6","7","8","9","0","MinusAndUnderscore","EqualsAndPlus","Backspace","Insert","Home","PageUp"],
            [1,7,13,19,25,31,37,43,49,55,61,67,73,79,85,91,97], [0,1,2,3,4,5,6,7,8,9,10,11,12,13.5,15.5,16.5,17.5]);
        Row(2.5, ["Tab","Q","W","E","R","T","Y","U","I","O","P","BracketLeft","BracketRight","Backslash","Delete","End","PageDown"],
            [2,8,14,20,26,32,38,44,50,56,62,68,74,80,86,92,98], [0,1.5,2.5,3.5,4.5,5.5,6.5,7.5,8.5,9.5,10.5,11.5,12.5,13.5,15.5,16.5,17.5]);
        Row(3.5, ["CapsLock","A","S","D","F","G","H","J","K","L","SemicolonAndColon","ApostropheAndDoubleQuote","Enter"],
            [3,9,15,21,27,33,39,45,51,57,63,69,81], [0,1.75,2.75,3.75,4.75,5.75,6.75,7.75,8.75,9.75,10.75,11.75,13.25]);
        Row(4.5, ["LeftShift","Z","X","C","V","B","N","M","CommaAndLessThan","PeriodAndBiggerThan","SlashAndQuestionMark","RightShift","ArrowUp"],
            [4,10,16,22,28,34,40,46,52,58,64,82,94], [0,2.25,3.25,4.25,5.25,6.25,7.25,8.25,9.25,10.25,11.25,13.25,16.5]);
        Row(5.5, ["LeftCtrl","LeftGui","LeftAlt","Space","RightAlt","Function","Application","RightCtrl","ArrowLeft","ArrowDown","ArrowRight"],
            [5,11,17,35,53,59,65,83,89,95,101], [0,1.25,2.5,6.25,10,11.25,12.5,13.75,15.5,16.5,17.5]);
    }

    private void Row(double y, string[] names, int[] slots, double[] xs)
    {
        for (int i = 0; i < names.Length; i++)
            AddLed(Enum.Parse<LedId>("Keyboard_" + names[i]), new Point((float)(xs[i] * 19.05), (float)(y * 19.05)), new Size(18, 18), slots[i]);
    }
}

internal sealed class G87Info : IRGBDeviceInfo
{
    public RGBDeviceType DeviceType => RGBDeviceType.Keyboard;
    public string Manufacturer => "MCHOSE";
    public string Model => "G87 Wireless";
    public string DeviceName => $"{Manufacturer} {Model}";
    public object? LayoutMetadata { get; set; }
}
