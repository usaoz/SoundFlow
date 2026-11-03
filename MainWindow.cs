using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolTipIcon = System.Windows.Forms.ToolTipIcon;

namespace SoundFlow
{
    public class MainWindow : Window
    {
        private AudioEngine _engine;
        private ProfileService _profileService;
        private NotifyIcon _notifyIcon;

        // UI References
        private System.Windows.Controls.ComboBox _profileCombo;
        private TextBlock _profileDescText;
        private System.Windows.Controls.ComboBox _moveAllCombo;
        private System.Windows.Controls.CheckBox _chkSetDefault;
        private StackPanel _sessionsPanel;
        private StackPanel _devicesPanel;
        private TextBlock _activeAppsCountText;
        private TextBlock _statusMessageText;
        private TabControl _tabControl;
        private bool _suppressMoveAllAutoRoute = false;
        private System.Windows.Forms.MenuItem _trayProfilesMenu;
        private QuickFlyoutWindow _quickFlyout;
        private bool _showHiddenDevices = false;

        // Colors
        private readonly System.Windows.Media.Color BgColor = System.Windows.Media.Color.FromRgb(15, 16, 21);
        private readonly System.Windows.Media.Color CardBgColor = System.Windows.Media.Color.FromRgb(24, 25, 32);
        private readonly System.Windows.Media.Color CardBorderColor = System.Windows.Media.Color.FromRgb(42, 44, 56);
        private readonly System.Windows.Media.Color AccentColor = System.Windows.Media.Color.FromRgb(99, 102, 241);
        private readonly System.Windows.Media.Color AccentHoverColor = System.Windows.Media.Color.FromRgb(129, 140, 248);
        private readonly System.Windows.Media.Color CyanAccentColor = System.Windows.Media.Color.FromRgb(6, 182, 212);
        private readonly System.Windows.Media.Color TextPrimary = System.Windows.Media.Color.FromRgb(243, 244, 246);
        private readonly System.Windows.Media.Color TextSecondary = System.Windows.Media.Color.FromRgb(156, 163, 175);
        private readonly System.Windows.Media.Color DangerColor = System.Windows.Media.Color.FromRgb(239, 68, 68);

        public MainWindow(AudioEngine engine, ProfileService profileService)
        {
            _engine = engine;
            _profileService = profileService;

            InitializeWindow();
            _quickFlyout = new QuickFlyoutWindow(_engine, _profileService, () => ShowAndRestore());
            BuildUI();
            InitializeTray();

            _engine.OnDevicesUpdated += () => Dispatcher.Invoke(new Action(RefreshDeviceViews));
            _engine.OnSessionsUpdated += () => Dispatcher.Invoke(new Action(RefreshSessionViews));

            _engine.Start();
            RefreshDeviceViews();
            RefreshSessionViews();
        }

