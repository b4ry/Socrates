using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Socrates.Encryption
{
    public class AESEncryption : IAESEncryption
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IRSAEncryption _rsa;
        private readonly Aes _aes;

        public AESEncryption(IRSAEncryption rsa, IConnectionMultiplexer redis)
        {
            _redis = redis;
            _redisDb = redis.GetDatabase();
            _rsa = rsa;
            _aes = Aes.Create();
        }

        public async Task<string> EncryptMessage(string message, string user)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(message);

            var decryptedSymmetricKey = _rsa.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicKeysKey, user))!);
            var decryptedSymmetricIV = _rsa.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicIVsKey, user))!);

            _aes.Key = decryptedSymmetricKey;
            _aes.IV = decryptedSymmetricIV;

            var aesEncryptor = _aes.CreateEncryptor();

            using MemoryStream ms = new();
            using (CryptoStream cs = new(ms, aesEncryptor, CryptoStreamMode.Write))
            {
                await cs.WriteAsync(textBytes);
            }

            return Convert.ToBase64String(ms.ToArray());
        }
    }
}
