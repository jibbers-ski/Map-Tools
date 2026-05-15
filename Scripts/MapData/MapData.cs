using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class MapData : ISerializable
    {

        public string id;
        public string name;
        public string version = Utility.Version;

        public MapTerrainChunkData[] chunks;
        public SpawnPointData[] spawnPoints;

        public Vector3 camStartPosition;
        public bool allowBackgroundMountains;

        public Dictionary<string, MeshData> meshLibrary;
        public Dictionary<string, TextureData> textureLibrary;

        public bool headerOnly;

        public void Serialize(ISerializer serializer)
        {
            id = serializer.SerializeString("id", id);
            name = serializer.SerializeString("name", name);
            version = serializer.SerializeString("version", version);

            camStartPosition = serializer.SerializeVector3("cam-start-position", camStartPosition);
            allowBackgroundMountains = serializer.SerializeBool("allow-background-mountains", allowBackgroundMountains);

            if(!headerOnly)
            {
                serializer.SerializeSerializableArray("chunks", ref chunks, () => new MapTerrainChunkData());
                serializer.SerializeSerializableArray("spawnpoints", ref spawnPoints, () => new SpawnPointData());

                meshLibrary ??= new Dictionary<string, MeshData>();
                textureLibrary ??= new Dictionary<string, TextureData>();
                serializer.SerializeSerializableDict("mesh-library", ref meshLibrary, k => new MeshData(), true);
                serializer.SerializeSerializableDict("texture-library", ref textureLibrary, k => new TextureData(), true);
            }
        }

    }

}