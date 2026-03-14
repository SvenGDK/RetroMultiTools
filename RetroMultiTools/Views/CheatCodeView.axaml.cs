using Avalonia.Controls;
using RetroMultiTools.Utilities;

namespace RetroMultiTools.Views;

public partial class CheatCodeView : UserControl
{
    public CheatCodeView()
    {
        InitializeComponent();
    }

    private CheatCodeConverter.CheatSystem GetSelectedSystem()
    {
        if (SystemCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return Enum.TryParse<CheatCodeConverter.CheatSystem>(tag, out var system)
                ? system
                : CheatCodeConverter.CheatSystem.NesGameGenie;
        }
        return CheatCodeConverter.CheatSystem.NesGameGenie;
    }

    private void DecodeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string code = DecodeInputTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
        {
            ShowError("Please enter a cheat code to decode.");
            return;
        }

        ErrorBorder.IsVisible = false;

        try
        {
            var system = GetSelectedSystem();
            var result = CheatCodeConverter.Decode(code, system);

            DecodeResultText.Text = $"System:  {CheatCodeConverter.GetSystemName(system)}\n" +
                                    $"Code:    {code.ToUpperInvariant()}\n" +
                                    $"{result.Description}";
            DecodeResultBorder.IsVisible = true;
        }
        catch (ArgumentException ex)
        {
            ShowError($"Decode error: {ex.Message}");
            DecodeResultBorder.IsVisible = false;
        }
    }

    private void EncodeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string addressStr = AddressTextBox.Text?.Trim() ?? "";
        string valueStr = ValueTextBox.Text?.Trim() ?? "";
        string compareStr = CompareTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(addressStr) || string.IsNullOrEmpty(valueStr))
        {
            ShowError("Please enter both address and value in hexadecimal.");
            return;
        }

        ErrorBorder.IsVisible = false;

        try
        {
            uint address = Convert.ToUInt32(addressStr, 16);
            ushort value = Convert.ToUInt16(valueStr, 16);
            byte? compareValue = string.IsNullOrEmpty(compareStr) ? null : Convert.ToByte(compareStr, 16);

            var system = GetSelectedSystem();
            string code = CheatCodeConverter.Encode(address, value, system, compareValue);

            EncodeResultText.Text = $"System:  {CheatCodeConverter.GetSystemName(system)}\n" +
                                    $"Address: ${address:X}\n" +
                                    $"Value:   ${value:X2}\n" +
                                    (compareValue.HasValue ? $"Compare: ${compareValue:X2}\n" : "") +
                                    $"Code:    {code}";
            EncodeResultBorder.IsVisible = true;
        }
        catch (FormatException)
        {
            ShowError("Invalid hex values. Please enter valid hexadecimal numbers.");
            EncodeResultBorder.IsVisible = false;
        }
        catch (OverflowException)
        {
            ShowError("Value out of range. Address should be 16/24-bit, Value should be 8-bit (00-FF).");
            EncodeResultBorder.IsVisible = false;
        }
        catch (ArgumentException ex)
        {
            ShowError($"Encode error: {ex.Message}");
            EncodeResultBorder.IsVisible = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.IsVisible = true;
    }
}
