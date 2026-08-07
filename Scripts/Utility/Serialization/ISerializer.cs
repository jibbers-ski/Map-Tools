using System;
using System.Collections.Generic;
using UnityEngine;

public interface ISerializer
{

    // writer = set serializer values aka write to file, reader = set object values aka read to memory

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

    public bool SerializeOptionalBool(string id, bool value, bool defaultValue = false)
        => SerializeBool(id, IsReader ? defaultValue : value);
    public float SerializeOptionalFloat(string id, float value, float defaultValue = 0)
        => SerializeFloat(id, IsReader ? defaultValue : value);
    public int SerializeOptionalInt(string id, int value, int defaultValue = 0)
        => SerializeInt(id, IsReader ? defaultValue : value);
    public string SerializeOptionalString(string id, string value, string defaultValue = null)
        => SerializeString(id, IsReader ? defaultValue : value);

    public byte[] SerializeBytes(string id, byte[] data);
    public void SerializeArray<T>(string id, ref T[] array, Func<string,T,T> func);
    public void SerializeSerializableArray<T>(string id, ref T[] array, Func<T> factory) where T : ISerializable;
    public void SerializeSerializableDict<T>(string id, ref Dictionary<string,T> dict, Func<string,T> factory, bool enterBlocks) where T : ISerializable;

    public void SerializeSerializableList<T>(string id, ref List<T> list, Func<T> factory, int maxCount = int.MaxValue) where T : ISerializable
    {
        int count = SerializeOptionalInt(id + "-count", list?.Count ?? 0);
        count = Mathf.Clamp(count, 0, maxCount);
        if (IsReader)
            list = new List<T>(count);

        if (count == 0)
            return;

        EnterBlock(id);
        for (int i = 0; i < count; ++i)
        {
            EnterBlock("item-" + i);
            if (IsReader)
            {
                var item = factory();
                item.Serialize(this);
                list.Add(item);
            }
            else
                list[i].Serialize(this);
            ExitBlock();
        }
        ExitBlock();
    }

    public void SerializeOptionalSerializableArray<T>(string id, ref T[] array, Func<T> factory, int maxCount = int.MaxValue) where T : ISerializable
    {
        int count = SerializeOptionalInt(id + "-count", array?.Length ?? 0);
        count = Mathf.Clamp(count, 0, maxCount);
        if (IsReader)
            array = count > 0 ? new T[count] : null;
        if (count == 0)
            return;

        EnterBlock(id);
        for (int i = 0; i < count; ++i)
        {
            EnterBlock("item-" + i);
            if (IsReader)
                array[i] = factory();
            array[i].Serialize(this);
            ExitBlock();
        }
        ExitBlock();
    }

    public void SerializeSparseBools(string id, bool[] array)
    {
        int count = 0;
        if (IsWriter)
            foreach (var b in array) if (b) count++;
        count = SerializeOptionalInt(id + "-count", count);

        if (IsWriter)
        {
            int n = 0;
            for (int i = 0; i < array.Length; ++i)
                if (array[i]) SerializeInt(id + "-" + n++, i);
        }
        else
        {
            Array.Clear(array, 0, array.Length);
            for (int i = 0; i < count; ++i)
            {
                int idx = SerializeInt(id + "-" + i, 0);
                if ((uint)idx < (uint)array.Length)
                    array[idx] = true;
            }
        }
    }

    public Vector2 SerializeVector2(string id, Vector2 value);
    public Vector3 SerializeVector3(string id, Vector3 value);

    public Vector2 SerializeOptionalVector2(string id, Vector2 value, Vector2 defaultValue = default)
        => SerializeVector2(id, IsReader ? defaultValue : value);
    public Vector3 SerializeOptionalVector3(string id, Vector3 value, Vector3 defaultValue = default)
        => SerializeVector3(id, IsReader ? defaultValue : value);

    public Quaternion SerializeQuaternion(string id, Quaternion value);

    public Quaternion SerializeOptionalQuaternion(string id, Quaternion value, Quaternion defaultValue = default)
        => SerializeQuaternion(id, IsReader ? defaultValue : value);

    public void EnterBlock(string id = "");
    public void ExitBlock();

    public bool TryEnterBlock(string id, bool present)
    {
        present = SerializeBool(id + "-has", present);
        if (present) EnterBlock(id);
        return present;
    }

    public IEnumerable<string> EnumerateKeys();
    public bool IsType<T>(string id);

}

public interface ISerializable {
    public void Serialize(ISerializer serializer);
}