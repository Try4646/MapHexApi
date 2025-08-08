using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using BepInEx.Logging;
using MapHexAPI.Utils;
using System.Runtime.CompilerServices;

namespace MapHexAPI
{
    public static class HexRegistry
    {
        public class HexDefinition
        {
            public GameObject Prefab { get; set; }
            public Material MapIcon { get; set; }
            public string DisplayName { get; set; }
            public int HexType { get; set; }
            public List<MonoBehaviour> Components { get; set; } = new List<MonoBehaviour>();
            public List<NetworkBehaviour> NetworkComponents { get; set; } = new List<NetworkBehaviour>();
            public bool IsValid { get; set; } = false;
        }

        private static readonly Dictionary<int, HexDefinition> _hexes = new Dictionary<int, HexDefinition>();
        private static ManualLogSource _log;
        private static int _nextHexType = 9; // Starting after vanilla types

        public static void Initialize(ManualLogSource logger)
        {
            _log = logger;
#if DEBUG
            _log.LogInfo("[DEBUG] HexRegistry initialized with loggers");
            _log.LogDebug($"[DEBUG] Next available hex type: {_nextHexType}");
            _log.LogDebug($"[DEBUG] Initial hex count: {_hexes.Count}");
#endif
        }

        public static int RegisterHex(GameObject prefab, Material mapIcon, string displayName = null, List<MonoBehaviour> components = null, List<NetworkBehaviour> netcomponents = null)
        {
#if DEBUG
            _log.LogInfo($"[DEBUG] RegisterHex called with prefab: {prefab?.name ?? "NULL"}, icon: {mapIcon?.name ?? "NULL"}, name: {displayName ?? "NULL"}");
#endif
            try
            {
                int hexType = _nextHexType++;

                var definition = new HexDefinition
                {
                    Prefab = prefab,
                    MapIcon = mapIcon,
                    DisplayName = displayName ?? $"Custom Hex {hexType}",
                    Components = components ?? new List<MonoBehaviour>(),
                    NetworkComponents = netcomponents ?? new List<NetworkBehaviour>(),
                    HexType = hexType,
                    IsValid = prefab != null && mapIcon != null
                };

                _hexes[hexType] = definition;

                _log.LogInfo($"Registered hex type {hexType}: {displayName}");
                _log.LogDebug($"Prefab: {prefab.name}, Icon: {mapIcon?.name ?? "None"}");

#if DEBUG
                _log.LogDebug($"[DEBUG] Hex registered successfully. Details:");
                _log.LogDebug($"[DEBUG] HexType: {hexType}");
                _log.LogDebug($"[DEBUG] Prefab valid: {prefab != null}");
                _log.LogDebug($"[DEBUG] MapIcon valid: {mapIcon != null}");
                _log.LogDebug($"[DEBUG] Total registered hexes: {_hexes.Count}");
                _log.LogDebug($"[DEBUG] Next hex type will be: {_nextHexType}");

                if (prefab != null)
                {
                    _log.LogDebug($"[DEBUG] Prefab components:");
                    foreach (var component in prefab.GetComponents<Component>())
                    {
                        _log.LogDebug($"[DEBUG] - {component.GetType().Name}");
                    }
                }
#endif

                return hexType;
            }
            catch (Exception ex)
            {
                _log.LogError($"Error registering hex: {ex}");
#if DEBUG
                _log.LogError($"[DEBUG] Error details:");
                _log.LogError($"[DEBUG] Prefab: {prefab?.name ?? "NULL"}");
                _log.LogError($"[DEBUG] MapIcon: {mapIcon?.name ?? "NULL"}");
                _log.LogError($"[DEBUG] DisplayName: {displayName ?? "NULL"}");
                _log.LogError($"[DEBUG] Current hex count: {_hexes.Count}");
                _log.LogError($"[DEBUG] Next hex type: {_nextHexType}");
#endif
                return -1;
            }
        }

        public static bool TryGetHex(int hexType, out HexDefinition definition)
        {
#if DEBUG
            _log.LogDebug($"[DEBUG] TryGetHex called for type: {hexType}");
#endif
            bool result = _hexes.TryGetValue(hexType, out definition);
#if DEBUG
            _log.LogDebug($"[DEBUG] TryGetHex result: {result}");
            if (result)
            {
                _log.LogDebug($"[DEBUG] Found hex definition:");
                _log.LogDebug($"[DEBUG] DisplayName: {definition.DisplayName}");
                _log.LogDebug($"[DEBUG] Prefab: {definition.Prefab?.name ?? "NULL"}");
                _log.LogDebug($"[DEBUG] MapIcon: {definition.MapIcon?.name ?? "NULL"}");
                _log.LogDebug($"[DEBUG] IsValid: {definition.IsValid}");
            }
#endif
            return result;
        }

        public static void Reset()
        {
            _hexes.Clear();
        }

        public static IEnumerable<HexDefinition> GetAllHexes()
        {
#if DEBUG
            _log.LogDebug($"[DEBUG] GetAllHexes called, returning {_hexes.Count} hexes");
            foreach (var hex in _hexes.Values)
            {
                _log.LogDebug($"[DEBUG] Hex {hex.HexType}: {hex.DisplayName} (Valid: {hex.IsValid})");
            }
#endif
            return _hexes.Values;
        }

        public static void RegisterPrefabsWithNetwork()
        {
#if DEBUG
            _log.LogInfo("[DEBUG] RegisterPrefabsWithNetwork called");
            _log.LogDebug($"[DEBUG] Processing {_hexes.Count} hex definitions");
#endif

            // Track registered prefabs by their name to prevent duplicates
            var registeredPrefabNames = new HashSet<string>();

            foreach (var hex in _hexes.Values)
            {
#if DEBUG
                _log.LogDebug($"[DEBUG] Processing hex type {hex.HexType}: {hex.DisplayName}");
                _log.LogDebug($"[DEBUG] Prefab status: {(hex.Prefab != null ? "Valid" : "NULL")}");
#endif

                if (hex.Prefab != null)
                {
                    string prefabName = hex.Prefab.name;

                    if (registeredPrefabNames.Contains(prefabName))
                    {
#if DEBUG
                        _log.LogWarning($"[DEBUG] Skipping duplicate prefab '{prefabName}' for hex type {hex.HexType} " +
                                       $"(already registered)");
#endif
                        continue;
                    }

#if DEBUG
                    _log.LogDebug($"[DEBUG] Registering prefab: {prefabName} for hex type {hex.HexType}");
                    var startTime = DateTime.Now;
#endif

                    AssetLoader.RegisterPrefabWithNetworkManager(hex.Prefab);
                    registeredPrefabNames.Add(prefabName);

#if DEBUG
                    var duration = DateTime.Now - startTime;
                    _log.LogDebug($"[DEBUG] Registration completed in {duration.TotalMilliseconds}ms");
#endif
                }
#if DEBUG
                else
                {
                    _log.LogWarning($"[DEBUG] Skipping null prefab for hex type {hex.HexType}");
                }
#endif
            }
#if DEBUG
            _log.LogInfo($"[DEBUG] Finished registering {registeredPrefabNames.Count} unique prefabs with network");
            _log.LogDebug("[DEBUG] Registered prefabs: " + string.Join(", ", registeredPrefabNames));
#endif
        }
    }
}