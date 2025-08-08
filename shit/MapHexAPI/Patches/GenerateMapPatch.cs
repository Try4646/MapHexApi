using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using MapHexAPI;
using System.Linq;
using MapHexAPI.Utils;
using UnityEngine.Rendering.HighDefinition;

namespace MapHexAPI.Patches
{
    [HarmonyPatch(typeof(DungeonGenerator))]
    public static class DungeonGeneratorPatches
    {
        private static Dictionary<DungeonGenerator, IEnumerator> activeCoroutines =
            new Dictionary<DungeonGenerator, IEnumerator>();

        // Cached method delegates for performance
        private static Action<DungeonGenerator> _largeMapRPC;
        private static Action<DungeonGenerator> _smallMapRPC;
        private static Action<DungeonGenerator> _placeMountain;
        private static Action<DungeonGenerator, int, int> _serverPlaceHex;

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        static void ExtendHexArrays(DungeonGenerator __instance)
        {
            try
            {
                // Check if this is the first run for this instance
                var extendedField = Traverse.Create(__instance).Field<bool>("__hexArraysExtended");
                if (extendedField.Value)
                {
                    MapHexAPIPlugin.Log.LogInfo("[DEBUG] Arrays already extended for this instance. Skipping.");
                    return;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo("[DEBUG] Extending hex arrays...");
#endif

                var hexesField = AccessTools.Field(typeof(DungeonGenerator), "Hexes");
                var iconsField = AccessTools.Field(typeof(DungeonGenerator), "hexmapicons");
                var mapiconsField = AccessTools.Field(typeof(DungeonGenerator), "mapicons");

                var originalHexes = (GameObject[])hexesField.GetValue(__instance);
                var originalIcons = (Material[])iconsField.GetValue(__instance);
                var originalMapIcons = (DecalProjector[])mapiconsField.GetValue(__instance);

                var customHexes = HexRegistry.GetAllHexes().ToList();

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Original hexes length: {originalHexes.Length}");
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Original icons length: {originalIcons.Length}");
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Custom hexes count: {customHexes.Count}");

                foreach (var hex in customHexes)
                {
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Custom Hex Type: {hex.HexType}, " +
                        $"Prefab: {hex.Prefab?.name ?? "NULL"}, " +
                        $"Icon: {hex.MapIcon?.name ?? "NULL"}, " +
                        $"Valid: {hex.IsValid}");
                }
#endif

                // Find the maximum hex type needed
                int maxHexType = Math.Max(
                    originalHexes.Length - 1,
                    customHexes.Max(h => h.HexType)
                );

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Max hex type needed: {maxHexType}");
#endif

                // Create properly sized arrays
                var newHexes = new GameObject[maxHexType + 1];
                var newIcons = new Material[maxHexType + 1];
                var newMapIcons = new DecalProjector[originalMapIcons.Length]; // Keep original projector count

                // Copy original content
                Array.Copy(originalHexes, newHexes, originalHexes.Length);
                Array.Copy(originalIcons, newIcons, originalIcons.Length);
                Array.Copy(originalMapIcons, newMapIcons, originalMapIcons.Length);
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Copied original arrays");
#endif

                // Add custom hexes at their correct type indices
                foreach (var hex in customHexes)
                {
                    int hexType = hex.HexType;

#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Processing hex type {hexType}");
#endif

                    if (hexType < newHexes.Length)
                    {
                        if (newHexes[hexType] != null)
                        {
                            MapHexAPIPlugin.Log.LogWarning($"Overwriting existing hex at type {hexType} " +
                                $"(Old: {newHexes[hexType]?.name ?? "NULL"}, New: {hex.Prefab?.name ?? "NULL"})");
                        }

                        newHexes[hexType] = hex.Prefab;

                        if (hex.MapIcon == null)
                        {
#if DEBUG
                            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Creating default decal material for hex type {hexType}");
#endif
                            var defaultTexture = new Texture2D(2, 2);
                            hex.MapIcon = AssetLoader.CreateDecalMaterial(defaultTexture);
                            MapHexAPIPlugin.Log.LogWarning($"Created default decal material for hex type {hexType}");
                        }

                        // CORRECTED: Set at hexType index
                        Material newMat = new Material(Shader.Find("HDRP/Decal"));
                        newMat.name = $"{hex.Prefab.name}_Icon";
                        newMat.CopyPropertiesFromMaterial(newIcons[0]);
                        newMat.mainTexture = hex.MapIcon.mainTexture;
                        newIcons[hexType] = newMat;

#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Registered hex type {hexType}: " +
                            $"{hex.Prefab?.name ?? "NULL"} with icon {hex.MapIcon?.name ?? "NULL"}");
#endif
                    }
                    else
                    {
                        MapHexAPIPlugin.Log.LogError($"HexType {hexType} is out of bounds for array (max: {newHexes.Length - 1})");
                    }
                }

                // Apply changes
                hexesField.SetValue(__instance, newHexes);
                iconsField.SetValue(__instance, newIcons);
                mapiconsField.SetValue(__instance, newMapIcons);

                // Mark as extended
                extendedField.Value = true;

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Final hex array contents:");
                for (int i = 0; i < newHexes.Length; i++)
                {
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] [{i}] {newHexes[i]?.name ?? "NULL"}");
                }
#endif

