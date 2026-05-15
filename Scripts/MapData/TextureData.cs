using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class TextureData : ISerializable
    {

        public TextureFormat format;
        public int width;
        public int height;

        public byte[] data;

        public TextureData() {}
        public TextureData(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            format = TextureFormat.RGBA32;

            if (!texture.isReadable)
            {
                Debug.LogError($"[TextureData] Texture '{texture.name}' is not readable. Enable Read/Write in import settings.");
                data = new byte[width * height * 4];
                return;
            }

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.SetPixels(texture.GetPixels());
            readable.Apply();
            data = readable.GetRawTextureData();
        }

        public void Serialize(ISerializer serializer)
        {
            format = (TextureFormat) serializer.SerializeInt("format", (int) format);
            width = serializer.SerializeInt("width", width);
            height = serializer.SerializeInt("height", height);
            data = serializer.SerializeBytes("data", data);
        }

        public Texture2D GetTexture()
        {
            var texture = new Texture2D(width, height, format, false, true);
            texture.LoadRawTextureData(data);
            texture.Apply(false, false);
            return texture;
        }

    }

}
