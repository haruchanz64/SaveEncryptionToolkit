using System;
using UnityEngine;

namespace SaveEncryptionToolkit.Handlers
{
    public class JsonSaveHandler : ISaveHandler
    {
        public string Serialize<T>(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return JsonUtility.ToJson(data);
        }

        public T Deserialize<T>(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Serialized data cannot be null or empty.", nameof(data));

            return JsonUtility.FromJson<T>(data);
        }
    }
}