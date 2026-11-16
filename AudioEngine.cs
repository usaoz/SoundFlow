using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SoundFlow
{
    public class AudioEngine : IDisposable
    {
        private IMMDeviceEnumerator _deviceEnumerator;
        private readonly Timer _rescanTimer;
        private readonly Dictionary<string, Image> _iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DeviceConfig> _deviceConfigs = new Dictionary<string, DeviceConfig>(StringComparer.OrdinalIgnoreCase);
        private readonly string _deviceConfigPath;
        private bool _isWindowActive = false;
        private string _defaultDeviceId = null;
        private List<string> _allActiveDeviceIds = new List<string>();

        public BindingList<AudioDeviceItem> Devices { get; private set; }
        public BindingList<AudioSessionItem> Sessions { get; private set; }

        public event Action OnDevicesUpdated;
        public event Action OnSessionsUpdated;

        public bool IsWindowActive
        {
            get { return _isWindowActive; }
            set
            {
                _isWindowActive = value;
                if (_isWindowActive)
                {
                    if (!_rescanTimer.Enabled) _rescanTimer.Start();
                }
                else
                {
                    if (_rescanTimer.Enabled) _rescanTimer.Stop();
                }
            }
        }

        public AudioEngine()
        {
            Devices = new BindingList<AudioDeviceItem>();
            Sessions = new BindingList<AudioSessionItem>();

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundFlow");
            Directory.CreateDirectory(appData);
            _deviceConfigPath = Path.Combine(appData, "devices.json");
            LoadDeviceConfigs();

            InitializeCom();

            _rescanTimer = new Timer();
            _rescanTimer.Interval = 1500;
            _rescanTimer.Tick += (s, e) =>
            {
                if (_isWindowActive)
                {
                    ScanSessions(false);
                }
            };
        }

        private void InitializeCom()
        {
            var IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
            var CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Type enumeratorType = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator, true);
            _deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);
        }

        public void Start()
        {
            ScanAll();
            IsWindowActive = true;
        }

        public void Stop()
        {
            IsWindowActive = false;
        }

        public void ScanAll()
        {
            ScanDevices();
            ScanSessions(true);
        }

        public List<AudioDeviceItem> GetVisibleDevices()
        {
            var list = new List<AudioDeviceItem>();
            foreach (var d in Devices)
            {
                if (!d.IsHidden) list.Add(d);
            }
            return list;
        }

        public void MoveDeviceOrder(string deviceId, bool moveUp)
        {
            int idx = -1;
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i].Id == deviceId) { idx = i; break; }
            }
            if (idx < 0) return;

            int targetIdx = moveUp ? idx - 1 : idx + 1;
            if (targetIdx < 0 || targetIdx >= Devices.Count) return;

            var current = Devices[idx];
            var other = Devices[targetIdx];

            int tempOrder = current.Order;
            current.Order = other.Order;
            other.Order = tempOrder;

            UpdateConfigOrder(current.Id, current.Order);
            UpdateConfigOrder(other.Id, other.Order);
            SaveDeviceConfigs();

            SortDevices();
            if (OnDevicesUpdated != null) OnDevicesUpdated();
        }

        public void ToggleDeviceVisibility(string deviceId)
        {
            foreach (var d in Devices)
            {
                if (d.Id == deviceId)
                {
                    d.IsHidden = !d.IsHidden;
                    UpdateConfigHidden(d.Id, d.IsHidden);
                    SaveDeviceConfigs();
                    break;
                }
            }
            if (OnDevicesUpdated != null) OnDevicesUpdated();
            ScanSessions();
        }

        public void SetDeviceCustomName(string deviceId, string customName)
        {
            DeviceConfig cfg;
            if (_deviceConfigs.TryGetValue(deviceId, out cfg))
            {
                cfg.CustomName = string.IsNullOrEmpty(customName) ? "" : customName.Trim();
                SaveDeviceConfigs();
            }
            ScanDevices();
            if (OnDevicesUpdated != null) OnDevicesUpdated();
        }

        public static void FormatDeviceNaming(string rawName, string customName, out string categoryTag, out string cleanName, out string fullName)
        {
            if (!string.IsNullOrEmpty(customName))
            {
                categoryTag = DetectCategory(customName);
                cleanName = customName.Trim();
                fullName = string.Format("[{0}] {1}", categoryTag, cleanName);
                return;
            }

            string raw = rawName ?? "";
            string lower = raw.ToLowerInvariant();

            if (lower.Contains("g435"))
            {
                categoryTag = "AURICULAR";
                cleanName = "G435 Wireless";
            }
            else if (lower.Contains("headset") || lower.Contains("auricular") || lower.Contains("headphone") || lower.Contains("earphone"))
            {
                categoryTag = "AURICULAR";
                cleanName = ExtractSimplifiedName(raw);
            }
            else if (lower.Contains("steam") || lower.Contains("streaming") || lower.Contains("broadcast"))
            {
                categoryTag = "STREAMING";
                cleanName = "Steam Streaming";
            }
            else if (lower.Contains("usb audio"))
            {
                categoryTag = "PARLANTES";
                cleanName = "USB Audio Device";
            }
            else if (lower.Contains("altavoces") || lower.Contains("speakers") || lower.Contains("parlante"))
            {
                categoryTag = "PARLANTES";
                cleanName = ExtractSimplifiedName(raw);
            }
            else
            {
                categoryTag = "AUDIO";
                cleanName = raw.Trim();
            }

            fullName = string.Format("[{0}] {1}", categoryTag, cleanName);
        }

        private static string DetectCategory(string text)
        {
            string lower = text.ToLowerInvariant();
            if (lower.Contains("auricular") || lower.Contains("headset") || lower.Contains("headphone") || lower.Contains("g435") || lower.Contains("auriculares")) return "AURICULAR";
            if (lower.Contains("parlante") || lower.Contains("parlantes") || lower.Contains("speaker") || lower.Contains("speakers") || lower.Contains("altavoz") || lower.Contains("altavoces") || lower.Contains("usb")) return "PARLANTES";
            if (lower.Contains("streaming") || lower.Contains("steam") || lower.Contains("stream")) return "STREAMING";
            return "AUDIO";
        }

        private static string ExtractSimplifiedName(string raw)
        {
            int open = raw.IndexOf('(');
            int close = raw.LastIndexOf(')');
            if (open >= 0 && close > open)
            {
                string inside = raw.Substring(open + 1, close - open - 1).Trim();
                if (inside.Length >= 3) return inside;
            }
            return raw.Trim();
        }

        private void UpdateConfigOrder(string id, int order)
        {
            DeviceConfig cfg;
            if (!_deviceConfigs.TryGetValue(id, out cfg))
            {
                cfg = new DeviceConfig { Id = id, IsHidden = false, Order = order };
                _deviceConfigs[id] = cfg;
            }
            else
            {
                cfg.Order = order;
            }
        }

        private void UpdateConfigHidden(string id, bool isHidden)
        {
            DeviceConfig cfg;
            if (!_deviceConfigs.TryGetValue(id, out cfg))
            {
                cfg = new DeviceConfig { Id = id, IsHidden = isHidden, Order = 0 };
                _deviceConfigs[id] = cfg;
            }
            else
            {
                cfg.IsHidden = isHidden;
            }
        }

        private void LoadDeviceConfigs()
        {
            try
            {
                if (File.Exists(_deviceConfigPath))
                {
                    string json = File.ReadAllText(_deviceConfigPath, Encoding.UTF8);
                    var serializer = new JavaScriptSerializer();
                    var list = serializer.Deserialize<List<DeviceConfig>>(json);
                    if (list != null)
                    {
                        foreach (var c in list)
                        {
                            if (!string.IsNullOrEmpty(c.Id)) _deviceConfigs[c.Id] = c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadDeviceConfigs error: " + ex);
            }
        }

        private void SaveDeviceConfigs()
        {
            try
            {
                var list = new List<DeviceConfig>(_deviceConfigs.Values);
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(list);
                File.WriteAllText(_deviceConfigPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveDeviceConfigs error: " + ex);
            }
        }

        private void SortDevices()
        {
            var sorted = new List<AudioDeviceItem>(Devices);
            sorted.Sort((a, b) =>
            {
                if (a.IsDefault != b.IsDefault) return a.IsDefault ? -1 : 1;
                int cmp = a.Order.CompareTo(b.Order);
                if (cmp != 0) return cmp;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Devices.Clear();
            foreach (var d in sorted) Devices.Add(d);
        }

        private void ScanDevices()
        {
            IMMDeviceCollection collection = null;
            IMMDevice defaultDev = null;
            try
            {
                _deviceEnumerator.EnumAudioEndpoints((int)EDataFlow.eRender, 1 /* DEVICE_STATE_ACTIVE */, out collection);
                if (collection == null) return;

                int count;
                collection.GetCount(out count);

                string defaultDevId = null;
                try
                {
                    _deviceEnumerator.GetDefaultAudioEndpoint((int)EDataFlow.eRender, (int)ERole.eMultimedia, out defaultDev);
                    if (defaultDev != null)
                    {
                        defaultDev.GetId(out defaultDevId);
                    }
                }
                catch { }

                _defaultDeviceId = defaultDevId;

                var foundIds = new HashSet<string>();

                for (int i = 0; i < count; i++)
                {
                    IMMDevice dev = null;
                    IPropertyStore store = null;
                    PropVariant pv = new PropVariant();

                    try
                    {
                        collection.Item(i, out dev);
                        if (dev == null) continue;

                        string id;
                        dev.GetId(out id);
                        foundIds.Add(id);

                        string name = "Dispositivo " + i;
                        int hr = dev.OpenPropertyStore(0, out store);
                        if (hr == 0 && store != null)
                        {
                            var key = PropertyKey.PKEY_Device_FriendlyName;
                            store.GetValue(ref key, out pv);
                            name = Marshal.PtrToStringUni(pv.pwszVal) ?? name;
                        }

                        IAudioEndpointVolume endpointVol = null;
                        int volPercent = 100;
                        bool isMuted = false;
                        try
                        {
                            var iidEndpoint = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
                            object epObj;
                            dev.Activate(ref iidEndpoint, 1, IntPtr.Zero, out epObj);
                            endpointVol = epObj as IAudioEndpointVolume;
                            if (endpointVol != null)
                            {
                                float level;
                                endpointVol.GetMasterVolumeLevelScalar(out level);
                                volPercent = (int)Math.Round(level * 100);

                                int muteInt;
                                endpointVol.GetMute(out muteInt);
                                isMuted = (muteInt != 0);
                            }
                        }
                        catch { }

                        bool isDef = (id == defaultDevId);

                        DeviceConfig cfg;
                        if (!_deviceConfigs.TryGetValue(id, out cfg))
                        {
                            int maxOrder = 0;
                            foreach (var c in _deviceConfigs.Values) { if (c.Order > maxOrder) maxOrder = c.Order; }
                            cfg = new DeviceConfig { Id = id, IsHidden = false, Order = maxOrder + 10, CustomName = "" };
                            _deviceConfigs[id] = cfg;
                            SaveDeviceConfigs();
                        }

                        string catTag, cleanName, fullName;
                        FormatDeviceNaming(name, cfg.CustomName, out catTag, out cleanName, out fullName);

                        AudioDeviceItem existing = null;
                        foreach (var d in Devices)
                        {
                            if (d.Id == id) { existing = d; break; }
                        }

                        if (existing != null)
                        {
                            existing.RawName = name;
                            existing.CustomName = cfg.CustomName;
                            existing.CategoryTag = catTag;
                            existing.CleanName = cleanName;
                            existing.Name = fullName;
                            existing.IsDefault = isDef;
                            existing.Volume = volPercent;
                            existing.IsMuted = isMuted;
                            existing.EndpointVolume = endpointVol;
                            existing.IsHidden = cfg.IsHidden;
                            existing.Order = cfg.Order;
                        }
                        else
                        {
                            Devices.Add(new AudioDeviceItem
                            {
                                Id = id,
                                RawName = name,
                                CustomName = cfg.CustomName,
                                CategoryTag = catTag,
                                CleanName = cleanName,
                                Name = fullName,
                                IsDefault = isDef,
                                Volume = volPercent,
                                IsMuted = isMuted,
                                EndpointVolume = endpointVol,
                                IsHidden = cfg.IsHidden,
                                Order = cfg.Order
                            });
                        }
                    }
                    finally
                    {
                        Win32.PropVariantClear(ref pv);
                        if (store != null) Marshal.ReleaseComObject(store);
                        if (dev != null) Marshal.ReleaseComObject(dev);
                    }
                }

                // Remove unplugged devices
                _allActiveDeviceIds = new List<string>(foundIds);
                for (int i = Devices.Count - 1; i >= 0; i--)
                {
                    if (!foundIds.Contains(Devices[i].Id))
                    {
                        Devices.RemoveAt(i);
                    }
                }

                // Deduplicate devices with the same formatted Name, preferring the default device
                var seenNames = new Dictionary<string, AudioDeviceItem>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < Devices.Count; i++)
                {
                    var d = Devices[i];
                    AudioDeviceItem existingByName;
                    if (seenNames.TryGetValue(d.Name, out existingByName))
                    {
                        if (d.IsDefault && !existingByName.IsDefault)
                        {
                            Devices.Remove(existingByName);
                            seenNames[d.Name] = d;
                            i--;
                        }
                        else
                        {
                            Devices.RemoveAt(i);
                            i--;
                        }
                    }
                    else
                    {
                        seenNames[d.Name] = d;
                    }
                }

                SortDevices();
                if (OnDevicesUpdated != null) OnDevicesUpdated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ScanDevices error: " + ex);
            }
            finally
            {
                if (defaultDev != null) Marshal.ReleaseComObject(defaultDev);
                if (collection != null) Marshal.ReleaseComObject(collection);
            }
        }

        private class RawSessionInfo
        {
            public uint Pid;
            public string DeviceId;
            public bool IsDeviceHidden;
            public string ProcName;
            public string ExePath;
            public string DisplayName;
            public AudioSessionState State;
            public float Volume;
            public bool IsMuted;
            public string RoutedDevId;
            public ISimpleAudioVolume SimpleVol;
            public IAudioMeterInformation Meter;
            public IAudioSessionControl2 SessionControl;
        }

        public void ScanSessions(bool forceUpdate = false)
        {
            try
            {
                var IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                var rawList = new List<RawSessionInfo>();

                // Scan all active audio endpoints (including deduplicated ones)
                foreach (var deviceId in _allActiveDeviceIds)
                {
                    IMMDevice dev = null;
                    object mgrObj = null;
                    IAudioSessionManager2 mgr = null;
                    IAudioSessionEnumerator sessionEnum = null;

                    try
                    {
                        _deviceEnumerator.GetDevice(deviceId, out dev);
                        if (dev == null) continue;

                        int hr = dev.Activate(ref IID_IAudioSessionManager2, 1, IntPtr.Zero, out mgrObj);
                        if (hr != 0 || mgrObj == null) continue;

                        mgr = mgrObj as IAudioSessionManager2;
                        if (mgr == null) continue;

                        mgr.GetSessionEnumerator(out sessionEnum);
                        if (sessionEnum == null) continue;

                        int sessionCount;
                        sessionEnum.GetCount(out sessionCount);

                        for (int s = 0; s < sessionCount; s++)
                        {
                            IAudioSessionControl ctl = null;
                            IAudioSessionControl2 ctl2 = null;

                            try
                            {
                                sessionEnum.GetSession(s, out ctl);
                                ctl2 = ctl as IAudioSessionControl2;
                                if (ctl2 == null) continue;

                                uint pid;
                                ctl2.GetProcessId(out pid);

                                // 1. EXCLUDE SYSTEM SOUNDS COMPLETELY
                                if (pid == 0) continue;

                                int isSysSound = ctl2.IsSystemSoundsSession();
                                if (isSysSound == 0) continue; // S_OK (0) means it IS a system sounds session!

                                AudioSessionState state;
                                ctl2.GetState(out state);
                                if (state == AudioSessionState.AudioSessionStateExpired) continue;

                                // Check if process is still alive and has not exited
                                Process proc = null;
                                try
                                {
                                    proc = Process.GetProcessById((int)pid);
                                    if (proc.HasExited)
                                    {
                                        proc = null;
                                    }
                                }
                                catch
                                {
                                    proc = null;
                                }

                                if (proc == null)
                                {
                                    continue; // Process is terminated, skip zombie session!
                                }

                                string procName = proc.ProcessName;
                                string exePath = "";
                                try { exePath = Win32.GetProcessPathSafe((int)pid); } catch { }
                                if (string.IsNullOrEmpty(exePath))
                                {
                                    try { exePath = proc.MainModule.FileName; } catch { }
                                }

                                if (string.IsNullOrEmpty(procName)) continue;

                                // Filter out system audio services and unwanted system monitors
                                if (procName.Equals("audiodg", StringComparison.OrdinalIgnoreCase) ||
                                    procName.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                                    procName.Equals("SystemSounds", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string displayName = "";
                                ctl2.GetDisplayName(out displayName);
                                if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("@"))
                                {
                                    displayName = FormatDisplayName(procName);
                                }

                                if (displayName.IndexOf("Sonidos del Sistema", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    displayName.IndexOf("System Sounds", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    continue;
                                }

                                var vol = ctl as ISimpleAudioVolume;
                                float level = 1f;
                                int mute = 0;
                                if (vol != null)
                                {
                                    vol.GetMasterVolume(out level);
                                    vol.GetMute(out mute);
                                }

                                var meter = ctl as IAudioMeterInformation;
                                string routedDevId = AudioPolicyManager.GetProcessAudioEndpoint((int)pid);

                                rawList.Add(new RawSessionInfo
                                {
                                    Pid = pid,
                                    DeviceId = deviceId,
                                    IsDeviceHidden = false,
                                    ProcName = procName,
                                    ExePath = exePath,
                                    DisplayName = displayName,
                                    State = state,
                                    Volume = level,
                                    IsMuted = (mute != 0),
                                    RoutedDevId = routedDevId,
                                    SimpleVol = vol,
                                    Meter = meter,
                                    SessionControl = ctl2
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Session parse item error: " + ex);
                            }
                        }
                    }
                    finally
                    {
                        if (sessionEnum != null) { try { Marshal.ReleaseComObject(sessionEnum); } catch { } }
                        if (dev != null) { try { Marshal.ReleaseComObject(dev); } catch { } }
                    }
                }

                // 2. DEDUPLICATE SESSIONS BY APPLICATION NAME (ProcName)
                var appGroups = new Dictionary<string, List<RawSessionInfo>>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in rawList)
                {
                    List<RawSessionInfo> list;
                    if (!appGroups.TryGetValue(raw.ProcName, out list))
                    {
                        list = new List<RawSessionInfo>();
                        appGroups[raw.ProcName] = list;
                    }
                    list.Add(raw);
                }

                var currentAppKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool sessionsChanged = false;

                foreach (var kvp in appGroups)
                {
                    string appKey = kvp.Key; // e.g. "steam", "opera", "discord"
                    try
                    {
                        if (Process.GetProcessesByName(appKey).Length == 0)
                        {
                            continue; // Ensure at least one process exists with this name!
                        }
                    }
                    catch { }

                    var sessionsOfApp = kvp.Value;

                    // Choose primary session:
                    RawSessionInfo primary = null;
                    foreach (var s in sessionsOfApp)
                    {
                        if (!s.IsDeviceHidden && s.State == AudioSessionState.AudioSessionStateActive)
                        {
                            primary = s;
                            break;
                        }
                    }

                    if (primary == null)
                    {
                        foreach (var s in sessionsOfApp)
                        {
                            if (!string.IsNullOrEmpty(s.RoutedDevId) && s.DeviceId == s.RoutedDevId)
                            {
                                primary = s;
                                break;
                            }
                        }
                    }

                    if (primary == null && !string.IsNullOrEmpty(_defaultDeviceId))
                    {
                        foreach (var s in sessionsOfApp)
                        {
                            if (s.DeviceId == _defaultDeviceId)
                            {
                                primary = s;
                                break;
                            }
                        }
                    }

                    if (primary == null)
                    {
                        foreach (var s in sessionsOfApp)
                        {
                            if (!s.IsDeviceHidden)
                            {
                                primary = s;
                                break;
                            }
                        }
                    }

                    if (primary == null && sessionsOfApp.Count > 0)
                    {
                        primary = sessionsOfApp[0];
                    }

                    if (primary == null) continue;

                    currentAppKeys.Add(appKey);

                    // Collect all volumes and PIDs of this application
                    var allVolumes = new List<ISimpleAudioVolume>();
                    var allPids = new List<int>();
                    var pidSet = new HashSet<int>();

                    foreach (var s in sessionsOfApp)
                    {
                        if (s.SimpleVol != null && !allVolumes.Contains(s.SimpleVol))
                        {
                            allVolumes.Add(s.SimpleVol);
                        }
                        if (s.Pid > 0 && !pidSet.Contains((int)s.Pid))
                        {
                            pidSet.Add((int)s.Pid);
                            allPids.Add((int)s.Pid);
                        }
                    }

                    // Find existing session item
                    AudioSessionItem existing = null;
                    foreach (var s in Sessions)
                    {
                        if (string.Equals(s.ProcessName, appKey, StringComparison.OrdinalIgnoreCase))
                        {
                            existing = s;
                            break;
                        }
                    }

                    int volPercent = (int)Math.Round(primary.Volume * 100);
                    string selectedDev = !string.IsNullOrEmpty(primary.RoutedDevId) ? primary.RoutedDevId : primary.DeviceId;
                    if (string.IsNullOrEmpty(selectedDev)) selectedDev = _defaultDeviceId;

                    if (existing != null)
                    {
                        if (existing.CurrentDeviceId != primary.DeviceId ||
                            existing.SelectedDeviceId != selectedDev)
                        {
                            sessionsChanged = true;
                        }

                        existing.Volume = volPercent;
                        existing.IsMuted = primary.IsMuted;
                        existing.CurrentDeviceId = primary.DeviceId;
                        existing.SelectedDeviceId = selectedDev;
                        existing.SimpleVolume = primary.SimpleVol;
                        existing.Meter = primary.Meter;
                        existing.SessionControl = primary.SessionControl;
                        existing.AllVolumes = allVolumes;
                        existing.AllPids = allPids;
                    }
                    else
                    {
                        Image icon = GetIconForProcess(primary.ProcName, primary.ExePath);

                        var newItem = new AudioSessionItem
                        {
                            SessionKey = appKey,
                            ProcessId = (int)primary.Pid,
                            ProcessName = primary.ProcName,
                            DisplayName = primary.DisplayName,
                            ExePath = primary.ExePath,
                            Icon = icon,
                            Volume = volPercent,
                            IsMuted = primary.IsMuted,
                            CurrentDeviceId = primary.DeviceId,
                            SelectedDeviceId = selectedDev,
                            SimpleVolume = primary.SimpleVol,
                            Meter = primary.Meter,
                            SessionControl = primary.SessionControl,
                            AllVolumes = allVolumes,
                            AllPids = allPids
                        };

                        Sessions.Add(newItem);
                        sessionsChanged = true;
                    }
                }

                // Remove closed applications
                for (int i = Sessions.Count - 1; i >= 0; i--)
                {
                    string pName = Sessions[i].ProcessName;
                    bool isAlive = false;
                    try
                    {
                        if (currentAppKeys.Contains(pName) && Process.GetProcessesByName(pName).Length > 0)
                        {
                            isAlive = true;
                        }
                    }
                    catch { }

                    if (!isAlive)
                    {
                        Sessions.RemoveAt(i);
                        sessionsChanged = true;
                    }
                }

                if (forceUpdate || sessionsChanged)
                {
                    if (OnSessionsUpdated != null) OnSessionsUpdated();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ScanSessions error: " + ex);
            }
        }

        public void SetSessionVolume(AudioSessionItem session, int volumePercent)
        {
            if (session == null) return;
            session.Volume = volumePercent;
            float level = Math.Max(0f, Math.Min(1f, volumePercent / 100f));
            Guid ctx = Guid.Empty;

            if (session.AllVolumes != null)
            {
                foreach (var vol in session.AllVolumes)
                {
                    try { vol.SetMasterVolume(level, ref ctx); } catch { }
                }
            }
            else if (session.SimpleVolume != null)
            {
                try { session.SimpleVolume.SetMasterVolume(level, ref ctx); } catch { }
            }
        }

        public void SetSessionMute(AudioSessionItem session, bool isMuted)
        {
            if (session == null) return;
            session.IsMuted = isMuted;
            Guid ctx = Guid.Empty;

            if (session.AllVolumes != null)
            {
                foreach (var vol in session.AllVolumes)
                {
                    try { vol.SetMute(isMuted ? 1 : 0, ref ctx); } catch { }
                }
            }
            else if (session.SimpleVolume != null)
            {
                try { session.SimpleVolume.SetMute(isMuted ? 1 : 0, ref ctx); } catch { }
            }
        }

        public bool RouteSession(AudioSessionItem session, string targetDeviceId)
        {
            if (session == null) return false;
            try
            {
                string endpointIdToSet = (targetDeviceId == "DEFAULT" || string.IsNullOrEmpty(targetDeviceId)) ? null : targetDeviceId;
                session.SelectedDeviceId = targetDeviceId;

                // Route all PIDs associated with this app
                if (session.AllPids != null && session.AllPids.Count > 0)
                {
                    foreach (int pid in session.AllPids)
                    {
                        try { AudioPolicyManager.SetProcessAudioEndpoint(pid, endpointIdToSet); } catch { }
                    }
                }
                else if (session.ProcessId > 0)
                {
                    AudioPolicyManager.SetProcessAudioEndpoint(session.ProcessId, endpointIdToSet);
                }

                // Also find any running processes with this name to ensure complete routing
                try
                {
                    foreach (var p in Process.GetProcessesByName(session.ProcessName))
                    {
                        try { AudioPolicyManager.SetProcessAudioEndpoint(p.Id, endpointIdToSet); } catch { }
                    }
                }
                catch { }

                // Delayed scan to reflect change
                var rescanTimer = new Timer { Interval = 400 };
                rescanTimer.Tick += (s, e) =>
                {
                    rescanTimer.Stop();
                    rescanTimer.Dispose();
                    ScanSessions();
                };
                rescanTimer.Start();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RouteSession error: " + ex);
                return false;
            }
        }

        public void RouteAllSessions(string targetDeviceId)
        {
            try
            {
                string endpointIdToSet = (targetDeviceId == "DEFAULT" || string.IsNullOrEmpty(targetDeviceId)) ? null : targetDeviceId;

                foreach (var session in Sessions)
                {
                    session.SelectedDeviceId = targetDeviceId;
                    if (session.AllPids != null)
                    {
                        foreach (int pid in session.AllPids)
                        {
                            try { AudioPolicyManager.SetProcessAudioEndpoint(pid, endpointIdToSet); } catch { }
                        }
                    }
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(session.ProcessName))
                        {
                            try { AudioPolicyManager.SetProcessAudioEndpoint(p.Id, endpointIdToSet); } catch { }
                        }
                    }
                    catch { }
                }

                var rescanTimer = new Timer { Interval = 500 };
                rescanTimer.Tick += (s, e) =>
                {
                    rescanTimer.Stop();
                    rescanTimer.Dispose();
                    ScanSessions();
                };
                rescanTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RouteAllSessions error: " + ex);
            }
        }

        public void SetDeviceVolume(AudioDeviceItem device, int volumePercent)
        {
            if (device == null || device.EndpointVolume == null) return;
            try
            {
                float level = Math.Max(0f, Math.Min(1f, volumePercent / 100f));
                Guid ctx = Guid.Empty;
                device.EndpointVolume.SetMasterVolumeLevelScalar(level, ref ctx);
                device.Volume = volumePercent;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetDeviceVolume error: " + ex);
            }
        }

        public void SetDeviceMute(AudioDeviceItem device, bool isMuted)
        {
            if (device == null || device.EndpointVolume == null) return;
            try
            {
                Guid ctx = Guid.Empty;
                device.EndpointVolume.SetMute(isMuted ? 1 : 0, ref ctx);
                device.IsMuted = isMuted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetDeviceMute error: " + ex);
            }
        }

        public void SetDefaultDevice(AudioDeviceItem device)
        {
            if (device == null) return;
            try
            {
                AudioPolicyManager.SetSystemDefaultEndpoint(device.Id);
                ScanDevices();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetDefaultDevice error: " + ex);
            }
        }

        private Image GetIconForProcess(string procName, string exePath)
        {
            string cacheKey = !string.IsNullOrEmpty(exePath) ? exePath : procName;
            if (_iconCache.ContainsKey(cacheKey)) return _iconCache[cacheKey];

            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using (Icon sysIcon = Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (sysIcon != null)
                        {
                            var bmp = new Bitmap(24, 24);
                            using (var g = Graphics.FromImage(bmp))
                            {
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.DrawIcon(sysIcon, new Rectangle(0, 0, 24, 24));
                            }
                            _iconCache[cacheKey] = bmp;
                            return bmp;
                        }
                    }
                }
            }
            catch { }

            // Default fallback clean bitmap icon
            var fallback = CreateDefaultAudioIcon();
            _iconCache[cacheKey] = fallback;
            return fallback;
        }

        private Image CreateDefaultAudioIcon()
        {
            var bmp = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var brush = new SolidBrush(Color.FromArgb(70, 70, 70)))
                {
                    // Speaker body
                    Point[] pts = new Point[]
                    {
                        new Point(4, 9),
                        new Point(8, 9),
                        new Point(14, 4),
                        new Point(14, 20),
                        new Point(8, 15),
                        new Point(4, 15)
                    };
                    g.FillPolygon(brush, pts);
                }

                using (var pen = new Pen(Color.FromArgb(70, 70, 70), 1.6f))
                {
                    g.DrawArc(pen, new Rectangle(12, 8, 8, 8), -40, 80);
                }
            }
            return bmp;
        }

        private string FormatDisplayName(string procName)
        {
            if (string.IsNullOrEmpty(procName)) return "Aplicacion";
            string lower = procName.ToLowerInvariant();
            if (lower == "operagx" || lower == "opera") return "Opera GX";
            if (lower == "discord") return "Discord";
            if (lower == "steam" || lower == "steamwebhelper") return "Steam";
            if (lower == "chrome") return "Google Chrome";
            if (lower == "msedge") return "Microsoft Edge";
            if (lower == "firefox") return "Mozilla Firefox";
            if (lower == "spotify") return "Spotify";
            if (lower == "antigravity") return "Antigravity";
            if (lower == "cs2") return "Counter-Strike 2";
            if (lower == "valorant") return "Valorant";
            if (lower == "obs64") return "OBS Studio";

            if (procName.Length > 1)
            {
                return char.ToUpperInvariant(procName[0]) + procName.Substring(1);
            }
            return procName;
        }

        public void Dispose()
        {
            Stop();
            if (_rescanTimer != null) _rescanTimer.Dispose();
            foreach (var img in _iconCache.Values)
            {
                try { img.Dispose(); } catch { }
            }
            _iconCache.Clear();
            if (_deviceEnumerator != null) { try { Marshal.ReleaseComObject(_deviceEnumerator); } catch { } }
        }
    }
}
