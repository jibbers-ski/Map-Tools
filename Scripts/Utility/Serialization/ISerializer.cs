using System;
using System.Collections.Generic;
using UnityEngine;

public interface ISerializer
{

    // writer = set serializer values, reader = set object values

    public bool IsReader { get; }
    public bool IsWriter => !IsReader;
    public object Data { get; }

    public string CurrentPath { get; }
    public int CurrentBlockCount { get; }

    public void Begin(params object[] parameters);
    public void Close();

    public float SerializeFloat(string id, float value);
    public bool SerializeBool(string id, bool value);
    public string SerializeString(string id, string value);
    public int SerializeInt(string id, int value);
    public byte[] SerializeBytes(string id, byte[] data);
    public void SerializeArray<T>(string id, ref T[] array, Func<string,T,T> func);
    public void SerializeSerializableArray<T>(string id, ref T[] array, Func<T> factory) where T : ISerializable;
    public void SerializeSerializableDict<T>(string id, ref Dictionary<string,T> dict, Func<string,T> factory, bool enterBlocks) where T : ISerializable;

    public Vector2 SerializeVector2(string id, Vector2 value);
    public Vector3 SerializeVector3(string id, Vector3 value);

    public void EnterBlock(string id = "");
    public void ExitBlock();

    public IEnumerable<string> EnumerateKeys();
    public bool IsType<T>(string id);

}

public interface ISerializable {
    public void Serialize(ISerializer serializer);
}