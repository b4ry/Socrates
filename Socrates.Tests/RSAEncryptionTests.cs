using Socrates.Encryption;
using Socrates.Encryption.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Socrates.Tests
{
    public class RSAEncryptionTests
    {
        private readonly IRSAEncryption _rsa;

        public RSAEncryptionTests()
        {
            _rsa = new RSAEncryption();
        }

        [Fact]
        public void PublicKey_ShouldReturnRSAPublicKeyInCorrectXmlFormat()
        {
            // Arrange
            string pattern = @"<RSAKeyValue>\s*<Modulus>.*?</Modulus>\s*<Exponent>.*?</Exponent>\s*</RSAKeyValue>";
            Regex regex = new(pattern, RegexOptions.IgnoreCase);

            // Act
            bool isMatch = regex.IsMatch(_rsa.PublicKey);

            // Assert
            Assert.True(isMatch);
        }

        [Fact]
        public void Decrypt_ShouldDecryptAnEncryptedMessage_WhenTheMessageIsEncryptedWithCorrespondingPublicKey()
        {
            // Arrange
            var rsa = RSA.Create();
            rsa.FromXmlString(_rsa.PublicKey);
            var messageToEncrypt = "test";
            var encryptedMessage = rsa.Encrypt(Encoding.UTF8.GetBytes(messageToEncrypt), RSAEncryptionPadding.Pkcs1);

            // Act
            var decryptedMessage = Encoding.UTF8.GetString(_rsa.Decrypt(encryptedMessage));

            // Assert
            Assert.Equal(messageToEncrypt, decryptedMessage);
        }
    }
}