using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundFlow
{
    public static class PrtScnRemapper
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SNAPSHOT = 0x2C; // Print Screen (ImpPnt)
        private const byte VK_F13 = 0x7C;     // F13 key (código 124)
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint LLKHF_INJECTED = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
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

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        // Referencia estática permanente para evitar que el Garbage Collector lo limpie
        private static LowLevelKeyboardProc _proc;
        private static IntPtr _hookId = IntPtr.Zero;

        public static void Start()
        {
            if (_hookId != IntPtr.Zero) return;
            try
            {
                _proc = HookCallback;
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
            catch { }
        }

        public static void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                try
                {
                    UnhookWindowsHookEx(_hookId);
                }
                catch { }
                _hookId = IntPtr.Zero;
                _proc = null;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Si fue un evento inyectado artificialmente, dejar pasar
                if ((kbd.flags & LLKHF_INJECTED) != 0)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // Si es la tecla ImpPnt / Print Screen
                if (kbd.vkCode == VK_SNAPSHOT)
                {
                    int msg = wParam.ToInt32();
                    byte scanCode = (byte)MapVirtualKey(VK_F13, 0);

                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        keybd_event(VK_F13, scanCode, 0, UIntPtr.Zero);
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        keybd_event(VK_F13, scanCode, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }

                    // Retornar 1 para anular completamente VK_SNAPSHOT en Windows
                    // Evita que Windows saque capturas de pantalla o toque el portapapeles
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
