using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Umvc3MusicTool.Views;

public partial class ColorEditor : UserControl
{
    public static readonly StyledProperty<Color> ValueProperty =
        AvaloniaProperty.Register<ColorEditor, Color>(nameof(Value), defaultValue: Colors.White);

    public Color Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private bool _syncing;

    public ColorEditor()
    {
        InitializeComponent();
        HexBox.TextChanged += OnHexChanged;
        RedSlider.PropertyChanged += OnSliderPropertyChanged;
        GreenSlider.PropertyChanged += OnSliderPropertyChanged;
        BlueSlider.PropertyChanged += OnSliderPropertyChanged;
        PropertyChanged += OnEditorPropertyChanged;
        SyncFromValue(Value);
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_syncing && e.Property == ValueProperty)
            SyncFromValue((Color)e.NewValue!);
    }

    private void OnHexChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
            return;

        if (Color.TryParse(HexBox.Text, out var c))
            SetValueFromEditor(c);
    }

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != RangeBase.ValueProperty)
            return;

        var r = (byte)Math.Clamp((int)Math.Round(RedSlider.Value), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round(GreenSlider.Value), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round(BlueSlider.Value), 0, 255);
        SetValueFromEditor(Color.FromArgb(255, r, g, b));
    }

    private void SetValueFromEditor(Color c)
    {
        _syncing = true;
        Value = c;
        _syncing = false;
    }

    private void SyncFromValue(Color c)
    {
        _syncing = true;
        HexBox.Text = FormatHex(c);
        RedSlider.Value = c.R;
        GreenSlider.Value = c.G;
        BlueSlider.Value = c.B;
        RedValue.Text = c.R.ToString();
        GreenValue.Text = c.G.ToString();
        BlueValue.Text = c.B.ToString();
        Swatch.Background = new SolidColorBrush(c);
        _syncing = false;
    }

    private static string FormatHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
