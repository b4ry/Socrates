namespace Socrates.Encryption.Interfaces
{
    public interface IAssymmetricEncryption
    {
        public string PublicKey { get; }
        public byte[] Decrypt(byte[] encryptedTextBytes);
    }
}
