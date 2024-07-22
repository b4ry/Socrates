using System.Security.Cryptography;

namespace Socrates.Encryption
{
    public static class RSAEncryption
    {
        private static readonly RSA _rsa = RSA.Create();

        public static string PublicKey { get => _rsa.ToXmlString(false); }

        public static string Decrypt(byte[] encryptedTextBytes)
        {
            var decryptedTextBytes = _rsa.Decrypt(encryptedTextBytes, RSAEncryptionPadding.Pkcs1);
            var decryptedText = BitConverter.ToString(decryptedTextBytes);

            return decryptedText;
        }
    }
}
