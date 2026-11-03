using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SoundFlow
{
    public class QuickFlyoutForm : Form
    {
        private readonly AudioEngine _engine;
        private readonly Action _openMainWindow;

        private ComboBox _moveAllCombo;
        private Panel _appsPanel;
        private Label _lblNoApps;
        private bool _suppressAutoRoute = false;
        private MouseWheelScrollFilter _flyoutFilter;

        public QuickFlyoutForm(AudioEngine engine, Action openMainWindow)
        {
            _engine = engine;
            _openMainWindow = openMainWindow;

            InitializeFlyout();
            BuildUI();

            _flyoutFilter = new MouseWheelScrollFilter(_appsPanel);
            Application.AddMessageFilter(_flyoutFilter);

            _engine.OnSessionsUpdated += () =>
            {
                if (!IsHandleCreated || !Visible) return;
                try
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshApps));
                    else RefreshApps();
                }
                catch { }
            };

            _engine.OnDevicesUpdated += () =>
            {
                if (!IsHandleCreated || !Visible) return;
                try
                {
                    if (InvokeRequired) BeginInvoke(new Action(RefreshDevices));
                    else RefreshDevices();
                }
                catch { }
            };
        }

        public static DateTime LastDeactivateTime = DateTime.MinValue;

        private void InitializeFlyout()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 380;
            Height = 480;
            BackColor = SystemColors.Control;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            KeyPreview = true;
            try { Icon = Program.CreateSpeakerIcon(32); } catch { }

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    Hide();
                    _engine.IsWindowActive = false;
                }
            };

            Deactivate += (s, e) =>
            {
                LastDeactivateTime = DateTime.UtcNow;
                Hide();
                _engine.IsWindowActive = false;
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_flyoutFilter != null) Application.RemoveMessageFilter(_flyoutFilter);
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw clean classic 3D border
            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Raised);
        }

        private void BuildUI()
        {
            // 1. Header Bar
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(8, 6, 8, 4),
                BackColor = SystemColors.ControlLight
            };

            var lblTitle = new Label
            {
                Text = "SoundFlow - Acceso Rapido",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(8, 8)
            };
            topPanel.ChildrenAdd(lblTitle);

            var btnClose = new Button
            {
                Text = "X",
                Width = 24,
                Height = 22,
                Location = new Point(Width - 32, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnClose.Click += (s, e) => Hide();
            topPanel.ChildrenAdd(btnClose);

            var btnOpenFull = new Button
            {
                Text = "Abrir SoundFlow",
                Width = 110,
                Height = 22,
                Location = new Point(Width - 148, 6),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
            };
            btnOpenFull.Click += (s, e) =>
            {
                Hide();
                if (_openMainWindow != null) _openMainWindow();
            };
            topPanel.ChildrenAdd(btnOpenFull);

            Controls.Add(topPanel);

            // 2. Master Output Selector ("PC:")
            var pcPanel = new Panel
            {
                Location = new Point(8, 42),
                Width = Width - 16,
                Height = 32,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = SystemColors.Control
            };

            var lblPC = new Label
            {
                Text = "PC:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Location = new Point(4, 6),
                AutoSize = true,
                ForeColor = Color.Black
            };
            pcPanel.Controls.Add(lblPC);

            _moveAllCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(36, 4),
                Width = pcPanel.Width - 40,
                DropDownWidth = 280,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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
            pcPanel.Controls.Add(_moveAllCombo);

            Controls.Add(pcPanel);

            // 3. Apps Mixer Scrollable Area
            var lblAppsHeader = new Label
            {
                Text = "Aplicaciones activas:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.Black,
                Location = new Point(10, 80),
                AutoSize = true
            };
            Controls.Add(lblAppsHeader);

            _appsPanel = new Panel
            {
                Location = new Point(8, 102),
                Width = Width - 16,
                Height = Height - 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = SystemColors.Window
            };
            _appsPanel.Resize += (s, e) =>
            {
                if (_appsPanel.Controls.Count == 0 || _engine.Sessions.Count == 0) return;
                int sbW = SystemInformation.VerticalScrollBarWidth;
                int totalH = _engine.Sessions.Count * 74 + 8;
                bool needsScroll = totalH > _appsPanel.ClientSize.Height;
                int w = _appsPanel.ClientSize.Width - (needsScroll ? (sbW + 8) : 10);
                if (w < 180) w = 180;

                _appsPanel.SuspendLayout();
                foreach (Control c in _appsPanel.Controls)
                {
                    if (c != _lblNoApps) c.Width = w;
                }
                _appsPanel.ResumeLayout();
            };

            _lblNoApps = new Label
            {
                Text = "No hay aplicaciones reproduciendo audio.",
                ForeColor = SystemColors.GrayText,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            _appsPanel.Controls.Add(_lblNoApps);

            Controls.Add(_appsPanel);
        }

        public void ShowAtTaskbar()
        {
            try
            {
                _engine.IsWindowActive = true;
                _engine.ScanAll();
                RefreshDevices();
                RefreshApps();
            }
            catch { }

            try
            {
                Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
                Left = workArea.Right - Width - 8;
                Top = workArea.Bottom - Height - 8;

                TopMost = true;
                Show();
                BringToFront();
                Activate();
            }
            catch { }
        }

        public void ToggleAtTaskbar()
        {
            if (Visible)
            {
                Hide();
                _engine.IsWindowActive = false;
            }
            else
            {
                _engine.IsWindowActive = true;
                _engine.ScanAll();
                RefreshDevices();
                RefreshApps();

                Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
                Left = workArea.Right - Width - 8;
                Top = workArea.Bottom - Height - 8;

                Show();
                Activate();
            }
        }

        public void RefreshDevices()
        {
            // Move all devices
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
        }

        public void RefreshApps()
        {
            int savedScrollY = Math.Abs(_appsPanel.AutoScrollPosition.Y);

            _appsPanel.AutoScrollPosition = new Point(0, 0);
            _appsPanel.AutoScroll = false;
            _appsPanel.VerticalScroll.Value = 0;
            _appsPanel.HorizontalScroll.Value = 0;

            _appsPanel.SuspendLayout();
            _appsPanel.Controls.Clear();

            if (_engine.Sessions.Count == 0)
            {
                _appsPanel.Controls.Add(_lblNoApps);
                _appsPanel.ResumeLayout();
                _appsPanel.AutoScroll = true;
                return;
            }

            int y = 4;
            int scrollBarW = SystemInformation.VerticalScrollBarWidth;
            int totalExpectedHeight = _engine.Sessions.Count * 74 + 8;
            bool willNeedScroll = totalExpectedHeight > _appsPanel.ClientSize.Height;
            int cardWidth = _appsPanel.ClientSize.Width - (willNeedScroll ? (scrollBarW + 8) : 10);
            if (cardWidth < 180) cardWidth = 180;

            foreach (var session in _engine.Sessions)
            {
                var card = CreateAppCard(session, cardWidth);
                card.Location = new Point(4, y);
                card.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _appsPanel.Controls.Add(card);
                y += card.Height + 6;
            }

            _appsPanel.ResumeLayout();
            _appsPanel.AutoScroll = true;

            if (savedScrollY > 0)
            {
                try
                {
                    int maxScroll = Math.Max(0, _appsPanel.VerticalScroll.Maximum - _appsPanel.ClientSize.Height);
                    int restoreY = Math.Min(savedScrollY, maxScroll);
                    _appsPanel.AutoScrollPosition = new Point(0, restoreY);
                }
                catch { }
            }
        }

        private Panel CreateAppCard(AudioSessionItem session, int width)
        {
            var pnl = new Panel
            {
                Width = width,
                Height = 68,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Control
            };

            // 1. Icon
            var pic = new PictureBox
            {
                Image = session.Icon,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(24, 24),
                Location = new Point(6, 6)
            };
            pnl.Controls.Add(pic);

            // 2. Name
            var lblName = new Label
            {
                Text = session.DisplayName,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.Black,
                Location = new Point(36, 8),
                AutoSize = true
            };
            pnl.Controls.Add(lblName);

            // 3. Device Selector ComboBox
            var devCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(pnl.Width - 192, 5),
                Width = 185,
                DropDownWidth = 270,
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

            // 4. Volume TrackBar
            var tbVol = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Max(0, Math.Min(100, session.Volume)),
                TickStyle = TickStyle.None,
                Location = new Point(6, 34),
                Width = pnl.Width - 130,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblVol = new Label
            {
                Text = session.Volume + "%",
                Location = new Point(pnl.Width - 120, 38),
                Width = 40,
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
                Location = new Point(pnl.Width - 75, 34),
                Width = 68,
                Height = 24,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular)
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
    }

    internal static class ControlExtensions
    {
        public static void ChildrenAdd(this Control parent, Control child)
        {
            parent.Controls.Add(child);
        }
    }

    public class DeviceComboItem
    {
        public string DeviceId { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal static class ComboBoxHelper
    {
        public static void EnableBoldBrackets(ComboBox cb)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 20;

            var boldFont = new Font(cb.Font, FontStyle.Bold);

            cb.Disposed += (s, e) =>
            {
                if (boldFont != null)
                {
                    boldFont.Dispose();
                    boldFont = null;
                }
            };

            cb.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= cb.Items.Count) return;

                bool isEdit = (e.State & DrawItemState.ComboBoxEdit) != 0;
                bool isSelected = (e.State & DrawItemState.Selected) != 0;

                Color backColor = cb.Enabled ? SystemColors.Window : SystemColors.Control;
                Color textColor = cb.Enabled ? SystemColors.WindowText : SystemColors.GrayText;
                Color tagColor = cb.Enabled ? Color.FromArgb(0, 50, 140) : SystemColors.GrayText;

                if (!isEdit && isSelected)
                {
                    backColor = SystemColors.Highlight;
                    textColor = SystemColors.HighlightText;
                    tagColor = SystemColors.HighlightText;
                }

                using (var bgBrush = new SolidBrush(backColor))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }

                object itemObj = cb.Items[e.Index];
                string text = "";
                if (itemObj is DeviceComboItem)
                {
                    text = ((DeviceComboItem)itemObj).DisplayName;
                }
                else if (itemObj != null)
                {
                    text = itemObj.ToString();
                }

                int bracketClose = text.IndexOf(']');
                if (text.StartsWith("[") && bracketClose > 0)
                {
                    string tag = text.Substring(0, bracketClose + 1);
                    string rest = text.Substring(bracketClose + 1).TrimStart();

                    TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter;
                    Size tagSize = TextRenderer.MeasureText(e.Graphics, tag, boldFont, Size.Empty, flags);

                    Rectangle tagRect = new Rectangle(e.Bounds.X + 3, e.Bounds.Y, tagSize.Width, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, tag, boldFont, tagRect, tagColor, flags);

                    Rectangle restRect = new Rectangle(tagRect.Right + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - tagRect.Width - 8), e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, rest, cb.Font, restRect, textColor, flags | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, textColor, TextFormatFlags.VerticalCenter);
                }

                if (!isEdit && isSelected)
                {
                    e.DrawFocusRectangle();
                }
            };
        }
    }
}
