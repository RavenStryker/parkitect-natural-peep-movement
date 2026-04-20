using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NaturalPeepMovement
{
    // Hotkey combo: Ctrl/Shift/Alt toggles + a non-modifier main key.
    internal static class HotkeySettings
    {
        public static readonly KeyCode DefaultMainKey = KeyCode.Backslash;
        public const bool DefaultRequireCtrl = true;
        public const bool DefaultRequireShift = false;
        public const bool DefaultRequireAlt = true;

        [Serializable]
        private class Persisted
        {
            public string mainKey;
            public bool requireCtrl;
            public bool requireShift;
            public bool requireAlt;
        }

        private static readonly object _lock = new object();
        private static KeyCode _mainKey = DefaultMainKey;
        private static bool _requireCtrl = DefaultRequireCtrl;
        private static bool _requireShift = DefaultRequireShift;
        private static bool _requireAlt = DefaultRequireAlt;
        private static bool _loaded;

        public static KeyCode MainKey { get { EnsureLoaded(); return _mainKey; } }
        public static bool RequireCtrl { get { EnsureLoaded(); return _requireCtrl; } }
        public static bool RequireShift { get { EnsureLoaded(); return _requireShift; } }
        public static bool RequireAlt { get { EnsureLoaded(); return _requireAlt; } }

        public static void SetCombo(KeyCode mainKey, bool requireCtrl, bool requireShift, bool requireAlt)
        {
            EnsureLoaded();
            lock (_lock)
            {
                _mainKey = mainKey;
                _requireCtrl = requireCtrl;
                _requireShift = requireShift;
                _requireAlt = requireAlt;
                SaveLocked();
            }
        }

        public static void ResetToDefault()
        {
            SetCombo(DefaultMainKey, DefaultRequireCtrl, DefaultRequireShift, DefaultRequireAlt);
        }

        public static string FormatCombo()
        {
            EnsureLoaded();
            List<string> parts = new List<string>();
            if (_requireCtrl) parts.Add("Ctrl");
            if (_requireShift) parts.Add("Shift");
            if (_requireAlt) parts.Add("Alt");
            parts.Add(FormatKey(_mainKey));
            return string.Join(" + ", parts.ToArray());
        }

        public static string FormatKey(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.None: return "(None)";
                case KeyCode.BackQuote: return "~";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Esc";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Slash: return "/";
                case KeyCode.Period: return ".";
                case KeyCode.Comma: return ",";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                default: return k.ToString();
            }
        }

        public static bool IsModifier(KeyCode k)
        {
            return k == KeyCode.LeftControl || k == KeyCode.RightControl
                || k == KeyCode.LeftShift || k == KeyCode.RightShift
                || k == KeyCode.LeftAlt || k == KeyCode.RightAlt;
        }

        private static string GetFilePath()
        {
            return FilePaths.getFolderPath("Mods/NaturalPeepMovement/settings.json");
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            lock (_lock)
            {
                if (_loaded) return;
                _loaded = true;

                string path = GetFilePath();
                if (!File.Exists(path)) return;

                try
                {
                    Persisted p = JsonUtility.FromJson<Persisted>(File.ReadAllText(path));
                    // Missing mainKey = old/unknown format; keep all defaults.
                    if (p == null || string.IsNullOrEmpty(p.mainKey)) return;

                    try { _mainKey = (KeyCode)Enum.Parse(typeof(KeyCode), p.mainKey, ignoreCase: true); }
                    catch { _mainKey = DefaultMainKey; }

                    _requireCtrl = p.requireCtrl;
                    _requireShift = p.requireShift;
                    _requireAlt = p.requireAlt;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[NaturalPeepMovement] HotkeySettings load failed: " + ex);
                }
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

                Persisted p = new Persisted
                {
                    mainKey = _mainKey.ToString(),
                    requireCtrl = _requireCtrl,
                    requireShift = _requireShift,
                    requireAlt = _requireAlt,
                };
                File.WriteAllText(path, JsonUtility.ToJson(p, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] HotkeySettings save failed: " + ex);
            }
        }
    }
}
