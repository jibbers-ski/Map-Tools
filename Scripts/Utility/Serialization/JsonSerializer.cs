using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

public class JsonSerializer : ISerializer
{

    public static bool EnableLog;

    public bool IsReader => !isWriting;
    public object Data => mainObject.ToString(Formatting.Indented);

    public string CurrentPath => CurrentObject?.Path ?? "null";
    public int CurrentBlockCount => CurrentObject.Properties().Count();

    private bool isWriting = true;

    private JObject mainObject;
    private Stack<JObject> blockStack = new Stack<JObject>();
    private JObject CurrentObject => blockStack.Count == 0 ? mainObject : blockStack.Peek();

    public void Begin(params object[] parameters)
    {
        if (parameters.Length > 0)
            isWriting = (bool)parameters[0];

        if (parameters.Length > 1 && parameters[1] is string s)
        {
            try
            {
                mainObject = JObject.Parse(s);
            }
            catch (JsonException e)
            {
                Debug.LogError($"[JsonSerializer] Failed to parse JSON: {e.Message}");
                mainObject = new JObject();
            }
        }

        mainObject ??= new JObject();
    }

    public void Close()
    {
        blockStack.Clear();
    }

    public void EnterBlock(string id)
    {
        if (isWriting)
        {
            var newObject = new JObject();
            if (AddValue(id, newObject))
                blockStack.Push(newObject);
        }
        else
        {
            if (TryGet(id, out JObject obj))
                blockStack.Push(obj);
            else
                Debug.LogWarning($"[JsonSerializer] Couldn't find Block with id '{id}'");
        }
    }

    public void ExitBlock()
    {
        if (blockStack.Count > 0)
            blockStack.Pop();
        else
            Debug.LogWarning("[JsonSerializer] Tried to exit block at root");
    }

    public bool SerializeBool(string id, bool value = default)
    {
        if (isWriting)
            AddValue(id, value);
        else if (TryGet(id, out bool result))
            return result;
        return value;
    }

    public float SerializeFloat(string id, float value = default)
    {
        if (isWriting)
            AddValue(id, value);
        else if (TryGet(id, out float result))
            return result;
        return value;
    }

    public double SerializeDouble(string id, double value = default)
    {
        if (isWriting)
            AddValue(id, value);
        else if (TryGet(id, out double result))
            return result;
        return value;
    }

    public int SerializeInt(string id, int value = default)
    {
        if (isWriting)
            AddValue(id, value);
        else if (TryGet(id, out int result))
            return result;
        return value;
    }

    public string SerializeString(string id, string value = default)
    {
        if (isWriting)
            AddValue(id, value);
        else if (TryGet(id, out string result))
            return result;
        return value;
    }

    public Vector2 SerializeVector2(string id, Vector2 value = default)
    {
        value.x = SerializeFloat(id + "-x", value.x);
        value.y = SerializeFloat(id + "-y", value.y);
        return value;
    }

    public Vector3 SerializeVector3(string id, Vector3 value = default)
    {
        value.x = SerializeFloat(id + "-x", value.x);
        value.y = SerializeFloat(id + "-y", value.y);
        value.z = SerializeFloat(id + "-z", value.z);
        return value;
    }

    public byte[] SerializeBytes(string id, byte[] data)
    {
        if (isWriting)
        {
            if (data == null)
            {
                AddValue(id, null);
                return null;
            }

            string base64 = Convert.ToBase64String(data);
            AddValue(id, base64);
            return data;
        }
        else
        {
            if (TryGet(id, out string base64))
            {
                if (string.IsNullOrEmpty(base64))
                    return null;

                try
                {
                    return Convert.FromBase64String(base64);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[JsonSerializer] Failed to decode base64 for '{id}': {e.Message}");
                    return null;
                }
            }

            return null;
        }
    }