                MapHexAPIPlugin.Log.LogInfo($"Extended arrays to {newHexes.Length} hexes and {newIcons.Length} icons");
            }
            catch (Exception ex)
            {
                MapHexAPIPlugin.Log.LogError($"Array extension failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("GenerateMap")]
        static bool GenerateMapPrefix(DungeonGenerator __instance, ref IEnumerator __result)
        {
#if DEBUG
            MapHexAPIPlugin.Log.LogDebug($"[DEBUG] GenerateMapPrefix called for instance {__instance.GetInstanceID()}");
#endif

            if (activeCoroutines.TryGetValue(__instance, out var coroutine))
            {
#if DEBUG
                MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Found existing coroutine, reusing it");
#endif
                __result = coroutine;
                return false;
            }

            // Initialize method delegates if needed
            if (_largeMapRPC == null)
            {
                _largeMapRPC = AccessTools.MethodDelegate<Action<DungeonGenerator>>(
                    AccessTools.Method(typeof(DungeonGenerator), "LargeMapRPC"));
#if DEBUG
                MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Initialized _largeMapRPC delegate");
#endif
            }

            if (_placeMountain == null)
            {
                _placeMountain = AccessTools.MethodDelegate<Action<DungeonGenerator>>(
                    AccessTools.Method(typeof(DungeonGenerator), "PlaceMountain"));
#if DEBUG
                MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Initialized _placeMountain delegate");
#endif
            }

            if (_serverPlaceHex == null)
            {
                _serverPlaceHex = AccessTools.MethodDelegate<Action<DungeonGenerator, int, int>>(
                    AccessTools.Method(typeof(DungeonGenerator), "ServerPlaceHex"));
#if DEBUG
                MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Initialized _serverPlaceHex delegate");
#endif
            }

            coroutine = CustomGenerateMap(__instance);
            activeCoroutines[__instance] = coroutine;
            __result = coroutine;

#if DEBUG
            MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Starting new coroutine for instance {__instance.GetInstanceID()}");
#endif
            return false;
        }

        private static IEnumerator CustomGenerateMap(DungeonGenerator instance)
        {
#if DEBUG
            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] CustomGenerateMap started for instance {instance.GetInstanceID()}");
#endif

            var prevHexSpawned = Traverse.Create(instance).Field<bool>("PrevHexSpawned");
            var occupiedHexes = Traverse.Create(instance).Field<bool[]>("OccupiedHexes");
            var dungeonPlaced = Traverse.Create(instance).Field<bool>("dungeonPlaced");
            var hexesPlaced = Traverse.Create(instance).Field<bool>("HexesPlaced");

            try
            {
                _largeMapRPC(instance);
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] LargeMapRPC called");
#endif

                for (int j = 0; j < instance.ToggledDeformers.Length; j++)
                {
                    instance.ToggledDeformers[j].SetActive(j < 4);
                }

                if (UnityEngine.Random.Range(0, 100) < 33)
                {
#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Placing mountain...");
#endif
                    _placeMountain(instance);
                    while (!prevHexSpawned.Value)
                    {
                        yield return null;
                    }
                    prevHexSpawned.Value = false;
#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Mountain placed");
#endif
                }

                List<int> hexTypes = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
                foreach (var hex in HexRegistry.GetAllHexes())
                {
                    hexTypes.Add(hex.HexType);
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] All hex types before shuffle: {string.Join(", ", hexTypes)}");
#endif

                // Fisher-Yates shuffle
                for (int k = hexTypes.Count - 1; k > 0; k--)
                {
                    int randomIndex = UnityEngine.Random.Range(0, k + 1);
                    (hexTypes[k], hexTypes[randomIndex]) = (hexTypes[randomIndex], hexTypes[k]);
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Shuffled hex types: {string.Join(", ", hexTypes)}");
#endif

                for (int i = 0; i < occupiedHexes.Value.Length; i++)
                {
                    if (occupiedHexes.Value[i])
                    {
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Skipping occupied hex at index {i}");
#endif
                        continue;
                    }

                    int hexType = hexTypes[i];
                    bool isDungeon = hexType == 3;

#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Placing hex type {hexType} at index {i}");
#endif

                    if (isDungeon)
                    {
                        dungeonPlaced.Value = true;
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Marked as dungeon");
#endif
                    }

                    // Use delegate to call private method
                    _serverPlaceHex(instance, hexType, i);

                    while (!prevHexSpawned.Value && (isDungeon ? !instance.isDungeonGenerated : true))
                    {
                        yield return null;
                    }

                    prevHexSpawned.Value = false;
#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Hex type {hexType} placement completed at index {i}");
#endif
                }

                hexesPlaced.Value = true;

                for (int l = 0; l < occupiedHexes.Value.Length; l++)
                {
                    occupiedHexes.Value[l] = false;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Map generation completed");
#endif
            }
            finally
            {
                activeCoroutines.Remove(instance);
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Removed coroutine tracking for instance {instance.GetInstanceID()}");
#endif
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("GenerateSmallMap")]
        static bool GenerateSmallMapPrefix(DungeonGenerator __instance, ref IEnumerator __result)
        {
#if DEBUG
            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] GenerateSmallMapPrefix called for instance {__instance.GetInstanceID()}");
#endif

            if (activeCoroutines.TryGetValue(__instance, out var coroutine))
            {
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Found existing coroutine, reusing it");
#endif
                __result = coroutine;
                return false;
            }

            // Initialize method delegates if needed
            if (_smallMapRPC == null)
            {
                _smallMapRPC = AccessTools.MethodDelegate<Action<DungeonGenerator>>(
                    AccessTools.Method(typeof(DungeonGenerator), "SmallMapRPC"));
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Initialized _smallMapRPC delegate");
#endif
            }

            if (_serverPlaceHex == null)
            {
                _serverPlaceHex = AccessTools.MethodDelegate<Action<DungeonGenerator, int, int>>(
                    AccessTools.Method(typeof(DungeonGenerator), "ServerPlaceHex"));
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Initialized _serverPlaceHex delegate");
#endif
            }

            coroutine = CustomGenerateSmallMap(__instance);
            activeCoroutines[__instance] = coroutine;
            __result = coroutine;

#if DEBUG
            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Starting new coroutine for instance {__instance.GetInstanceID()}");
#endif
            return false;
        }

        private static IEnumerator CustomGenerateSmallMap(DungeonGenerator instance)
        {
#if DEBUG
            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] CustomGenerateSmallMap started for instance {instance.GetInstanceID()}");
#endif

            var prevHexSpawned = Traverse.Create(instance).Field<bool>("PrevHexSpawned");
            var occupiedHexes = Traverse.Create(instance).Field<bool[]>("OccupiedHexes");
            var dungeonPlaced = Traverse.Create(instance).Field<bool>("dungeonPlaced");
            var hexesPlaced = Traverse.Create(instance).Field<bool>("HexesPlaced");

            try
            {
                _smallMapRPC(instance);
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] SmallMapRPC called");
#endif

                int numloothexes = 0;

                occupiedHexes.Value[0] = true;
                occupiedHexes.Value[6] = true;

                List<int> hexTypes = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
                foreach (var hex in HexRegistry.GetAllHexes())
                {
                    hexTypes.Add(hex.HexType);
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] All hex types before shuffle: {string.Join(", ", hexTypes)}");
#endif

                // Fisher-Yates shuffle
                for (int j = hexTypes.Count - 1; j > 0; j--)
                {
                    int randomIndex = UnityEngine.Random.Range(0, j + 1);
                    (hexTypes[j], hexTypes[randomIndex]) = (hexTypes[randomIndex], hexTypes[j]);
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Shuffled hex types: {string.Join(", ", hexTypes)}");
#endif

                for (int i = 0; i < occupiedHexes.Value.Length; i++)
                {
                    if (occupiedHexes.Value[i])
                    {
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Skipping occupied hex at index {i}");
#endif
                        continue;
                    }

                    int hexType = hexTypes[i];
                    bool isLootHex = hexType == 0 || hexType == 3 || hexType == 5;

#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Considering hex type {hexType} at index {i} (isLootHex: {isLootHex})");
#endif

                    if (isLootHex) numloothexes++;

                    if (numloothexes > 2 && isLootHex)
                    {
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Too many loot hexes ({numloothexes}), finding replacement");
#endif
                        foreach (var t in hexTypes)
                        {
                            if (t != 0 && t != 3 && t != 5)
                            {
                                hexType = t;
                                break;
                            }
                        }
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Replaced with hex type {hexType}");
#endif
                    }
                    else if (numloothexes < 2 && i == 5)
                    {
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Need more loot hexes (current: {numloothexes}), forcing loot hex");
#endif
                        if (hexTypes.Contains(5)) hexType = 5;
                        else if (hexTypes.Contains(0)) hexType = 0;
                        else if (hexTypes.Contains(3)) hexType = 3;
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Forced hex type to {hexType}");
#endif
                    }

                    bool isDungeon = hexType == 3;

                    if (isDungeon)
                    {
                        dungeonPlaced.Value = true;
#if DEBUG
                        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Marked as dungeon");
#endif
                    }

                    // Use delegate to call private method
                    _serverPlaceHex(instance, hexType, i);

                    while (!prevHexSpawned.Value && (isDungeon ? !instance.isDungeonGenerated : true))
                    {
                        yield return null;
                    }

                    prevHexSpawned.Value = false;
#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Hex type {hexType} placement completed at index {i}");
#endif
                }

                hexesPlaced.Value = true;

                for (int l = 0; l < occupiedHexes.Value.Length; l++)
                {
                    occupiedHexes.Value[l] = false;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Small map generation completed");
#endif
            }
            finally
            {
                activeCoroutines.Remove(instance);
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Removed coroutine tracking for instance {instance.GetInstanceID()}");
#endif
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("ServerPlaceHex")]
        static bool HandleCustomHexSpawn(
            DungeonGenerator __instance,
            int HexType,
            int hexindex,
            ref bool __runOriginal)
        {
#if DEBUG
            MapHexAPIPlugin.Log.LogDebug($"[DEBUG] ServerPlaceHex called for type {HexType} at index {hexindex}");
#endif

            if (!__runOriginal) return false;
            if (!HexRegistry.TryGetHex(HexType, out var hexDef))
            {
#if DEBUG
                MapHexAPIPlugin.Log.LogDebug($"[DEBUG] Not a custom hex, running original");
#endif
                return true;
            }

            try
            {
#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Handling custom hex type {HexType}");
#endif

                var hexPoints = (Transform[])AccessTools
                    .Field(typeof(DungeonGenerator), "HexPoints")
                    .GetValue(__instance);

                if (hexPoints == null || hexindex >= hexPoints.Length)
                {
                    MapHexAPIPlugin.Log.LogError($"Invalid hex points array or index");
                    return true;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Hex point position: {hexPoints[hexindex].position}");
#endif

                // Verify prefab
                if (hexDef.Prefab == null)
                {
                    MapHexAPIPlugin.Log.LogInfo($"Prefab is null for hex type {HexType}");
                    return true;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Prefab name: {hexDef.Prefab.name}");
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Prefab instance ID: {hexDef.Prefab.GetInstanceID()}");
#endif

                // Create proper decal material if needed
                if (hexDef.MapIcon == null)
                {
                    MapHexAPIPlugin.Log.LogWarning($"Creating temporary decal material for hex type {HexType}");
                    hexDef.MapIcon = AssetLoader.CreateDecalMaterial(Texture2D.whiteTexture);
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Instantiating prefab...");
#endif

                var spawnedHex = UnityEngine.Object.Instantiate(
                    hexDef.Prefab,
                    hexPoints[hexindex].position,
                    Quaternion.identity
                );

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Instantiated hex with ID: {spawnedHex.GetInstanceID()}");
#endif

                if (__instance.NetworkObject?.ServerManager != null)
                {
#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] ServerManager available, spawning network object...");
#endif
                    __instance.NetworkObject.ServerManager.Spawn(spawnedHex);

                    // Update map visuals
                    AccessTools.Method(typeof(DungeonGenerator), "updatemap")
                        .Invoke(__instance, new object[] { HexType, hexindex });

                    Traverse.Create(__instance).Field("PrevHexSpawned").SetValue(true);

#if DEBUG
                    MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Custom hex spawned successfully");
#endif

                    __runOriginal = false;
                    return false;
                }

#if DEBUG
                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] No ServerManager, destroying spawned hex");
#endif
                UnityEngine.Object.Destroy(spawnedHex);
                return true;
            }
            catch (Exception ex)
            {
                MapHexAPIPlugin.Log.LogError($"Error spawning custom hex: {ex}");
#if DEBUG
                MapHexAPIPlugin.Log.LogError($"[DEBUG] Error details:");
                MapHexAPIPlugin.Log.LogError($"[DEBUG] HexType: {HexType}");
                MapHexAPIPlugin.Log.LogError($"[DEBUG] HexIndex: {hexindex}");
                MapHexAPIPlugin.Log.LogError($"[DEBUG] Prefab: {hexDef.Prefab?.name ?? "NULL"}");
                MapHexAPIPlugin.Log.LogError($"[DEBUG] MapIcon: {hexDef.MapIcon?.name ?? "NULL"}");
#endif
                return true;
            }
        }
    }
}