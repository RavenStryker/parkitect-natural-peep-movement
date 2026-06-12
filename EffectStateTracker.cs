using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaturalPeepMovement
{
    internal static class EffectStateTracker
    {
        private static readonly object _lock = new object();
        private static HashSet<GameObject> _activeTargets = new HashSet<GameObject>();
        private static readonly HashSet<EffectRunner> _subscribed = new HashSet<EffectRunner>();
        private static bool _initialized;
        private static bool _enabled;

        public static bool IsEnabled => _enabled;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                if (CommandController.Instance != null && CommandController.Instance.isInMultiplayerMode())
                {
                    Debug.Log("[NaturalPeepMovement] EffectStateTracker disabled (multiplayer session — effects aren't synced across clients).");
                    _enabled = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[NaturalPeepMovement] MP check failed; disabling EffectStateTracker as a precaution: " + ex);
                _enabled = false;
                return;
            }

            _enabled = true;
            Debug.Log("[NaturalPeepMovement] EffectStateTracker initialized, enabled=" + _enabled);
        }

        public static bool IsActive(SerializedMonoBehaviour target)
        {
            if (!_enabled || target == null) return false;
            GameObject go = target.gameObject;
            if (go == null) return false;
            HashSet<GameObject> snapshot = _activeTargets;
            return snapshot.Contains(go);
        }

        public static void OnEffectBoxAwake(EffectBox box)
        {
            if (!_enabled || box == null) return;
            EffectRunner runner = box.GetComponent<EffectRunner>();
            if (runner == null) return;
            Subscribe(runner);
        }

        public static void RebuildFromScene()
        {
            if (!_enabled) return;

            EffectBox[] boxes = UnityEngine.Object.FindObjectsOfType<EffectBox>();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null) continue;
                EffectRunner runner = boxes[i].GetComponent<EffectRunner>();
                if (runner != null) Subscribe(runner);
            }

            lock (_lock)
            {
                _activeTargets = new HashSet<GameObject>();
            }
        }

        private static void Subscribe(EffectRunner runner)
        {
            lock (_lock)
            {
                if (!_subscribed.Add(runner)) return;
            }
            runner.onEffectEntryExecuted += OnEffectEntryExecuted;
            runner.onEffectEntryEnded += OnEffectEntryEnded;
        }

        private static void OnEffectEntryExecuted(EffectEntry entry)
        {
            if (entry == null) return;
            SerializedMonoBehaviour target = entry.getTarget();
            if (target == null) return;
            GameObject go = target.gameObject;
            if (go == null) return;

            lock (_lock)
            {
                if (_activeTargets.Contains(go)) return;
                HashSet<GameObject> newSet = new HashSet<GameObject>(_activeTargets) { go };
                _activeTargets = newSet;
            }

            Debug.Log("[NaturalPeepMovement] Effect started on '" + GetDecoName(target) + "'");
        }

        private static void OnEffectEntryEnded(EffectEntry entry)
        {
            if (entry == null) return;
            SerializedMonoBehaviour target = entry.getTarget();
            if (target == null) return;
            GameObject go = target.gameObject;
            if (go == null) return;

            lock (_lock)
            {
                if (!_activeTargets.Contains(go)) return;
                HashSet<GameObject> newSet = new HashSet<GameObject>(_activeTargets);
                newSet.Remove(go);
                _activeTargets = newSet;
            }

            Debug.Log("[NaturalPeepMovement] Effect ended on '" + GetDecoName(target) + "'");
        }

        private static string GetDecoName(SerializedMonoBehaviour target)
        {
            try
            {
                BuildableObject obj = target as BuildableObject;
                if (obj != null) return obj.getReferenceName();
                return target.GetType().Name;
            }
            catch
            {
                return "<unknown>";
            }
        }
    }
}
