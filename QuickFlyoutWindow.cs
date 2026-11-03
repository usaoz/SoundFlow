using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SoundFlow
{
    public class QuickFlyoutWindow : Window
    {
        private readonly AudioEngine _engine;
        private readonly ProfileService _profileService;
        private readonly Action _openMainWindow;

        private ComboBox _profileCombo;
        private ComboBox _moveAllCombo;
        private StackPanel _appsPanel;
        private TextBlock _noAppsText;
        private bool _suppressAutoRoute = false;

        // Colors matching SoundFlow theme
        private readonly Color BgColor = Color.FromRgb(16, 17, 23);
        private readonly Color CardBgColor = Color.FromRgb(24, 25, 34);
        private readonly Color CardBorderColor = Color.FromRgb(45, 48, 64);
        private readonly Color AccentColor = Color.FromRgb(99, 102, 241);
        private readonly Color CyanAccentColor = Color.FromRgb(6, 182, 212);
        private readonly Color TextPrimary = Color.FromRgb(243, 244, 246);
        private readonly Color TextSecondary = Color.FromRgb(156, 163, 175);
        private readonly Color DangerColor = Color.FromRgb(239, 68, 68);

        public QuickFlyoutWindow(AudioEngine engine, ProfileService profileService, Action openMainWindow)
        {
            _engine = engine;
            _profileService = profileService;
            _openMainWindow = openMainWindow;

            InitializeFlyoutWindow();
            BuildUI();

            _engine.OnSessionsUpdated += () => Dispatcher.Invoke(new Action(RefreshApps));
            _engine.OnDevicesUpdated += () => Dispatcher.Invoke(new Action(RefreshDevicesAndProfiles));
        }

        private void InitializeFlyoutWindow()
        {
            Title = "SoundFlow Rápido";
            Width = 380;
            Height = 500;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(BgColor);
            Foreground = new SolidColorBrush(TextPrimary);
            BorderThickness = new Thickness(1.5);
            BorderBrush = new SolidColorBrush(Color.FromRgb(79, 70, 229));

            Deactivated += (s, e) => Hide();
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Quick Controls Card (Profiles & Move All)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Apps Mixer ScrollViewer

            // 1. Header Bar
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 21, 29)),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 8, 8)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var iconBlock = new TextBlock { Text = "⚡", FontSize = 14, Margin = new Thickness(0, 0, 6, 0) };
            var titleBlock = new TextBlock { Text = "SoundFlow Rápido", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = Brushes.White };
            titleStack.Children.Add(iconBlock);
            titleStack.Children.Add(titleBlock);
            headerGrid.Children.Add(titleStack);
            Grid.SetColumn(titleStack, 0);

            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal };

            var btnOpenFull = new Button
            {
                Content = "🗖 Abrir SoundFlow",
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(40, 44, 60)),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnOpenFull.Click += (s, e) =>
            {
                Hide();
                if (_openMainWindow != null) _openMainWindow();
            };
            rightButtons.Children.Add(btnOpenFull);

            var btnClose = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(TextSecondary),
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => Hide();
            rightButtons.Children.Add(btnClose);

            headerGrid.Children.Add(rightButtons);
            Grid.SetColumn(rightButtons, 1);

            headerBorder.Child = headerGrid;
            mainGrid.Children.Add(headerBorder);
            Grid.SetRow(headerBorder, 0);

            // 2. Quick Actions Card (Profile switcher & Mover todo a)
            var quickCard = new Border
            {
                Background = new SolidColorBrush(CardBgColor),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10, 8, 10, 6),
                Padding = new Thickness(10)
            };

            var quickStack = new StackPanel();

            // Row A: Perfiles
            var profRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            profRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            profRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            profRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var profLabel = new TextBlock
            {
                Text = "🎯 Perfil:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(CyanAccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            profRow.Children.Add(profLabel);
            Grid.SetColumn(profLabel, 0);

            _profileCombo = new ComboBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            SetupHighContrastComboBox(_profileCombo, "Name");
            profRow.Children.Add(_profileCombo);
            Grid.SetColumn(_profileCombo, 1);

            var btnApplyProfile = new Button
            {
                Content = "✓ Aplicar",
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(AccentColor),
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnApplyProfile.Click += (s, e) =>
            {
                var sel = _profileCombo.SelectedItem as AudioProfile;
                if (sel != null)
                {
                    _profileService.ApplyProfile(sel, _engine);
                }
            };
            profRow.Children.Add(btnApplyProfile);
            Grid.SetColumn(btnApplyProfile, 2);

            quickStack.Children.Add(profRow);

            // Row B: Mover todo a
            var moveRow = new Grid();
            moveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            moveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var moveLabel = new TextBlock
            {
                Text = "🔀 Mover a:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            moveRow.Children.Add(moveLabel);
            Grid.SetColumn(moveLabel, 0);

            _moveAllCombo = new ComboBox
            {
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            SetupHighContrastComboBox(_moveAllCombo, "DisplayName");
            _moveAllCombo.SelectionChanged += (s, e) =>
            {
                if (_suppressAutoRoute) return;
                var sel = _moveAllCombo.SelectedItem as DeviceComboItem;
                if (sel != null)
                {
                    _engine.RouteAllSessions(sel.DeviceId);
                }
            };
            moveRow.Children.Add(_moveAllCombo);
            Grid.SetColumn(_moveAllCombo, 1);

            quickStack.Children.Add(moveRow);

            quickCard.Child = quickStack;
            mainGrid.Children.Add(quickCard);
            Grid.SetRow(quickCard, 1);

            // 3. Active Applications Mixer
            var mixerBox = new Border
            {
                Margin = new Thickness(10, 0, 10, 10)
            };

            var mixerGrid = new Grid();
            mixerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mixerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var mixerHeader = new TextBlock
            {
                Text = "🎛️ Mezclador por Aplicación",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextSecondary),
                Margin = new Thickness(2, 4, 0, 6)
            };
            mixerGrid.Children.Add(mixerHeader);
            Grid.SetRow(mixerHeader, 0);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _appsPanel = new StackPanel();
            scroll.Content = _appsPanel;

            _noAppsText = new TextBlock
            {
                Text = "No hay aplicaciones reproduciendo audio en este momento.",
                FontSize = 11,
                Foreground = new SolidColorBrush(TextSecondary),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 20, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            mixerGrid.Children.Add(_noAppsText);
            Grid.SetRow(_noAppsText, 1);

            mixerGrid.Children.Add(scroll);
            Grid.SetRow(scroll, 1);

            mixerBox.Child = mixerGrid;
            mainGrid.Children.Add(mixerBox);
            Grid.SetRow(mixerBox, 2);

            Content = mainGrid;
        }

        public void ToggleAtTaskbar()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                RefreshDevicesAndProfiles();
                RefreshApps();

                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 12;
                Top = workArea.Bottom - Height - 12;

                Show();
                Activate();
            }
        }

        public void RefreshDevicesAndProfiles()
        {
            // Profiles
            _profileCombo.ItemsSource = null;
            _profileCombo.ItemsSource = _profileService.Profiles;
            if (_profileService.Profiles.Count > 0 && _profileCombo.SelectedIndex < 0)
            {
                _profileCombo.SelectedIndex = 0;
            }

            // Move all devices (visible only)
            _suppressAutoRoute = true;
            try
            {
                var moveList = new List<DeviceComboItem>();
                moveList.Add(new DeviceComboItem { DeviceId = "DEFAULT", DisplayName = "📌 Por Defecto de Windows" });
                foreach (var d in _engine.GetVisibleDevices())
                {
                    moveList.Add(new DeviceComboItem
                    {
                        DeviceId = d.Id,
                        DisplayName = (d.IsDefault ? "⭐ " : "🔊 ") + d.Name
                    });
                }

                string prevSelId = (_moveAllCombo.SelectedItem as DeviceComboItem) != null
                    ? (_moveAllCombo.SelectedItem as DeviceComboItem).DeviceId
                    : null;

                _moveAllCombo.ItemsSource = moveList;

                int targetIdx = 0;
                if (!string.IsNullOrEmpty(prevSelId))
                {
                    for (int i = 0; i < moveList.Count; i++)
                    {
                        if (moveList[i].DeviceId == prevSelId) { targetIdx = i; break; }
                    }
                }
                if (moveList.Count > 0) _moveAllCombo.SelectedIndex = targetIdx;
            }
            finally
            {
                _suppressAutoRoute = false;
            }
        }

        public void RefreshApps()
        {
            _appsPanel.Children.Clear();

            if (_engine.Sessions.Count == 0)
            {
                _noAppsText.Visibility = Visibility.Visible;
                return;
            }

            _noAppsText.Visibility = Visibility.Collapsed;

            foreach (var session in _engine.Sessions)
            {
                _appsPanel.Children.Add(CreateMiniAppCard(session));
            }
        }

        private UIElement CreateMiniAppCard(AudioSessionItem session)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBgColor),
                BorderBrush = new SolidColorBrush(CardBorderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(8, 6, 8, 6)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Icon, Name, Output Route)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Slider + Mute + %

            // Row 0: App info & quick device selector
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconImg = new Image
            {
                Source = session.Icon,
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            topGrid.Children.Add(iconImg);
            Grid.SetColumn(iconImg, 0);

            var nameBlock = new TextBlock
            {
                Text = session.DisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            topGrid.Children.Add(nameBlock);
            Grid.SetColumn(nameBlock, 1);

            // Mini routing dropdown for this app
            var routeCombo = new ComboBox
            {
                Width = 140,
                Height = 24,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            SetupHighContrastComboBox(routeCombo, "DisplayName");

            var devList = new List<DeviceComboItem>();
            devList.Add(new DeviceComboItem { DeviceId = "DEFAULT", DisplayName = "📌 Por Defecto" });
            foreach (var d in _engine.GetVisibleDevices())
            {
                devList.Add(new DeviceComboItem
                {
                    DeviceId = d.Id,
                    DisplayName = (d.IsDefault ? "⭐ " : "🔊 ") + d.Name
                });
            }
            routeCombo.ItemsSource = devList;

            int selIndex = 0;
            for (int i = 0; i < devList.Count; i++)
            {
                if (devList[i].DeviceId == session.SelectedDeviceId) { selIndex = i; break; }
            }
            routeCombo.SelectedIndex = selIndex;

            routeCombo.SelectionChanged += (s, e) =>
            {
                var item = routeCombo.SelectedItem as DeviceComboItem;
                if (item != null && item.DeviceId != session.SelectedDeviceId)
                {
                    _engine.RouteSession(session, item.DeviceId);
                }
            };

            topGrid.Children.Add(routeCombo);
            Grid.SetColumn(routeCombo, 2);

            grid.Children.Add(topGrid);
            Grid.SetRow(topGrid, 0);

            // Row 1: Volume Slider & Mute
            var volGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var btnMute = new Button
            {
                Content = session.IsMuted ? "🔇" : "🔊",
                Width = 26,
                Height = 24,
                FontSize = 11,
                Background = new SolidColorBrush(session.IsMuted ? DangerColor : Color.FromRgb(36, 39, 52)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnMute.Click += (s, e) =>
            {
                bool newMute = !session.IsMuted;
                _engine.SetSessionMute(session, newMute);
                btnMute.Content = newMute ? "🔇" : "🔊";
                btnMute.Background = new SolidColorBrush(newMute ? DangerColor : Color.FromRgb(36, 39, 52));
            };
            volGrid.Children.Add(btnMute);
            Grid.SetColumn(btnMute, 0);

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = session.Volume,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center
            };

            var volText = new TextBlock
            {
                Text = session.Volume + "%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            slider.ValueChanged += (s, e) =>
            {
                int val = (int)Math.Round(slider.Value);
                _engine.SetSessionVolume(session, val);
                volText.Text = val + "%";
            };

            volGrid.Children.Add(slider);
            Grid.SetColumn(slider, 1);

            volGrid.Children.Add(volText);
            Grid.SetColumn(volText, 2);

            grid.Children.Add(volGrid);
            Grid.SetRow(volGrid, 1);

            card.Child = grid;
            return card;
        }

        private void SetupHighContrastComboBox(ComboBox combo, string displayPropertyName)
        {
            combo.Background = Brushes.White;
            combo.Foreground = Brushes.Black;
            combo.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            combo.BorderThickness = new Thickness(1);
            combo.FontWeight = FontWeights.Bold;
            combo.FontSize = 11;
            combo.Padding = new Thickness(4, 1, 4, 1);

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.Black));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.FontWeightProperty, FontWeights.SemiBold));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.FontSizeProperty, 11.0));
            itemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(6, 4, 6, 4)));

            var hoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(224, 231, 255))));
            hoverTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.Black));
            itemStyle.Triggers.Add(hoverTrigger);

            var selTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(199, 210, 254))));
            selTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.Black));
            itemStyle.Triggers.Add(selTrigger);

            combo.ItemContainerStyle = itemStyle;

            if (!string.IsNullOrEmpty(displayPropertyName))
            {
                var template = new DataTemplate();
                var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
                tbFactory.SetBinding(TextBlock.TextProperty, new Binding(displayPropertyName));
                tbFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Black);
                tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
                tbFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
                tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                template.VisualTree = tbFactory;
                combo.ItemTemplate = template;
            }
        }
    }
}
