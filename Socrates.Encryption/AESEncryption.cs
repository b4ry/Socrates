using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Socrates.Encryption
{
    public class AESEncryption : ISymmetricEncryption
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _redisDb;
        private readonly IAssymmetricEncryption _rsa;
        private readonly Aes _aes;

        public AESEncryption(IAssymmetricEncryption rsa, IConnectionMultiplexer redis)
        {
            _redis = redis;
            _redisDb = redis.GetDatabase();
            _rsa = rsa;
            _aes = Aes.Create();
        }

        public async Task<string> EncryptMessage(string message, string user)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(message);

            var encryptedUserPublicKey = await _redisDb.HashGetAsync(Redis.UserPublicKeysKey, user);
            var encryptedUserPublicIV = await _redisDb.HashGetAsync(Redis.UserPublicIVsKey, user);
            var decryptedSymmetricKey = _rsa.Decrypt(encryptedUserPublicKey!);
            var decryptedSymmetricIV = _rsa.Decrypt(encryptedUserPublicIV!);

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