        private void InitializeWindow()
        {
            Title = "SoundFlow - Control de Sonido Inteligente";
            Width = 620;
            Height = 780;
            MinWidth = 520;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(BgColor);
            Foreground = new SolidColorBrush(TextPrimary);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            BorderThickness = new Thickness(1);
            BorderBrush = new SolidColorBrush(CardBorderColor);
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "SoundFlow - Clic para ajustes rápidos";
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;

            var contextMenu = new System.Windows.Forms.ContextMenu();
            contextMenu.MenuItems.Add("⚡ Ajustes Rápidos", (s, e) => _quickFlyout.ToggleAtTaskbar());
            contextMenu.MenuItems.Add("🗖 Abrir SoundFlow", (s, e) => ShowAndRestore());
            contextMenu.MenuItems.Add("-");

            _trayProfilesMenu = new System.Windows.Forms.MenuItem("Cargar Perfil");
            RefreshTrayMenu();
            contextMenu.MenuItems.Add(_trayProfilesMenu);

            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("Salir", (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _quickFlyout.Close();
                _engine.Stop();
                System.Windows.Application.Current.Shutdown();
            });

            _notifyIcon.ContextMenu = contextMenu;
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    _quickFlyout.ToggleAtTaskbar();
                }
            };
            _notifyIcon.DoubleClick += (s, e) => ShowAndRestore();
        }

        private void RefreshTrayMenu()
        {
            if (_trayProfilesMenu == null) return;
            _trayProfilesMenu.MenuItems.Clear();

            if (_profileService.Profiles.Count == 0)
            {
                var emptyItem = new System.Windows.Forms.MenuItem("(Sin perfiles creados)");
                emptyItem.Enabled = false;
                _trayProfilesMenu.MenuItems.Add(emptyItem);
                return;
            }

            foreach (var p in _profileService.Profiles)
            {
                var profile = p;
                _trayProfilesMenu.MenuItems.Add(profile.Name, (s, e) =>
                {
                    _profileService.ApplyProfile(profile, _engine);
                    ShowStatus("Perfil aplicado: " + profile.Name);
                });
            }
        }

        private void ShowAndRestore()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); // Title Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Profile Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Move All Bar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content Tabs
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Status Footer

            // 1. Custom Title Bar
            rootGrid.Children.Add(CreateTitleBar());
            Grid.SetRow(rootGrid.Children[0], 0);

            // 2. Profile Selection Bar
            var profileBar = CreateProfileBar();
            rootGrid.Children.Add(profileBar);
            Grid.SetRow(profileBar, 1);

            // 3. Move All Quick Bar
            var moveAllBar = CreateMoveAllBar();
            rootGrid.Children.Add(moveAllBar);
            Grid.SetRow(moveAllBar, 2);

            // 4. Tabs for Apps Mixer vs Master Devices
            _tabControl = new TabControl
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(14, 8, 14, 8)
            };

            // Tab 1: Apps Mixer (EarTrumpet Style)
            var mixerTab = new TabItem
            {
                Header = "🎛️ Mezclador por Aplicación",
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            var mixerScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 8, 8, 8)
            };
            _sessionsPanel = new StackPanel();
            mixerScroll.Content = _sessionsPanel;
            mixerTab.Content = mixerScroll;

            // Tab 2: Devices Master
            var devicesTab = new TabItem
            {
                Header = "🔊 Dispositivos de Salida",
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            var devicesScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 8, 8, 8)
            };
            _devicesPanel = new StackPanel();
            devicesScroll.Content = _devicesPanel;
            devicesTab.Content = devicesScroll;

            _tabControl.Items.Add(mixerTab);
            _tabControl.Items.Add(devicesTab);

            rootGrid.Children.Add(_tabControl);
            Grid.SetRow(_tabControl, 3);

            // 5. Status Bar
            var statusBar = CreateStatusBar();
            rootGrid.Children.Add(statusBar);
            Grid.SetRow(statusBar, 4);

            Content = rootGrid;
        }

        private UIElement CreateTitleBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(19, 20, 26)),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 0, 8, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Allow dragging window from title bar
            grid.MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

            // Left icon & title
            var titleStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var iconBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = new LinearGradientBrush(AccentColor, CyanAccentColor, 45),
                Margin = new Thickness(0, 0, 10, 0)
            };
            var iconText = new TextBlock
            {
                Text = "⚡",
                FontSize = 14,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            titleStack.Children.Add(iconBorder);

            var titleText = new TextBlock
            {
                Text = "SoundFlow",
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(titleText);

            var subTitleText = new TextBlock
            {
                Text = " | Audio Router & Mixer",
                FontSize = 12,
                Foreground = new SolidColorBrush(TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 1, 0, 0)
            };
            titleStack.Children.Add(subTitleText);

            grid.Children.Add(titleStack);
            Grid.SetColumn(titleStack, 0);

            // Right action buttons (refresh, minimize, close)
            var actionsStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnRefresh = CreateIconButton("🔄", "Actualizar dispositivos y sesiones", (s, e) =>
            {
                _engine.ScanAll();
                ShowStatus("Dispositivos y aplicaciones actualizados.");
            });
            actionsStack.Children.Add(btnRefresh);

            var btnMin = CreateIconButton("—", "Minimizar a la bandeja del sistema", (s, e) => WindowState = WindowState.Minimized);
            actionsStack.Children.Add(btnMin);

            var btnClose = CreateIconButton("✕", "Cerrar ventana (sigue en bandeja)", (s, e) =>
            {
                Hide();
                _notifyIcon.ShowBalloonTip(1500, "SoundFlow", "SoundFlow sigue activo en segundo plano en la bandeja del sistema.", ToolTipIcon.Info);
            });
            actionsStack.Children.Add(btnClose);

            grid.Children.Add(actionsStack);
            Grid.SetColumn(actionsStack, 2);

            bar.Child = grid;
            return bar;
        }

        private UIElement CreateProfileBar()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBgColor),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(14, 10, 14, 5),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var mainStack = new StackPanel();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "🎯 Perfil de Audio:",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(CyanAccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            topRow.Children.Add(label);
            Grid.SetColumn(label, 0);

            _profileCombo = new System.Windows.Controls.ComboBox
            {
                ItemsSource = _profileService.Profiles,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            SetupHighContrastComboBox(_profileCombo, "Name");

            if (_profileService.Profiles.Count > 0)
            {
                _profileCombo.SelectedIndex = 0;
            }
            _profileCombo.SelectionChanged += ProfileCombo_SelectionChanged;

            topRow.Children.Add(_profileCombo);
            Grid.SetColumn(_profileCombo, 1);

            var buttonsPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

            var btnApply = CreateModernButton("✓ Aplicar", AccentColor, (s, e) =>
            {
                var sel = _profileCombo.SelectedItem as AudioProfile;
                if (sel != null)
                {
                    _profileService.ApplyProfile(sel, _engine);
                    ShowStatus("Perfil aplicado: " + sel.Name);
                }
            });
            btnApply.Margin = new Thickness(0, 0, 6, 0);
            buttonsPanel.Children.Add(btnApply);

            var btnSaveCurrent = CreateModernButton("💾 Guardar Actual...", System.Windows.Media.Color.FromRgb(45, 48, 62), (s, e) =>
            {
                PromptSaveNewProfile();
            });
            btnSaveCurrent.Margin = new Thickness(0, 0, 6, 0);
            buttonsPanel.Children.Add(btnSaveCurrent);

            var btnDelete = CreateIconButton("🗑", "Eliminar perfil seleccionado", (s, e) =>
            {
                var sel = _profileCombo.SelectedItem as AudioProfile;
                if (sel != null)
                {
                    var res = System.Windows.MessageBox.Show("¿Seguro que deseas eliminar el perfil '" + sel.Name + "'?", "Eliminar Perfil", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        _profileService.DeleteProfile(sel);
                        ShowStatus("Perfil eliminado.");
                        if (_profileService.Profiles.Count > 0)
                        {
                            _profileCombo.SelectedIndex = 0;
                        }
                        else
                        {
                            _profileCombo.SelectedIndex = -1;
                        }
                        UpdateProfileDescription();
                        RefreshTrayMenu();
                    }
                }
                else
                {
                    ShowStatus("No hay ningún perfil seleccionado.");
                }
            });
            buttonsPanel.Children.Add(btnDelete);

            topRow.Children.Add(buttonsPanel);
            Grid.SetColumn(buttonsPanel, 2);

            mainStack.Children.Add(topRow);

            _profileDescText = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(2, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            UpdateProfileDescription();
            mainStack.Children.Add(_profileDescText);

            card.Child = mainStack;
            return card;
        }

        private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProfileDescription();
        }

        private void UpdateProfileDescription()
        {
            var sel = _profileCombo.SelectedItem as AudioProfile;
            if (sel != null)
            {
                _profileDescText.Text = string.IsNullOrEmpty(sel.Description) ? "Sin descripción adicional" : sel.Description;
            }
            else
            {
                _profileDescText.Text = "No hay perfiles creados. Ajusta el audio de tus aplicaciones y presiona '💾 Guardar Actual...' para crear uno a tu gusto.";
            }
        }

        private void PromptSaveNewProfile()
        {
            var win = new Window
            {
                Title = "Guardar Perfil Actual",
                Width = 380,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(BgColor),
                Foreground = new SolidColorBrush(TextPrimary),
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(18) };

            stack.Children.Add(new TextBlock
            {
                Text = "Nombre del Nuevo Perfil:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var txtName = new System.Windows.Controls.TextBox
            {
                Text = "Mi Perfil Personalizado",
                Height = 30,
                Background = System.Windows.Media.Brushes.White,
                Foreground = System.Windows.Media.Brushes.Black,
                CaretBrush = System.Windows.Media.Brushes.Black,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                Padding = new Thickness(6, 4, 6, 4),
                FontWeight = FontWeights.SemiBold
            };
            stack.Children.Add(txtName);

            stack.Children.Add(new TextBlock
            {
                Text = "Descripción (opcional):",
                Margin = new Thickness(0, 10, 0, 6),
                Foreground = new SolidColorBrush(TextSecondary)
            });

            var txtDesc = new System.Windows.Controls.TextBox
            {
                Text = "Guarda las rutas y volúmenes actuales de mis aplicaciones",
                Height = 30,
                Background = System.Windows.Media.Brushes.White,
                Foreground = System.Windows.Media.Brushes.Black,
                CaretBrush = System.Windows.Media.Brushes.Black,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
                Padding = new Thickness(6, 4, 6, 4)
            };
            stack.Children.Add(txtDesc);

            var btnRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };

            var btnCancel = CreateModernButton("Cancelar", System.Windows.Media.Color.FromRgb(40, 42, 54), (s, e) => win.Close());
            btnCancel.Margin = new Thickness(0, 0, 8, 0);
            btnRow.Children.Add(btnCancel);

            var btnSave = CreateModernButton("Guardar", AccentColor, (s, e) =>
            {
                string name = txtName.Text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    var created = _profileService.CreateProfileFromCurrent(name, txtDesc.Text.Trim(), _engine);
                    _profileCombo.SelectedItem = created;
                    UpdateProfileDescription();
                    RefreshTrayMenu();
                    ShowStatus("Perfil '" + name + "' guardado exitosamente.");
                    win.Close();
                }
            });
            btnRow.Children.Add(btnSave);

            stack.Children.Add(btnRow);
            win.Content = stack;
            win.ShowDialog();
        }

        private UIElement CreateMoveAllBar()
        {
            var card = new Border
            {
                Background = new LinearGradientBrush(
                    System.Windows.Media.Color.FromRgb(22, 24, 34),
                    System.Windows.Media.Color.FromRgb(28, 30, 42),
                    0),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 70, 229)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(14, 5, 14, 8),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "🔀 Mover todo a:",
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            grid.Children.Add(label);
            Grid.SetColumn(label, 0);

            _moveAllCombo = new System.Windows.Controls.ComboBox
            {
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            SetupHighContrastComboBox(_moveAllCombo, "DisplayName");
            _moveAllCombo.SelectionChanged += MoveAllCombo_SelectionChanged;
            grid.Children.Add(_moveAllCombo);
            Grid.SetColumn(_moveAllCombo, 1);

            var actionsPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnMoveAll = CreateModernButton("⚡ Mover Todo", AccentColor, (s, e) =>
            {
                var sel = _moveAllCombo.SelectedItem as DeviceComboItem;
                if (sel != null)
                {
                    ExecuteMoveAll(sel);
                }
            });
            btnMoveAll.Margin = new Thickness(0, 0, 8, 0);
            actionsPanel.Children.Add(btnMoveAll);

            _chkSetDefault = new System.Windows.Controls.CheckBox
            {
                Content = "También como Predeterminado",
                IsChecked = false,
                Foreground = new SolidColorBrush(TextSecondary),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            actionsPanel.Children.Add(_chkSetDefault);

            grid.Children.Add(actionsPanel);
            Grid.SetColumn(actionsPanel, 2);

            card.Child = grid;
            return card;
        }

        private void MoveAllCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressMoveAllAutoRoute) return;
            var sel = _moveAllCombo.SelectedItem as DeviceComboItem;
            if (sel != null)
            {
                ExecuteMoveAll(sel);
            }
        }

        private void ExecuteMoveAll(DeviceComboItem sel)
        {
            if (sel == null) return;
            _engine.RouteAllSessions(sel.DeviceId);

            if (_chkSetDefault != null && _chkSetDefault.IsChecked == true && sel.DeviceId != "DEFAULT")
            {
                foreach (var d in _engine.Devices)
                {
                    if (d.Id == sel.DeviceId)
                    {
                        _engine.SetDefaultDevice(d);
                        break;
                    }
                }
            }

            ShowStatus("Todas las aplicaciones se han movido a: " + sel.DisplayName);
        }

        private UIElement CreateStatusBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 13, 17)),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 0, 14, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusMessageText = new TextBlock
            {
                Text = "SoundFlow listo | Administrador de volumen y enrutamiento activo",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(_statusMessageText);
            Grid.SetColumn(_statusMessageText, 0);

            _activeAppsCountText = new TextBlock
            {
                Text = "0 Aplicaciones",
                FontSize = 11,
                Foreground = new SolidColorBrush(CyanAccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            grid.Children.Add(_activeAppsCountText);
            Grid.SetColumn(_activeAppsCountText, 1);

            bar.Child = grid;
            return bar;
        }

        private void ShowStatus(string msg)
        {
            _statusMessageText.Text = msg;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _statusMessageText.Text = "SoundFlow listo | Administrador de volumen y enrutamiento activo";
            };
            timer.Start();
        }

        public void RefreshDeviceViews()
        {
            _suppressMoveAllAutoRoute = true;
            try
            {
                // Populate Move All Combo with VISIBLE devices only
                var moveList = new List<DeviceComboItem>();
                moveList.Add(new DeviceComboItem { DeviceId = "DEFAULT", DisplayName = "📌 Dispositivo Predeterminado de Windows" });
                foreach (var dev in _engine.GetVisibleDevices())
                {
                    moveList.Add(new DeviceComboItem
                    {
                        DeviceId = dev.Id,
                        DisplayName = (dev.IsDefault ? "⭐ " : "🔊 ") + dev.Name
                    });
                }

                string currentSelId = (_moveAllCombo.SelectedItem as DeviceComboItem) != null
                    ? (_moveAllCombo.SelectedItem as DeviceComboItem).DeviceId
                    : null;

                _moveAllCombo.ItemsSource = moveList;

                int targetIndex = 0;
                if (!string.IsNullOrEmpty(currentSelId))
                {
                    for (int i = 0; i < moveList.Count; i++)
                    {
                        if (moveList[i].DeviceId == currentSelId) { targetIndex = i; break; }
                    }
                }
                if (moveList.Count > 0)
                {
                    _moveAllCombo.SelectedIndex = targetIndex;
                }

                // Populate Devices Tab Panel
                _devicesPanel.Children.Clear();

                int hiddenCount = 0;
                foreach (var d in _engine.Devices) { if (d.IsHidden) hiddenCount++; }

                // Guidance & Options Card
                var topInfoCard = new Border
                {
                    Background = new SolidColorBrush(CardBgColor),
                    BorderBrush = new SolidColorBrush(CardBorderColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(12, 10, 12, 10)
                };

                var topInfoGrid = new Grid();
                topInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                topInfoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel();
                infoStack.Children.Add(new TextBlock
                {
                    Text = "💡 Gestión de Dispositivos y Periféricos",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(CyanAccentColor),
                    Margin = new Thickness(0, 0, 0, 3)
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = "Usa ▲ / ▼ para ordenar la prioridad de tus dispositivos. Usa 👁️ para ocultar los que no uses. En Windows siempre hay un predeterminado: para cambiarlo haz clic en '⭐ Hacer Predeterminado' en el auricular o altavoz que desees.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(TextSecondary),
                    TextWrapping = TextWrapping.Wrap
                });
                topInfoGrid.Children.Add(infoStack);
                Grid.SetColumn(infoStack, 0);

                var chkHidden = new System.Windows.Controls.CheckBox
                {
                    Content = string.Format("Mostrar ocultos ({0})", hiddenCount),
                    IsChecked = _showHiddenDevices,
                    Foreground = new SolidColorBrush(TextPrimary),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                chkHidden.Click += (s, e) =>
                {
                    _showHiddenDevices = (chkHidden.IsChecked == true);
                    RefreshDeviceViews();
                };
                topInfoGrid.Children.Add(chkHidden);
                Grid.SetColumn(chkHidden, 1);

                topInfoCard.Child = topInfoGrid;
                _devicesPanel.Children.Add(topInfoCard);

                foreach (var dev in _engine.Devices)
                {
                    if (dev.IsHidden && !_showHiddenDevices) continue;
                    _devicesPanel.Children.Add(CreateDeviceCard(dev));
                }
            }
            finally
            {
                _suppressMoveAllAutoRoute = false;
            }
        }

        public void RefreshSessionViews()
        {
            _sessionsPanel.Children.Clear();
            _activeAppsCountText.Text = _engine.Sessions.Count + " Apps Activas";

            if (_engine.Sessions.Count == 0)
            {
                var emptyCard = new Border
                {
                    Background = new SolidColorBrush(CardBgColor),
                    BorderBrush = new SolidColorBrush(CardBorderColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(24),
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var emptyStack = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "🔇 No hay aplicaciones reproduciendo audio en este momento.",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(TextSecondary),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Abre un juego, Discord, Spotify o tu navegador para comenzar a controlar su audio y redirigirlo.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(TextSecondary),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0)
                });
                emptyCard.Child = emptyStack;
                _sessionsPanel.Children.Add(emptyCard);
                return;
            }

            foreach (var session in _engine.Sessions)
            {
                _sessionsPanel.Children.Add(CreateSessionCard(session));
            }
        }

        private UIElement CreateSessionCard(AudioSessionItem session)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBgColor),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(14, 12, 14, 12)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Icon, Name, Device Route Dropdown)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Sliders & Meters

            // Row 0: App info & Output Routing Dropdown
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // App Icon
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name + PID
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Routing Dropdown

            // Icon
            var iconImg = new System.Windows.Controls.Image
            {
                Source = session.Icon,
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(iconImg);
            Grid.SetColumn(iconImg, 0);

            // Name + PID + Current Physical Device
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameBlock = new TextBlock
            {
                Text = session.DisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(TextPrimary)
            };
            titleStack.Children.Add(nameBlock);

            string currentDevName = "Desconocido";
            foreach (var d in _engine.Devices)
            {
                if (d.Id == session.CurrentDeviceId) { currentDevName = d.Name; break; }
            }

            var subBlock = new TextBlock
            {
                Text = string.Format("PID: {0} | Sonando en: {1}", session.ProcessId, currentDevName),
                FontSize = 11,
                Foreground = new SolidColorBrush(TextSecondary),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(subBlock);

            headerGrid.Children.Add(titleStack);
            Grid.SetColumn(titleStack, 1);

            // Output Routing Dropdown (The EarTrumpet Killer Feature!)
            var routeStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            routeStack.Children.Add(new TextBlock
            {
                Text = "Salida:",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(CyanAccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            var devCombo = new System.Windows.Controls.ComboBox
            {
                Width = 210,
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            SetupHighContrastComboBox(devCombo, "DisplayName");

            var devItems = new List<DeviceComboItem>();
            devItems.Add(new DeviceComboItem { DeviceId = "DEFAULT", DisplayName = "📌 Por Defecto del Sistema" });
            foreach (var d in _engine.GetVisibleDevices())
            {
                devItems.Add(new DeviceComboItem
                {
                    DeviceId = d.Id,
                    DisplayName = (d.IsDefault ? "⭐ " : "🔊 ") + d.Name
                });
            }

            if (session.SelectedDeviceId != null && session.SelectedDeviceId != "DEFAULT")
            {
                bool found = false;
                foreach (var it in devItems) { if (it.DeviceId == session.SelectedDeviceId) { found = true; break; } }
                if (!found)
                {
                    foreach (var d in _engine.Devices)
                    {
                        if (d.Id == session.SelectedDeviceId)
                        {
                            devItems.Add(new DeviceComboItem { DeviceId = d.Id, DisplayName = "👁️ (Oculto) " + d.Name });
                            break;
                        }
                    }
                }
            }

            devCombo.ItemsSource = devItems;

            // Set current selected
            int selIndex = 0;
            for (int i = 0; i < devItems.Count; i++)
            {
                if (devItems[i].DeviceId == session.SelectedDeviceId)
                {
                    selIndex = i;
                    break;
                }
            }
            devCombo.SelectedIndex = selIndex;

            devCombo.SelectionChanged += (s, e) =>
            {
                var item = devCombo.SelectedItem as DeviceComboItem;
                if (item != null && item.DeviceId != session.SelectedDeviceId)
                {
                    bool ok = _engine.RouteSession(session, item.DeviceId);
                    if (ok)
                    {
                        ShowStatus(string.Format("Audio de '{0}' redirigido a '{1}'", session.DisplayName, item.DisplayName));
                    }
                }
            };

            routeStack.Children.Add(devCombo);
            headerGrid.Children.Add(routeStack);
            Grid.SetColumn(routeStack, 2);

            mainGrid.Children.Add(headerGrid);
            Grid.SetRow(headerGrid, 0);

            // Row 1: Volume Slider, Numeric Badge, Mute Button & Live Peak Meter
            var volGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Mute Button
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Slider + Meter
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Volume % Text

            // Mute Button
            var btnMute = new System.Windows.Controls.Button
            {
                Content = session.IsMuted ? "🔇" : "🔊",
                Width = 34,
                Height = 30,
                FontSize = 13,
                Background = new SolidColorBrush(session.IsMuted ? DangerColor : System.Windows.Media.Color.FromRgb(35, 38, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnMute.Click += (s, e) =>
            {
                bool newMute = !session.IsMuted;
                _engine.SetSessionMute(session, newMute);
                btnMute.Content = newMute ? "🔇" : "🔊";
                btnMute.Background = new SolidColorBrush(newMute ? DangerColor : System.Windows.Media.Color.FromRgb(35, 38, 50));
            };
            volGrid.Children.Add(btnMute);
            Grid.SetColumn(btnMute, 0);

            // Center: Slider with Live Peak Meter underneath
            var sliderMeterStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var volSlider = new System.Windows.Controls.Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = session.Volume,
                Height = 22,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var volText = new TextBlock
            {
                Text = session.Volume + "%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            volSlider.ValueChanged += (s, e) =>
            {
                int val = (int)Math.Round(volSlider.Value);
                _engine.SetSessionVolume(session, val);
                volText.Text = val + "%";
            };

            // Live Peak Audio Meter
            var meterBar = new System.Windows.Controls.ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Height = 4,
                Foreground = new LinearGradientBrush(CyanAccentColor, System.Windows.Media.Color.FromRgb(34, 197, 94), 0),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 32, 42)),
                BorderThickness = new Thickness(0)
            };

            // Bind ProgressBar Value to session.PeakLevel
            var meterBinding = new System.Windows.Data.Binding("PeakLevel")
            {
                Source = session,
                Mode = BindingMode.OneWay
            };
            meterBar.SetBinding(System.Windows.Controls.ProgressBar.ValueProperty, meterBinding);

            sliderMeterStack.Children.Add(volSlider);
            sliderMeterStack.Children.Add(meterBar);

            volGrid.Children.Add(sliderMeterStack);
            Grid.SetColumn(sliderMeterStack, 1);

            volGrid.Children.Add(volText);
            Grid.SetColumn(volText, 2);

            mainGrid.Children.Add(volGrid);
            Grid.SetRow(volGrid, 1);

            card.Child = mainGrid;
            return card;
        }

        private UIElement CreateDeviceCard(AudioDeviceItem device)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBgColor),
                BorderBrush = new SolidColorBrush(device.IsDefault ? AccentColor : CardBorderColor),
                BorderThickness = new Thickness(device.IsDefault ? 1.5 : 1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(14, 12, 14, 12)
            };

            if (device.IsHidden)
            {
                card.Opacity = 0.65;
            }

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header: Name + Actions (Reorder, Hide/Show, Set Default)
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "🔊 ",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });

            var nameBlock = new TextBlock
            {
                Text = device.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(nameBlock);

            if (device.IsHidden)
            {
                var hiddenBadge = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 239, 68, 68)),
                    BorderBrush = new SolidColorBrush(DangerColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0)
                };
                hiddenBadge.Child = new TextBlock
                {
                    Text = "👁️ OCULTO",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(DangerColor)
                };
                titleStack.Children.Add(hiddenBadge);
            }

            headerGrid.Children.Add(titleStack);
            Grid.SetColumn(titleStack, 0);

            var rightPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Reorder Up
            var btnUp = CreateIconButton("▲", "Subir prioridad en las listas", (s, e) =>
            {
                _engine.MoveDeviceOrder(device, -1);
            });
            rightPanel.Children.Add(btnUp);

            // Reorder Down
            var btnDown = CreateIconButton("▼", "Bajar prioridad en las listas", (s, e) =>
            {
                _engine.MoveDeviceOrder(device, 1);
            });
            btnDown.Margin = new Thickness(0, 0, 8, 0);
            rightPanel.Children.Add(btnDown);

            // Hide / Show toggle
            if (device.IsHidden)
            {
                var btnShow = CreateModernButton("👁️ Mostrar", System.Windows.Media.Color.FromRgb(34, 197, 94), (s, e) =>
                {
                    _engine.SetDeviceHidden(device, false);
                    ShowStatus("Dispositivo visible: " + device.Name);
                });
                btnShow.Margin = new Thickness(0, 0, 8, 0);
                rightPanel.Children.Add(btnShow);
            }
            else
            {
                var btnHide = CreateIconButton("👁️", "Ocultar este dispositivo de las listas", (s, e) =>
                {
                    _engine.SetDeviceHidden(device, true);
                    ShowStatus("Dispositivo ocultado: " + device.Name);
                });
                btnHide.Margin = new Thickness(0, 0, 8, 0);
                rightPanel.Children.Add(btnHide);
            }

            // Default Status or Button
            if (device.IsDefault)
            {
                var defBadge = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 99, 102, 241)),
                    BorderBrush = new SolidColorBrush(AccentColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3)
                };
                defBadge.Child = new TextBlock
                {
                    Text = "⭐ PREDETERMINADO",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(AccentHoverColor)
                };
                rightPanel.Children.Add(defBadge);
            }
            else
            {
                var btnSetDefault = CreateModernButton("⭐ Hacer Predeterminado", System.Windows.Media.Color.FromRgb(40, 43, 56), (s, e) =>
                {
                    _engine.SetDefaultDevice(device);
                    ShowStatus("Dispositivo predeterminado cambiado a: " + device.Name);
                });
                rightPanel.Children.Add(btnSetDefault);
            }

            headerGrid.Children.Add(rightPanel);
            Grid.SetColumn(rightPanel, 1);

            mainGrid.Children.Add(headerGrid);
            Grid.SetRow(headerGrid, 0);

            // Row 1: Master Volume Slider & Mute
            var volGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

            var btnMute = new System.Windows.Controls.Button
            {
                Content = device.IsMuted ? "🔇" : "🔊",
                Width = 34,
                Height = 30,
                FontSize = 13,
                Background = new SolidColorBrush(device.IsMuted ? DangerColor : System.Windows.Media.Color.FromRgb(35, 38, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnMute.Click += (s, e) =>
            {
                bool newMute = !device.IsMuted;
                _engine.SetDeviceMute(device, newMute);
                btnMute.Content = newMute ? "🔇" : "🔊";
                btnMute.Background = new SolidColorBrush(newMute ? DangerColor : System.Windows.Media.Color.FromRgb(35, 38, 50));
            };
            volGrid.Children.Add(btnMute);
            Grid.SetColumn(btnMute, 0);

            var volSlider = new System.Windows.Controls.Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = device.Volume,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };

            var volText = new TextBlock
            {
                Text = device.Volume + "%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            volSlider.ValueChanged += (s, e) =>
            {
                int val = (int)Math.Round(volSlider.Value);
                _engine.SetDeviceVolume(device, val);
                volText.Text = val + "%";
            };

            volGrid.Children.Add(volSlider);
            Grid.SetColumn(volSlider, 1);

            volGrid.Children.Add(volText);
            Grid.SetColumn(volText, 2);

            mainGrid.Children.Add(volGrid);
            Grid.SetRow(volGrid, 1);

            card.Child = mainGrid;
            return card;
        }

        private System.Windows.Controls.Button CreateModernButton(string text, System.Windows.Media.Color bgColor, RoutedEventHandler clickHandler)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = text,
                Height = 30,
                Padding = new Thickness(12, 0, 12, 0),
                Background = new SolidColorBrush(bgColor),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var style = new Style(typeof(System.Windows.Controls.Button));
            var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(System.Windows.Controls.Button.PaddingProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;
            style.Setters.Add(new Setter(System.Windows.Controls.Button.TemplateProperty, template));
            btn.Style = style;

            btn.Click += clickHandler;
            return btn;
        }

        private System.Windows.Controls.Button CreateIconButton(string icon, string tooltip, RoutedEventHandler clickHandler)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content = icon,
                Width = 28,
                Height = 28,
                Margin = new Thickness(2, 0, 2, 0),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new SolidColorBrush(TextSecondary),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                ToolTip = tooltip,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            btn.MouseEnter += (s, e) => { btn.Foreground = System.Windows.Media.Brushes.White; btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)); };
            btn.MouseLeave += (s, e) => { btn.Foreground = new SolidColorBrush(TextSecondary); btn.Background = System.Windows.Media.Brushes.Transparent; };

            btn.Click += clickHandler;
            return btn;
        }

        private void SetupHighContrastComboBox(System.Windows.Controls.ComboBox combo, string displayPropertyName)
        {
            combo.Background = System.Windows.Media.Brushes.White;
            combo.Foreground = System.Windows.Media.Brushes.Black;
            combo.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
            combo.BorderThickness = new Thickness(1);
            combo.FontWeight = FontWeights.Bold;
            combo.FontSize = 12;
            combo.Padding = new Thickness(6, 2, 6, 2);

            // ItemContainerStyle for dropdown list items: white background and black text
            var itemStyle = new Style(typeof(System.Windows.Controls.ComboBoxItem));
            itemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, System.Windows.Media.Brushes.White));
            itemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.FontWeightProperty, FontWeights.SemiBold));
            itemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.FontSizeProperty, 12.0));
            itemStyle.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));

            var hoverTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 231, 255))));
            hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemStyle.Triggers.Add(hoverTrigger);

            var selTrigger = new Trigger { Property = System.Windows.Controls.ComboBoxItem.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 210, 254))));
            selTrigger.Setters.Add(new Setter(System.Windows.Controls.ComboBoxItem.ForegroundProperty, System.Windows.Media.Brushes.Black));
            itemStyle.Triggers.Add(selTrigger);

            combo.ItemContainerStyle = itemStyle;

            // ItemTemplate explicitly binding to displayPropertyName with pure Black text
            if (!string.IsNullOrEmpty(displayPropertyName))
            {
                var template = new DataTemplate();
                var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
                tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(displayPropertyName));
                tbFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Black);
                tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                tbFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
                tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                template.VisualTree = tbFactory;
                combo.ItemTemplate = template;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Minimize to tray on close
            e.Cancel = true;
            Hide();
            _notifyIcon.ShowBalloonTip(1500, "SoundFlow", "SoundFlow sigue ejecutándose en segundo plano. Haz clic en el icono para ajustes rápidos.", ToolTipIcon.Info);
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
}
