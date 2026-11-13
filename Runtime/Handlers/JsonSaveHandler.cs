using System;
using UnityEngine;

namespace SaveEncryptionToolkit.Handlers
{
    /// <summary>
    /// Default <see cref="ISaveHandler"/> implementation backed by Unity's <see cref="JsonUtility"/>.
    /// Supports types compatible with Unity's serializer (classes or structs marked with <see cref="SerializableAttribute"/>).
    /// </summary>
    public class JsonSaveHandler : ISaveHandler
    {
        /// <inheritdoc/>
        public string Serialize<T>(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return JsonUtility.ToJson(data);
        }

        /// <inheritdoc/>
        public T Deserialize<T>(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("Serialized data cannot be null or empty.", nameof(data));

            return JsonUtility.FromJson<T>(data);
        }
    }
}