using UnityEngine;

namespace SaveEncryptionToolkit.Handlers
{
    public class JsonSaveHandler : ISaveHandler
    {
        public string Serialize<T>(T data) => JsonUtility.ToJson(data);
        public T Deserialize<T>(string data) => JsonUtility.FromJson<T>(data);
    }
}