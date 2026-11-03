using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;

namespace SoundFlow
{
    public class ProfileService
    {
        private readonly string _storagePath;
        private readonly JavaScriptSerializer _serializer;

        public ObservableCollection<AudioProfile> Profiles { get; private set; }

        public ProfileService()
        {
            _serializer = new JavaScriptSerializer();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "SoundFlow");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _storagePath = Path.Combine(folder, "profiles.json");
            Profiles = new ObservableCollection<AudioProfile>();

            LoadProfiles();
        }

        public void LoadProfiles()
        {
            Profiles.Clear();

            if (File.Exists(_storagePath))
            {
                try
                {
                    string json = File.ReadAllText(_storagePath);
                    var list = _serializer.Deserialize<List<AudioProfile>>(json);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var p in list) Profiles.Add(p);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading profiles: " + ex);
                }
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var list = new List<AudioProfile>(Profiles);
                string json = _serializer.Serialize(list);
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error saving profiles: " + ex);
            }
        }

        public void ApplyProfile(AudioProfile profile, AudioEngine engine)
        {
            if (profile == null || engine == null) return;

            // 1. If profile specifies a default master device, apply it
            if (!string.IsNullOrEmpty(profile.DefaultDeviceName))
            {
                AudioDeviceItem targetMaster = null;
                foreach (var d in engine.Devices)
                {
                    if (d.Name.IndexOf(profile.DefaultDeviceName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (d.RawName != null && d.RawName.IndexOf(profile.DefaultDeviceName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        d.Id.Equals(profile.DefaultDeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetMaster = d;
                        break;
                    }
                }

                if (targetMaster != null && !targetMaster.IsDefault)
                {
                    engine.SetDefaultDevice(targetMaster);
                }
            }

            // 2. Apply per-application routing rules
            foreach (var session in engine.Sessions)
            {
                string procName = session.ProcessName.ToLowerInvariant();
                string targetDeviceQuery = null;

                if (profile.AppRoutes.ContainsKey(procName))
                {
                    targetDeviceQuery = profile.AppRoutes[procName];
                }
                else if (profile.AppRoutes.ContainsKey("*"))
                {
                    targetDeviceQuery = profile.AppRoutes["*"];
                }

                if (!string.IsNullOrEmpty(targetDeviceQuery))
                {
                    string targetDeviceId = null;
                    if (targetDeviceQuery.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
                    {
                        targetDeviceId = "DEFAULT";
                    }
                    else
                    {
                        foreach (var d in engine.Devices)
                        {
                            if (d.Name.IndexOf(targetDeviceQuery, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (d.RawName != null && d.RawName.IndexOf(targetDeviceQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                d.Id.Equals(targetDeviceQuery, StringComparison.OrdinalIgnoreCase))
                            {
                                targetDeviceId = d.Id;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(targetDeviceId))
                    {
                        engine.RouteSession(session, targetDeviceId);
                    }
                }

                // 3. Apply volume rules
                if (profile.AppVolumes.ContainsKey(procName))
                {
                    engine.SetSessionVolume(session, profile.AppVolumes[procName]);
                }
                else if (profile.AppVolumes.ContainsKey("*"))
                {
                    engine.SetSessionVolume(session, profile.AppVolumes["*"]);
                }
            }
        }

        public AudioProfile CreateProfileFromCurrentState(string name, string desc, AudioEngine engine)
        {
            var p = new AudioProfile
            {
                Name = name,
                Description = desc,
                IsBuiltIn = false
            };

            foreach (var dev in engine.Devices)
            {
                if (dev.IsDefault)
                {
                    p.DefaultDeviceName = dev.Name;
                    break;
                }
            }

            foreach (var s in engine.Sessions)
            {
                string pName = s.ProcessName.ToLowerInvariant();
                p.AppVolumes[pName] = s.Volume;

                if (!string.IsNullOrEmpty(s.SelectedDeviceId) && s.SelectedDeviceId != "DEFAULT")
                {
                    string devName = s.SelectedDeviceId;
                    foreach (var d in engine.Devices)
                    {
                        if (d.Id == s.SelectedDeviceId)
                        {
                            devName = d.Name;
                            break;
                        }
                    }
                    p.AppRoutes[pName] = devName;
                }
            }

            // Remove existing profile with same name if any
            for (int i = Profiles.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Profiles[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    Profiles.RemoveAt(i);
                }
            }

            Profiles.Add(p);
            SaveProfiles();
            return p;
        }

        public void DeleteProfile(string profileName)
        {
            for (int i = Profiles.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Profiles[i].Name, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    Profiles.RemoveAt(i);
                }
            }
            SaveProfiles();
        }

        public void DeleteProfile(AudioProfile profile)
        {
            if (profile != null)
            {
                Profiles.Remove(profile);
                SaveProfiles();
            }
        }
    }
}
