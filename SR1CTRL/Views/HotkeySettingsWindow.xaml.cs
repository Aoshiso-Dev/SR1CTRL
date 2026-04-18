using SR1CTRL.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SR1CTRL.Views;

public partial class HotkeySettingsWindow : Window
{
    public HotkeySettingsWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private void HotkeyInputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { Tag: string actionName })
        {
            return;
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.StartHotkeyCaptureCommand.CanExecute(actionName))
        {
            return;
        }

        vm.StartHotkeyCaptureCommand.Execute(actionName);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.CancelHotkeyCaptureCommand.CanExecute(null))
        {
            return;
        }

        vm.CancelHotkeyCaptureCommand.Execute(null);
    }
}
