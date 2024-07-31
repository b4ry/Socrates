using System.Security.Cryptography;
using Socrates.Encryption.Interfaces;

namespace Socrates.Encryption
{
    public class RSAEncryption : IRSAEncryption
    {
        private readonly RSA _rsa = RSA.Create();

        public string PublicKey { get => _rsa.ToXmlString(false); }

        public byte[] Decrypt(byte[] encryptedTextBytes)
        {
            return _rsa.Decrypt(encryptedTextBytes, RSAEncryptionPadding.Pkcs1);
        }
    }
}
