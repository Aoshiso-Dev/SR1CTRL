using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.Models;
using SR1CTRL.Application.UseCases;
using SR1CTRL.Presentation.Config;

namespace SR1CTRL.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const double AxisGapMin = 0.0001;
    private const double AxisUpperBound = 0.9999;

    private readonly IComPortProvider _ports;
    private readonly DeviceControlUseCase _deviceControl;
    private readonly IAppStateStore _appStateStore;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

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

    [ObservableProperty] private string d0Text = string.Empty;
    [ObservableProperty] private string d1Text = string.Empty;
    [ObservableProperty] private string d2Text = string.Empty;
    [ObservableProperty] private string statusText = "Ready";
    [ObservableProperty] private string errorText = string.Empty;

    [ObservableProperty] private double l_Min;
    [ObservableProperty] private double l_Max;
    [ObservableProperty] private double l_Speed;

    [ObservableProperty] private double r_Min;
    [ObservableProperty] private double r_Max;
    [ObservableProperty] private double r_Speed;

    public double LinearSpeedMin => MotionDefaults.LinearSpeedMin;
    public double LinearSpeedMax => MotionDefaults.LinearSpeedMax;
    public double RotateSpeedMin => MotionDefaults.RotateSpeedMin;
    public double RotateSpeedMax => MotionDefaults.RotateSpeedMax;

    partial void OnL_MinChanged(double value) => ApplyLinear();
    partial void OnL_MaxChanged(double value) => ApplyLinear();
    partial void OnL_SpeedChanged(double value) => ApplyLinear();

    partial void OnR_MinChanged(double value) => ApplyRotate();
    partial void OnR_MaxChanged(double value) => ApplyRotate();
    partial void OnR_SpeedChanged(double value) => ApplyRotate();

    [RelayCommand]
    private void RefreshPortList() => RefreshPorts();

    [RelayCommand]
    private async Task Connect()
    {
        await RunExclusiveAsync(async () =>
        {
            if (IsConnected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                throw new InvalidOperationException("COMポートを選択してください。");
            }

            if (BaudRate <= 0)
            {
                throw new InvalidOperationException("有効なボーレートを入力してください。");
            }

            SetStatus($"Connecting to {SelectedPort}...");
            var state = await _deviceControl.ConnectAsync(SelectedPort, BaudRate, CancellationToken.None);
            SyncState(state);

            SetStatus("Connected. Reading device info...");
            await QueryDeviceInfoCore();

            ApplyAll();
            SetStatus("Connected");
        });
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await RunExclusiveAsync(async () =>
        {
            if (!IsConnected)
            {
                return;
            }

            SetStatus("Disconnecting...");
            var state = await _deviceControl.DisconnectAsync(CancellationToken.None);
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
        await RunExclusiveAsync(async () =>
        {
            if (!IsConnected || IsRunning)
            {
                return;
            }

            SetStatus("Starting...");
            var state = await _deviceControl.StartAsync(CancellationToken.None);
            SyncState(state);
            SetStatus("Running");
        });
    }

    [RelayCommand]
    private async Task Stop()
    {
        await RunExclusiveAsync(async () =>
        {
            if (!IsRunning)
            {
                return;
            }

            SetStatus("Stopping...");
            var state = await _deviceControl.StopAsync(CancellationToken.None);
            SyncState(state);
            SetStatus(IsConnected ? "Connected" : "Disconnected");
        });
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

    public void ReportNonFatalError(string message)
    {
        SetError(message);
    }

    public void SaveCurrentState()
    {
        try
        {
            _appStateStore.Save(new AppStateSnapshot
            {
                SelectedPort = SelectedPort,
                BaudRate = BaudRate,
                L_Min = L_Min,
                L_Max = L_Max,
                L_Speed = L_Speed,
                R_Min = R_Min,
                R_Max = R_Max,
                R_Speed = R_Speed
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
            SetError($"COMポート一覧の取得に失敗しました: {ex.Message}");
        }
    }

    private void ApplyAll()
    {
        ApplyLinear();
        ApplyRotate();
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
            SetError($"Linear設定の適用に失敗しました: {ex.Message}");
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
            SetError($"Rotate設定の適用に失敗しました: {ex.Message}");
        }
    }

    private void SyncState(DeviceRuntimeState state)
    {
        IsConnected = state.IsConnected;
        IsRunning = state.IsRunning;
    }

    private async Task QueryDeviceInfoCore()
    {
        if (!IsConnected)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var info = await _deviceControl.QueryDeviceInfoAsync(cts.Token);

        D0Text = info.D0;
        D1Text = info.D1;
        D2Text = info.D2;
        SetStatus("Device info updated");
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        if (!await _operationLock.WaitAsync(0))
        {
            SetStatus("Another operation is running...");
            return;
        }

        try
        {
            IsBusy = true;
            await action();
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
            IsBusy = false;
            _operationLock.Release();
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

        BaudRate = state.BaudRate.GetValueOrDefault(MotionDefaults.DefaultBaudRate);

        L_Min = Clamp(state.L_Min ?? MotionDefaults.LinearMinDefault, 0.0, AxisUpperBound);
        L_Max = Clamp(state.L_Max ?? MotionDefaults.LinearMaxDefault, 0.0, AxisUpperBound);
        L_Speed = Clamp(state.L_Speed ?? MotionDefaults.LinearSpeedDefault, MotionDefaults.LinearSpeedMin, MotionDefaults.LinearSpeedMax);

        R_Min = Clamp(state.R_Min ?? MotionDefaults.RotateMinDefault, 0.0, AxisUpperBound);
        R_Max = Clamp(state.R_Max ?? MotionDefaults.RotateMaxDefault, 0.0, AxisUpperBound);
        R_Speed = Clamp(state.R_Speed ?? MotionDefaults.RotateSpeedDefault, MotionDefaults.RotateSpeedMin, MotionDefaults.RotateSpeedMax);

        if (!string.IsNullOrWhiteSpace(state.SelectedPort))
        {
            SelectedPort = _portNames.Contains(state.SelectedPort, StringComparer.OrdinalIgnoreCase)
                ? state.SelectedPort
                : _portNames.FirstOrDefault();
            return;
        }

        SelectedPort = _portNames.FirstOrDefault();
    }
}
