using SR1CTRL.Presentation.Input;
using SR1CTRL.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SR1CTRL.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly GlobalKeyboardHook _keyboardHook;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();

        DataContext = vm;
        _vm = vm;

        _keyboardHook = new GlobalKeyboardHook();
        _keyboardHook.KeyPressed += OnGlobalKeyPressed;

        Closed += (_, _) => _keyboardHook.Dispose();
    }

    private async void OnGlobalKeyPressed(object? sender, Key key)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            switch (key)
            {
                case Key.F13:
                    await _vm.ToggleStartStopAsync();
                    break;
                case Key.F14:
                    _vm.LSpeedDown();
                    break;
                case Key.F15:
                    _vm.LSpeedUp();
                    break;
                case Key.F16:
                    _vm.RSpeedDown();
                    break;
                case Key.F17:
                    _vm.RSpeedUp();
                    break;
            }
        });
    }
}