    public void SerializeArray<T>(string id, ref T[] array, Func<string,T,T> func)
    {
        if(EnableLog) Debug.Log("Serializing Array " + id);
        var count = SerializeInt(id + "-count", array?.Length??0);
        if(IsReader)
            array = new T[count];
        EnterBlock(id);
        if(array != null)
            for(int i = 0; i < array.Length; ++i)
                array[i] = func("value-" + i, array[i]);
        ExitBlock();
    }

    public void SerializeSerializableArray<T>(string id, ref T[] array, Func<T> factory) where T : ISerializable
    {
        if(EnableLog) Debug.Log("Serializing Serializable Array " + id);
        SerializeArray(id, ref array, (eId,current) =>
        {
            EnterBlock(eId);
            if(IsReader)
                current = factory();
            current.Serialize(this);
            ExitBlock();
            return current;
        });
    }

    public void SerializeSerializableDict<T>(string id, ref Dictionary<string,T> dict, Func<string,T> factory, bool enterBlocks) where T : ISerializable
    {
        if(EnableLog) Debug.Log("Serializing Serializable Dict " + id);
        EnterBlock(id);
        if(isWriting)
        {
            foreach(var entry in dict)
            {
                if(enterBlocks) EnterBlock(entry.Key);
                entry.Value.Serialize(this);
                if(enterBlocks) ExitBlock();
            }
        } else
        {
            dict = new();
            foreach(var key in EnumerateKeys())
            {
                if(enterBlocks) EnterBlock(key);
                var newObj = factory(key);
                newObj.Serialize(this);
                dict.Add(key, newObj);
                if(enterBlocks) ExitBlock();
            }
        }
        ExitBlock();
    }

    private bool AddValue(string id, object value)
    {
        if (CurrentObject.ContainsKey(id))
        {
            Debug.LogWarning($"[JsonSerializer] Key '{id}' already taken!");
            return false;
        }

        if(EnableLog) Debug.Log("Setting " + CurrentObject.Path + " > " + id + " to " + value);

        if (value is JToken token)
            CurrentObject[id] = token;
        else
            CurrentObject[id] = JToken.FromObject(value);

        return true;
    }

    public bool TryGet<T>(string id, out T result, bool ignoreWarnings = false, bool strongTyped = false)
    {
        result = default(T);

        if(EnableLog) Debug.Log("Getting " + CurrentPath + " > " + id );

        if (CurrentObject.TryGetValue(id, out JToken token))
        {
            try
            {
                if(strongTyped && ((JValue)token).Value is not T)
                {
                    if(EnableLog) Debug.Log(" => Strong Type Fail: " + ((JValue)token).Value.GetType().ToString());
                    return false;
                }

                // Special handling for JObject
                if (typeof(T) == typeof(JObject) && token.Type == JTokenType.Object)
                {
                    result = (T)(object)token;
                    if(EnableLog) Debug.Log(" => Success " + result);
                    return true;
                }

                result = token.ToObject<T>();
                if(EnableLog) Debug.Log(" => Success " + result);
                return true;
            }
            catch (Exception e)
            {
                if(!ignoreWarnings)
                    Debug.LogWarning($"[JsonSerializer] Object with id '{id}' could not be converted to '{typeof(T)}': {e.Message}");
                if(EnableLog) Debug.Log(" => Conversion Fail");
                return false;
            }
        }

        if(!ignoreWarnings)
            Debug.LogWarning($"[JsonSerializer] Couldn't load object of Type '{typeof(T)}' with id '{id}'");
        if(EnableLog) Debug.Log(" => Not Found Fail");
        return false;
    }

    public IEnumerable<string> EnumerateKeys()
    {
        if (isWriting)
            throw new InvalidOperationException("Enumeration only valid in reader mode");

        foreach (var prop in CurrentObject.Properties())
            yield return prop.Name;
    }

    public bool IsType<T>(string id) => TryGet<T>(id, out _, ignoreWarnings:true, strongTyped:true);

}