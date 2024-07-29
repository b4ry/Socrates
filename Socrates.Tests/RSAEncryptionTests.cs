using Socrates.Encryption;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Socrates.Tests
{
    public class RSAEncryptionTests
    {
        [Fact]
        public void PublicKey_ShouldReturnRSAPublicKeyInCorrectXmlFormat()
        {
            // Arrange
            string pattern = @"<RSAKeyValue>\s*<Modulus>.*?</Modulus>\s*<Exponent>.*?</Exponent>\s*</RSAKeyValue>";
            Regex regex = new(pattern, RegexOptions.IgnoreCase);

            // Act
            bool isMatch = regex.IsMatch(RSAEncryption.PublicKey);

            // Assert
            Assert.True(isMatch);
        }

        [Fact]
        public void Decrypt_ShouldDecryptAnEncryptedMessage_WhenTheMessageIsEncryptedWithCorrespondingPublicKey()
        {
            // Arrange
            var rsa = RSA.Create();
            rsa.FromXmlString(RSAEncryption.PublicKey);
            var messageToEncrypt = "test";
            var encryptedMessage = rsa.Encrypt(Encoding.UTF8.GetBytes(messageToEncrypt), RSAEncryptionPadding.Pkcs1);

            // Act
            var decryptedMessage = Encoding.UTF8.GetString(RSAEncryption.Decrypt(encryptedMessage));

            // Assert
            Assert.Equal(messageToEncrypt, decryptedMessage);
        }
    }
}