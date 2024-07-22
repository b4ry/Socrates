using System.Security.Cryptography;

namespace Socrates.Encryption
{
    public static class RSAEncryption
    {
        private static readonly RSA _rsa = RSA.Create();

        public static string PublicKey { get => _rsa.ToXmlString(false); }

        public static byte[] Decrypt(byte[] encryptedTextBytes)
        {
            return _rsa.Decrypt(encryptedTextBytes, RSAEncryptionPadding.Pkcs1);
        }
    }
}
