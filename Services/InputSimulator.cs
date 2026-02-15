using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TypingApp.Services
{
    public class InputSimulator : ITypingEngine
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const ushort VK_TAB = 0x09;
        private const ushort VK_RETURN = 0x0D;
        private const ushort VK_SHIFT = 0x10;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_MENU = 0x12;

        public async Task EnsureModifiersUpAsync()
        {
            SendKeyUp(VK_CONTROL);
            SendKeyUp(VK_SHIFT);
            SendKeyUp(VK_MENU);
            await Task.Delay(50);
        }

        public void SendKeyUp(ushort vk)
        {
            var inputs = new INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
            };
            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_ESCAPE = 0x1B;

        public async Task TypeTextAsync(string text, int delayMilliseconds = 0)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                // Check if ESC is pressed to cancel typing
                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                {
                    return;
                }

                if (c == '\r') continue; // Skip Carriage Return, handle on New Line

                if (c == '\n')
                {
                    SendVirtualKey(VK_RETURN);
                }
                else if (c == '\t')
                {
                    SendVirtualKey(VK_TAB);
                }
                else
                {
                    SendChar(c);
                }

                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds);
                }
            }
        }

        private void SendVirtualKey(ushort vk)
        {
            var inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } }
            };

            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        private void SendChar(char c)
        {
            var inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE
                    }
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }
    }
}
