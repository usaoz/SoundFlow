using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SoundFlow
{
    public class AudioDeviceItem : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _rawName;
        private string _categoryTag;
        private string _cleanName;
        private string _customName;
        private bool _isDefault;
        private int _volume;
        private bool _isMuted;
        private bool _isHidden;
        private int _order;

        public string Id
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged("Id"); }
        }

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged("Name"); }
        }

        public string RawName
        {
            get { return _rawName; }
            set { _rawName = value; OnPropertyChanged("RawName"); }
        }

        public string CategoryTag
        {
            get { return _categoryTag; }
            set { _categoryTag = value; OnPropertyChanged("CategoryTag"); }
        }

        public string CleanName
        {
            get { return _cleanName; }
            set { _cleanName = value; OnPropertyChanged("CleanName"); }
        }

        public string CustomName
        {
            get { return _customName; }
            set { _customName = value; OnPropertyChanged("CustomName"); }
        }

        public bool IsDefault
        {
            get { return _isDefault; }
            set { _isDefault = value; OnPropertyChanged("IsDefault"); }
        }

        public int Volume
        {
            get { return _volume; }
            set { _volume = value; OnPropertyChanged("Volume"); }
        }

        public bool IsMuted
        {
            get { return _isMuted; }
            set { _isMuted = value; OnPropertyChanged("IsMuted"); }
        }

        public bool IsHidden
        {
            get { return _isHidden; }
            set { _isHidden = value; OnPropertyChanged("IsHidden"); }
        }

        public int Order
        {
            get { return _order; }
            set { _order = value; OnPropertyChanged("Order"); }
        }

        public IAudioEndpointVolume EndpointVolume { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class AudioSessionItem : INotifyPropertyChanged
    {
        private int _processId;
        private string _processName;
        private string _displayName;
        private string _exePath;
        private Image _icon;
        private int _volume;
        private bool _isMuted;
        private double _peakLevel;
        private string _currentDeviceId;
        private string _selectedDeviceId;

        public string SessionKey { get; set; }

        public int ProcessId
        {
            get { return _processId; }
            set { _processId = value; OnPropertyChanged("ProcessId"); }
        }

        public string ProcessName
        {
            get { return _processName; }
            set { _processName = value; OnPropertyChanged("ProcessName"); }
        }

        public string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; OnPropertyChanged("DisplayName"); }
        }

        public string ExePath
        {
            get { return _exePath; }
            set { _exePath = value; OnPropertyChanged("ExePath"); }
        }

        public Image Icon
        {
            get { return _icon; }
            set { _icon = value; OnPropertyChanged("Icon"); }
        }

        public int Volume
        {
            get { return _volume; }
            set { _volume = value; OnPropertyChanged("Volume"); }
        }

        public bool IsMuted
        {
            get { return _isMuted; }
            set { _isMuted = value; OnPropertyChanged("IsMuted"); }
        }

        public double PeakLevel
        {
            get { return _peakLevel; }
            set { _peakLevel = value; OnPropertyChanged("PeakLevel"); }
        }

        public string CurrentDeviceId
        {
            get { return _currentDeviceId; }
            set { _currentDeviceId = value; OnPropertyChanged("CurrentDeviceId"); }
        }

        public string SelectedDeviceId
        {
            get { return _selectedDeviceId; }
            set { _selectedDeviceId = value; OnPropertyChanged("SelectedDeviceId"); }
        }

        public ISimpleAudioVolume SimpleVolume { get; set; }
        public IAudioMeterInformation Meter { get; set; }
        public IAudioSessionControl2 SessionControl { get; set; }

        // All volume controls and PIDs associated with this application
        public List<ISimpleAudioVolume> AllVolumes { get; set; }
        public List<int> AllPids { get; set; }

        public AudioSessionItem()
        {
            AllVolumes = new List<ISimpleAudioVolume>();
            AllPids = new List<int>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DeviceConfig
    {
        public string Id { get; set; }
        public bool IsHidden { get; set; }
        public int Order { get; set; }
        public string CustomName { get; set; }
    }

    public class MouseWheelScrollFilter : IMessageFilter
    {
        private readonly ScrollableControl _panel;

        public MouseWheelScrollFilter(ScrollableControl panel)
        {
            _panel = panel;
        }

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL && _panel != null && _panel.IsHandleCreated && _panel.Visible)
            {
                bool canScroll = _panel.VerticalScroll.Visible || _panel.DisplayRectangle.Height > _panel.ClientSize.Height;
                if (!canScroll) return false;

                Point mousePos = Cursor.Position;
                Rectangle screenRect = _panel.RectangleToScreen(_panel.ClientRectangle);
                if (screenRect.Contains(mousePos))
                {
                    Control ctl = Control.FromHandle(m.HWnd);
                    if (ctl is TrackBar || ctl is ComboBox)
                    {
                        return false;
                    }

                    int delta = unchecked((short)((long)m.WParam >> 16));
                    int currentY = Math.Abs(_panel.AutoScrollPosition.Y);
                    int step = 60;
                    int maxScroll = Math.Max(0, _panel.VerticalScroll.Maximum - _panel.ClientSize.Height);
                    int newY = delta > 0 ? Math.Max(0, currentY - step) : Math.Min(maxScroll, currentY + step);
                    _panel.AutoScrollPosition = new Point(0, newY);
                    return true;
                }
            }
            return false;
        }
    }
}
