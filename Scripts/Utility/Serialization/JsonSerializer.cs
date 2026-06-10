using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

public class JsonSerializer : ISerializer
{

    public static bool EnableLog;
    const bool LogWarnings = false;

    public bool EnableCompression;

    public bool IsReader => !isWriting;
    public object Data => mainObject.ToString(Formatting.Indented);

    public string CurrentPath => CurrentObject?.Path ?? "null";
    public int CurrentBlockCount => CurrentObject.Properties().Count();

    private bool isWriting = true;

    private JObject mainObject;
    private Stack<JObject> blockStack = new Stack<JObject>();
    private JObject CurrentObject => blockStack.Count == 0 ? mainObject : blockStack.Peek();

    const int ParallelDecodeMinChars = 512;
    private Dictionary<string, Task<byte[]>> decodeTasks;

    public void Begin(params object[] parameters)
    {
        if (parameters.Length > 0)
            isWriting = (bool)parameters[0];

        if (parameters.Length > 1 && parameters[1] is string s)
        {
            try
            {
                mainObject = JObject.Parse(s);
                if (!isWriting)
                    StartParallelDecode();
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
        decodeTasks = null;
    }

    private void StartParallelDecode()
    {
        decodeTasks = new Dictionary<string, Task<byte[]>>();
        foreach (var token in mainObject.Descendants())
        {
            if (token is not JValue jv || jv.Type != JTokenType.String) continue;
            var str = (string) jv.Value;
            if (str == null || str.Length < ParallelDecodeMinChars) continue;
            decodeTasks[jv.Path] = Task.Run(() =>
            {
                try { return DecodeBlob(str); }
                catch { return null; }
            });
        }
        if (decodeTasks.Count == 0)
            decodeTasks = null;
    }

    private static byte[] DecodeBlob(string base64)
    {
        byte[] bytes = Convert.FromBase64String(base64);
        // detect gzip magic header
        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            using var input = new MemoryStream(bytes);
            using var gz = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gz.CopyTo(output);
            return output.ToArray();
        }
        return bytes;
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
            {
                //Debug.LogWarning($"[Reading JsonSerializer] Couldn't find Block with id '{id}', inserting new..");
                blockStack.Push(new JObject());
            }
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

            if (EnableCompression)
            {
                byte[] compressed;
                using (var ms = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
                        gz.Write(data, 0, data.Length);
                    compressed = ms.ToArray();
                }
                AddValue(id, Convert.ToBase64String(compressed));
            }
            else
            {
                AddValue(id, Convert.ToBase64String(data));
            }
            return data;
        }
        else
        {
            if (TryGet(id, out string base64))
            {
                if (string.IsNullOrEmpty(base64))
                    return null;

                if (decodeTasks != null
                    && CurrentObject.TryGetValue(id, out JToken token)
                    && decodeTasks.TryGetValue(token.Path, out var task))
                {
                    var decoded = task.Result;
                    if (decoded != null)
                        return decoded;
                }

                try
                {
                    return DecodeBlob(base64);
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

        if (value == null)
            CurrentObject[id] = JValue.CreateNull();
        else if (value is JToken token)
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
                if(LogWarnings && !ignoreWarnings)
                    Debug.LogWarning($"[JsonSerializer] Object with id '{id}' could not be converted to '{typeof(T)}': {e.Message}");
                if(EnableLog) Debug.Log(" => Conversion Fail");
                return false;
            }
        }

        if(LogWarnings && !ignoreWarnings)
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