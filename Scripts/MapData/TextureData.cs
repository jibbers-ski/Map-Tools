using System;
using System.Collections.Generic;
using System.Linq;
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

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.SetPixels(texture.GetPixels());
            readable.Apply();

            format = TextureFormat.RGBA32;
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
