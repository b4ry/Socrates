namespace Socrates.Encryption.Interfaces
{
    public interface ISymmetricEncryption
    {
        public Task<string> EncryptMessage(string message, string user);
    }
}
