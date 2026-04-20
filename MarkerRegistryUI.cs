using System;
using HarmonyLib;
using Parkitect.UI;
using UnityEngine;

namespace NaturalPeepMovement
{
    internal static class MarkerRegistryUI
    {
        private static RegistrationWindow _openWindow;

        private static GameObject _tickerGO;
        private static Park _lastSeenPark;

        // Suppress hotkey while mod settings panel is open.
        public static bool SuppressHotkey { get; set; }

        public static void Initialize(Harmony harmony)
        {
            if (_tickerGO == null)
            {
                _tickerGO = new GameObject("[NaturalPeepMovement] MarkerRegistryUI");
                _tickerGO.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(_tickerGO);
                _tickerGO.AddComponent<HotkeyTicker>();
            }
        }

        private static void OnParkLoaded(Park park)
        {
            if (park == null) return;

            string name = park.parkName;
            if (string.IsNullOrEmpty(name)) return;

            string sanitizeError;
            string clean = MarkerRegistry.SanitizeFilename(name, out sanitizeError);
            if (clean == null) return;
            if (!MarkerRegistry.PresetExists(clean)) return;

            string loadError;
            int loadedCount;
            if (!MarkerRegistry.LoadFromFile(clean, out loadError, out loadedCount))
            {
                Debug.LogWarning("[NaturalPeepMovement] Auto-load matched '" + clean + ".json' but failed: " + loadError);
                return;
            }

            Debug.Log("[NaturalPeepMovement] Auto-loaded " + loadedCount + " markers from '" + clean +
                ".json' (matched park name '" + name + "')");

            // Refresh open window so it reflects the new state.
            if (_openWindow != null && _openWindow.windowFrame != null)
            {
                try { _openWindow.RefreshAll(); }
                catch (Exception ex) { Debug.LogError("[NaturalPeepMovement] Window refresh failed: " + ex); }
            }

            ShowAutoLoadNotifications(loadedCount, clean);
        }

        private static void ShowAutoLoadNotifications(int count, string presetName)
        {
            string title = "Path Blocking Markers";
            string body = count + " path blocking markers loaded from " + presetName + ".json";

            try
            {
                if (NotificationBar.Instance != null)
                    NotificationBar.Instance.addNotification(new Notification(title, body, Notification.Type.SAVE));
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] Persistent notification failed: " + ex);
            }

            try
            {
                if (SystemNotificationManager.Instance != null)
                    SystemNotificationManager.Instance.spawnNotification(title, body);
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] Toast notification failed: " + ex);
            }
        }

        public static void Teardown()
        {
            if (_tickerGO != null)
            {
                UnityEngine.Object.Destroy(_tickerGO);
                _tickerGO = null;
            }
        }

        public class HotkeyTicker : MonoBehaviour
        {
            // Last frame's combo state for rising-edge detection.
            private bool _lastFrameComboHeld;

            private void Update()
            {
                HandleHotkey();
                AutoSyncWindowDeco();
                DetectParkLoad();
            }

            // Poll-based: initial load can precede mod enable, so a Harmony patch misses it.
            private void DetectParkLoad()
            {
                if (GameController.Instance == null) return;
                if (GameController.Instance.isLoadingGame) return;

                Park currentPark = GameController.Instance.park;
                if (currentPark == _lastSeenPark) return;

                _lastSeenPark = currentPark;
                if (currentPark != null)
                {
                    try { OnParkLoaded(currentPark); }
                    catch (Exception ex) { Debug.LogError("[NaturalPeepMovement] OnParkLoaded threw: " + ex); }
                }
            }

            private void HandleHotkey()
            {
                bool comboHeldNow = SuppressHotkey ? false : IsComboHeld();
                bool risingEdge = comboHeldNow && !_lastFrameComboHeld;
                _lastFrameComboHeld = comboHeldNow;

                if (!risingEdge) return;

                // Pure toggle: if window is open, close it.
                if (_openWindow != null && _openWindow.windowFrame != null)
                {
                    _openWindow.windowFrame.close();
                    _openWindow = null;
                    return;
                }

                string name = TryGetActiveDecoName();
                if (name == null)
                {
                    Debug.Log("[NaturalPeepMovement] Hotkey: no Deco selected for placement.");
                    return;
                }

                _openWindow = RegistrationWindow.Build(name);
                UIWindowFrame frame = UIWindowsController.Instance.spawnWindow(_openWindow);
                frame.OnClose += OnWindowClosed;
            }

            private static bool IsComboHeld()
            {
                if (HotkeySettings.MainKey == KeyCode.None) return false;

                if (HotkeySettings.RequireCtrl &&
                    !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    return false;
                if (HotkeySettings.RequireShift &&
                    !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                    return false;
                if (HotkeySettings.RequireAlt &&
                    !(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                    return false;

                return Input.GetKey(HotkeySettings.MainKey);
            }

            // Live-sync the open window to the active DecoBuilder's deco.
            private void AutoSyncWindowDeco()
            {
                if (_openWindow == null || _openWindow.windowFrame == null) return;

                string current = TryGetActiveDecoName();
                if (current == null) return;
                if (current == _openWindow.DecoName) return;

                _openWindow.SetDeco(current);
            }

            private static string TryGetActiveDecoName()
            {
                if (GameController.Instance == null) return null;
                DecoBuilder builder = GameController.Instance.getActiveMouseTool() as DecoBuilder;
                if (builder == null) return null;
                Deco deco = builder.builtObjectGO as Deco;
                if (deco == null) return null;
                string name = deco.getReferenceName();
                return string.IsNullOrEmpty(name) ? null : name;
            }

            private void OnWindowClosed(UIWindowFrame frame)
            {
                _openWindow = null;
            }
        }
    }
}
