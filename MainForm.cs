using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundFlow
{
    public class MainForm : Form
    {
        private readonly AudioEngine _engine;
        private readonly QuickFlyoutForm _quickFlyout;
        private readonly Action _exitApp;

        private TabControl _tabControl;
        private TabPage _tabApps;
        private TabPage _tabDevices;

        // Tab 1 controls
        private ComboBox _moveAllCombo;
        private Panel _appsListPanel;
        private Label _lblNoApps;
        private bool _suppressAutoRoute = false;

        // Tab 2 controls
        private Panel _devicesListPanel;

        // Status bar
        private Label _lblStatus;

        private MouseWheelScrollFilter _appsFilter;
        private MouseWheelScrollFilter _devicesFilter;

        public MainForm(AudioEngine engine, QuickFlyoutForm quickFlyout, Action exitApp)
        {
            _engine = engine;
            _quickFlyout = quickFlyout;
            _exitApp = exitApp;

            InitializeForm();
            BuildUI();

            _appsFilter = new MouseWheelScrollFilter(_appsListPanel);
            _devicesFilter = new MouseWheelScrollFilter(_devicesListPanel);
            Application.AddMessageFilter(_appsFilter);
            Application.AddMessageFilter(_devicesFilter);

            _engine.OnSessionsUpdated += () =>
            {
                if (!IsHandleCreated) return;
                try
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshAppsTab));
                    else RefreshAppsTab();
                }
                catch { }
            };

            _engine.OnDevicesUpdated += () =>
            {
                if (!IsHandleCreated) return;
                try
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshDevicesTab));
                    else RefreshDevicesTab();
                }
                catch { }
            };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshAppsTab();
            RefreshDevicesTab();
        }

        private void InitializeForm()
        {
            Text = "SoundFlow - Administrador de Audio";
            Width = 640;
            Height = 700;
            MinimumSize = new Size(540, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = SystemColors.Control;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            try { Icon = Program.CreateSpeakerIcon(32); } catch { }

            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    HideToTray();
                }
            };

            Activated += (s, e) => { _engine.IsWindowActive = true; };
            Deactivate += (s, e) => { _engine.IsWindowActive = false; };
        }

        public bool StartHidden { get; set; }

        protected override void SetVisibleCore(bool value)
        {
            if (StartHidden && !IsHandleCreated)
            {
                value = false;
                CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_appsFilter != null) Application.RemoveMessageFilter(_appsFilter);
                if (_devicesFilter != null) Application.RemoveMessageFilter(_devicesFilter);
            }
            base.Dispose(disposing);
        }

        public void ShowAndActivate()
        {
            StartHidden = false;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            _engine.IsWindowActive = true;
            _engine.ScanAll();
            RefreshAppsTab();
            RefreshDevicesTab();
        }

        public void HideToTray()
        {
            Hide();
            _engine.IsWindowActive = false;
            // Trim working set memory to keep RAM usage minimal
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Win32TrimMemory(Process.GetCurrentProcess().Handle, -1, -1);
            }
            catch { }
        }

        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]
        private static extern bool Win32TrimMemory(IntPtr proc, int min, int max);

        private void BuildUI()
        {
            // Bottom Status / Minimize Bar
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = SystemColors.ControlLight
            };

            var btnMinToTray = new Button
            {
                Text = "Ocultar panel",
                Location = new Point(8, 6),
                Width = 120,
                Height = 26,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnMinToTray.Click += (s, e) => HideToTray();
            bottomPanel.Controls.Add(btnMinToTray);

            var chkStartup = new CheckBox
            {
                Text = "Iniciar con Windows",
                Location = new Point(138, 10),
                AutoSize = true,
                Checked = Program.IsRunOnStartup(),
                ForeColor = Color.Black
            };
            chkStartup.CheckedChanged += (s, e) =>
            {
                Program.SetRunOnStartup(chkStartup.Checked);
            };
            bottomPanel.Controls.Add(chkStartup);

            _lblStatus = new Label
            {
                Location = new Point(285, 11),
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Font = new Font("Segoe UI", 8.5f)
            };
            bottomPanel.Controls.Add(_lblStatus);

            var btnExit = new Button
            {
                Text = "Salir de SoundFlow",
                Location = new Point(bottomPanel.Width - 140, 6),
                Width = 130,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            btnExit.Click += (s, e) => { if (_exitApp != null) _exitApp(); else Application.Exit(); };
            bottomPanel.Controls.Add(btnExit);

            Controls.Add(bottomPanel);

            // Tab Control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(12, 6)
            };

            // TAB 1: Control por Aplicacion
            _tabApps = new TabPage("Control por Aplicacion");
            BuildAppsTab();
            _tabControl.TabPages.Add(_tabApps);

            // TAB 2: Dispositivos de Audio
            _tabDevices = new TabPage("Dispositivos de Salida");
            BuildDevicesTab();
            _tabControl.TabPages.Add(_tabDevices);

            Controls.Add(_tabControl);
            _tabControl.BringToFront();
        }

        // ==================== TAB 1: APPS ====================
        private void BuildAppsTab()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 8, 8, 8),
                BackColor = SystemColors.Control
            };

            var lblMoveAll = new Label
            {
                Text = "PC:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(10, 11),
                AutoSize = true,
                ForeColor = Color.Black
            };
            topPanel.Controls.Add(lblMoveAll);

            _moveAllCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(48, 9),
                Width = 320,
                DropDownWidth = 320
            };
            ComboBoxHelper.EnableBoldBrackets(_moveAllCombo);
            _moveAllCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressAutoRoute) return;
                var item = _moveAllCombo.SelectedItem as DeviceComboItem;
                if (item != null)
                {
                    _engine.RouteAllSessions(item.DeviceId);
                }
            };
            topPanel.Controls.Add(_moveAllCombo);

            var btnRefresh = new Button
            {
                Text = "Actualizar",
                Location = new Point(376, 8),
                Width = 85,
                Height = 26
            };
            btnRefresh.Click += (s, e) => _engine.ScanAll();
            topPanel.Controls.Add(btnRefresh);

            _tabApps.Controls.Add(topPanel);

            _appsListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = SystemColors.Window
            };
            _appsListPanel.Resize += (s, e) =>
            {
                if (_appsListPanel.Controls.Count == 0 || _engine.Sessions.Count == 0) return;
                int sbW = SystemInformation.VerticalScrollBarWidth;
                int totalH = _engine.Sessions.Count * 86 + 12;
                bool needsScroll = totalH > _appsListPanel.ClientSize.Height;
                int w = _appsListPanel.ClientSize.Width - (needsScroll ? (sbW + 10) : 14);
                if (w < 200) w = 200;

                _appsListPanel.SuspendLayout();
                foreach (Control c in _appsListPanel.Controls)
                {
                    if (c != _lblNoApps) c.Width = w;
                }
                _appsListPanel.ResumeLayout();
            };

            _lblNoApps = new Label
            {
                Text = "No hay aplicaciones activas reproduciendo sonido en este momento.",
                ForeColor = SystemColors.GrayText,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            _appsListPanel.Controls.Add(_lblNoApps);

            _tabApps.Controls.Add(_appsListPanel);
            _appsListPanel.BringToFront();
        }

        public void RefreshAppsTab()
        {
            // Populate move all combo
            _suppressAutoRoute = true;
            try
            {
                var list = new List<DeviceComboItem>();
                foreach (var d in _engine.GetVisibleDevices())
                {
                    list.Add(new DeviceComboItem
                    {
                        DeviceId = d.Id,
                        DisplayName = d.Name + (d.IsDefault ? " (Pred)" : "")
                    });
                }

                string prevSelId = (_moveAllCombo.SelectedItem as DeviceComboItem) != null
                    ? (_moveAllCombo.SelectedItem as DeviceComboItem).DeviceId
                    : null;

                _moveAllCombo.Items.Clear();
                _moveAllCombo.DisplayMember = "DisplayName";
                foreach (var itm in list) _moveAllCombo.Items.Add(itm);

                int targetIdx = 0;
                if (!string.IsNullOrEmpty(prevSelId))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].DeviceId == prevSelId) { targetIdx = i; break; }
                    }
                }
                if (_moveAllCombo.Items.Count > 0 && targetIdx >= 0 && targetIdx < _moveAllCombo.Items.Count)
                {
                    _moveAllCombo.SelectedIndex = targetIdx;
                }
            }
            finally
            {
                _suppressAutoRoute = false;
            }

            // Populate apps list
            int savedScrollY = Math.Abs(_appsListPanel.AutoScrollPosition.Y);

            _appsListPanel.AutoScrollPosition = new Point(0, 0);
            _appsListPanel.AutoScroll = false;
            _appsListPanel.VerticalScroll.Value = 0;
            _appsListPanel.HorizontalScroll.Value = 0;

            _appsListPanel.SuspendLayout();
            _appsListPanel.Controls.Clear();

            if (_engine.Sessions.Count == 0)
            {
                _appsListPanel.Controls.Add(_lblNoApps);
                _appsListPanel.ResumeLayout();
                _appsListPanel.AutoScroll = true;
                UpdateStatus();
                return;
            }

            int y = 6;
            int scrollBarW = SystemInformation.VerticalScrollBarWidth;
            int totalExpectedHeight = _engine.Sessions.Count * 86 + 12;
            bool willNeedScroll = totalExpectedHeight > _appsListPanel.ClientSize.Height;
            int cardWidth = _appsListPanel.ClientSize.Width - (willNeedScroll ? (scrollBarW + 10) : 14);
            if (cardWidth < 200) cardWidth = 200;

            foreach (var session in _engine.Sessions)
            {
                var card = CreateMainAppCard(session, cardWidth);
                card.Location = new Point(6, y);
                card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _appsListPanel.Controls.Add(card);
                y += card.Height + 8;
            }

            _appsListPanel.ResumeLayout();
            _appsListPanel.AutoScroll = true;

            if (savedScrollY > 0)
            {
                try
                {
                    int maxScroll = Math.Max(0, _appsListPanel.VerticalScroll.Maximum - _appsListPanel.ClientSize.Height);
                    int restoreY = Math.Min(savedScrollY, maxScroll);
                    _appsListPanel.AutoScrollPosition = new Point(0, restoreY);
                }
                catch { }
            }

            UpdateStatus();
        }

        private Panel CreateMainAppCard(AudioSessionItem session, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 78,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Control
            };

            // Icon
            var pic = new PictureBox
            {
                Image = session.Icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(28, 28),
                Location = new Point(8, 8)
            };
            pnl.Controls.Add(pic);

            // Name
            var lblName = new Label
            {
                Text = session.DisplayName,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                Location = new Point(42, 10),
                AutoSize = true
            };
            pnl.Controls.Add(lblName);

            // Device selector label and combobox
            var lblRoute = new Label
            {
                Text = "Dispositivo de salida:",
                Location = new Point(pnl.Width - 360, 11),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true,
                ForeColor = Color.Black
            };
            pnl.Controls.Add(lblRoute);

            var devCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(pnl.Width - 230, 8),
                Width = 220,
                DropDownWidth = 280,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            ComboBoxHelper.EnableBoldBrackets(devCombo);

            var devList = new List<DeviceComboItem>();
            foreach (var d in _engine.GetVisibleDevices())
            {
                devList.Add(new DeviceComboItem
                {
                    DeviceId = d.Id,
                    DisplayName = d.Name + (d.IsDefault ? " (Pred)" : "")
                });
            }

            devCombo.Items.Clear();
            devCombo.DisplayMember = "DisplayName";
            foreach (var d in devList) devCombo.Items.Add(d);

            int selIdx = 0;
            for (int i = 0; i < devList.Count; i++)
            {
                if (devList[i].DeviceId == session.SelectedDeviceId ||
                    devList[i].DeviceId == session.CurrentDeviceId)
                {
                    selIdx = i;
                    break;
                }
            }
            if (devCombo.Items.Count > 0 && selIdx >= 0 && selIdx < devCombo.Items.Count)
            {
                devCombo.SelectedIndex = selIdx;
            }

            devCombo.SelectedIndexChanged += (s, e) =>
            {
                var sel = devCombo.SelectedItem as DeviceComboItem;
                if (sel != null)
                {
                    _engine.RouteSession(session, sel.DeviceId);
                }
            };
            pnl.Controls.Add(devCombo);

            // Volume TrackBar
            var tbVol = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Max(0, Math.Min(100, session.Volume)),
                TickStyle = TickStyle.None,
                Location = new Point(8, 42),
                Width = pnl.Width - 180,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblVol = new Label
            {
                Text = session.Volume + "%",
                Location = new Point(pnl.Width - 170, 46),
                Width = 45,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.Black
            };

            tbVol.Scroll += (s, e) =>
            {
                lblVol.Text = tbVol.Value + "%";
                _engine.SetSessionVolume(session, tbVol.Value);
            };

            var btnMute = new Button
            {
                Text = session.IsMuted ? "Silenciado" : "Silenciar",
                Location = new Point(pnl.Width - 115, 42),
                Width = 75,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            btnMute.Click += (s, e) =>
            {
                bool newMute = !session.IsMuted;
                _engine.SetSessionMute(session, newMute);
                btnMute.Text = newMute ? "Silenciado" : "Silenciar";
            };

            pnl.Controls.Add(tbVol);
            pnl.Controls.Add(lblVol);
            pnl.Controls.Add(btnMute);

            return pnl;
        }

        // ==================== TAB 2: DEVICES ====================
        private void BuildDevicesTab()
        {
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8),
                BackColor = SystemColors.Control
            };

            var lblInfo = new Label
            {
                Text = "Organiza, oculta o selecciona tus dispositivos de salida (Hardware):",
                Location = new Point(8, 12),
                AutoSize = true,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            topPanel.Controls.Add(lblInfo);

            _tabDevices.Controls.Add(topPanel);

            _devicesListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = SystemColors.Window
            };
            _devicesListPanel.Resize += (s, e) =>
            {
                if (_devicesListPanel.Controls.Count == 0 || _engine.Devices.Count == 0) return;
                int sbW = SystemInformation.VerticalScrollBarWidth;
                int totalH = _engine.Devices.Count * 88 + 12;
                bool needsScroll = totalH > _devicesListPanel.ClientSize.Height;
                int w = _devicesListPanel.ClientSize.Width - (needsScroll ? (sbW + 10) : 14);
                if (w < 200) w = 200;

                _devicesListPanel.SuspendLayout();
                foreach (Control c in _devicesListPanel.Controls)
                {
                    c.Width = w;
                }
                _devicesListPanel.ResumeLayout();
            };
            _tabDevices.Controls.Add(_devicesListPanel);
            _devicesListPanel.BringToFront();
        }

        public void RefreshDevicesTab()
        {
            int savedScrollY = Math.Abs(_devicesListPanel.AutoScrollPosition.Y);

            _devicesListPanel.AutoScrollPosition = new Point(0, 0);
            _devicesListPanel.AutoScroll = false;
            _devicesListPanel.VerticalScroll.Value = 0;
            _devicesListPanel.HorizontalScroll.Value = 0;

            _devicesListPanel.SuspendLayout();
            _devicesListPanel.Controls.Clear();

            int y = 6;
            int scrollBarW = SystemInformation.VerticalScrollBarWidth;
            int totalExpectedHeight = _engine.Devices.Count * 88 + 12;
            bool willNeedScroll = totalExpectedHeight > _devicesListPanel.ClientSize.Height;
            int cardWidth = _devicesListPanel.ClientSize.Width - (willNeedScroll ? (scrollBarW + 10) : 14);
            if (cardWidth < 200) cardWidth = 200;

            foreach (var dev in _engine.Devices)
            {
                var card = CreateDeviceCard(dev, cardWidth);
                card.Location = new Point(6, y);
                card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _devicesListPanel.Controls.Add(card);
                y += card.Height + 8;
            }

            _devicesListPanel.ResumeLayout();
            _devicesListPanel.AutoScroll = true;

            if (savedScrollY > 0)
            {
                try
                {
                    int maxScroll = Math.Max(0, _devicesListPanel.VerticalScroll.Maximum - _devicesListPanel.ClientSize.Height);
                    int restoreY = Math.Min(savedScrollY, maxScroll);
                    _devicesListPanel.AutoScrollPosition = new Point(0, restoreY);
                }
                catch { }
            }

            UpdateStatus();
        }

        private Panel CreateDeviceCard(AudioDeviceItem dev, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 80,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = dev.IsHidden ? SystemColors.ControlLight : SystemColors.Control
            };

            // Device Name
            var lblName = new Label
            {
                Text = (dev.IsDefault ? "[PREDETERMINADO] " : "") + dev.Name,
                Font = new Font("Segoe UI", 9.5f, dev.IsDefault ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = dev.IsDefault ? Color.DarkBlue : Color.Black,
                Location = new Point(8, 6),
                AutoSize = true
            };
            pnl.Controls.Add(lblName);

            var lblHardware = new Label
            {
                Text = "Hardware: " + dev.RawName,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = SystemColors.GrayText,
                Location = new Point(8, 26),
                AutoSize = true
            };
            pnl.Controls.Add(lblHardware);

            // Volume TrackBar
            var tbVol = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Max(0, Math.Min(100, dev.Volume)),
                TickStyle = TickStyle.None,
                Location = new Point(8, 46),
                Width = pnl.Width - 440,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblVol = new Label
            {
                Text = dev.Volume + "%",
                Location = new Point(pnl.Width - 430, 50),
                Width = 40,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                ForeColor = Color.Black
            };

            tbVol.Scroll += (s, e) =>
            {
                lblVol.Text = tbVol.Value + "%";
                _engine.SetDeviceVolume(dev, tbVol.Value);
            };
            pnl.Controls.Add(tbVol);
            pnl.Controls.Add(lblVol);

            // Action Buttons
            int rightX = pnl.Width - 75;

            // Down button
            var btnDown = new Button
            {
                Text = "Bajar",
                Location = new Point(rightX, 6),
                Width = 65,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDown.Click += (s, e) => _engine.MoveDeviceOrder(dev.Id, false);
            pnl.Controls.Add(btnDown);

            // Up button
            var btnUp = new Button
            {
                Text = "Subir",
                Location = new Point(rightX, 42),
                Width = 65,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnUp.Click += (s, e) => _engine.MoveDeviceOrder(dev.Id, true);
            pnl.Controls.Add(btnUp);

            // Hide/Show button
            var btnHide = new Button
            {
                Text = dev.IsHidden ? "Mostrar" : "Ocultar",
                Location = new Point(rightX - 75, 6),
                Width = 70,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnHide.Click += (s, e) => _engine.ToggleDeviceVisibility(dev.Id);
            pnl.Controls.Add(btnHide);

            // Default button
            var btnDefault = new Button
            {
                Text = dev.IsDefault ? "Predeterminado" : "Hacer Predet.",
                Location = new Point(rightX - 175, 6),
                Width = 95,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = !dev.IsDefault
            };
            btnDefault.Click += (s, e) => _engine.SetDefaultDevice(dev);
            pnl.Controls.Add(btnDefault);

            // Rename button
            var btnRename = new Button
            {
                Text = "Renombrar",
                Location = new Point(rightX - 265, 6),
                Width = 85,
                Height = 30,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRename.Click += (s, e) => ShowRenameDialog(dev);
            pnl.Controls.Add(btnRename);

            return pnl;
        }

        private void ShowRenameDialog(AudioDeviceItem dev)
        {
            using (var prompt = new Form())
            {
                prompt.Width = 380;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = "Renombrar dispositivo";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.BackColor = SystemColors.Control;
                prompt.Font = new Font("Segoe UI", 9f);

                var lblInfo = new Label
                {
                    Left = 16,
                    Top = 14,
                    Width = 330,
                    Text = "Escribe un nombre personalizado o dejalo vacio para automatico:"
                };
                prompt.Controls.Add(lblInfo);

                var txtInput = new TextBox
                {
                    Left = 16,
                    Top = 40,
                    Width = 330,
                    Text = !string.IsNullOrEmpty(dev.CustomName) ? dev.CustomName : dev.CleanName
                };
                prompt.Controls.Add(txtInput);

                var btnReset = new Button
                {
                    Text = "Restablecer",
                    Left = 45,
                    Top = 78,
                    Width = 95,
                    Height = 26
                };
                btnReset.Click += (s, e) =>
                {
                    _engine.SetDeviceCustomName(dev.Id, "");
                    prompt.Close();
                };
                prompt.Controls.Add(btnReset);

                var btnSave = new Button
                {
                    Text = "Guardar",
                    Left = 155,
                    Top = 78,
                    Width = 85,
                    Height = 26
                };
                btnSave.Click += (s, e) =>
                {
                    _engine.SetDeviceCustomName(dev.Id, txtInput.Text.Trim());
                    prompt.Close();
                };
                prompt.Controls.Add(btnSave);

                var btnCancel = new Button
                {
                    Text = "Cancelar",
                    Left = 250,
                    Top = 78,
                    Width = 85,
                    Height = 26,
                    DialogResult = DialogResult.Cancel
                };
                prompt.Controls.Add(btnCancel);

                prompt.AcceptButton = btnSave;
                prompt.CancelButton = btnCancel;

                prompt.ShowDialog(this);
            }
        }

        private void UpdateStatus()
        {
            if (_lblStatus == null) return;
            long memBytes = Process.GetCurrentProcess().WorkingSet64;
            int memMb = (int)(memBytes / (1024 * 1024));
            _lblStatus.Text = string.Format("RAM: {0} MB | Apps activas: {1} | Dispositivos: {2}",
                memMb, _engine.Sessions.Count, _engine.GetVisibleDevices().Count);
        }
    }
}
