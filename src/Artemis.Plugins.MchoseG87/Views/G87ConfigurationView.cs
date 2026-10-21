using Artemis.Plugins.MchoseG87.ViewModels;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Avalonia;

namespace Artemis.Plugins.MchoseG87.Views;

public sealed class G87ConfigurationView : ReactiveUserControl<G87ConfigurationViewModel>
{
    public G87ConfigurationView()
    {
        var label = new TextBlock();
        var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 1, IsSnapToTickEnabled = true, Value = 100 };
        var fpsLabel = new TextBlock();
        var fpsSlider = new Slider { Minimum = 1, Maximum = 30, TickFrequency = 1, IsSnapToTickEnabled = true, Value = 9 };
        bool loading = true;
        slider.PropertyChanged += (_, args) =>
        {
            if (args.Property != Slider.ValueProperty) return;
            int percent = (int)Math.Round(slider.Value);
            label.Text = $"键盘亮度 / Brightness: {percent}%";
            if (!loading) ViewModel?.SetBrightness(percent);
        };
        fpsSlider.PropertyChanged += (_, args) =>
        {
            if (args.Property != Slider.ValueProperty) return;
            int fps = (int)Math.Round(fpsSlider.Value);
            fpsLabel.Text = $"更新速率 / FPS: {fps}";
            if (!loading) ViewModel?.SetFramesPerSecond(fps);
        };
        DataContextChanged += (_, _) =>
        {
            loading = true;
            slider.Value = Math.Clamp(ViewModel?.Brightness.Value ?? 100, 0, 100);
            label.Text = $"键盘亮度 / Brightness: {(int)slider.Value}%";
            fpsSlider.Value = Math.Clamp(ViewModel?.FramesPerSecond.Value ?? 9, 1, 30);
            fpsLabel.Text = $"更新速率 / FPS: {(int)fpsSlider.Value}";
            loading = false;
        };
        Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16, MinWidth = 480,
            Children =
            {
                new TextBlock { Text = "MCHOSE G87 无线灯光", FontSize = 20 }, label, slider,
                new TextBlock { Text = "0–100% 连续亮度，通过 RGB 缩放实现。设置自动保存，0% 为熄灭。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                fpsLabel, fpsSlider,
                new TextBlock { Text = "1–30 FPS，默认 9 FPS，设置自动保存。显示不全时可降低；复杂画面的实际帧率可能低于设定值。", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new TextBlock { Text = "请退出 MCHOSE HUB。亮度在下一次灯光更新时生效。", TextWrapping = Avalonia.Media.TextWrapping.Wrap }
            }
        };
    }
}
