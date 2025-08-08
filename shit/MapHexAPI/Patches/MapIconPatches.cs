using System;
using HarmonyLib;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace MapHexAPI.Patches
{

    [HarmonyPatch(typeof(DungeonGenerator))]
    internal class MapIconPatches
    {

        
        //[HarmonyPrefix]
        //[HarmonyPatch("RpcLogic___updatemap_1692629761")]
        //static bool UpdateMapPrefix(DungeonGenerator __instance, int ht, int hid)
        //{
        //    try
        //    {
        //        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] UpdateMapPrefix called with HexType: {ht}, HexIndex: {hid}");
        //
        //        var mapicons = Traverse.Create(__instance).Field<DecalProjector[]>("mapicons").Value;
        //        var hexmapicons = Traverse.Create(__instance).Field<Material[]>("hexmapicons").Value;
        //        var isSmallMap = Traverse.Create(__instance).Field<bool>("smallmap").Value;
        //
        //        // Skip if invalid index
        //        if (hid >= mapicons.Length || mapicons[hid] == null)
        //        {
        //            MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Invalid index or mapicon iss null at index {hid}. Skipping.");
        //            return true;
        //        }
        //
        //        // Handle small map mode
        //        if (isSmallMap)
        //        {
        //            // Skip positions 0 and 8 (tiles 1 and 9)
        //            if (hid == 0 || hid == 8)
        //            {
        //                if (mapicons[hid] != null)
        //                    mapicons[hid].enabled = false;
        //                return false;
        //            }
        //        }
        //
        //        // Handle hex types within range
        //        if (ht >= 0 && ht < hexmapicons.Length && hexmapicons[ht] != null)
        //        {
        //            if (HexRegistry.GetAllHexes().ToList().Exists(x => x.HexType == ht))
        //            {
        //                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Using registered hex type {ht} for HexIndex {hid}");
        //                HexRegistry.HexDefinition hxdf;
        //                HexRegistry.TryGetHex(ht, out hxdf);
        //                mapicons[hid].material.mainTexture = hxdf.MapIcon.mainTexture;     
        //            }
        //            else
        //            {
        //                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Using material from hexmapicons for HexType {ht} at index {hid}");
        //                mapicons[hid].material = hexmapicons[ht];
        //            }   
        //        }
        //        else
        //        {
        //            MapHexAPIPlugin.Log.LogWarning($"[DEBUG] Invalid hex type {ht} or missing material. Using fallback.");
        //
        //            // Create fallback material
        //            var fallbackMat = new Material(Shader.Find("HDRP/Decal"));
        //            fallbackMat.SetTexture("_BaseColorMap", Texture2D.whiteTexture);
        //            fallbackMat.SetColor("_BaseColor", Color.white);
        //            mapicons[hid].material = fallbackMat;
        //        }
        //
        //        // Handle coloring
        //        var color = (ht == 1 || ht == 2 || ht == 4 || ht == 6 || ht == 7) ? Color.gray : Color.white;
        //        MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Setting mapicon color for {hid} to {color}");
        //        mapicons[hid].material.color = color;
        //
        //        // Update resource manager
        //        var resourceManager = __instance.GetComponent<ResourceManager>();
        //        if (resourceManager != null)
        //        {
        //            var resourceMapicons = Traverse.Create(resourceManager).Field<int[]>("mapicons").Value;
        //            if (hid < resourceMapicons.Length)
        //            {
        //                resourceMapicons[hid] = ht;
        //                MapHexAPIPlugin.Log.LogInfo($"[DEBUG] Updated resource manager mapicons at index {hid} with HexType {ht}");
        //            }
        //        }
        //
        //        return false;
        //    }
        //    catch (Exception ex)
        //    {
        //        MapHexAPIPlugin.Log.LogError($"[ERROR] UpdateMapPrefix failed: {ex}");
        //        return true;
        //    }
        //}
        //[HarmonyPostfix]
        //[HarmonyPatch("SmallMapRPC")]
        //static void CleanupSmallMap(DungeonGenerator __instance)
        //{
        //    try
        //    {
        //        var mapicons = Traverse.Create(__instance).Field<DecalProjector[]>("mapicons").Value;
        //        // Hide the extra projector (index 7) in small map mode
        //        if (mapicons.Length > 7 && mapicons[7] != null)
        //            mapicons[7].enabled = false;
        //    }
        //    catch (Exception ex)
        //    {
        //        MapHexAPIPlugin.Log.LogError($"CleanupSmallMap error: {ex}");
        //    }
        //}
    }
}
