using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SaveEncryptionToolkit.Handlers
{
    /// <summary>
    /// Legacy <see cref="ISaveHandler"/> implementation backed by <see cref="BinaryFormatter"/> and Base64 encoding.
    /// Prefer <see cref="JsonSaveHandler"/> or a custom <see cref="ISaveHandler"/> for new projects because <see cref="BinaryFormatter"/> is obsolete in modern .NET.
    /// </summary>
    public class BinarySaveHandler : ISaveHandler
    {
        /// <inheritdoc/>
        public string Serialize<T>(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

#pragma warning disable SYSLIB0011 // Suppress obsolete BinaryFormatter warning
            using var memoryStream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, data);
            return Convert.ToBase64String(memoryStream.ToArray());
#pragma warning restore SYSLIB0011
        }

        /// <inheritdoc/>
        public T Deserialize<T>(string serialized)
        {
            if (string.IsNullOrEmpty(serialized))
                throw new ArgumentException("Serialized data cannot be null or empty.", nameof(serialized));

#pragma warning disable SYSLIB0011
            var bytes = Convert.FromBase64String(serialized);
            using var memoryStream = new MemoryStream(bytes);
            var formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(memoryStream);
#pragma warning restore SYSLIB0011
        }
    }
}