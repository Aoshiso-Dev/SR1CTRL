using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.Models;
using SR1CTRL.Application.UseCases;
using SR1CTRL.Domain;
using SR1CTRL.Presentation.Config;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SR1CTRL.ViewModels;

public sealed record MotionProfileOption(MotionProfileKind Kind, string Label);

public partial class MainViewModel : ObservableObject
{
    private const double AxisGapMin = 0.0001;
    private const double AxisUpperBound = 0.9999;
    private static readonly IReadOnlyList<Key> HotkeyDefaults =
    [
        Key.F13, Key.F14, Key.F15, Key.F16, Key.F17
    ];

    private readonly IComPortProvider _ports;
    private readonly DeviceControlUseCase _deviceControl;
    private readonly IAppStateStore _appStateStore;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _operationCtsGate = new();
    private CancellationTokenSource? _activeOperationCts;
    private bool _isApplyingMaxSpeed;
    private double? _maxSpeedRestoreLinear;
    private double? _maxSpeedRestoreRotate;

    public MainViewModel(
        IComPortProvider ports,
        DeviceControlUseCase deviceControl,
        IAppStateStore appStateStore,
        ILogger<MainViewModel> logger)
    {
        _ports = ports;
        _deviceControl = deviceControl;
        _appStateStore = appStateStore;
        _logger = logger;
        HidDiagnosticEntries = new ReadOnlyObservableCollection<string>(_hidDiagnosticEntries);

        RefreshPorts();
        ApplyLoadedState(_appStateStore.Load());

        SyncState(_deviceControl.GetState());
    }

    public IReadOnlyList<string> PortNames => _portNames;
    private IReadOnlyList<string> _portNames = Array.Empty<string>();

    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private int baudRate;

    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isMaxSpeedEnabled;

    [ObservableProperty] private string d0Text = string.Empty;
    [ObservableProperty] private string d1Text = string.Empty;
    [ObservableProperty] private string d2Text = string.Empty;
    [ObservableProperty] private string statusText = "Ready";
    [ObservableProperty] private string errorText = string.Empty;
    [ObservableProperty] private bool isHidDiagnosticEnabled;
    private string? _hotkeyDeviceName;
    private readonly ObservableCollection<string> _hidDiagnosticEntries = [];
    private readonly IReadOnlyList<Key> _availableHotkeys =
    [
        Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
        Key.F13, Key.F14, Key.F15, Key.F16, Key.F17, Key.F18, Key.F19, Key.F20, Key.F21, Key.F22, Key.F23, Key.F24
    ];

    [ObservableProperty] private Key startStopHotkey = HotkeyDefaults[0];
    [ObservableProperty] private Key linearSpeedDownHotkey = HotkeyDefaults[1];
    [ObservableProperty] private Key linearSpeedUpHotkey = HotkeyDefaults[2];
    [ObservableProperty] private Key rotateSpeedDownHotkey = HotkeyDefaults[3];
    [ObservableProperty] private Key rotateSpeedUpHotkey = HotkeyDefaults[4];
    [ObservableProperty] private HotkeyAction? listeningHotkeyAction;

    [ObservableProperty] private double l_Min;
    [ObservableProperty] private double l_Max;
    [ObservableProperty] private double l_Speed;

    [ObservableProperty] private double r_Min;
    [ObservableProperty] private double r_Max;
    [ObservableProperty] private double r_Speed;
    [ObservableProperty] private MotionProfileKind motionProfile = MotionProfileKind.IndependentLoop;
    [ObservableProperty] private double motionIntensity = 0.65;

