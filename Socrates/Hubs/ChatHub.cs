using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Socrates.Constants;
using Socrates.Encryption;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace Socrates.Hubs
{
    [Authorize]
    public class ChatHub : Hub<IChatHub>
    {
        private readonly ILogger<ChatHub> _logger;
        private readonly IDatabase _redisDb;
        private readonly Aes _aes;

        public ChatHub(ILogger<ChatHub> logger, IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
            _logger = logger;
            _aes = Aes.Create();
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context.User?.Identity?.Name;

            if (username != null)
            {
                var users = await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey);

                if (users.Length > 0)
                {
                    await Clients.Caller.GetUsers(users.Select(x => x.Name.ToString()).ToList());

                    foreach (var user in users)
                    {
                        var encryptedMessage = await EncryptMessage($"{username} joined the chat!", user.Name!);

                        await Clients.Client(user.Value!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
                    }

                    await Clients.Others.UserJoinsChat(username);
                }

                await Clients.Caller.GetAsymmetricPublicKey(RSAEncryption.PublicKey);
                await _redisDb.HashSetAsync(Redis.ConnectedUsersKey, username, Context.ConnectionId);
            }
            else
            {
                _logger.LogError("User without an identity name!");
                // TODO: logs it out
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception.ToString());
            }

            var disconnectingUsername = Context.User?.Identity?.Name;

            if (disconnectingUsername != null)
            {
                var usernames = (await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey))
                    .Where(x => x.Name != disconnectingUsername)
                    .Select(x => x.Name)
                    .ToList();

                foreach (var username in usernames)
                {
                    var encryptedMessage = await EncryptMessage($"{disconnectingUsername} left the chat!", username!);

                    await Clients.User(username!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
                }

                await _redisDb.HashDeleteAsync(Redis.ConnectedUsersKey, disconnectingUsername);
                await _redisDb.HashDeleteAsync(Redis.UserPublicIVsKey, disconnectingUsername);
                await _redisDb.HashDeleteAsync(Redis.UserPublicKeysKey, disconnectingUsername);

                await Clients.Others.UserLogsOut(disconnectingUsername);
            }
            else
            {
                _logger.LogError("User without an identity name!");
                // TODO: logs it out
            }
        }

        public async Task SendMessage(string user, string message)
        {
            var sourceUsername = Context?.User?.Identity?.Name ?? MessageSourceNames.Unknown;
            var newMessage = $"{sourceUsername}: {message}";

            if (user == MessageSourceNames.Server)
            {
                var usernames = (await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey))
                    .Where(x => x.Name != sourceUsername)
                    .Select(x => x.Name)
                    .ToList();

                foreach (var username in usernames)
                {
                    var encryptedMessage = await EncryptMessage(newMessage, username!);

                    await Clients.User(username!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
                }
            }
            else
            {
                var encryptedMessage = await EncryptMessage(newMessage, user);

                await Clients.User(user).ReceiveMessage(sourceUsername, encryptedMessage);
            }
        }

        public async void StoreSymmetricKey((byte[] encryptedSymmetricKey, byte[] encryptedSymmetricIV) encryptedSymmetricKeyInfo)
        {
            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                await _redisDb.HashSetAsync(Redis.UserPublicKeysKey, userName, encryptedSymmetricKeyInfo.encryptedSymmetricKey);
                await _redisDb.HashSetAsync(Redis.UserPublicIVsKey, userName, encryptedSymmetricKeyInfo.encryptedSymmetricIV);
            }
        }

        private async Task<string> EncryptMessage(string message, string user)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(message);

            var decryptedSymmetricKey = RSAEncryption.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicKeysKey, user))!);
            var decryptedSymmetricIV = RSAEncryption.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicIVsKey, user))!);

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
