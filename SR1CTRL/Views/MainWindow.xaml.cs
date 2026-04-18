using Microsoft.Extensions.Logging;
using SR1CTRL.Presentation.Input;
using SR1CTRL.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SR1CTRL.Views;

public partial class MainWindow : Window
{
    private static readonly TimeProvider AppTimeProvider = TimeProvider.System;

    private readonly MainViewModel _vm;
    private readonly ILogger<MainWindow> _logger;
    private readonly HidInputDiagnosticAnalyzer _hidAnalyzer = new();
    private static readonly Regex VidPidRegex = new("vid_[0-9a-f]{4}&pid_[0-9a-f]{4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private RawInputKeyboardSource? _keyboardInput;
    private HwndSource? _hwndSource;
    private string? _boundHotkeyDeviceName;
    private string? _boundHotkeyDeviceKey;
    private DeviceInfoWindow? _deviceInfoWindow;
    private HotkeySettingsWindow? _hotkeySettingsWindow;
    private HidDiagnosticWindow? _hidDiagnosticWindow;

    public MainWindow(MainViewModel vm, ILogger<MainWindow> logger)
    {
        InitializeComponent();

        DataContext = vm;
        _vm = vm;
        _logger = logger;
        _boundHotkeyDeviceName = _vm.HotkeyDeviceName;
        _boundHotkeyDeviceKey = CreateDeviceBindingKey(_boundHotkeyDeviceName);

        SourceInitialized += OnSourceInitialized;
        Closed += OnWindowClosed;
    }

    private async void ReadDeviceInfoButton_Click(object sender, RoutedEventArgs e)
    {
        var deviceInfoWindow = _deviceInfoWindow;
        if (deviceInfoWindow is null || !deviceInfoWindow.IsLoaded)
        {
            deviceInfoWindow = new DeviceInfoWindow
            {
                Owner = this,
                DataContext = _vm
            };
            deviceInfoWindow.Closed += (_, _) => _deviceInfoWindow = null;
            _deviceInfoWindow = deviceInfoWindow;
            deviceInfoWindow.Show();
        }
        else
        {
            deviceInfoWindow.Activate();
        }

        if (_vm.QueryDeviceInfoCommand.CanExecute(null))
        {
            await _vm.QueryDeviceInfoCommand.ExecuteAsync(null);
        }
    }

    private void OpenHidDiagnosticButton_Click(object sender, RoutedEventArgs e)
    {
        var hidDiagnosticWindow = _hidDiagnosticWindow;
        if (hidDiagnosticWindow is null || !hidDiagnosticWindow.IsLoaded)
        {
            hidDiagnosticWindow = new HidDiagnosticWindow
            {
                Owner = this,
                DataContext = _vm
            };
            hidDiagnosticWindow.Closed += (_, _) => _hidDiagnosticWindow = null;
            _hidDiagnosticWindow = hidDiagnosticWindow;
            hidDiagnosticWindow.Show();
            return;
        }

        hidDiagnosticWindow.Activate();
    }

    private void OpenHotkeySettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var hotkeySettingsWindow = _hotkeySettingsWindow;
        if (hotkeySettingsWindow is null || !hotkeySettingsWindow.IsLoaded)
        {
            hotkeySettingsWindow = new HotkeySettingsWindow
            {
                Owner = this,
                DataContext = _vm
            };
            hotkeySettingsWindow.Closed += (_, _) => _hotkeySettingsWindow = null;
            _hotkeySettingsWindow = hotkeySettingsWindow;
            hotkeySettingsWindow.Show();
            return;
        }

        hotkeySettingsWindow.Activate();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            _keyboardInput = new RawInputKeyboardSource(handle, _logger);
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Raw Input keyboard initialization failed. Hotkeys are disabled.");
            _vm.ReportNonFatalError("Failed to initialize hardware input. Hotkey features are disabled.");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _keyboardInput?.Dispose();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_keyboardInput?.TryGetInput((uint)msg, lParam, out var inputEvent) is not true)
        {
            return 0;
        }