    public double LinearSpeedMin => MotionDefaults.LinearSpeedMin;
    public double LinearSpeedMax => MotionDefaults.LinearSpeedMax;
    public double RotateSpeedMin => MotionDefaults.RotateSpeedMin;
    public double RotateSpeedMax => MotionDefaults.RotateSpeedMax;
    public IReadOnlyList<MotionProfileOption> MotionProfiles { get; } =
    [
        new(MotionProfileKind.IndependentLoop, "Independent Loop"),
        new(MotionProfileKind.SmoothStroke, "Smooth Stroke"),
        new(MotionProfileKind.TwistStroke, "Twist Stroke"),
        new(MotionProfileKind.AccentTwist, "Accent Twist")
    ];
    public IReadOnlyList<Key> AvailableHotkeys => _availableHotkeys;
    public string StartStopHotkeyText => StartStopHotkey.ToString();
    public string LinearSpeedDownHotkeyText => LinearSpeedDownHotkey.ToString();
    public string LinearSpeedUpHotkeyText => LinearSpeedUpHotkey.ToString();
    public string RotateSpeedDownHotkeyText => RotateSpeedDownHotkey.ToString();
    public string RotateSpeedUpHotkeyText => RotateSpeedUpHotkey.ToString();
    public ReadOnlyObservableCollection<string> HidDiagnosticEntries { get; }
    public string HidDiagnosticButtonText => IsHidDiagnosticEnabled ? "Stop HID Diagnostics" : "Start HID Diagnostics";
    public string HidDiagnosticSummary => string.IsNullOrWhiteSpace(_hotkeyDeviceName)
        ? "No bound target device. Start diagnostics and operate a macro keyboard key/knob."
        : $"Target device: {_hotkeyDeviceName}";
    public bool IsHotkeyCaptureActive => ListeningHotkeyAction.HasValue;
    public string HotkeyCaptureStatus => ListeningHotkeyAction is null
        ? "Input capture is idle."
        : $"Waiting for input: {GetHotkeyActionLabel(ListeningHotkeyAction.Value)}";
    public string MaxSpeedButtonText => IsMaxSpeedEnabled ? "Max Speed On" : "Max Speed";

    partial void OnL_MinChanged(double value) => ApplyLinear();
    partial void OnL_MaxChanged(double value) => ApplyLinear();
    partial void OnL_SpeedChanged(double value)
    {
        ApplyLinear();
        SyncMaxSpeedStateFromManualChange();
    }

    partial void OnR_MinChanged(double value) => ApplyRotate();
    partial void OnR_MaxChanged(double value) => ApplyRotate();
    partial void OnR_SpeedChanged(double value)
    {
        ApplyRotate();
        SyncMaxSpeedStateFromManualChange();
    }
    partial void OnMotionProfileChanged(MotionProfileKind value) => ApplyMotionProfile();
    partial void OnMotionIntensityChanged(double value) => ApplyMotionProfile();
    partial void OnIsMaxSpeedEnabledChanged(bool value) => OnPropertyChanged(nameof(MaxSpeedButtonText));
    partial void OnStartStopHotkeyChanged(Key value) => OnPropertyChanged(nameof(StartStopHotkeyText));
    partial void OnLinearSpeedDownHotkeyChanged(Key value) => OnPropertyChanged(nameof(LinearSpeedDownHotkeyText));
    partial void OnLinearSpeedUpHotkeyChanged(Key value) => OnPropertyChanged(nameof(LinearSpeedUpHotkeyText));
    partial void OnRotateSpeedDownHotkeyChanged(Key value) => OnPropertyChanged(nameof(RotateSpeedDownHotkeyText));
    partial void OnRotateSpeedUpHotkeyChanged(Key value) => OnPropertyChanged(nameof(RotateSpeedUpHotkeyText));
    partial void OnListeningHotkeyActionChanged(HotkeyAction? value)
    {
        OnPropertyChanged(nameof(IsHotkeyCaptureActive));
        OnPropertyChanged(nameof(HotkeyCaptureStatus));
    }

    partial void OnIsHidDiagnosticEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HidDiagnosticButtonText));
        SetStatus(value ? "HID diagnostics started." : "HID diagnostics stopped.");
    }

    [RelayCommand]
    private void RefreshPortList() => RefreshPorts();

    [RelayCommand]
    private void ToggleHidDiagnostic() => IsHidDiagnosticEnabled = !IsHidDiagnosticEnabled;

    [RelayCommand]
    private void ClearHidDiagnostics() => _hidDiagnosticEntries.Clear();

    [RelayCommand]
    private void StartHotkeyCapture(string actionName)
    {
        if (!Enum.TryParse<HotkeyAction>(actionName, true, out var action))
        {
            return;
        }

        ListeningHotkeyAction = action;
        SetStatus($"Input capture started: {GetHotkeyActionLabel(action)}");
    }

    [RelayCommand]
    private void CancelHotkeyCapture()
    {
        ListeningHotkeyAction = null;
        SetStatus("Input capture canceled.");
    }

    [RelayCommand]
    private async Task Connect()
    {
        await RunExclusiveAsync(async cancellationToken =>
        {
            if (IsConnected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                throw new InvalidOperationException("Select a COM port.");
            }

            if (BaudRate <= 0)
            {
                throw new InvalidOperationException("Enter a valid baud rate.");
            }

            SetStatus($"Connecting to {SelectedPort}...");
            var state = await _deviceControl.ConnectAsync(SelectedPort, BaudRate, cancellationToken);
            SyncState(state);

            SetStatus("Connected. Reading device info...");
            await QueryDeviceInfoCore(cancellationToken);

            ApplyAll();
            SetStatus("Connected");
        });
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await RunExclusiveAsync(async cancellationToken =>
        {
            if (!IsConnected)
            {
                return;
            }

            SetStatus("Disconnecting...");
            var state = await _deviceControl.DisconnectAsync(cancellationToken);
            SyncState(state);
            SetStatus("Disconnected");
        });
    }

    [RelayCommand]
    private async Task QueryDeviceInfo()
    {
        await RunExclusiveAsync(QueryDeviceInfoCore);
    }

    [RelayCommand]
    private async Task Start()
    {
        await RunExclusiveAsync(async cancellationToken =>
        {
            if (!IsConnected || IsRunning)
            {
                return;
            }

            SetStatus("Starting...");
            var state = await _deviceControl.StartAsync(cancellationToken);
            SyncState(state);
            SetStatus("Running");
        });
    }

    [RelayCommand]
    private async Task Stop()
    {
        CancelActiveOperation();

        await RunExclusiveAsync(async cancellationToken =>
        {
            if (!IsRunning)
            {
                return;
            }

            SetStatus("Stopping...");
            var state = await _deviceControl.StopAsync(cancellationToken);
            SyncState(state);
            SetStatus(IsConnected ? "Connected" : "Disconnected");
        }, waitForTurn: true);
    }

    public async Task ToggleStartStopAsync()
    {
        if (IsRunning)
        {
            await Stop();
            return;
        }

        await Start();
    }

    public void LSpeedUp(double step = MotionDefaults.LinearSpeedStep)
    {
        L_Speed = Math.Min(MotionDefaults.LinearSpeedMax, L_Speed + step);
    }

    public void LSpeedDown(double step = MotionDefaults.LinearSpeedStep)
    {
        L_Speed = Math.Max(MotionDefaults.LinearSpeedMin, L_Speed - step);
    }

    public void RSpeedUp(double step = MotionDefaults.RotateSpeedStep)
    {
        R_Speed = Math.Min(MotionDefaults.RotateSpeedMax, R_Speed + step);
    }

    public void RSpeedDown(double step = MotionDefaults.RotateSpeedStep)
    {
        R_Speed = Math.Max(MotionDefaults.RotateSpeedMin, R_Speed - step);
    }

    [RelayCommand]
    private void MaxSpeed()
    {
        if (IsMaxSpeedEnabled)
        {
            DisableMaxSpeed(restorePreviousSpeeds: true);
            return;
        }

        EnableMaxSpeed();
    }

    public void EnableMaxSpeed()
    {
        if (IsMaxSpeedEnabled)
        {
            SetStatus("Max speed is already on");
            return;
        }

        _maxSpeedRestoreLinear = L_Speed;
        _maxSpeedRestoreRotate = R_Speed;
        _isApplyingMaxSpeed = true;
        try
        {
            IsMaxSpeedEnabled = true;
            L_Speed = MotionDefaults.LinearSpeedMax;
            R_Speed = MotionDefaults.RotateSpeedMax;
        }
        finally
        {
            _isApplyingMaxSpeed = false;
        }

        SetStatus("Max speed on");
    }

    private void DisableMaxSpeed(bool restorePreviousSpeeds)
    {
        var restoreLinear = _maxSpeedRestoreLinear;
        var restoreRotate = _maxSpeedRestoreRotate;

        _isApplyingMaxSpeed = true;
        try
        {
            IsMaxSpeedEnabled = false;
            if (restorePreviousSpeeds)
            {
                if (restoreLinear.HasValue)
                {
                    L_Speed = Clamp(restoreLinear.Value, MotionDefaults.LinearSpeedMin, MotionDefaults.LinearSpeedMax);
                }

                if (restoreRotate.HasValue)
                {
                    R_Speed = Clamp(restoreRotate.Value, MotionDefaults.RotateSpeedMin, MotionDefaults.RotateSpeedMax);
                }
            }
        }
        finally
        {
            _isApplyingMaxSpeed = false;
        }

        _maxSpeedRestoreLinear = null;
        _maxSpeedRestoreRotate = null;
        SetStatus("Max speed off");
    }

    private void SyncMaxSpeedStateFromManualChange()
    {
        if (_isApplyingMaxSpeed || !IsMaxSpeedEnabled)
        {
            return;
        }

        if (L_Speed == MotionDefaults.LinearSpeedMax && R_Speed == MotionDefaults.RotateSpeedMax)
        {
            return;
        }

        _maxSpeedRestoreLinear = null;
        _maxSpeedRestoreRotate = null;
        _isApplyingMaxSpeed = true;
        try
        {
            IsMaxSpeedEnabled = false;
        }
        finally
        {
            _isApplyingMaxSpeed = false;
        }
    }

    public void ReportNonFatalError(string message)
    {
        SetError(message);
    }

    public string? HotkeyDeviceName => _hotkeyDeviceName;

    public void SetHotkeyDeviceName(string? deviceName)
    {
        _hotkeyDeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? null
            : deviceName;
        OnPropertyChanged(nameof(HidDiagnosticSummary));
    }

    public void AddHidDiagnosticEntry(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _hidDiagnosticEntries.Add(message);
        while (_hidDiagnosticEntries.Count > 200)
        {
            _hidDiagnosticEntries.RemoveAt(0);
        }
    }

    public void SaveCurrentState()
    {
        try
        {
            _appStateStore.Save(new AppStateSnapshot
            {
                HotkeyDeviceName = _hotkeyDeviceName,
                HotkeyStartStopKey = StartStopHotkey.ToString(),
                HotkeyLinearSpeedDownKey = LinearSpeedDownHotkey.ToString(),
                HotkeyLinearSpeedUpKey = LinearSpeedUpHotkey.ToString(),
                HotkeyRotateSpeedDownKey = RotateSpeedDownHotkey.ToString(),
                HotkeyRotateSpeedUpKey = RotateSpeedUpHotkey.ToString(),
                SelectedPort = SelectedPort,
                BaudRate = BaudRate,
                L_Min = L_Min,
                L_Max = L_Max,
                L_Speed = L_Speed,
                R_Min = R_Min,
                R_Max = R_Max,
                R_Speed = R_Speed,
                MotionProfile = MotionProfile.ToString(),
                MotionIntensity = MotionIntensity
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save current state.");
        }
    }

    private void RefreshPorts()
    {
        try
        {
            _portNames = _ports.GetPortNames();
            OnPropertyChanged(nameof(PortNames));

            if (SelectedPort is null && _portNames.Count > 0)
            {
                SelectedPort = _portNames[0];
            }

            SetStatus(_portNames.Count == 0 ? "No COM ports found" : $"Ports refreshed ({_portNames.Count})");
        }
        catch (Exception ex)
        {
            SetError($"Failed to load COM ports: {ex.Message}");
        }
    }

    private void ApplyAll()
    {
        ApplyLinear();
        ApplyRotate();
        ApplyMotionProfile();
    }

    private void ApplyLinear()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            var normalized = NormalizeAxisRange(L_Min, L_Max);
            _deviceControl.ApplyLinear(new LinearMotionRequest(
                Channel: MotionDefaults.DefaultChannel,
                Min: normalized.Min,
                Max: normalized.Max,
                SpeedPerSecond: Clamp(L_Speed, MotionDefaults.LinearSpeedMin, MotionDefaults.LinearSpeedMax)));
        }
        catch (Exception ex)
        {
            SetError($"Failed to apply Linear settings: {ex.Message}");
        }
    }

    private void ApplyRotate()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            var normalized = NormalizeAxisRange(R_Min, R_Max);
            _deviceControl.ApplyRotate(new RotateMotionRequest(
                Channel: MotionDefaults.DefaultChannel,
                Min: normalized.Min,
                Max: normalized.Max,
                SpeedPerSecond: Clamp(R_Speed, MotionDefaults.RotateSpeedMin, MotionDefaults.RotateSpeedMax)));
        }
        catch (Exception ex)
        {
            SetError($"Failed to apply Rotate settings: {ex.Message}");
        }
    }

    private void ApplyMotionProfile()
    {
        if (!IsConnected)
        {
            return;
        }

        try
        {
            _deviceControl.ApplyMotionProfile(new MotionProfileSettings(
                MotionProfile,
                Clamp(MotionIntensity, 0.0, 1.0)));
        }
        catch (Exception ex)
        {
            SetError($"Failed to apply motion profile: {ex.Message}");
        }
    }

    private void SyncState(DeviceRuntimeState state)
    {
        IsConnected = state.IsConnected;
        IsRunning = state.IsRunning;
    }

    private async Task QueryDeviceInfoCore(CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        var info = await _deviceControl.QueryDeviceInfoAsync(cts.Token);

        D0Text = info.D0;
        D1Text = info.D1;
        D2Text = info.D2;
        SetStatus("Device info updated");
    }

    private async Task RunExclusiveAsync(Func<CancellationToken, Task> action, bool waitForTurn = false)
    {
        if (waitForTurn)
        {
            await _operationLock.WaitAsync();
        }
        else if (!await _operationLock.WaitAsync(0))
        {
            SetStatus("Another operation is running...");
            return;
        }

        using var cts = new CancellationTokenSource();
        SetActiveOperationCancellation(cts);

        try
        {
            IsBusy = true;
            await action(cts.Token);
            ErrorText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            SetError("Operation was canceled.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            ClearActiveOperationCancellation(cts);
            IsBusy = false;
            _operationLock.Release();
        }
    }

    private void CancelActiveOperation()
    {
        CancellationTokenSource? cts;
        lock (_operationCtsGate)
        {
            cts = _activeOperationCts;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SetActiveOperationCancellation(CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);

        lock (_operationCtsGate)
        {
            _activeOperationCts = cts;
        }
    }

    private void ClearActiveOperationCancellation(CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);

        lock (_operationCtsGate)
        {
            if (ReferenceEquals(_activeOperationCts, cts))
            {
                _activeOperationCts = null;
            }
        }
    }

    private void SetStatus(string text)
    {
        StatusText = text;
    }

    private void SetError(string text)
    {
        ErrorText = text;
        StatusText = "Error";
    }

    private static (double Min, double Max) NormalizeAxisRange(double a, double b)
    {
        var min = Clamp(Math.Min(a, b), 0.0, AxisUpperBound);
        var max = Clamp(Math.Max(a, b), 0.0, AxisUpperBound);

        if (max - min < AxisGapMin)
        {
            max = Math.Min(AxisUpperBound, min + AxisGapMin);
            if (max - min < AxisGapMin)
            {
                min = Math.Max(0.0, max - AxisGapMin);
            }
        }

        return (min, max);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void ApplyLoadedState(AppStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _hotkeyDeviceName = string.IsNullOrWhiteSpace(state.HotkeyDeviceName)
            ? null
            : state.HotkeyDeviceName;
        StartStopHotkey = ParseHotkeyOrDefault(state.HotkeyStartStopKey, HotkeyDefaults[0]);
        LinearSpeedDownHotkey = ParseHotkeyOrDefault(state.HotkeyLinearSpeedDownKey, HotkeyDefaults[1]);
        LinearSpeedUpHotkey = ParseHotkeyOrDefault(state.HotkeyLinearSpeedUpKey, HotkeyDefaults[2]);
        RotateSpeedDownHotkey = ParseHotkeyOrDefault(state.HotkeyRotateSpeedDownKey, HotkeyDefaults[3]);
        RotateSpeedUpHotkey = ParseHotkeyOrDefault(state.HotkeyRotateSpeedUpKey, HotkeyDefaults[4]);

        BaudRate = state.BaudRate.GetValueOrDefault(MotionDefaults.DefaultBaudRate);

        L_Min = Clamp(state.L_Min ?? MotionDefaults.LinearMinDefault, 0.0, AxisUpperBound);
        L_Max = Clamp(state.L_Max ?? MotionDefaults.LinearMaxDefault, 0.0, AxisUpperBound);
        L_Speed = Clamp(state.L_Speed ?? MotionDefaults.LinearSpeedDefault, MotionDefaults.LinearSpeedMin, MotionDefaults.LinearSpeedMax);

        R_Min = Clamp(state.R_Min ?? MotionDefaults.RotateMinDefault, 0.0, AxisUpperBound);
        R_Max = Clamp(state.R_Max ?? MotionDefaults.RotateMaxDefault, 0.0, AxisUpperBound);
        R_Speed = Clamp(state.R_Speed ?? MotionDefaults.RotateSpeedDefault, MotionDefaults.RotateSpeedMin, MotionDefaults.RotateSpeedMax);
        MotionProfile = ParseMotionProfileOrDefault(state.MotionProfile, MotionProfileKind.IndependentLoop);
        MotionIntensity = Clamp(state.MotionIntensity ?? 0.65, 0.0, 1.0);

        if (!string.IsNullOrWhiteSpace(state.SelectedPort))
        {
            SelectedPort = _portNames.Contains(state.SelectedPort, StringComparer.OrdinalIgnoreCase)
                ? state.SelectedPort
                : _portNames.FirstOrDefault();
            return;
        }

        SelectedPort = _portNames.FirstOrDefault();
    }

    public bool TryGetHotkeyAction(Key key, out HotkeyAction action)
    {
        if (key == StartStopHotkey)
        {
            action = HotkeyAction.StartStop;
            return true;
        }

        if (key == LinearSpeedDownHotkey)
        {
            action = HotkeyAction.LinearSpeedDown;
            return true;
        }

        if (key == LinearSpeedUpHotkey)
        {
            action = HotkeyAction.LinearSpeedUp;
            return true;
        }

        if (key == RotateSpeedDownHotkey)
        {
            action = HotkeyAction.RotateSpeedDown;
            return true;
        }

        if (key == RotateSpeedUpHotkey)
        {
            action = HotkeyAction.RotateSpeedUp;
            return true;
        }

        action = default;
        return false;
    }

    public bool TryCaptureHotkey(Key key, out HotkeyAction action)
    {
        action = default;
        if (ListeningHotkeyAction is not { } targetAction)
        {
            return false;
        }

        AssignHotkey(targetAction, key);
        ListeningHotkeyAction = null;
        action = targetAction;
        SetStatus($"Hotkey updated: {GetHotkeyActionLabel(targetAction)} = {key}");
        return true;
    }

    private static Key ParseHotkeyOrDefault(string? value, Key fallback)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<Key>(value, true, out var parsed)
            && parsed != Key.None)
        {
            return parsed;
        }

        return fallback;
    }

    private static MotionProfileKind ParseMotionProfileOrDefault(string? value, MotionProfileKind fallback)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<MotionProfileKind>(value, true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private void AssignHotkey(HotkeyAction action, Key key)
    {
        switch (action)
        {
            case HotkeyAction.StartStop:
                StartStopHotkey = key;
                return;
            case HotkeyAction.LinearSpeedDown:
                LinearSpeedDownHotkey = key;
                return;
            case HotkeyAction.LinearSpeedUp:
                LinearSpeedUpHotkey = key;
                return;
            case HotkeyAction.RotateSpeedDown:
                RotateSpeedDownHotkey = key;
                return;
            case HotkeyAction.RotateSpeedUp:
                RotateSpeedUpHotkey = key;
                return;
        }
    }

    private static string GetHotkeyActionLabel(HotkeyAction action)
    {
        return action switch
        {
            HotkeyAction.StartStop => "Start / Stop",
            HotkeyAction.LinearSpeedDown => "Linear Speed Down",
            HotkeyAction.LinearSpeedUp => "Linear Speed Up",
            HotkeyAction.RotateSpeedDown => "Rotate Speed Down",
            HotkeyAction.RotateSpeedUp => "Rotate Speed Up",
            _ => action.ToString()
        };
    }
}


