using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using StackExchange.Redis;

namespace Socrates.Hubs
{
    [Authorize]
    public class ChatHub : Hub<IChatHub>
    {
        private readonly ILogger<ChatHub> _logger;
        private readonly IDatabase _redisDb;
        private readonly IRSAEncryption _rsa;
        private readonly IAESEncryption _aes;

        public ChatHub(ILogger<ChatHub> logger, IConnectionMultiplexer redis, IRSAEncryption rsa, IAESEncryption aes)
        {
            _redisDb = redis.GetDatabase();
            _logger = logger;
            _rsa = rsa;
            _aes = aes;
        }

        public override async Task OnConnectedAsync()
        {
            var username = Context?.User?.Identity?.Name;

            if (username != null)
            {
                var users = await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey);

                if (users.Length > 0)
                {
                    await Clients.Caller.GetUsers(users.Select(x => x.Name.ToString()).ToList());

                    foreach (var user in users)
                    {
                        await EncryptMessageWithUserKeyAndSendIt(username, user);
                    }

                    await Clients.Others.UserJoinsChat(username);
                }

                await Clients.Caller.GetAsymmetricPublicKey(_rsa.PublicKey);
                await _redisDb.HashSetAsync(Redis.ConnectedUsersKey, username, Context!.ConnectionId);
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
                    var encryptedMessage = await _aes.EncryptMessage($"{disconnectingUsername} left the chat!", username!);

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
                    var encryptedMessage = await _aes.EncryptMessage(newMessage, username!);

                    await Clients.User(username!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
                }
            }
            else
            {
                var encryptedMessage = await _aes.EncryptMessage(newMessage, user);

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

        private async Task EncryptMessageWithUserKeyAndSendIt(string username, HashEntry user)
        {
            var encryptedMessage = await _aes.EncryptMessage($"{username} joined the chat!", user.Name!);

            await Clients.Client(user.Value!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
        }
    }
}