        return inputEvent.Kind switch
        {
            RawInputKind.Keyboard => HandleKeyboardInput(inputEvent.Keyboard),
            RawInputKind.Hid => HandleHidInput(inputEvent.Hid),
            _ => 0
        };
    }

    private nint HandleKeyboardInput(RawKeyboardInput keyInput)
    {
        var isBoundHardware = IsBoundHardware(keyInput.DeviceName);
        if (_vm.IsHidDiagnosticEnabled && isBoundHardware)
        {
            var now = AppTimeProvider.GetLocalNow();
            _vm.AddHidDiagnosticEntry(
                $"{now:HH:mm:ss.fff} keyboard key={keyInput.Key} vk=0x{keyInput.VirtualKey:X2} device={keyInput.DeviceName}");
        }

        if (!isBoundHardware)
        {
            return 0;
        }

        if (_vm.TryCaptureHotkey(keyInput.Key, out var capturedAction))
        {
            var now = AppTimeProvider.GetLocalNow();
            _vm.AddHidDiagnosticEntry(
                $"{now:HH:mm:ss.fff} assigned {capturedAction} -> {keyInput.Key}");
            return 0;
        }

        if (!_vm.TryGetHotkeyAction(keyInput.Key, out var action))
        {
            return 0;
        }

        _ = Dispatcher.InvokeAsync(() => HandleHotkeyAsync(action));
        return 0;
    }

    private nint HandleHidInput(RawHidInput hidInput)
    {
        if (!_vm.IsHidDiagnosticEnabled || !IsBoundHardware(hidInput.DeviceName))
        {
            return 0;
        }

        var logs = _hidAnalyzer.Analyze(hidInput, AppTimeProvider.GetLocalNow());
        if (logs.Count == 0)
        {
            return 0;
        }

        _ = Dispatcher.InvokeAsync(() =>
        {
            foreach (var log in logs)
            {
                _vm.AddHidDiagnosticEntry(log);
            }
        });

        return 0;
    }

    private bool IsBoundHardware(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        var currentDeviceKey = CreateDeviceBindingKey(deviceName);
        if (string.IsNullOrWhiteSpace(currentDeviceKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_boundHotkeyDeviceName))
        {
            if (!string.IsNullOrWhiteSpace(_boundHotkeyDeviceKey))
            {
                return string.Equals(_boundHotkeyDeviceKey, currentDeviceKey, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(_boundHotkeyDeviceName, deviceName, StringComparison.OrdinalIgnoreCase);
        }

        _boundHotkeyDeviceName = deviceName;
        _boundHotkeyDeviceKey = currentDeviceKey;
        _vm.SetHotkeyDeviceName(deviceName);
        _logger.LogInformation("Bound hotkey source device: {DeviceName} ({DeviceKey})", deviceName, currentDeviceKey);
        return true;
    }

    private static string? CreateDeviceBindingKey(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        var match = VidPidRegex.Match(deviceName);
        if (match.Success)
        {
            return match.Value.ToLowerInvariant();
        }

        return deviceName.Trim().ToLowerInvariant();
    }

    private Task HandleHotkeyAsync(HotkeyAction action)
    {
        if (!_vm.IsConnected)
        {
            return Task.CompletedTask;
        }

        switch (action)
        {
            case HotkeyAction.StartStop:
                return _vm.ToggleStartStopAsync();
            case HotkeyAction.LinearSpeedDown:
                _vm.LSpeedDown();
                return Task.CompletedTask;
            case HotkeyAction.LinearSpeedUp:
                _vm.LSpeedUp();
                return Task.CompletedTask;
            case HotkeyAction.RotateSpeedDown:
                _vm.RSpeedDown();
                return Task.CompletedTask;
            case HotkeyAction.RotateSpeedUp:
                _vm.RSpeedUp();
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }
}


