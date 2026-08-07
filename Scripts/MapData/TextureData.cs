using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Jibbers.MapTools
{

    public class TextureData : ISerializable
    {

        public TextureFormat format;
        public int width;
        public int height;
        public string encoding;
        public bool isLinear = true;
        public bool mipmaps = false;
        public int compression = 1;

        public byte[] data;

        Task encodeTask;

        public TextureData() {}
        public TextureData(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            format = TextureFormat.RGBA32;
            isLinear = DetectIsLinear(texture);
            mipmaps = DetectMipmaps(texture);
            compression = DetectCompression(texture);

            if (texture.isReadable
                && GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat)
                && !GraphicsFormatUtility.IsCrunchFormat(texture.format))
            {
                encoding = "raw";
                format   = texture.format;
                mipmaps  = texture.mipmapCount > 1;
                data     = texture.GetRawTextureData();
                return;
            }

            encoding = "png";
            var pixels = CapturePixels(texture);
            var gfxFormat = isLinear ? GraphicsFormat.R8G8B8A8_UNorm : GraphicsFormat.R8G8B8A8_SRGB;
            int w = width, h = height;
            string textureName = texture.name;
            encodeTask = Task.Run(() =>
            {
                try
                {
                    data = ImageConversion.EncodeArrayToPNG(pixels, gfxFormat, (uint) w, (uint) h);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TextureData] PNG encode failed for '{textureName}': {e.Message}");
                    data = Array.Empty<byte>();
                }
            });
        }

        public void FinishEncode()
        {
            if (encodeTask == null) return;
            encodeTask.Wait();
            encodeTask = null;
        }

        Color32[] CapturePixels(Texture2D texture)
        {
            if (texture.isReadable && !GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat))
                return texture.GetPixels32();

            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32,
                isLinear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, isLinear);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = readable.GetPixels32();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(readable);
            else
                UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }

        public void Serialize(ISerializer serializer)
        {
            if (serializer.IsWriter)
                FinishEncode();
            format = (TextureFormat) serializer.SerializeInt("format", (int) format);
            width = serializer.SerializeInt("width", width);
            height = serializer.SerializeInt("height", height);
            encoding = serializer.SerializeString("encoding", encoding ?? "");
            isLinear = serializer.SerializeBool("linear", isLinear);
            mipmaps = serializer.SerializeBool("mipmaps", mipmaps);
            if (serializer.IsReader)
            {
                bool legacyCompressed = serializer.SerializeBool("compressed", true);
                compression = serializer.SerializeInt("compression", legacyCompressed ? 1 : 0);
            }
            else
                compression = serializer.SerializeInt("compression", compression);
            data = serializer.SerializeBytes("data", data);
        }

        public Texture2D GetTexture(bool forceMips = false)
        {
            if (encoding == "png")
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps || forceMips, isLinear);
                texture.LoadImage(data, false);
                if (compression > 0)
                    texture.Compress(compression >= 2);
                return texture;
            }

            var raw = new Texture2D(width, height, format, mipmaps, isLinear);
            raw.LoadRawTextureData(data);
            raw.Apply(false, false);
            return raw;
        }

        static bool DetectIsLinear(Texture2D texture)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer != null)
                    return !importer.sRGBTexture;
            }
#endif
            return true;
        }

        static bool DetectMipmaps(Texture2D texture)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer != null)
                    return importer.mipmapEnabled;
            }
#endif
            return false;
        }

        static int DetectCompression(Texture2D texture)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer != null)
                    switch (importer.textureCompression)
                    {
                        case UnityEditor.TextureImporterCompression.Uncompressed: return 0;
                        case UnityEditor.TextureImporterCompression.CompressedHQ:  return 2;
                        default:                                                   return 1;
                    }
            }
#endif
            return 1;
        }

    }

}
