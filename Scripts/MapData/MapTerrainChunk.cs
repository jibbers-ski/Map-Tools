using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    [Serializable]
    public class MapTerrainChunk
    {

        public Terrain terrain;
        public Transform mapObjectContainer;

        [Header("Masks")]
        public Texture2D snowMask;

        [Header("Repeats")]
        public int repeats = 1;
        public Vector3 repeatOffset;

    }

    public class MapTerrainChunkData : ISerializable
    {

        public int heightmapResolution;
        public byte[] terrainData;
        public Vector3 position;
        public Vector3 size;

        public MapObjectData[] objects;

        public TextureData snowMaskData;

        public int repeats;
        public Vector3 repeatOffset;

        public MapTerrainChunkData() {}

        public MapTerrainChunkData(MapTerrainChunk chunk) {
            terrainData = chunk.terrain.ExportHeightmapR16();
            position = chunk.terrain.transform.position;
            size = chunk.terrain.terrainData.size;
            heightmapResolution = chunk.terrain.terrainData.heightmapResolution;

            if(chunk.mapObjectContainer)
            {
                var mapObjects = chunk.mapObjectContainer.GetComponentsInChildren<MapObject>();
                objects = mapObjects.Select(m => new MapObjectData(m)).ToArray();
            }

            snowMaskData = chunk.snowMask ? new TextureData(chunk.snowMask) : null;

            repeats = chunk.repeats;
            repeatOffset = chunk.repeatOffset;
        }

        public void Serialize(ISerializer serializer)
        {
            position = serializer.SerializeVector3("position", position);
            size = serializer.SerializeVector3("size", size);
            heightmapResolution = serializer.SerializeInt("heightmap-resolution", heightmapResolution);
            terrainData = serializer.SerializeBytes("terrain-data", terrainData);

            SerializeTexture(serializer, "snow-mask", ref snowMaskData);

            serializer.SerializeSerializableArray("objects", ref objects, () => new MapObjectData());

            repeats = serializer.SerializeInt("repeats", repeats);
            repeatOffset = serializer.SerializeVector3("repeat-offset", repeatOffset);
        }

        void SerializeTexture(ISerializer serializer, string id, ref TextureData textureData)
        {
            serializer.EnterBlock(id);
            if(serializer.IsWriter && textureData != null)
                textureData.Serialize(serializer);
            else if(serializer.IsReader && serializer.CurrentBlockCount > 0)
            {
                textureData = new TextureData();
                textureData.Serialize(serializer);
            }
            serializer.ExitBlock();
        }

    }

}
