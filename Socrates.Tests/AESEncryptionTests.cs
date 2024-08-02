using Moq;
using Socrates.Constants;
using Socrates.Encryption;
using Socrates.Encryption.Interfaces;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Socrates.Tests
{
    public class AESEncryptionTests
    {
        private ISymmetricEncryption? _aes;
        private readonly Mock<IAssymmetricEncryption> _mockRsa;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;

        public AESEncryptionTests()
        {
            _mockRsa = new Mock<IAssymmetricEncryption>();
            _mockRedis = new Mock<IConnectionMultiplexer>();
        }

        [Fact]
        public async Task EncryptMessage_ShouldEncryptMessageWithUserPublicKeyAndIV()
        {
            // Arrange
            var message = "testMessage";
            var aes = Aes.Create();

            var userKey = new RedisValue("testKey");
            var userIV = new RedisValue("testIV");

            var _mockDatabase = new Mock<IDatabase>();
            _mockDatabase.Setup(
                database => database.HashGetAsync(Redis.UserPublicKeysKey, It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())
            ).Returns(Task.FromResult(userKey));
            _mockDatabase.Setup(
                database => database.HashGetAsync(Redis.UserPublicIVsKey, It.IsAny<RedisValue>(), It.IsAny<CommandFlags>())
            ).Returns(Task.FromResult(userIV));
            _mockRedis.Setup(redis => redis.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);

            _mockRsa.Setup(x => x.Decrypt(userKey!)).Returns(aes.Key);
            _mockRsa.Setup(x => x.Decrypt(userIV!)).Returns(aes.IV);

            _aes = new AESEncryption(_mockRsa.Object, _mockRedis.Object);

            // Act
            var encryptedMessage = await _aes.EncryptMessage(message, "testUser");

            // Assert
            var decryptedMessage = await Decrypt(aes, encryptedMessage);
            Assert.Equal(message, decryptedMessage);
        }

        private static async Task<string> Decrypt(Aes aes, string encryptedText)
        {
            var aesDecryptor = aes.CreateDecryptor();
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            using MemoryStream ms = new();
            using (CryptoStream cs = new(ms, aesDecryptor, CryptoStreamMode.Write))
            {
                await cs.WriteAsync(encryptedBytes);
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
