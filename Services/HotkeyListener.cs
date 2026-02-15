using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace TypingApp.Services
{
    public class HotkeyListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104; // For Alt combinations

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        
        private Key _triggerKey = Key.V;
        private ModifierKeys _triggerModifiers = ModifierKeys.Control | ModifierKeys.Shift;

        public event Action? OnPasteHotkeyDetected;

        public HotkeyListener()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }
        
        public void UpdateHotkey(Key key, ModifierKeys modifiers)
        {
            _triggerKey = key;
            _triggerModifiers = modifiers;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null) throw new InvalidOperationException("Could not get main module.");
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                if (key == _triggerKey && CheckModifiers(_triggerModifiers))
                {
                    OnPasteHotkeyDetected?.Invoke();
                    return (IntPtr)1; 
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private bool CheckModifiers(ModifierKeys modifiers)
        {
            if ((modifiers & ModifierKeys.Control) != 0 && !IsKeyPressed(VK_CONTROL)) return false;
            if ((modifiers & ModifierKeys.Shift) != 0 && !IsKeyPressed(VK_SHIFT)) return false;
            if ((modifiers & ModifierKeys.Alt) != 0 && !IsKeyPressed(VK_MENU)) return false;
            return true;
        }

        private bool IsKeyPressed(int nVirtKey)
        {
            return (GetKeyState(nVirtKey) & 0x8000) != 0;
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetKeyState(int nVirtKey);
    }
}
