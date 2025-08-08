using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MapHexAPI.Patches;
using MapHexAPI.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MapHexAPI
{
    [BepInDependency("com.magearena.modsync", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("com.user.MapHexAPI", "MapHexAPI", "1.0.0")]
    public class MapHexAPIPlugin : BaseUnityPlugin
    {
        public static string modsync = "all";

        private const string MyGUID = "com.user.MapHexApi";
        private const string PluginName = "MapHexApi";
        private const string VersionString = "1.0.0";

        public static GameObject TemplateHexMaterial;

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log;

        
        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("MapHexAPI initializing...");

            // Initialize core systems
            HexRegistry.Initialize(Log);
            AssetLoader.Initialize(Log);

            // Apply Harmony patches
            Harmony.PatchAll();

            // Handle scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;

            Log.LogInfo("MapHexAPI initialized successfully");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogDebug($"Scene loaded: {scene.name}");
            HexRegistry.RegisterPrefabsWithNetwork();
        }

        private void OnDestroy()
        {
            AssetLoader.UnloadAllAssets();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}