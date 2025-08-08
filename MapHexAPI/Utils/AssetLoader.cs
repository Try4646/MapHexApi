using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MapHexAPI.Utils
{
    public static class AssetLoader
    {
        private static readonly Dictionary<string, AssetBundle> _bundles = new Dictionary<string, AssetBundle>();
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource logger) => _log = logger;

        public static GameObject LoadPrefabFromBundle(string bundlePath, string assetName)
        {
            try
            {
                if (!File.Exists(bundlePath))
                {
                    _log.LogError($"Bundle not found: {bundlePath}");
                    return null;
                }

                if (!_bundles.TryGetValue(bundlePath, out var bundle))
                {
                    bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle == null)
                    {
                        _log.LogError("Failed to load bundle");
                        return null;
                    }
                    _bundles[bundlePath] = bundle;
                }

                var prefab = bundle.LoadAsset<GameObject>(assetName);
                if (prefab == null)
                {
                    _log.LogError($"Prefab '{assetName}' not found in bundle");
                    return null;
                }

                EnsureNetworkObject(prefab);
                return prefab;
            }
            catch (Exception ex)
            {
                _log.LogError($"Error loading prefab: {ex}");
                return null;
            }
        }

        public static Texture2D LoadTexture(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _log.LogError($"Texture not found: {path}");
                    return null;
                }

                // Load the image bytes and create the texture
                var textureBytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                // Load the texture data from bytes
                if (!texture.LoadImage(textureBytes))
                {
                    _log.LogError($"Failed to load texture frdom file: {path}");
                    return null;
                }

                // Assign the texture name based on the file name
                texture.name = Path.GetFileNameWithoutExtension(path);

                return texture;
            }
            catch (Exception ex)
            {
                _log.LogError($"Error loading texture: {ex}");
                return null;
            }
        }


        public static Material CreateMaterial(Texture2D texture, Shader shader = null)
        {
            if (texture == null) return null;

            shader = shader ?? Shader.Find("HDRP/Decal");

            Material material = new Material(shader);

            material.mainTexture = texture;
            material.SetTexture("_BaseColor", texture);

            return material;
        }
        public static Material CreateDecalMaterial(Texture2D texture)
        {
            // Ensure we always have a valid texture
            if (texture == null)
            {
                MapHexAPIPlugin.Log.LogWarning("Texture is null when creating decal material - using default white texture");
                texture = new Texture2D(2, 2);
                texture.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
            }

            // Try to get the HDRP decal shader with multiple fallback options
            Shader decalShader = Shader.Find("HDRP/Decal");

            if (decalShader == null)
            {
                // Try other possible decal shader paths
                decalShader = Shader.Find("Universal Render Pipeline/Decal");

                if (decalShader == null)
                {
                    decalShader = Shader.Find("Standard");
                    MapHexAPIPlugin.Log.LogWarning("Decal shader not found! Using Standard shader as fallback");
                }
            }

            // Create material - if shader is still null, use the default shader
            Material material = new Material(decalShader ?? Shader.Find("Standard"));

            try
            {
                material.name = texture.name + "_Material";
                material.mainTexture = texture;

                // Configure material properties based on shader type
                if (decalShader != null && decalShader.name.Contains("Decal"))
                {
                    // HDRP/URP Decal shader properties
                    material.SetTexture("_BaseColorMap", texture);
                    material.SetColor("_BaseColor", Color.white); // Adjust based on your template color
                    material.SetFloat("_NormalBlendSrc", 1f); // Adjust to match your template settings
                    material.enableInstancing = true;

                    // Set additional properties from your template
                    material.SetFloat("_DecalBlend", 1f);
                    material.SetInt("_DecalMeshBiasType", 1);
                    material.SetInt("_PassCount", 4);  // Adjust the pass count based on template
                    material.renderQueue = 2003;       // Set render queue to match template
                    material.SetInt("_RawRenderQueue", 2003);  // Additional render queue setting

                    // Set keywords and flags from your template
                    material.SetFloat("_EmissiveIsBlack", 1f);  // Set based on template's 'EmissiveIsBlack' flag
                    material.EnableKeyword("_COLORMAP");
                    material.EnableKeyword("_MATERIAL_AFFECTS_ALBEDO");
                    material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
                    material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                }
                else
                {
                    // Standard shader fallback configuration
                    material.SetFloat("_Mode", 1); // Cutout mode
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
            }
            catch (Exception e)
            {
                MapHexAPIPlugin.Log.LogError($"Error confidguring decal material: {e.Message}");
                // Return the material even if configuration failed
            }
            return material;
        }




        public static bool RegisterPrefabWithNetworkManager(GameObject prefab)
        {
            try
            {
                var netManager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (netManager == null)
                {
                    _log.LogError("NetworkManager not found");
                    return false;
                }

                var netObj = prefab.GetComponent<NetworkObject>();
                if (netObj == null)
                {
                    _log.LogError("Prefab has no NetworkObject");
                    return false;
                }

  
                    netManager.SpawnablePrefabs.AddObject(netObj, true);
                    _log.LogInfo($"Registered prefab: {prefab.name}");
                    return true;
                
            }
            catch (Exception ex)
            {
                _log.LogError($"Error registering prefab: {ex}");
                return false;
            }
        }

        private static void EnsureNetworkObject(GameObject prefab)
        {
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                prefab.AddComponent<NetworkObject>();
                _log.LogDebug("Added NetworkObject to prefab");
            }
        }

        public static void UnloadAllAssets()
        {
            foreach (var bundle in _bundles.Values)
            {
                bundle.Unload(true);
            }
            _bundles.Clear();
            _log.LogInfo("Unloaded all assets");
        }
    }
}