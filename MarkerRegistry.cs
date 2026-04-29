using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NaturalPeepMovement
{
    internal static class MarkerRegistry
    {
        [Serializable]
        private class Persisted
        {
            public List<string> registeredPrefabNames = new List<string>();
        }

        private static readonly object _lock = new object();
        private static HashSet<string> _names;
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

                _names = new HashSet<string>(StringComparer.Ordinal);
                string path = GetFilePath();

                try
                {
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        Persisted p = JsonUtility.FromJson<Persisted>(json);
                        if (p != null && p.registeredPrefabNames != null)
                        {
                            for (int i = 0; i < p.registeredPrefabNames.Count; i++)
                            {
                                string n = p.registeredPrefabNames[i];
                                if (!string.IsNullOrEmpty(n)) _names.Add(n);
                            }
                        }
                    }
                    // No file → start empty; users register their own markers.
                }
                catch (Exception ex)
                {
                    Debug.LogError("[NaturalPeepMovement] MarkerRegistry load failed: " + ex);
                    _names.Clear();
                }

                _loaded = true;
            }
        }

        // Caller must hold _lock.
        private static void SaveLocked()
        {
            string path = GetFilePath();
            try
            {
                string dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Persisted p = new Persisted();
                p.registeredPrefabNames = new List<string>(_names);
                p.registeredPrefabNames.Sort(StringComparer.Ordinal);
                File.WriteAllText(path, JsonUtility.ToJson(p, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] MarkerRegistry save failed: " + ex);
            }
        }

        public static bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                return _names.Contains(name);
            }
        }

        public static bool IsEmpty()
        {
            EnsureLoaded();
            lock (_lock)
            {
                return _names.Count == 0;
            }
        }

        public static bool Register(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            EnsureLoaded();
            lock (_lock)
            {
                if (!_names.Add(name)) return false;
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
                if (!_names.Remove(name)) return false;
                SaveLocked();
                return true;
            }
        }

        public static List<string> GetAll()
        {
            EnsureLoaded();
            lock (_lock)
            {
                List<string> list = new List<string>(_names);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        // Preset files live in presets/ subfolder; Load also writes through to working file.
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
                string json = File.ReadAllText(path);
                Persisted p = JsonUtility.FromJson<Persisted>(json);
                if (p == null || p.registeredPrefabNames == null)
                {
                    error = "File is empty or malformed.";
                    return false;
                }

                EnsureLoaded();
                lock (_lock)
                {
                    _names.Clear();
                    for (int i = 0; i < p.registeredPrefabNames.Count; i++)
                    {
                        string n = p.registeredPrefabNames[i];
                        if (!string.IsNullOrEmpty(n)) _names.Add(n);
                    }
                    loadedCount = _names.Count;
                    // Write through so next launch boots from this state.
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

                Persisted p = new Persisted();
                lock (_lock)
                {
                    p.registeredPrefabNames = new List<string>(_names);
                }
                p.registeredPrefabNames.Sort(StringComparer.Ordinal);
                savedCount = p.registeredPrefabNames.Count;

                File.WriteAllText(path, JsonUtility.ToJson(p, prettyPrint: true));
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
