namespace Socrates.Encryption.Interfaces
{
    public interface IAESEncryption
    {
        public Task<string> EncryptMessage(string message, string user);
    }
}
