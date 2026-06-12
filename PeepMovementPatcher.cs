using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BehaviourTree;
using HarmonyLib;
using UnityEngine;

namespace NaturalPeepMovement
{
    internal static class PeepMovementPatcher
    {
        public static int PatchedCount { get; private set; }
        public static string PatchError { get; private set; }
        private static GameObject _markerCacheTickerGO;

        public static void PatchAll(Harmony harmony)
        {
            PatchedCount = 0;
            PatchError = null;

            try
            {
                EnsureMarkerCacheTicker();

                harmony.Patch(
                    AccessTools.Method(typeof(BlockNeighbour), "calculateSide"),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(BlockNeighbour_calculateSide_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Block), "canMoveFromThisTo", new Type[] { typeof(Block), typeof(Vector3) }),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Block_canMoveFromThisTo_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Block), "findConnected"),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Block_findConnected_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Block), "getConnectedSidesBitmask", new Type[] { typeof(BlockData), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Block_getConnectedSidesBitmask_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(PathTileMapper), "getTileForwardFor"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(PathTileMapper_getTileForwardFor_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(PathTileMapper), "getTileFor"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(PathTileMapper_getTileFor_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Path), "canBeSteppedOn"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Path_canBeSteppedOn_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Block), "instantiateTunnelFrame"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Block_instantiateTunnelFrame_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Pathfinding.PathNode), "calculateCosts"),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Pathfinding_PathNode_calculateCosts_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Pathfinding.PathNode), "fillReachableNodesList",
                        new Type[] { typeof(Pathfinding.PathfindingData), typeof(bool), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Pathfinding_PathNode_fillReachableNodesList_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Block), "canBeWanderedOn",
                        new Type[] { typeof(Block), typeof(Person) }),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Block_canBeWanderedOn_Postfix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(WalkToPositionAction), "run"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(WalkToPositionAction_run_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(Person), "checkIfReachedNewBlock"),
                    prefix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(Person_checkIfReachedNewBlock_Prefix)));
                PatchedCount++;

                harmony.Patch(
                    AccessTools.Method(typeof(SerializedMonoBehaviour), "Awake"),
                    postfix: new HarmonyMethod(typeof(PeepMovementPatcher), nameof(SerializedMonoBehaviour_Awake_Postfix)));
                PatchedCount++;
            }
            catch (Exception ex)
            {
                PatchError = ex.GetType().Name + ": " + ex.Message;
                Debug.LogError("[NaturalPeepMovement] Patch failed: " + ex);
            }
        }

        public static void Teardown()
        {
            if (_markerCacheTickerGO != null)
            {
                UnityEngine.Object.Destroy(_markerCacheTickerGO);
                _markerCacheTickerGO = null;
            }
        }

        private static void EnsureMarkerCacheTicker()
        {
            if (_markerCacheTickerGO != null) return;
            _markerCacheTickerGO = new GameObject("[NaturalPeepMovement] MarkerCacheTicker");
            _markerCacheTickerGO.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(_markerCacheTickerGO);
            _markerCacheTickerGO.AddComponent<MarkerCacheTicker>();
        }

        public static void BlockNeighbour_calculateSide_Postfix(BlockNeighbour __instance, Vector3 relativeTo)
        {
            if (__instance == null || __instance.block == null) return;

            int side = BlockHelper.calculateSideOtherTileIsOn(
                Mathf.FloorToInt(relativeTo.x),
                Mathf.FloorToInt(relativeTo.z),
                __instance.block.tilePosition.x,
                __instance.block.tilePosition.z);

            if (side < 0) return;

            __instance.side = side;
            __instance.sideBit = 1 << side;
        }

        public static void Block_canMoveFromThisTo_Postfix(Block __instance, Block otherBlock, ref bool __result)
        {
            if (__result) return;
            if (__instance == null || otherBlock == null) return;

            int dx = otherBlock.tilePosition.x - __instance.tilePosition.x;
            int dz = otherBlock.tilePosition.z - __instance.tilePosition.z;
            if (dx == 0 || dz == 0) return;
            if (Mathf.Abs(dx) > 1 || Mathf.Abs(dz) > 1) return;

            if (!IsWalkablePath(__instance) || !IsWalkablePath(otherBlock)) return;
            if (!Mathf.Approximately(__instance.tilePosition.y, otherBlock.tilePosition.y)) return;

            __result = true;
        }

        public static void Block_findConnected_Postfix(Block __instance, BlockData blockData, ref List<BlockNeighbour> __result)
        {
            if (__result == null || __result.Count == 0) return;

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                BlockNeighbour bn = __result[i];
                if (bn == null || bn.block == null) continue;
                if (bn.side < 4) continue;

                if (!IsAllowedDiagonalConnection(__instance, bn.block, blockData))
                    __result.RemoveAt(i);
            }
        }

        internal static class MarkerCache
        {
            private const float RefreshIntervalSeconds = 1.0f;

            internal struct MarkerEntry
            {
                public Bounds Bounds;
                public SerializedMonoBehaviour Target;
                public bool OnlyBlockWhileEffectActive;
            }

            private static List<MarkerEntry> _entries = new List<MarkerEntry>();
            private static float _lastRefreshTime = -1f;

            private static readonly HashSet<BuildableObject> _allMarkerCandidates = new HashSet<BuildableObject>();
            private static readonly List<BuildableObject> _scratchToRemove = new List<BuildableObject>();

            public static void RegisterCandidate(BuildableObject obj)
            {
                if (obj == null) return;
                _allMarkerCandidates.Add(obj);
            }

            public static void RebuildDecoSetFromScene()
            {
                _allMarkerCandidates.Clear();
                _entries = new List<MarkerEntry>();
                _lastRefreshTime = -1f;

                Deco[] decos = UnityEngine.Object.FindObjectsOfType<Deco>();
                for (int i = 0; i < decos.Length; i++)
                    if (decos[i] != null) _allMarkerCandidates.Add(decos[i]);

                PathAttachment[] pas = UnityEngine.Object.FindObjectsOfType<PathAttachment>();
                for (int i = 0; i < pas.Length; i++)
                    if (pas[i] != null) _allMarkerCandidates.Add(pas[i]);
            }

            private static float _currentTimeApprox;
            public static float CurrentTimeApprox { get { return _currentTimeApprox; } }

            public static void UpdateCurrentTimeMainThread()
            {
                _currentTimeApprox = Time.unscaledTime;
            }

            public static bool IsBlockedByActiveEffectGate(Block b)
            {
                if (b == null) return false;
                List<MarkerEntry> snapshot = _entries;
                if (snapshot.Count == 0) return false;

                int tileX = b.tilePosition.x;
                int tileZ = b.tilePosition.z;
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                float yCenter = b.centerPosition.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    MarkerEntry e = snapshot[i];
                    if (!e.OnlyBlockWhileEffectActive) continue;
                    Bounds m = e.Bounds;
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;
                    if (EffectStateTracker.IsActive(e.Target)) return true;
                }
                return false;
            }

            public static bool IsBlockBlocked(Block b)
            {
                if (b == null) return false;
                List<MarkerEntry> snapshot = _entries;
                if (snapshot.Count == 0) return false;

                int tileX = b.tilePosition.x;
                int tileZ = b.tilePosition.z;
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                float yCenter = b.centerPosition.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    MarkerEntry e = snapshot[i];
                    Bounds m = e.Bounds;
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;

                    if (e.OnlyBlockWhileEffectActive && !EffectStateTracker.IsActive(e.Target))
                        continue;

                    return true;
                }
                return false;
            }

            public static bool IsTargetTileBlocked(Vector3 pos)
            {
                List<MarkerEntry> snapshot = _entries;
                if (snapshot.Count == 0) return false;

                int tileX = Mathf.FloorToInt(pos.x);
                int tileZ = Mathf.FloorToInt(pos.z);
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                float yCenter = pos.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    MarkerEntry e = snapshot[i];
                    Bounds m = e.Bounds;
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;

                    if (e.OnlyBlockWhileEffectActive && !EffectStateTracker.IsActive(e.Target))
                        continue;

                    return true;
                }
                return false;
            }

            public static bool IsTargetTileBlockedByActiveEffect(Vector3 pos)
            {
                List<MarkerEntry> snapshot = _entries;
                if (snapshot.Count == 0) return false;

                int tileX = Mathf.FloorToInt(pos.x);
                int tileZ = Mathf.FloorToInt(pos.z);
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                float yCenter = pos.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    MarkerEntry e = snapshot[i];
                    if (!e.OnlyBlockWhileEffectActive) continue;
                    Bounds m = e.Bounds;
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;
                    if (EffectStateTracker.IsActive(e.Target)) return true;
                }
                return false;
            }

            public static bool IsBlockBlockedByPermanentMarker(Block b)
            {
                if (b == null) return false;
                List<MarkerEntry> snapshot = _entries;
                if (snapshot.Count == 0) return false;

                int tileX = b.tilePosition.x;
                int tileZ = b.tilePosition.z;
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                float yCenter = b.centerPosition.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    MarkerEntry e = snapshot[i];
                    if (e.OnlyBlockWhileEffectActive) continue;
                    Bounds m = e.Bounds;
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;
                    return true;
                }
                return false;
            }

            public static void RefreshIfDueMainThread()
            {
                float now = Time.unscaledTime;
                if (_lastRefreshTime >= 0f && now - _lastRefreshTime < RefreshIntervalSeconds) return;
                _lastRefreshTime = now;

                if (MarkerRegistry.IsEmpty())
                {
                    if (_entries.Count != 0) _entries = new List<MarkerEntry>();
                    return;
                }

                List<MarkerEntry> newEntries = new List<MarkerEntry>();
                _scratchToRemove.Clear();

                foreach (BuildableObject obj in _allMarkerCandidates)
                {
                    if (obj == null)
                    {
                        _scratchToRemove.Add(obj);
                        continue;
                    }

                    string name = obj.getReferenceName();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!MarkerRegistry.Contains(name)) continue;

                    if (!TryComputeWorldBounds(obj, out Bounds wb)) continue;

                    MarkerOptions opts = MarkerRegistry.GetOptions(name);
                    bool effectGated = opts != null && opts.OnlyBlockWhileEffectActive;
                    newEntries.Add(new MarkerEntry
                    {
                        Bounds = wb,
                        Target = obj,
                        OnlyBlockWhileEffectActive = effectGated,
                    });
                }

                for (int i = 0; i < _scratchToRemove.Count; i++)
                    _allMarkerCandidates.Remove(_scratchToRemove[i]);

                _entries = newEntries;
            }

            private static bool TryComputeWorldBounds(BuildableObject obj, out Bounds bounds)
            {
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
                if (renderers == null || renderers.Length == 0)
                {
                    bounds = default;
                    return false;
                }

                Bounds total = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    total.Encapsulate(renderers[i].bounds);

                bounds = total;
                return true;
            }

        }

        public class MarkerCacheTicker : MonoBehaviour
        {
            private void Update()
            {
                MarkerCache.UpdateCurrentTimeMainThread();
                MarkerCache.RefreshIfDueMainThread();
            }
        }

        private static bool IsAllowedDiagonalConnection(Block from, Block to, BlockData blockData)
        {
            if (blockData == null) return false;
            if (!IsWalkablePath(from) || !IsWalkablePath(to)) return false;

            int dx = to.tilePosition.x - from.tilePosition.x;
            int dz = to.tilePosition.z - from.tilePosition.z;
            if (dx == 0 || dz == 0) return false;

            float y = from.tilePosition.y;
            Block cornerA = blockData.getBlock(from.tilePosition.x + dx, y, from.tilePosition.z);
            Block cornerB = blockData.getBlock(from.tilePosition.x, y, from.tilePosition.z + dz);

            if (!IsWalkablePath(cornerA) || !IsWalkablePath(cornerB)) return false;

            if (TileHasWall(from) || TileHasWall(to) ||
                TileHasWall(cornerA) || TileHasWall(cornerB))
                return false;

            if (IsCardinalOnlyBlock(from) || IsCardinalOnlyBlock(to) ||
                IsCardinalOnlyBlock(cornerA) || IsCardinalOnlyBlock(cornerB))
                return false;

            return true;
        }

        private static bool IsCardinalOnlyBlock(Block b)
        {
            if (b == null) return false;
            return b is GuestEntrance || b is EntranceExit;
        }

        private static bool IsWalkablePath(Block b)
        {
            if (b == null) return false;
            if (!(b is Path)) return false;
            if (b is Queue) return false;
            if (!b.isFlat()) return false;
            return true;
        }

        private static bool TileHasWall(Block tile)
        {
            if (tile == null) return false;
            Walls walls = GameController.Instance?.park?.walls;
            if (walls == null) return false;

            List<Wall> tileWalls = walls.getWalls(tile.tilePosition.x, tile.tilePosition.z);
            if (tileWalls == null || tileWalls.Count == 0) return false;

            float y = tile.centerPosition.y;
            for (int i = 0; i < tileWalls.Count; i++)
            {
                if (tileWalls[i] != null && tileWalls[i].mightBlockAtHeight(y))
                    return true;
            }
            return false;
        }

        public static void Block_getConnectedSidesBitmask_Postfix(ref int __result)
        {
            __result &= 0x0F;
        }

        private static readonly AccessTools.FieldRef<PathTileMapper, Dictionary<int, Vector3>> _forwardsRef =
            AccessTools.FieldRefAccess<PathTileMapper, Dictionary<int, Vector3>>("forwards");

        public static bool PathTileMapper_getTileForwardFor_Prefix(PathTileMapper __instance, int tileIndex, ref Vector3 __result)
        {
            if (__instance == null)
            {
                __result = Vector3.forward;
                return false;
            }

            int sanitized = tileIndex & 0xFF;
            Dictionary<int, Vector3> forwards = _forwardsRef(__instance);

            if (forwards != null && forwards.TryGetValue(sanitized, out Vector3 v))
            {
                __result = v;
                return false;
            }

            if (forwards != null && forwards.TryGetValue(sanitized & 0x0F, out v))
            {
                __result = v;
                return false;
            }

            __result = Vector3.forward;
            return false;
        }

        private static readonly AccessTools.FieldRef<PathTileMapper, Dictionary<int, GameObject>> _tileMapRef =
            AccessTools.FieldRefAccess<PathTileMapper, Dictionary<int, GameObject>>("tileMap");

        public static bool PathTileMapper_getTileFor_Prefix(PathTileMapper __instance, int tileIndex, ref GameObject __result)
        {
            if (__instance == null)
            {
                __result = null;
                return false;
            }

            int sanitized = tileIndex & 0xFF;
            Dictionary<int, GameObject> tileMap = _tileMapRef(__instance);

            if (tileMap != null && tileMap.TryGetValue(sanitized, out GameObject go))
            {
                __result = go;
                return false;
            }

            if (tileMap != null && tileMap.TryGetValue(sanitized & 0x0F, out go))
            {
                __result = go;
                return false;
            }

            __result = __instance.tileSingleGO;
            return false;
        }

        public static bool Path_canBeSteppedOn_Prefix(Path __instance, Block fromBlock, ref bool __result)
        {
            if (__instance == null || fromBlock == null) return true;

            int dx = fromBlock.tilePosition.x - __instance.tilePosition.x;
            int dz = fromBlock.tilePosition.z - __instance.tilePosition.z;
            if (dx == 0 || dz == 0) return true;

            __result = true;
            return false;
        }

        public static bool Block_instantiateTunnelFrame_Prefix(int side, ref GameObject __result)
        {
            if (side < 4) return true;
            __result = null;
            return false;
        }

        public static void Pathfinding_PathNode_fillReachableNodesList_Postfix(Pathfinding.PathfindingData pathfindingData)
        {
            if (pathfindingData == null) return;
            var reachable = pathfindingData.reachableNodes;
            if (reachable == null || reachable.Count == 0) return;

            BlockData blockData = GameController.Instance?.park?.blockData;
            if (blockData == null) return;

            for (int i = reachable.Count - 1; i >= 0; i--)
            {
                Pathfinding.PathNode node = reachable[i];
                if (node == null) continue;
                Block b = blockData.getBlock(node.tile.x, node.tile.y, node.tile.z);
                if (MarkerCache.IsBlockBlocked(b))
                    reachable.RemoveAt(i);
            }
        }

        public static void Block_canBeWanderedOn_Postfix(Block __instance, ref bool __result)
        {
            if (!__result) return;
            if (MarkerCache.IsBlockBlockedByPermanentMarker(__instance))
                __result = false;
        }

        private const float ReleaseDelayMin = 0.5f;
        private const float ReleaseDelayMax = 1.5f;

        private struct WaitInfo
        {
            public float ReleaseTime;
        }

        private static readonly ConcurrentDictionary<Person, WaitInfo> _waitInfo =
            new ConcurrentDictionary<Person, WaitInfo>();

        private static readonly System.Threading.ThreadLocal<System.Random> _random =
            new System.Threading.ThreadLocal<System.Random>(
                () => new System.Random(System.Guid.NewGuid().GetHashCode()));

        private static readonly AccessTools.FieldRef<WalkToPositionAction, Vector3> _walkToPositionTargetRef =
            AccessTools.FieldRefAccess<WalkToPositionAction, Vector3>("position");

        public static void WalkToPositionAction_run_Prefix(
            WalkToPositionAction __instance,
            DataContext dataContext)
        {
            if (dataContext == null) return;
            Person person = dataContext.person;
            if (person == null) return;

            Block currentBlock = person.currentBlock;
            if (currentBlock != null && MarkerCache.IsBlockBlocked(currentBlock))
            {
                if (_waitInfo.ContainsKey(person))
                    ClearWaitState(person, dataContext);
                return;
            }

            Vector3 target = _walkToPositionTargetRef(__instance);
            bool targetBlocked = MarkerCache.IsTargetTileBlocked(target);
            bool targetBlockedByEffect = targetBlocked && MarkerCache.IsTargetTileBlockedByActiveEffect(target);

            float now = MarkerCache.CurrentTimeApprox;
            WaitInfo info;
            bool wasWaiting = _waitInfo.TryGetValue(person, out info);

            if (targetBlocked)
            {
                if (targetBlockedByEffect)
                {
                    if (!wasWaiting)
                    {
                        info = new WaitInfo { ReleaseTime = 0f };
                        _waitInfo[person] = info;
                    }
                    else if (info.ReleaseTime != 0f)
                    {
                        info.ReleaseTime = 0f;
                        _waitInfo[person] = info;
                    }
                }

                dataContext.set(WalkToPositionAction.speedLimitKey, 0f);
                person.velocity = Vector3.zero;
                return;
            }

            if (!wasWaiting) return;

            if (info.ReleaseTime == 0f)
            {
                info.ReleaseTime = now + ReleaseDelayMin
                    + (float)(_random.Value.NextDouble() * (ReleaseDelayMax - ReleaseDelayMin));
                _waitInfo[person] = info;
            }

            if (now < info.ReleaseTime)
            {
                dataContext.set(WalkToPositionAction.speedLimitKey, 0f);
                person.velocity = Vector3.zero;
                return;
            }

            ClearWaitState(person, dataContext);
        }

        public static bool Person_checkIfReachedNewBlock_Prefix(Person __instance, int x, float y, int z)
        {
            if (__instance == null) return true;

            GameController gc = GameController.Instance;
            if (gc == null || gc.park == null || gc.park.blockData == null) return true;

            Block candidate;
            try
            {
                candidate = gc.park.blockData.getBlock(x, y, z);
            }
            catch
            {
                return true;
            }

            if (candidate == null) return true;
            if (candidate == __instance.currentBlock) return true;

            Block current = __instance.currentBlock;
            if (current != null && MarkerCache.IsBlockBlocked(current)) return true;

            if (!MarkerCache.IsBlockBlocked(candidate)) return true;

            if (current != null)
            {
                __instance.currentPosition = current.centerPosition;
                __instance.velocity = Vector3.zero;
            }

            return false;
        }

        private static void ClearWaitState(Person person, DataContext dataContext)
        {
            WaitInfo removed;
            _waitInfo.TryRemove(person, out removed);
            ClearSpeedLimit(dataContext);
        }

        private static void ClearSpeedLimit(DataContext dataContext)
        {
            dataContext.set(WalkToPositionAction.speedLimitKey, float.MaxValue);
        }

        public static void SerializedMonoBehaviour_Awake_Postfix(SerializedMonoBehaviour __instance)
        {
            Deco d = __instance as Deco;
            if (d != null) MarkerCache.RegisterCandidate(d);

            PathAttachment pa = __instance as PathAttachment;
            if (pa != null) MarkerCache.RegisterCandidate(pa);

            EffectBox box = __instance as EffectBox;
            if (box != null) EffectStateTracker.OnEffectBoxAwake(box);
        }

        public static void Pathfinding_PathNode_calculateCosts_Postfix(
            Pathfinding.PathNode __instance,
            Pathfinding.PathNode fromNode,
            IPathfindingAgent pathfindingAgent,
            ref float __result)
        {
            if (__instance == null || fromNode == null || pathfindingAgent == null) return;
            if (GameController.Instance == null || GameController.Instance.park == null) return;

            int dx = __instance.tile.x - fromNode.tile.x;
            int dz = __instance.tile.z - fromNode.tile.z;
            int absDx = Mathf.Abs(dx);
            int absDz = Mathf.Abs(dz);

            Block block1 = GameController.Instance.park.blockData.getBlock(
                fromNode.tile.x, fromNode.tile.y, fromNode.tile.z);
            Block block2 = GameController.Instance.park.blockData.getBlock(
                __instance.tile.x, __instance.tile.y, __instance.tile.z);
            float mult = pathfindingAgent.pathfindingCostsMultiplier(block1, block2);

            if (absDx != 0 && absDz != 0)
            {
                float dy = Mathf.Abs(__instance.tile.y - fromNode.tile.y);
                float horizontal = Mathf.Max(absDx, absDz) + Mathf.Min(absDx, absDz) * 0.45f;
                __result = (horizontal + dy) * mult + RandomGenerator.Instance().value * 0.01f;
            }

            if (fromNode.parentNode != null)
            {
                int prevDx = fromNode.tile.x - fromNode.parentNode.tile.x;
                int prevDz = fromNode.tile.z - fromNode.parentNode.tile.z;
                if (prevDx != dx || prevDz != dz)
                    __result += 0.15f * mult;
            }
        }
    }
}
