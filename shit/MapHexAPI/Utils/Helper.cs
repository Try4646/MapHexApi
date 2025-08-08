using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
namespace MapHexAPI.Utils
{
    internal static class Helper
    {
        internal static Dictionary<string, Sprite> CachedSprites = new Dictionary<string, Sprite>();
        public static Sprite LoadSpriteFromDisk(string path, float pixelsPerUnit = 1f)
        {
            Sprite result;
            try
            {
                Sprite sprite;
                bool flag = CachedSprites.TryGetValue(path + pixelsPerUnit.ToString(), out sprite);
                if (flag)
                {
                    result = sprite;
                }
                else
                {
                    Texture2D texture2D = LoadTextureFromDisk(path);
                    bool flag2 = texture2D == null;
                    if (flag2)
                    {
                        result = null;
                    }
                    else
                    {
                        sprite = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                        sprite.hideFlags |= HideFlags.HideAndDontSave;
                        result = (CachedSprites[path + pixelsPerUnit.ToString()] = sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                MapHexAPIPlugin.Log.LogError(ex);
                result = null;
            }
            return result;
        }

        public static Texture2D LoadTextureFromDisk(string path)
        {
            try
            {
                bool flag = File.Exists(path);
                if (flag)
                {
                    Texture2D texture2D = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    byte[] data = File.ReadAllBytes(path);
                    bool flag2 = texture2D.LoadImage(data, false);
                    bool flag3 = flag2;
                    if (flag3)
                    {
                        return texture2D;
                    }
                    MapHexAPIPlugin.Log.LogError("Failed to load image data into texture.");
                }
                else
                {
                    MapHexAPIPlugin.Log.LogError("File does not exist: " + path);
                }
            }
            catch (Exception ex)
            {
                ManualLogSource log = MapHexAPIPlugin.Log;
                string str = "Exception while loading texture: ";
                Exception ex2 = ex;
                log.LogError(str + ((ex2 != null) ? ex2.ToString() : null));
            }
            return null;
        }

        public static Sprite LoadSpriteFromResources(this Assembly assembly, string path, float pixelsPerUnit = 1f)
        {
            Sprite result;
            try
            {
                Sprite sprite;
                bool flag = CachedSprites.TryGetValue(path + pixelsPerUnit.ToString(), out sprite);
                if (flag)
                {
                    result = sprite;
                }
                else
                {
                    Texture2D texture2D = LoadTextureFromResources(assembly, path);
                    bool flag2 = texture2D == null;
                    if (flag2)
                    {
                        result = null;
                    }
                    else
                    {
                        sprite = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
                        sprite.hideFlags |= HideFlags.HideAndDontSave;
                        result = (CachedSprites[path + pixelsPerUnit.ToString()] = sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                MapHexAPIPlugin.Log.LogError(ex);
                result = null;
            }
            return result;
        }

        public static Texture2D LoadTextureFromResources(this Assembly assembly, string path)
        {
            Texture2D result;
            try
            {
                Stream manifestResourceStream = assembly.GetManifestResourceStream(path);
                bool flag = manifestResourceStream == null;
                if (flag)
                {
                    result = null;
                }
                else
                {
                    Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        manifestResourceStream.CopyTo(memoryStream);
                        bool flag2 = !texture2D.LoadImage(memoryStream.ToArray(), false);
                        if (flag2)
                        {
                            return null;
                        }
                    }
                    result = texture2D;
                }
            }
            catch (Exception ex)
            {
                MapHexAPIPlugin.Log.LogError(ex);
                result = null;
            }
            return result;
        }
        public static Material CreateMaterialFromPng(string pngPath, string shaderName = "HDRP/Decal")
        {
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
                throw new ArgumentException("Ungültiger PNG-Pfad.", nameof(pngPath));

            byte[] fileData = File.ReadAllBytes(pngPath);
            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(fileData))
                throw new Exception("PNG konnte nicht als Textur geladen werden.");

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new Exception($"Shader '{shaderName}' nicht gefunden.");

            Material material = new Material(shader);
            material.SetTexture("_BaseColorMap", texture);
            return material;
        }

        
    }
}
