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
            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                var users = await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey);

                if (users.Length > 0)
                {
                    await Clients.Caller.GetUsers(users.Select(x => x.Name.ToString()).ToList());

                    foreach (var user in users)
                    {
                        byte[] textBytes = Encoding.UTF8.GetBytes($"{userName} joined the chat!");

                        var decryptedSymmetricKey = RSAEncryption.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicKeysKey, user.Name))!);
                        var decryptedSymmetricIV = RSAEncryption.Decrypt((await _redisDb.HashGetAsync(Redis.UserPublicIVsKey, user.Name))!);

                        _aes.Key = decryptedSymmetricKey;
                        _aes.IV = decryptedSymmetricIV;

                        var aesEncryptor = _aes.CreateEncryptor();

                        using MemoryStream ms = new();
                        using (CryptoStream cs = new(ms, aesEncryptor, CryptoStreamMode.Write))
                        {
                            await cs.WriteAsync(textBytes);
                        }

                        var encryptedMessage = Convert.ToBase64String(ms.ToArray());

                        await Clients.Client(user.Value!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
                    }

                    await Clients.Others.UserJoinsChat(userName);
                }

                await Clients.Caller.GetAsymmetricPublicKey(RSAEncryption.PublicKey);
                await _redisDb.HashSetAsync(Redis.ConnectedUsersKey, userName, Context.ConnectionId);
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

            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                await Clients.Others.ReceiveMessage(MessageSourceNames.Server, $"{userName} left the chat!");
                await _redisDb.HashDeleteAsync(Redis.ConnectedUsersKey, userName);
                await Clients.Others.UserLogsOut(userName);
            }
            else
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        public async Task SendMessage(string user, string message)
        {
            var sourceUserName = Context?.User?.Identity?.Name ?? MessageSourceNames.Unknown;

            if (user == MessageSourceNames.Server)
            {
                await Clients.Others.ReceiveMessage(MessageSourceNames.Server, $"{sourceUserName}: {message}");
            }
            else
            {
                await Clients.User(user).ReceiveMessage(sourceUserName, message);
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
    }
}
