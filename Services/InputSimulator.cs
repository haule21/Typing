using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TypingApp.Services
{
    public class InputSimulator : ITypingEngine
    {
        private readonly Models.ConfigStore _configStore;

        public event Action<bool>? ProcessingChanged;

        public InputSimulator(Models.ConfigStore configStore)
        {
            _configStore = configStore;
        }

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
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public UIntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_ESCAPE = 0x1B;
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
            inputs[0] = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } };
            SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        private bool IsEscapePressed() => (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

        // [변경] 완전 즉시 주입 (Bulk Stream Injection)
        // 클립보드를 전혀 수정하지 않고, 전체 텍스트를 고속 스트림으로 주입합니다.
        // 시간 기반 유추가 아닌, 실제 모든 문자의 전송이 완료되는 시점을 정확히 추적합니다.
        public async Task PasteTextAsBulkAsync(string text, System.Threading.CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(text)) return;

            // [추가] 옵저버 패턴 (상태 변경 알림) - 시작
            ProcessingChanged?.Invoke(true);

            try
            {
                // 설정 적용
                if (_configStore != null)
                {
                    if (_configStore.Current.IgnoreTabs) text = text.Replace("\t", "");
                    if (_configStore.Current.IgnoreNewlines) text = text.Replace("\r", "").Replace("\n", "");
                }

                if (string.IsNullOrEmpty(text)) return;

                // 모든 문자를 순차적으로, 하지만 최대한 빠르게 시스템에 전달합니다.
                // 이를 통해 루프가 종료되는 시점이 정확히 '타이핑 완료' 시점이 됩니다.
                foreach (char c in text)
                {
                    if (ct.IsCancellationRequested || IsEscapePressed()) break;

                    var inputList = new List<INPUT>();
                    if (c == '\r') continue;
                    if (c == '\n') AddVirtualKeyInputs(inputList, VK_RETURN);
                    else if (c == '\t') AddVirtualKeyInputs(inputList, VK_TAB);
                    else AddCharInputs(inputList, c);

                    if (inputList.Count > 0)
                    {
                        SendInput((uint)inputList.Count, inputList.ToArray(), INPUT.Size);
                    }

                    // [수정] Yield 대신 1ms 대기를 주어 OS 입력 큐와의 동기화를 강화하고 
                    // 실제 타이핑 완료 시점을 더 신뢰할 수 있게 만듭니다.
                    await Task.Delay(1); 
                }
            }
            finally
            {
                // [추가] 옵저버 패턴 (상태 변경 알림) - 완료
                ProcessingChanged?.Invoke(false);
            }
        }

        // 지연 시간이 있는 일반 타이핑 시뮬레이션
        public async Task TypeTextAsync(string text, int delayMilliseconds = 0, System.Threading.CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 옵저버 패턴 (상태 변경 알림) - 시작
            ProcessingChanged?.Invoke(true);

            try
            {
                // 설정 적용
                if (_configStore != null)
                {
                    if (_configStore.Current.IgnoreTabs) text = text.Replace("\t", "");
                    if (_configStore.Current.IgnoreNewlines) text = text.Replace("\r", "").Replace("\n", "");
                }

                // 글자 단위로 입력하며 취소 확인
                foreach (char c in text)
                {
                    if (ct.IsCancellationRequested || IsEscapePressed()) break;

                    var inputList = new List<INPUT>();
                    if (c == '\r') continue;
                    if (c == '\n') AddVirtualKeyInputs(inputList, VK_RETURN);
                    else if (c == '\t') AddVirtualKeyInputs(inputList, VK_TAB);
                    else AddCharInputs(inputList, c);

                    SendInput((uint)inputList.Count, inputList.ToArray(), INPUT.Size);
                    
                    if (delayMilliseconds > 0)
                    {
                        await Task.Delay(delayMilliseconds);
                    }
                    else
                    {
                        await Task.Yield();
                    }
                }
            }
            finally
            {
                // 옵저버 패턴 (상태 변경 알림) - 완료
                ProcessingChanged?.Invoke(false);
            }
        }

        private void AddVirtualKeyInputs(List<INPUT> list, ushort vk)
        {
            list.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } } });
            list.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } });
        }

        private void AddCharInputs(List<INPUT> list, char c)
        {
            list.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE } } });
            list.Add(new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } } });
        }

        public async Task SimulateCtrlVAsync()
        {
            var inputs = new INPUT[4];
            inputs[0] = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = 0 } } };
            inputs[1] = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0x56, dwFlags = 0 } } };
            inputs[2] = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0x56, dwFlags = KEYEVENTF_KEYUP } } };
            inputs[3] = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } } };
            SendInput((uint)inputs.Length, inputs, INPUT.Size);
            await Task.Delay(50);
        }
    }
}
