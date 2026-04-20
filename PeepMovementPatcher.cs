using System;
using System.Collections.Generic;
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

        // Restore diagonal side that vanilla nulls to -1.
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

        // Allow diagonal Path-to-Path moves at same height.
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

        // Reject diagonals through non-path corners (also feeds visual bitmask).
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

        // Marker AABBs. Refresh on main thread; reads safe from any thread.
        private static class MarkerCache
        {
            private const float RefreshIntervalSeconds = 1.0f;

            // Atomic ref swap: workers see old or new, never torn.
            private static List<Bounds> _bounds = new List<Bounds>();
            private static float _lastRefreshTime = -1f;

            // Thread-safe: pure read against a captured snapshot.
            public static bool IsBlockBlocked(Block b)
            {
                if (b == null) return false;
                List<Bounds> snapshot = _bounds;
                if (snapshot.Count == 0) return false;

                // Tile footprint: XZ [tileX, tileX+1) x [tileZ, tileZ+1).
                int tileX = b.tilePosition.x;
                int tileZ = b.tilePosition.z;
                float xMin = tileX;
                float xMax = tileX + 1f;
                float zMin = tileZ;
                float zMax = tileZ + 1f;

                // Y slack so markers sitting on path still count.
                float yCenter = b.centerPosition.y;
                float yMin = yCenter - 0.25f;
                float yMax = yCenter + 1.0f;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    Bounds m = snapshot[i];
                    if (m.max.x <= xMin || m.min.x >= xMax) continue;
                    if (m.max.z <= zMin || m.min.z >= zMax) continue;
                    if (m.max.y <= yMin || m.min.y >= yMax) continue;
                    return true;
                }
                return false;
            }

            // MUST be called only from the Unity main thread.
            public static void RefreshIfDueMainThread()
            {
                float now = Time.unscaledTime;
                if (_lastRefreshTime >= 0f && now - _lastRefreshTime < RefreshIntervalSeconds) return;
                _lastRefreshTime = now;

                List<Bounds> newBounds = new List<Bounds>();

                Deco[] decos = UnityEngine.Object.FindObjectsOfType<Deco>();
                for (int i = 0; i < decos.Length; i++)
                {
                    Deco d = decos[i];
                    if (d == null) continue;

                    string name = d.getReferenceName();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!MarkerRegistry.Contains(name)) continue;

                    if (TryComputeWorldBounds(d, out Bounds wb))
                        newBounds.Add(wb);
                }

                _bounds = newBounds;
            }

            private static bool TryComputeWorldBounds(Deco d, out Bounds bounds)
            {
                MeshRenderer[] renderers = d.GetComponentsInChildren<MeshRenderer>();
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

        // Drives MarkerCache.Refresh on the main thread.
        public class MarkerCacheTicker : MonoBehaviour
        {
            private void Update()
            {
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

            return true;
        }

        private static bool IsWalkablePath(Block b)
        {
            if (b == null) return false;
            if (!(b is Path)) return false;
            if (b is Queue) return false;
            if (!b.isFlat()) return false;
            return true;
        }

        // True if tile has any wall, door, or gate at this height.
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

        // Hide diagonals from path visual bitmask.
        public static void Block_getConnectedSidesBitmask_Postfix(ref int __result)
        {
            __result &= 0x0F;
        }

        private static readonly AccessTools.FieldRef<PathTileMapper, Dictionary<int, Vector3>> _forwardsRef =
            AccessTools.FieldRefAccess<PathTileMapper, Dictionary<int, Vector3>>("forwards");

        // Graceful fallback for missing tile forwards.
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

        // Silent fallback for missing tile prefabs.
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

        // Skip cardinal-only sign check on diagonal approach.
        public static bool Path_canBeSteppedOn_Prefix(Path __instance, Block fromBlock, ref bool __result)
        {
            if (__instance == null || fromBlock == null) return true;

            int dx = fromBlock.tilePosition.x - __instance.tilePosition.x;
            int dz = fromBlock.tilePosition.z - __instance.tilePosition.z;
            if (dx == 0 || dz == 0) return true;

            __result = true;
            return false;
        }

        // Tunnel frames only exist on cardinal sides.
        public static bool Block_instantiateTunnelFrame_Prefix(int side, ref GameObject __result)
        {
            if (side < 4) return true;
            __result = null;
            return false;
        }

        // A* marker filter; runs late so visual bitmask isn't affected.
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

        // Per-step marker filter; A* alone misses these decisions.
        public static void Block_canBeWanderedOn_Postfix(Block __instance, ref bool __result)
        {
            if (!__result) return;
            if (MarkerCache.IsBlockBlocked(__instance))
                __result = false;
        }

        // Octile distance so diagonals cost sqrt(2), not 2.
        public static void Pathfinding_PathNode_calculateCosts_Postfix(
            Pathfinding.PathNode __instance,
            Pathfinding.PathNode fromNode,
            IPathfindingAgent pathfindingAgent,
            ref float __result)
        {
            if (__instance == null || fromNode == null || pathfindingAgent == null) return;

            int dx = Mathf.Abs(__instance.tile.x - fromNode.tile.x);
            int dz = Mathf.Abs(__instance.tile.z - fromNode.tile.z);
            if (dx == 0 || dz == 0) return;

            float dy = Mathf.Abs(__instance.tile.y - fromNode.tile.y);

            Block block1 = GameController.Instance.park.blockData.getBlock(
                fromNode.tile.x, fromNode.tile.y, fromNode.tile.z);
            Block block2 = GameController.Instance.park.blockData.getBlock(
                __instance.tile.x, __instance.tile.y, __instance.tile.z);

            float horizontal = Mathf.Max(dx, dz) + Mathf.Min(dx, dz) * 0.4142136f;
            float mult = pathfindingAgent.pathfindingCostsMultiplier(block1, block2);
            __result = (horizontal + dy) * mult + RandomGenerator.Instance().value * 0.01f;
        }
    }
}
