using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SaveEncryptionToolkit.Handlers
{
    public class BinarySaveHandler : ISaveHandler
    {
        public string Serialize<T>(T data)
        {
#pragma warning disable SYSLIB0011 // Suppress obsolete BinaryFormatter warning
            using var memoryStream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, data);
            return Convert.ToBase64String(memoryStream.ToArray());
#pragma warning restore SYSLIB0011
        }

        public T Deserialize<T>(string serialized)
        {
#pragma warning disable SYSLIB0011
            var bytes = Convert.FromBase64String(serialized);
            using var memoryStream = new MemoryStream(bytes);
            var formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(memoryStream);
#pragma warning restore SYSLIB0011
        }
    }
}