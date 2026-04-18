using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.Models;
using SR1CTRL.Application.UseCases;
using SR1CTRL.Presentation.Config;

namespace SR1CTRL.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IComPortProvider _ports;
    private readonly DeviceControlUseCase _deviceControl;

    public MainViewModel(IComPortProvider ports, DeviceControlUseCase deviceControl)
    {
        _ports = ports;
        _deviceControl = deviceControl;

        BaudRate = MotionDefaults.DefaultBaudRate;
        RefreshPorts();

        L_Min = MotionDefaults.LinearMinDefault;
        L_Max = MotionDefaults.LinearMaxDefault;
        L_Speed = MotionDefaults.LinearSpeedDefault;

        R_Min = MotionDefaults.RotateMinDefault;
        R_Max = MotionDefaults.RotateMaxDefault;
        R_Speed = MotionDefaults.RotateSpeedDefault;

        SyncState(_deviceControl.GetState());
    }

    public IReadOnlyList<string> PortNames => _portNames;
    private IReadOnlyList<string> _portNames = Array.Empty<string>();

    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private int baudRate;

    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isRunning;

    [ObservableProperty] private string d0Text = string.Empty;
    [ObservableProperty] private string d1Text = string.Empty;
    [ObservableProperty] private string d2Text = string.Empty;

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
        if (IsConnected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            throw new InvalidOperationException("COMポートを選択してください。");
        }

        var state = await _deviceControl.ConnectAsync(SelectedPort, BaudRate, CancellationToken.None);
        SyncState(state);

        await QueryDeviceInfo();
        ApplyAll();
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        if (!IsConnected)
        {
            return;
        }

        var state = await _deviceControl.DisconnectAsync(CancellationToken.None);
        SyncState(state);
    }

    [RelayCommand]
    private async Task QueryDeviceInfo()
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
    }

    [RelayCommand]
    private async Task Start()
    {
        if (!IsConnected || IsRunning)
        {
            return;
        }

        var state = await _deviceControl.StartAsync(CancellationToken.None);
        SyncState(state);
    }

    [RelayCommand]
    private async Task Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        var state = await _deviceControl.StopAsync(CancellationToken.None);
        SyncState(state);
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

    private void RefreshPorts()
    {
        _portNames = _ports.GetPortNames();
        OnPropertyChanged(nameof(PortNames));

        if (SelectedPort is null && _portNames.Count > 0)
        {
            SelectedPort = _portNames[0];
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

        _deviceControl.ApplyLinear(new LinearMotionRequest(
            Channel: MotionDefaults.DefaultChannel,
            Min: L_Min,
            Max: L_Max,
            SpeedPerSecond: L_Speed));
    }

    private void ApplyRotate()
    {
        if (!IsConnected)
        {
            return;
        }

        _deviceControl.ApplyRotate(new RotateMotionRequest(
            Channel: MotionDefaults.DefaultChannel,
            Min: R_Min,
            Max: R_Max,
            SpeedPerSecond: R_Speed));
    }

    private void SyncState(DeviceRuntimeState state)
    {
        IsConnected = state.IsConnected;
        IsRunning = state.IsRunning;
    }
}
