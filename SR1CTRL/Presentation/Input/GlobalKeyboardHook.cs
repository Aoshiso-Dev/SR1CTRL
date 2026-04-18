using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace SR1CTRL.Presentation.Input;

public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    private readonly LowLevelKeyboardProc _proc;
    private nint _hookId;

    public event EventHandler<Key>? KeyPressed;

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
        _hookId = SetHook(_proc);
        if (_hookId == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install global keyboard hook.");
        }
    }

    private nint SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;

        return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WM_KEYDOWN)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var key = KeyInterop.KeyFromVirtualKey(vkCode);
            KeyPressed?.Invoke(this, key);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId == 0)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = 0;
    }
}