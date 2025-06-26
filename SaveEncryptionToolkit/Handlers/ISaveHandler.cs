namespace SaveEncryptionToolkit.Handlers
{
    public interface ISaveHandler
    {
        string Serialize<T>(T data);
        T Deserialize<T>(string data);
    }
}