using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Socrates.Constants;

namespace Socrates.Hubs
{
    [Authorize]
    public class ChatHub(ILogger<ChatHub> logger) : Hub<IChatHub>
    {
        private readonly ILogger<ChatHub> _logger = logger;
        private readonly IDictionary<string, string> _userConnectionIds = new Dictionary<string, string>(); 

        public override async Task OnConnectedAsync()
        {
            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                _userConnectionIds.Add(userName, Context.ConnectionId);
                await Clients.All.ReceiveMessage(MessageSourceNames.Server, $"{userName} joined the chat!");
                await Clients.AllExcept(_userConnectionIds[userName]).NewUserJoinedChat(userName);
            }
            else
            {
                await base.OnConnectedAsync();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if(exception != null)
            {
                _logger.LogError(exception.ToString());
            }

            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                await Clients.All.ReceiveMessage(MessageSourceNames.Server, $"{userName} left the chat!");
            }
            else
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        public async Task SendMessage(string user, string message)
        {
            var sourceUserName = Context?.User?.Identity?.Name ?? MessageSourceNames.Unknown;

            if (user == "Server")
            {
                await Clients.Others.ReceiveMessage(MessageSourceNames.Server, $"{sourceUserName}: {message}");
            }
            else
            {
                await Clients.User(user).ReceiveMessage(sourceUserName, message);
            }
        }
    }
}
