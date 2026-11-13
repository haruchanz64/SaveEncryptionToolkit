namespace SaveEncryptionToolkit.Handlers
{
    /// <summary>
    /// Contract for converting save payloads between strongly typed objects and strings before encryption or after decryption.
    /// </summary>
    public interface ISaveHandler
    {
        /// <summary>
        /// Serializes <paramref name="data"/> into a string representation suitable for encryption.
        /// </summary>
        /// <typeparam name="T">Type of the save object being serialized.</typeparam>
        /// <param name="data">Save object instance to serialize.</param>
        /// <returns>The serialized string payload.</returns>
        string Serialize<T>(T data);

        /// <summary>
        /// Deserializes a string payload back into an instance of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target save object type.</typeparam>
        /// <param name="data">Serialized string payload produced by <see cref="Serialize{T}"/>.</param>
        /// <returns>The deserialized object instance.</returns>
        T Deserialize<T>(string data);
    }
}