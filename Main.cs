using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace NaturalPeepMovement
{
    public class Main : AbstractMod, IModSettings
    {
        public static Main Instance;
        public static string PatchStatus = "Not initialized";

        private object _harmony;

        // True while waiting for the user to press a combo.
        private bool _isListening;

        static Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name);
                if (!assemblyName.Name.Equals("0Harmony", StringComparison.OrdinalIgnoreCase))
                    return null;

                string modDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string harmonyPath = System.IO.Path.Combine(modDir, "0Harmony.dll");

                if (System.IO.File.Exists(harmonyPath))
                    return Assembly.LoadFrom(harmonyPath);

                return null;
            };
        }

        public override string getIdentifier() => "NaturalPeepMovement";
        public override string getName() => "Natural Peep Movement";

        public override string getDescription() =>
            "Enables peeps and workers to walk diagonally between connected path tiles " +
            "instead of being restricted to the four cardinal directions for more natural movement. " +
            "Queues, ride entrances, and shop interactions remain untouched.\n\n" +
            "Multiplayer: All players in the session must have this mod installed.";

        public override string getVersionNumber() => "2.0.0";
        public override bool isMultiplayerModeCompatible() => true;
        public override bool isRequiredByAllPlayersInMultiplayerMode() => true;

        public override void onEnabled()
        {
            Instance = this;

            try
            {
                var harmony = new HarmonyLib.Harmony(getIdentifier());
                _harmony = harmony;
                PeepMovementPatcher.PatchAll(harmony);
                MarkerRegistryUI.Initialize(harmony);

                PatchStatus = "Patched " + PeepMovementPatcher.PatchedCount + " methods";
                if (PeepMovementPatcher.PatchError != null)
                    PatchStatus += " (error: " + PeepMovementPatcher.PatchError + ")";

                Debug.Log("[NaturalPeepMovement] " + PatchStatus);
            }
            catch (Exception ex)
            {
                PatchStatus = "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message;
                Debug.LogError("[NaturalPeepMovement] " + ex);
            }
        }

        public override void onDisabled()
        {
            MarkerRegistryUI.Teardown();
            PeepMovementPatcher.Teardown();

            if (_harmony != null)
            {
                ((HarmonyLib.Harmony)_harmony).UnpatchAll(getIdentifier());
                _harmony = null;
            }
        }

        // IModSettings — IMGUI panel inside ModsSettingsTab.

        public void onSettingsOpened()
        {
            _isListening = false;
            // Suppress hotkey so binding it doesn't fire the action.
            MarkerRegistryUI.SuppressHotkey = true;
        }

        public void onSettingsClosed()
        {
            _isListening = false;
            MarkerRegistryUI.SuppressHotkey = false;
        }

        public void onDrawSettingsUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hotkey:", GUILayout.Width(80f));

            string display = _isListening ? "Press a combo… (Esc to cancel)" : HotkeySettings.FormatCombo();
            if (GUILayout.Button(display, GUILayout.Width(280f)))
            {
                _isListening = true;
            }
            GUILayout.EndHorizontal();

            if (_isListening)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.isKey)
                {
                    if (e.keyCode == KeyCode.Escape)
                    {
                        _isListening = false;
                        e.Use();
                    }
                    else if (e.keyCode != KeyCode.None && !HotkeySettings.IsModifier(e.keyCode))
                    {
                        HotkeySettings.SetCombo(e.keyCode, e.control, e.shift, e.alt);
                        _isListening = false;
                        e.Use();
                    }
                    // Pure modifiers ignored; wait for a real key.
                }
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("Reset to default (Ctrl + Alt + \\)", GUILayout.ExpandWidth(false)))
            {
                HotkeySettings.ResetToDefault();
                _isListening = false;
            }
        }
    }
}
