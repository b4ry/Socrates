namespace Socrates.Encryption.Interfaces
{
    public interface IRSAEncryption
    {
        public string PublicKey { get; }
        public byte[] Decrypt(byte[] encryptedTextBytes);
    }
}
