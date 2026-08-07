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
        public Texture2D snowMask2;

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
        public CustomMapObjectData[] customObjects;
        public TreePrototypeData[] treePrototypes;

        public TextureData snowMaskData;
        public bool snowMask4Channel;
        public TextureData snowMask2Data;

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
            if (snowMaskData != null)
                snowMaskData.compression = 0;

            snowMask2Data = chunk.snowMask2 ? new TextureData(chunk.snowMask2) : null;
            if (snowMask2Data != null)
                snowMask2Data.compression = 0;

            var mat = chunk.terrain.materialTemplate;
            snowMask4Channel = mat != null && mat.HasProperty("_SnowMask4Channel") && mat.GetFloat("_SnowMask4Channel") > 0.5f;

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
            snowMask4Channel = serializer.SerializeBool("snow-mask-4channel", snowMask4Channel);
            SerializeTexture(serializer, "snow-mask-2", ref snowMask2Data);

            serializer.SerializeSerializableArray("objects", ref objects, () => new MapObjectData());

            serializer.SerializeSerializableArray("custom-objects", ref customObjects, () => new CustomMapObjectData());
            serializer.SerializeSerializableArray("tree-prototypes", ref treePrototypes, () => new TreePrototypeData());

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

    public class TreePrototypeData : ISerializable
    {

        public const int InstanceStride = 28;

        public string objectId;
        public CustomMapObjectPartData[] parts;
        public LODGroupData[] lodGroups;
        public ColliderData[] colliders;
        public byte[] instances;

        public TreePrototypeData() {}

        public void Serialize(ISerializer serializer)
        {
            objectId = serializer.SerializeString("object-id", objectId ?? "");
            serializer.SerializeSerializableArray("parts", ref parts, () => new CustomMapObjectPartData());
            serializer.SerializeSerializableArray("lod-groups", ref lodGroups, () => new LODGroupData());
            serializer.SerializeSerializableArray("colliders", ref colliders, () => new ColliderData());
            instances = serializer.SerializeBytes("instances", instances);
        }

        public int InstanceCount => (instances?.Length ?? 0) / InstanceStride;

    }

}
