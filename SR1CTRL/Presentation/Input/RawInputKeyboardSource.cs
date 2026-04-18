using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace SR1CTRL.Presentation.Input;

public sealed class RawInputKeyboardSource : IDisposable
{
    private const uint RidInput = 0x10000003;
    private const uint RidiDevicename = 0x20000007;
    private const int RimTypeKeyboard = 1;
    private const int RimTypeHid = 2;
    private const int WmInput = 0x00FF;
    private const uint RidevInputsink = 0x00000100;
    private const uint RidevPageonly = 0x00000020;
    private const ushort HidUsagePageGeneric = 0x01;
    private const ushort HidUsagePageConsumer = 0x0C;
    private const ushort HidUsageGenericKeyboard = 0x06;
    private const ushort RiKeyBreak = 0x0001;
    private const uint RawInputError = 0xFFFFFFFF;

    private readonly ILogger _logger;

    public RawInputKeyboardSource(nint targetWindowHandle, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        if (targetWindowHandle == 0)
        {
            throw new ArgumentException("A valid window handle is required.", nameof(targetWindowHandle));
        }

        var devices = new[]
        {
            new RawInputDevice
            {
                UsagePage = HidUsagePageGeneric,
                Usage = HidUsageGenericKeyboard,
                Flags = RidevInputsink,
                Target = targetWindowHandle
            },
            new RawInputDevice
            {
                UsagePage = HidUsagePageGeneric,
                Usage = 0,
                Flags = RidevInputsink | RidevPageonly,
                Target = targetWindowHandle
            },
            new RawInputDevice
            {
                UsagePage = HidUsagePageConsumer,
                Usage = 0,
                Flags = RidevInputsink | RidevPageonly,
                Target = targetWindowHandle
            }
        };

        if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw Input keyboard registration failed.");
        }
    }

    public bool TryGetInput(uint message, nint lParam, out RawInputEvent inputEvent)
    {
        inputEvent = default;
        if (message != WmInput || lParam == 0)
        {
            return false;
        }

        if (!TryReadRawInputBuffer(lParam, out var buffer, out var header))
        {
            return false;
        }

        try
        {
            var deviceName = TryGetDeviceName(header.DeviceHandle);
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            return header.Type switch
            {
                RimTypeKeyboard => TryBuildKeyboardInput(buffer, deviceName, out inputEvent),
                RimTypeHid => TryBuildHidInput(buffer, deviceName, out inputEvent),
                _ => false
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
    }

    private static bool TryBuildKeyboardInput(nint buffer, string deviceName, out RawInputEvent inputEvent)
    {
        inputEvent = default;

        var keyboardPtr = nint.Add(buffer, Marshal.SizeOf<RawInputHeader>());
        var keyboard = Marshal.PtrToStructure<RawKeyboard>(keyboardPtr);
        if ((keyboard.Flags & RiKeyBreak) != 0)
        {
            return false;
        }

        var key = KeyInterop.KeyFromVirtualKey(keyboard.VirtualKey);
        if (key == Key.None)
        {
            return false;
        }

        inputEvent = RawInputEvent.FromKeyboard(new RawKeyboardInput(key, keyboard.VirtualKey, deviceName));
        return true;
    }

    private static bool TryBuildHidInput(nint buffer, string deviceName, out RawInputEvent inputEvent)
    {
        inputEvent = default;

        var hidPtr = nint.Add(buffer, Marshal.SizeOf<RawInputHeader>());
        var hid = Marshal.PtrToStructure<RawHid>(hidPtr);
        if (hid.SizeHid == 0 || hid.Count == 0)
        {
            return false;
        }

        var totalBytes = checked((int)(hid.SizeHid * hid.Count));
        var rawData = new byte[totalBytes];
        var rawDataPtr = nint.Add(hidPtr, Marshal.SizeOf<RawHid>());
        Marshal.Copy(rawDataPtr, rawData, 0, totalBytes);

        inputEvent = RawInputEvent.FromHid(new RawHidInput(deviceName, rawData, (int)hid.SizeHid, (int)hid.Count));
        return true;
    }

    private static bool TryReadRawInputBuffer(nint lParam, out nint buffer, out RawInputHeader header)
    {
        buffer = nint.Zero;
        header = default;

        uint rawInputSize = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var probeResult = GetRawInputData(lParam, RidInput, nint.Zero, ref rawInputSize, headerSize);
        if (probeResult == RawInputError || rawInputSize == 0)
        {
            return false;
        }

        buffer = Marshal.AllocHGlobal((int)rawInputSize);
        var readResult = GetRawInputData(lParam, RidInput, buffer, ref rawInputSize, headerSize);
        if (readResult == RawInputError)
        {
            Marshal.FreeHGlobal(buffer);
            buffer = nint.Zero;
            return false;
        }

        header = Marshal.PtrToStructure<RawInputHeader>(buffer);
        return true;
    }

    private string? TryGetDeviceName(nint deviceHandle)
    {
        if (deviceHandle == 0)
        {
            return null;
        }

        uint size = 0;
        var probeResult = GetRawInputDeviceInfo(deviceHandle, RidiDevicename, nint.Zero, ref size);
        if (probeResult == RawInputError || size == 0)
        {
            return null;
        }

        var builder = new StringBuilder((int)size);
        var readResult = GetRawInputDeviceInfo(deviceHandle, RidiDevicename, builder, ref size);
        if (readResult == RawInputError)
        {
            _logger.LogDebug("Failed to read raw input device name. Win32Error={Win32Error}", Marshal.GetLastWin32Error());
            return null;
        }

        return builder.ToString();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        nint hRawInput,
        uint uiCommand,
        nint pData,
        ref uint pcbSize,
        uint cbSizeHeader);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        nint hDevice,
        uint uiCommand,
        [Out] StringBuilder pData,
        ref uint pcbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        nint hDevice,
        uint uiCommand,
        nint pData,
        ref uint pcbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint DeviceHandle;
        public nint Param;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawHid
    {
        public uint SizeHid;
        public uint Count;
    }
}

public enum RawInputKind
{
    Keyboard,
    Hid
}

public readonly record struct RawInputEvent(RawInputKind Kind, RawKeyboardInput Keyboard, RawHidInput Hid)
{
    public static RawInputEvent FromKeyboard(RawKeyboardInput keyboard) => new(RawInputKind.Keyboard, keyboard, default);

    public static RawInputEvent FromHid(RawHidInput hid) => new(RawInputKind.Hid, default, hid);
}

public readonly record struct RawKeyboardInput(Key Key, ushort VirtualKey, string DeviceName);

public readonly record struct RawHidInput(string DeviceName, byte[] Data, int ReportSize, int ReportCount);

