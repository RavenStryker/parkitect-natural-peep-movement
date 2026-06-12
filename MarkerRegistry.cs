using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace NaturalPeepMovement
{
    [Serializable]
    public class MarkerOptions
    {
        public bool OnlyBlockWhileEffectActive;
        public string DisplayName;

        public MarkerOptions() { }
        public MarkerOptions(bool onlyBlockWhileEffectActive, string displayName)
        {
            OnlyBlockWhileEffectActive = onlyBlockWhileEffectActive;
            DisplayName = displayName;
        }

        public MarkerOptions Copy()
        {
            return new MarkerOptions
            {
                OnlyBlockWhileEffectActive = OnlyBlockWhileEffectActive,
                DisplayName = DisplayName,
            };
        }
    }

    public class MarkerRegistryPersisted
    {
        public List<MarkerRegistryPersistedMarker> registeredMarkers { get; set; } = new List<MarkerRegistryPersistedMarker>();
    }

    public class MarkerRegistryPersistedMarker
    {
        public string prefabName { get; set; }
        public bool onlyBlockWhileEffectActive { get; set; }
        public string displayName { get; set; }
    }

    internal static class MarkerRegistry
    {

        private static readonly object _lock = new object();
        private static Dictionary<string, MarkerOptions> _markers;
        private static bool _loaded;

        private static string GetFilePath()
        {
            return FilePaths.getFolderPath("Mods/NaturalPeepMovement/marker_registry.json");
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            lock (_lock)
            {
                if (_loaded) return;

                _markers = new Dictionary<string, MarkerOptions>(StringComparer.Ordinal);
                string path = GetFilePath();

                try
                {
                    if (File.Exists(path))
                    {
                        MarkerRegistryPersisted p = JsonConvert.DeserializeObject<MarkerRegistryPersisted>(File.ReadAllText(path));
                        ApplyMarkerRegistryPersistedToMarkers(p, _markers);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[NaturalPeepMovement] MarkerRegistry load failed: " + ex);
                    _markers.Clear();
                }

                _loaded = true;
            }
        }

        private static void ApplyMarkerRegistryPersistedToMarkers(MarkerRegistryPersisted p, Dictionary<string, MarkerOptions> target)
        {
            target.Clear();
            if (p == null || p.registeredMarkers == null) return;

            for (int i = 0; i < p.registeredMarkers.Count; i++)
            {
                MarkerRegistryPersistedMarker pm = p.registeredMarkers[i];
                if (pm == null || string.IsNullOrEmpty(pm.prefabName)) continue;
                target[pm.prefabName] = new MarkerOptions(pm.onlyBlockWhileEffectActive, pm.displayName);
            }
        }

        private static void SaveLocked()
        {
            string path = GetFilePath();
            try
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, JsonConvert.SerializeObject(BuildMarkerRegistryPersisted(_markers), Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] MarkerRegistry save failed: " + ex);
            }
        }

        private static MarkerRegistryPersisted BuildMarkerRegistryPersisted(Dictionary<string, MarkerOptions> source)
        {
            List<string> sortedNames = new List<string>(source.Keys);
            sortedNames.Sort(StringComparer.Ordinal);

            MarkerRegistryPersisted p = new MarkerRegistryPersisted
            {
                registeredMarkers = new List<MarkerRegistryPersistedMarker>(sortedNames.Count),
            };

            for (int i = 0; i < sortedNames.Count; i++)
            {
                string name = sortedNames[i];
                MarkerOptions opts = source[name];
                p.registeredMarkers.Add(new MarkerRegistryPersistedMarker
                {
                    prefabName = name,
                    onlyBlockWhileEffectActive = opts.OnlyBlockWhileEffectActive,
                    displayName = opts.DisplayName,
                });
            }
            return p;
        }

        public static bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                return _markers.ContainsKey(name);
            }
        }

        public static bool IsEmpty()
        {
            EnsureLoaded();
            lock (_lock)
            {
                return _markers.Count == 0;
            }
        }

        public static bool Register(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                if (_markers.ContainsKey(name)) return false;
                _markers[name] = new MarkerOptions();
                SaveLocked();
                return true;
            }
        }

        public static bool Unregister(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                if (!_markers.Remove(name)) return false;
                SaveLocked();
                return true;
            }
        }

        public static List<string> GetAll()
        {
            EnsureLoaded();
            lock (_lock)
            {
                List<string> list = new List<string>(_markers.Keys);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        public static MarkerOptions GetOptions(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            EnsureLoaded();
            lock (_lock)
            {
                MarkerOptions opts;
                return _markers.TryGetValue(name, out opts) ? opts.Copy() : null;
            }
        }

        public static bool SetOptions(string name, MarkerOptions opts)
        {
            if (string.IsNullOrEmpty(name) || opts == null) return false;
            EnsureLoaded();
            lock (_lock)
            {
                if (!_markers.ContainsKey(name)) return false;
                _markers[name] = opts.Copy();
                SaveLocked();
                return true;
            }
        }

        private const string PresetsSubfolder = "Mods/NaturalPeepMovement/presets";

        private static readonly HashSet<char> InvalidFilenameChars =
            new HashSet<char>(System.IO.Path.GetInvalidFileNameChars());

        public static string GetPresetsFolder()
        {
            return FilePaths.getFolderPath(PresetsSubfolder);
        }

        private static void EnsurePresetsFolder()
        {
            string folder = GetPresetsFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        public static string SanitizeFilename(string raw, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(raw)) { error = "Name is empty."; return null; }

            string trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed)) { error = "Name is empty."; return null; }

            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 5).Trim();

            if (string.IsNullOrEmpty(trimmed)) { error = "Name is empty."; return null; }
            if (trimmed == "." || trimmed == "..") { error = "Invalid name."; return null; }
            if (trimmed.Contains("..")) { error = "Invalid name."; return null; }

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (InvalidFilenameChars.Contains(trimmed[i]))
                {
                    error = "Invalid character: " + trimmed[i];
                    return null;
                }
            }
            return trimmed;
        }

        public static List<string> ListPresets()
        {
            List<string> result = new List<string>();
            try
            {
                EnsurePresetsFolder();
                string[] files = Directory.GetFiles(GetPresetsFolder(), "*.json");
                for (int i = 0; i < files.Length; i++)
                {
                    string name = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                    if (!string.IsNullOrEmpty(name)) result.Add(name);
                }
                result.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] ListPresets failed: " + ex);
            }
            return result;
        }

        public static bool PresetExists(string presetName)
        {
            string error;
            string clean = SanitizeFilename(presetName, out error);
            if (clean == null) return false;
            string path = System.IO.Path.Combine(GetPresetsFolder(), clean + ".json");
            return File.Exists(path);
        }

        public static bool LoadFromFile(string presetName, out string error, out int loadedCount)
        {
            loadedCount = 0;
            string clean = SanitizeFilename(presetName, out error);
            if (clean == null) return false;

            string path = System.IO.Path.Combine(GetPresetsFolder(), clean + ".json");
            if (!File.Exists(path))
            {
                error = "File not found: " + clean + ".json";
                return false;
            }

            try
            {
                MarkerRegistryPersisted p = JsonConvert.DeserializeObject<MarkerRegistryPersisted>(File.ReadAllText(path));
                if (p == null || p.registeredMarkers == null || p.registeredMarkers.Count == 0)
                {
                    error = "File is empty or malformed.";
                    return false;
                }

                EnsureLoaded();
                lock (_lock)
                {
                    ApplyMarkerRegistryPersistedToMarkers(p, _markers);
                    loadedCount = _markers.Count;
                    SaveLocked();
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Load failed: " + ex.Message;
                Debug.LogError("[NaturalPeepMovement] LoadFromFile(" + clean + ") failed: " + ex);
                return false;
            }
        }

        public static bool SaveToFile(string presetName, out string error, out int savedCount)
        {
            savedCount = 0;
            string clean = SanitizeFilename(presetName, out error);
            if (clean == null) return false;

            EnsureLoaded();
            try
            {
                EnsurePresetsFolder();
                string path = System.IO.Path.Combine(GetPresetsFolder(), clean + ".json");

                MarkerRegistryPersisted p;
                lock (_lock)
                {
                    p = BuildMarkerRegistryPersisted(_markers);
                }
                savedCount = p.registeredMarkers.Count;

                File.WriteAllText(path, JsonConvert.SerializeObject(p, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                error = "Save failed: " + ex.Message;
                Debug.LogError("[NaturalPeepMovement] SaveToFile(" + clean + ") failed: " + ex);
                return false;
            }
        }
    }
}
