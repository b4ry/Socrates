using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Socrates.Constants;
using Socrates.Encryption;
using StackExchange.Redis;

namespace Socrates.Hubs
{
    [Authorize]
    public class ChatHub(ILogger<ChatHub> logger, IConnectionMultiplexer redis) : Hub<IChatHub>
    {
        private const string _connectedUsersRedisKey = "connectedUsers";

        public override async Task OnConnectedAsync()
        {
            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                var db = redis.GetDatabase();

                var users = (await db.HashGetAllAsync(_connectedUsersRedisKey)).Select(x => x.Value.ToString()).ToList();

                if (users.Count > 0)
                {
                    await Clients.Caller.GetUsers(users);
                }

                await Clients.All.ReceiveMessage(MessageSourceNames.Server, $"{userName} joined the chat!");
                await Clients.Caller.GetAsymmetricPublicKey(RSAEncryption.PublicKey);
                await Clients.AllExcept(Context.ConnectionId).UserJoinsChat(userName);

                await db.HashSetAsync(_connectedUsersRedisKey, Context.ConnectionId, userName);
            }
            else
            {
                logger.LogError("User without an identity name!");
                // TODO: logs it out
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if(exception != null)
            {
                logger.LogError(exception.ToString());
            }

            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                await Clients.Others.ReceiveMessage(MessageSourceNames.Server, $"{userName} left the chat!");

                var db = redis.GetDatabase();
                await db.HashDeleteAsync(_connectedUsersRedisKey, Context.ConnectionId);

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

        public void SendSymmetricKey((byte[] encryptedSymmetricKey, byte[] encryptedSymmetricIV) encryptedSymmetricKeyInfo)
        {
            var decryptedSymmetricKey = RSAEncryption.Decrypt(encryptedSymmetricKeyInfo.encryptedSymmetricKey);
            var decryptedSymmetricIV = RSAEncryption.Decrypt(encryptedSymmetricKeyInfo.encryptedSymmetricIV);
        }
    }
}
