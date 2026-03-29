using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class MapObject : MonoBehaviour
    {

        [Header("DO NOT CHANGE")]
        public string id;

    }

    public class MapObjectData : ISerializable
    {

        public string id;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public MapObjectData() {}
        public MapObjectData(MapObject mapObject)
        {
            id = mapObject.id;
            position = mapObject.transform.position;
            rotation = mapObject.transform.rotation.eulerAngles;
            scale = mapObject.transform.localScale;
        }

        public void Serialize(ISerializer serializer)
        {
            id = serializer.SerializeString("id", id);

            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            scale = serializer.SerializeVector3("scale", scale);
        }

    }

}
