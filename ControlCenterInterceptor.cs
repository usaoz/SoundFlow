using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SoundFlow
{
    public static class ControlCenterInterceptor
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint WINEVENT_SKIPOWNPROCESS = 2;

        private const byte VK_ESCAPE = 0x1B;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static WinEventDelegate _proc;
        private static IntPtr _hookForeground = IntPtr.Zero;
        private static Action _onTrigger;
        private static Control _invoker;
        private static DateTime _lastIntercept = DateTime.MinValue;

        private static bool _enabled = true;
        public static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public static void Start(Control invoker, Action onTrigger)
        {
            if (_hookForeground != IntPtr.Zero) return;

            _invoker = invoker;
            _onTrigger = onTrigger;
            _proc = WinEventProc;

            try
            {
                // Escuchamos cuando la ventana de ControlCenter pasa a primer plano
                _hookForeground = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero, _proc, 0, 0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            }
            catch { }
        }

        public static void Stop()
        {
            try
            {
                if (_hookForeground != IntPtr.Zero)
                {
                    UnhookWinEvent(_hookForeground);
                    _hookForeground = IntPtr.Zero;
                }
            }
            catch { }
            _proc = null;
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!_enabled || hwnd == IntPtr.Zero) return;
            if (idObject != 0) return; // OBJID_WINDOW is 0

            try
            {
                var sb = new StringBuilder(256);
                if (GetClassName(hwnd, sb, 256) > 0)
                {
                    string cls = sb.ToString();
                    if (cls.IndexOf("ControlCenter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cls.IndexOf("QuickSettings", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Debounce para evitar llamadas repetidas
                        if ((DateTime.UtcNow - _lastIntercept).TotalMilliseconds < 250)
                        {
                            return;
                        }
                        _lastIntercept = DateTime.UtcNow;

                        // Si SoundFlow se acaba de cerrar porque el usuario hizo clic en la barra para cerrar, descartar
                        if ((DateTime.UtcNow - QuickFlyoutForm.LastDeactivateTime).TotalMilliseconds < 350)
                        {
                            // Cerrar limpiamente el Control Center de Windows con Escape
                            keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                            keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            return;
                        }

                        // Abrir SoundFlow en primer plano por encima de la ventana nativa
                        if (_onTrigger != null && _invoker != null && _invoker.IsHandleCreated)
                        {
                            _invoker.BeginInvoke(_onTrigger);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
