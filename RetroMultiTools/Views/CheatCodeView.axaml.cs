using Avalonia.Controls;
using RetroMultiTools.Localization;
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
            ShowError(LocalizationManager.Instance["Cheat_EnterCode"]);
            return;
        }

        ErrorBorder.IsVisible = false;

        try
        {
            var system = GetSelectedSystem();
            var result = CheatCodeConverter.Decode(code, system);

            DecodeResultText.Text = string.Format(LocalizationManager.Instance["CheatCode_SystemLabel"], CheatCodeConverter.GetSystemName(system)) + "\n" +
                                    string.Format(LocalizationManager.Instance["CheatCode_CodeLabel"], code.ToUpperInvariant()) + "\n" +
                                    result.Description;
            DecodeResultBorder.IsVisible = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowError(string.Format(LocalizationManager.Instance["Cheat_DecodeError"], ex.Message));
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
            ShowError(LocalizationManager.Instance["Cheat_EnterAddressValue"]);
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

            EncodeResultText.Text = string.Format(LocalizationManager.Instance["CheatCode_SystemLabel"], CheatCodeConverter.GetSystemName(system)) + "\n" +
                                    string.Format(LocalizationManager.Instance["CheatCode_AddressLabel"], address.ToString("X")) + "\n" +
                                    string.Format(LocalizationManager.Instance["CheatCode_ValueLabel"], value.ToString("X2")) + "\n" +
                                    (compareValue.HasValue ? string.Format(LocalizationManager.Instance["CheatCode_CompareLabel"], compareValue.Value.ToString("X2")) + "\n" : "") +
                                    string.Format(LocalizationManager.Instance["CheatCode_CodeLabel"], code);
            EncodeResultBorder.IsVisible = true;
        }
        catch (FormatException)
        {
            ShowError(LocalizationManager.Instance["Cheat_InvalidHex"]);
            EncodeResultBorder.IsVisible = false;
        }
        catch (OverflowException)
        {
            ShowError(LocalizationManager.Instance["Cheat_ValueOutOfRange"]);
            EncodeResultBorder.IsVisible = false;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowError(string.Format(LocalizationManager.Instance["Cheat_EncodeError"], ex.Message));
            EncodeResultBorder.IsVisible = false;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBorder.IsVisible = true;
    }
}
