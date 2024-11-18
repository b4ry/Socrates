using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using StackExchange.Redis;

namespace Socrates.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChatHub : Hub<IChatHub>
    {
        private readonly Services.ILogger _logger;
        private readonly IDatabase _redisDb;
        private readonly IAssymmetricEncryption _rsa;
        private readonly ISymmetricEncryption _aes;

        public ChatHub(Services.ILogger logger, IConnectionMultiplexer redis, IAssymmetricEncryption rsa, ISymmetricEncryption aes)
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
                        await EncryptMessageWithUserKeyAndSendIt(user, $"{username} joined the chat!");
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
                _logger.LogError(exception, "Exception occured!");

                return;
            }

            var disconnectingUsername = Context?.User?.Identity?.Name;

            if (disconnectingUsername != null)
            {
                var users = (await _redisDb.HashGetAllAsync(Redis.ConnectedUsersKey))
                    .Where(x => x.Name != disconnectingUsername)
                    .ToList();

                foreach (var user in users)
                {
                    await EncryptMessageWithUserKeyAndSendIt(user, $"{disconnectingUsername} left the chat!");
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

        private async Task EncryptMessageWithUserKeyAndSendIt(HashEntry user, string message)
        {
            var encryptedMessage = await _aes.EncryptMessage(message, user.Name!);

            await Clients.Client(user.Value!).ReceiveMessage(MessageSourceNames.Server, encryptedMessage);
        }
    }
}
