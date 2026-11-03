using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoundFlow
{
    public enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    public enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    public enum AudioSessionState
    {
        AudioSessionStateInactive = 0,
        AudioSessionStateActive = 1,
        AudioSessionStateExpired = 2
    }

    public enum DeviceState
    {
        DEVICE_STATE_ACTIVE = 0x00000001,
        DEVICE_STATE_DISABLED = 0x00000002,
        DEVICE_STATE_NOTPRESENT = 0x00000004,
        DEVICE_STATE_UNPLUGGED = 0x00000008,
        DEVICE_STATEMASK_ALL = 0x0000000F
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    public class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int pcDevices);
        [PreserveSig] int Item(int nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.Interface)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int cProps);
        [PreserveSig] int GetAt(int iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant propvar);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid fmtid;
        public int pid;

        public static readonly PropertyKey PKEY_Device_FriendlyName = new PropertyKey
        {
            fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
            pid = 14
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PropVariant
    {
        [FieldOffset(0)] public short vt;
        [FieldOffset(2)] public short wReserved1;
        [FieldOffset(4)] public short wReserved2;
        [FieldOffset(6)] public short wReserved3;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags, [MarshalAs(UnmanagedType.Interface)] out ISimpleAudioVolume AudioVolume);
        [PreserveSig] int GetSessionEnumerator([MarshalAs(UnmanagedType.Interface)] out IAudioSessionEnumerator SessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr SessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionIndex, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl Session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out AudioSessionState pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
    }

    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioSessionControl2 : IAudioSessionControl
    {
        [PreserveSig] new int GetState(out AudioSessionState pRetVal);
        [PreserveSig] new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] new int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] new int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] new int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] new int UnregisterAudioSessionNotification(IntPtr NewNotifications);

        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference(int optOut);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute(int bMute, ref Guid EventContext);
        [PreserveSig] int GetMute(out int pbMute);
    }

    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float pfPeak);
        [PreserveSig] int GetMeteringChannelCount(out int pnChannelCount);
        [PreserveSig] int GetChannelsPeakValues(int u32ChannelCount, [Out] float[] afPeakValues);
        [PreserveSig] int QueryHardwareSupport(out int pdwHardwareSupportMask);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int GetChannelCount(out int pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(int nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(int nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(int nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(int nChannel, out float pfLevel);
        [PreserveSig] int SetMute(int bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute(out int pbMute);
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    public class PolicyConfigClient { }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPolicyConfig
    {
        int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
        int GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
        int ResetDeviceFormat(string pszDeviceName);
        int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        int GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
        int GetShareMode(string pszDeviceName, IntPtr pMode);
        int SetShareMode(string pszDeviceName, IntPtr mode);
        int GetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        int SetPropertyValue(string pszDeviceName, bool bFxStore, IntPtr key, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, ERole eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
    }

    [Guid("ab3d4648-e242-459f-b02f-541c70306324")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface IAudioPolicyConfigFactory21H2
    {
        int __dummy01(); int __dummy02(); int __dummy03(); int __dummy04();
        int __dummy05(); int __dummy06(); int __dummy07(); int __dummy08();
        int __dummy09(); int __dummy10(); int __dummy11(); int __dummy12();
        int __dummy13(); int __dummy14(); int __dummy15(); int __dummy16();
        int __dummy17(); int __dummy18(); int __dummy19();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.HString)] string deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    [Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface IAudioPolicyConfigFactoryDownlevel
    {
        int __dummy01(); int __dummy02(); int __dummy03(); int __dummy04();
        int __dummy05(); int __dummy06(); int __dummy07(); int __dummy08();
        int __dummy09(); int __dummy10(); int __dummy11(); int __dummy12();
        int __dummy13(); int __dummy14(); int __dummy15(); int __dummy16();
        int __dummy17(); int __dummy18(); int __dummy19();
        [PreserveSig] int SetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, [MarshalAs(UnmanagedType.HString)] string deviceId);
        [PreserveSig] int GetPersistedDefaultAudioEndpoint(uint processId, EDataFlow flow, ERole role, [Out, MarshalAs(UnmanagedType.HString)] out string deviceId);
        [PreserveSig] int ClearAllPersistedApplicationDefaultEndpoints();
    }

    public static class Combase
    {
        [DllImport("combase.dll", PreserveSig = false)]
        public static extern void RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out, MarshalAs(UnmanagedType.IInspectable)] out object factory);
    }

    public static class Win32
    {
        [DllImport("ole32.dll")]
        public static extern int PropVariantClear(ref PropVariant pvar);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public static string GetProcessPathSafe(int pid)
        {
            if (pid <= 0) return null;
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
            return null;
        }
    }

    public static class AudioPolicyManager
    {
        public const string MMDEVAPI_PREFIX = @"\\?\SWD#MMDEVAPI#";
        public const string DEVINTERFACE_AUDIO_RENDER = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        public const string DEVINTERFACE_AUDIO_CAPTURE = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";

        private static readonly Guid IID_21H2 = new Guid("ab3d4648-e242-459f-b02f-541c70306324");
        private static readonly Guid IID_Downlevel = new Guid("2a59116d-6c4f-45e0-a74f-707e3fef9258");
        private static object _cachedPolicyConfigFactory;
        private static readonly object _syncLock = new object();

        private static object GetPolicyConfigFactory()
        {
            lock (_syncLock)
            {
                if (_cachedPolicyConfigFactory != null) return _cachedPolicyConfigFactory;

                bool is21H2OrGreater = Environment.OSVersion.Version.Build >= 21390;
                Guid targetIid = is21H2OrGreater ? IID_21H2 : IID_Downlevel;

                try
                {
                    object factoryObj;
                    Combase.RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig", ref targetIid, out factoryObj);
                    _cachedPolicyConfigFactory = factoryObj;
                    return _cachedPolicyConfigFactory;
                }
                catch
                {
                    Guid fallbackIid = is21H2OrGreater ? IID_Downlevel : IID_21H2;
                    object factoryObj;
                    Combase.RoGetActivationFactory("Windows.Media.Internal.AudioPolicyConfig", ref fallbackIid, out factoryObj);
                    _cachedPolicyConfigFactory = factoryObj;
                    return _cachedPolicyConfigFactory;
                }
            }
        }

        public static string FormatDeviceId(string immDeviceId, EDataFlow flow = EDataFlow.eRender)
        {
            if (string.IsNullOrWhiteSpace(immDeviceId)) return null;
            string devInterface = (flow == EDataFlow.eRender) ? DEVINTERFACE_AUDIO_RENDER : DEVINTERFACE_AUDIO_CAPTURE;
            return string.Format("{0}{1}{2}", MMDEVAPI_PREFIX, immDeviceId, devInterface);
        }

        public static string UnpackDeviceId(string formattedDeviceId)
        {
            if (string.IsNullOrEmpty(formattedDeviceId)) return null;
            if (formattedDeviceId.StartsWith(MMDEVAPI_PREFIX, StringComparison.OrdinalIgnoreCase))
                formattedDeviceId = formattedDeviceId.Substring(MMDEVAPI_PREFIX.Length);
            if (formattedDeviceId.EndsWith(DEVINTERFACE_AUDIO_RENDER, StringComparison.OrdinalIgnoreCase))
                formattedDeviceId = formattedDeviceId.Substring(0, formattedDeviceId.Length - DEVINTERFACE_AUDIO_RENDER.Length);
            if (formattedDeviceId.EndsWith(DEVINTERFACE_AUDIO_CAPTURE, StringComparison.OrdinalIgnoreCase))
                formattedDeviceId = formattedDeviceId.Substring(0, formattedDeviceId.Length - DEVINTERFACE_AUDIO_CAPTURE.Length);
            return formattedDeviceId;
        }

        public static bool SetProcessAudioEndpoint(int processId, string endpointId, EDataFlow flow = EDataFlow.eRender)
        {
            try
            {
                object factory = GetPolicyConfigFactory();
                // If resetting to default, formattedDevId must be null
                string formattedDevId = string.IsNullOrEmpty(endpointId) ? null : FormatDeviceId(endpointId, flow);
                uint pid = (uint)processId;

                int hr1 = 0, hr2 = 0;
                var f21H2 = factory as IAudioPolicyConfigFactory21H2;
                if (f21H2 != null)
                {
                    hr1 = f21H2.SetPersistedDefaultAudioEndpoint(pid, flow, ERole.eMultimedia, formattedDevId);
                    hr2 = f21H2.SetPersistedDefaultAudioEndpoint(pid, flow, ERole.eConsole, formattedDevId);
                }
                else
                {
                    var fDown = factory as IAudioPolicyConfigFactoryDownlevel;
                    if (fDown != null)
                    {
                        hr1 = fDown.SetPersistedDefaultAudioEndpoint(pid, flow, ERole.eMultimedia, formattedDevId);
                        hr2 = fDown.SetPersistedDefaultAudioEndpoint(pid, flow, ERole.eConsole, formattedDevId);
                    }
                }

                return hr1 == 0 && hr2 == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetProcessAudioEndpoint error: " + ex);
                return false;
            }
        }

        public static string GetProcessAudioEndpoint(int processId, EDataFlow flow = EDataFlow.eRender)
        {
            try
            {
                object factory = GetPolicyConfigFactory();
                uint pid = (uint)processId;
                string rawDevId = null;
                int hr = -1;

                var f21H2 = factory as IAudioPolicyConfigFactory21H2;
                if (f21H2 != null)
                {
                    hr = f21H2.GetPersistedDefaultAudioEndpoint(pid, flow, ERole.eMultimedia, out rawDevId);
                }
                else
                {
                    var fDown = factory as IAudioPolicyConfigFactoryDownlevel;
                    if (fDown != null)
                    {
                        hr = fDown.GetPersistedDefaultAudioEndpoint(pid, flow, ERole.eMultimedia, out rawDevId);
                    }
                }

                if (hr == 0 && !string.IsNullOrEmpty(rawDevId))
                {
                    return UnpackDeviceId(rawDevId);
                }
            }
            catch { }
            return null;
        }

        public static void ClearAllProcessAudioEndpoints()
        {
            try
            {
                object factory = GetPolicyConfigFactory();
                var f21H2 = factory as IAudioPolicyConfigFactory21H2;
                if (f21H2 != null)
                    f21H2.ClearAllPersistedApplicationDefaultEndpoints();
                else
                {
                    var fDown = factory as IAudioPolicyConfigFactoryDownlevel;
                    if (fDown != null)
                        fDown.ClearAllPersistedApplicationDefaultEndpoints();
                }
            }
            catch { }
        }

        public static void SetSystemDefaultEndpoint(string endpointId, ERole role = ERole.eMultimedia)
        {
            try
            {
                var policy = (IPolicyConfig)new PolicyConfigClient();
                policy.SetDefaultEndpoint(endpointId, role);
                policy.SetDefaultEndpoint(endpointId, ERole.eConsole);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SetSystemDefaultEndpoint error: " + ex);
            }
        }
    }
}
