using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class MapData : ISerializable
    {

        public const int MaxIdLength = 36;

        public string id;
        public string name;
        public string version = Utility.Version;
        public string modVersion = "1.0";
        public string author;
        public string remixAuthors;
        public string remixIds;
        public float scale = 1;

        public MapTerrainChunkData[] chunks;
        public SpawnPointData[] spawnPoints;

        public Vector3 camStartPosition;
        public bool allowBackgroundMountains;

        public bool overrideTime;
        public float dayTime;
        public float dayTimeHours = -1;
        public float sunAngle;

        public Dictionary<string, MeshData> meshLibrary;
        public Dictionary<string, TextureData> textureLibrary;

        public bool headerOnly;

        public static bool IsValidId(string id)
            => !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength
                && id.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_');

        public static string IdFromName(string name)
        {
            var id = new System.Text.StringBuilder();
            foreach(var c in (name ?? "").ToLowerInvariant())
            {
                if(c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
                    id.Append(c);
                else if(id.Length > 0 && id[id.Length - 1] != '-')
                    id.Append('-');
            }
            var result = id.ToString();
            if(result.Length > MaxIdLength)
                result = result[..MaxIdLength];
            return result.TrimEnd('-');
        }

        public void Serialize(ISerializer serializer)
        {
            id = serializer.SerializeString("id", id);
            name = serializer.SerializeString("name", name);
            version = serializer.SerializeString("version", version);
            modVersion = serializer.SerializeString("mod-version", modVersion);
            author = serializer.SerializeString("author", author ?? "");
            remixAuthors = serializer.SerializeString("remix-authors", remixAuthors ?? "");
            remixIds = serializer.SerializeString("remix-ids", remixIds ?? "");
            scale = serializer.SerializeFloat("scale", scale);

            camStartPosition = serializer.SerializeVector3("cam-start-position", camStartPosition);
            allowBackgroundMountains = serializer.SerializeBool("allow-background-mountains", allowBackgroundMountains);

            overrideTime = serializer.SerializeBool("override-time", overrideTime);
            dayTime = serializer.SerializeFloat("day-time", dayTime);
            if (serializer.IsReader || dayTimeHours >= 0)
                dayTimeHours = serializer.SerializeFloat("day-time-hours", dayTimeHours);
            sunAngle = serializer.SerializeFloat("sun-angle", sunAngle);

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