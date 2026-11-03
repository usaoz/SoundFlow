using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SoundFlow
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        private static Mutex _appMutex;
        private static NotifyIcon _notifyIcon;
        private static AudioEngine _engine;
        private static QuickFlyoutForm _quickFlyout;
        private static MainForm _mainForm;

        private const string MutexName = "SoundFlow_Native_App_Mutex_Win11";
        private const string ShowEventName = "SoundFlow_Show_Window_Event_Win11";

        [STAThread]
        static void Main()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    try { SetProcessDpiAwareness(2); }
                    catch { SetProcessDPIAware(); }
                }
            }
            catch { }

            bool createdNew;
            _appMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                try
                {
                    using (var existingEvent = EventWaitHandle.OpenExisting(ShowEventName))
                    {
                        existingEvent.Set();
                    }
                }
                catch { }
                return;
            }

            try
            {
                var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
                var listenerThread = new Thread(() =>
                {
                    while (true)
                    {
                        try
                        {
                            showEvent.WaitOne();
                            if (_mainForm != null && _mainForm.IsHandleCreated)
                            {
                                _mainForm.BeginInvoke(new Action(() => _mainForm.ShowAndActivate()));
                            }
                        }
                        catch { break; }
                    }
                });
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (s, e) =>
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundFlow");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "crash.log"), DateTime.Now + " [Thread]: " + e.Exception + Environment.NewLine);
                }
                catch { }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundFlow");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "crash.log"), DateTime.Now + " [Domain]: " + e.ExceptionObject + Environment.NewLine);
                }
                catch { }
            };

            bool startInTray = false;
            string[] cmdArgs = Environment.GetCommandLineArgs();
            for (int i = 1; i < cmdArgs.Length; i++)
            {
                if (cmdArgs[i].Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                    cmdArgs[i].Equals("--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                    break;
                }
            }

            if (!IsRunOnStartup())
            {
                SetRunOnStartup(true);
            }

            try
            {
                _engine = new AudioEngine();

                _quickFlyout = new QuickFlyoutForm(_engine, () => _mainForm.ShowAndActivate());
                _mainForm = new MainForm(_engine, _quickFlyout, () => ExitApp());
                if (startInTray)
                {
                    _mainForm.StartHidden = true;
                }
                else
                {
                    _mainForm.ShowAndActivate();
                }

                var forceHandle = _quickFlyout.Handle;
                InitializeTray();

                _engine.Start();
                PrtScnRemapper.Start();

                Application.Run(new ApplicationContext());
            }
            catch (Exception ex)
            {
                try
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundFlow");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "crash.log"), DateTime.Now + ": " + ex + Environment.NewLine);
                }
                catch { }
                MessageBox.Show("Error iniciando SoundFlow: " + ex.Message, "SoundFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cleanup();
            }
        }

        public static Icon CreateSpeakerIcon(int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float scale = size / 32f;
                    using (var path = new GraphicsPath())
                    {
                        path.AddRectangle(new RectangleF(3f * scale, 11f * scale, 5.5f * scale, 10f * scale));
                        PointF[] cone = new PointF[]
                        {
                            new PointF(8f * scale, 11f * scale),
                            new PointF(15f * scale, 5.5f * scale),
                            new PointF(15f * scale, 26.5f * scale),
                            new PointF(8f * scale, 21f * scale)
                        };
                        path.AddPolygon(cone);

                        using (var brush = new SolidBrush(Color.White))
                        {
                            g.FillPath(brush, path);
                        }
                    }

                    float penWidth = Math.Max(1.5f, 2.2f * scale);
                    using (var pen = new Pen(Color.White, penWidth))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, new RectangleF(7f * scale, 10f * scale, 12f * scale, 12f * scale), -40, 80);
                        g.DrawArc(pen, new RectangleF(1f * scale, 5f * scale, 22f * scale, 22f * scale), -38, 76);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        public static bool IsRunOnStartup()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key != null && key.GetValue("SoundFlow") != null) return true;
                }
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                return File.Exists(Path.Combine(startupFolder, "SoundFlow.lnk"));
            }
            catch { return false; }
        }

        public static void SetRunOnStartup(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue("SoundFlow", "\"" + exePath + "\" --tray");
                        }
                        else
                        {
                            key.DeleteValue("SoundFlow", false);
                        }
                    }
                }
            }
            catch { }

            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, "SoundFlow.lnk");
                if (enable)
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = Application.ExecutablePath;
                        shortcut.Arguments = "--tray";
                        shortcut.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                        string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soundflow.ico");
                        if (File.Exists(icoPath)) shortcut.IconLocation = icoPath + ",0";
                        shortcut.Save();
                    }
                }
                else
                {
                    if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                }
            }
            catch { }
        }

        private static void InitializeTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "SoundFlow - Control de Sonido";

            int traySize = SystemInformation.SmallIconSize.Width;
            if (traySize <= 0) traySize = 16;
            try
            {
                _notifyIcon.Icon = CreateSpeakerIcon(traySize);
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;

            var ctxMenu = new ContextMenu();

            var itemQuick = new MenuItem("Acceso Rapido", (s, e) => _quickFlyout.ToggleAtTaskbar());
            itemQuick.DefaultItem = true;
            ctxMenu.MenuItems.Add(itemQuick);

            ctxMenu.MenuItems.Add("Abrir SoundFlow", (s, e) => _mainForm.ShowAndActivate());
            ctxMenu.MenuItems.Add("-");
            ctxMenu.MenuItems.Add("Salir", (s, e) => ExitApp());

            _notifyIcon.ContextMenu = ctxMenu;

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _quickFlyout.ToggleAtTaskbar();
                }
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                _mainForm.ShowAndActivate();
            };
        }

        private static void ExitApp()
        {
            Cleanup();
            Environment.Exit(0);
        }

        private static void Cleanup()
        {
            PrtScnRemapper.Stop();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_quickFlyout != null)
            {
                _quickFlyout.Dispose();
                _quickFlyout = null;
            }

            if (_engine != null)
            {
                _engine.Dispose();
                _engine = null;
            }

            if (_appMutex != null)
            {
                try { _appMutex.ReleaseMutex(); } catch { }
                _appMutex = null;
            }
        }
    }
}
