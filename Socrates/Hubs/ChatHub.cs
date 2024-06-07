using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Socrates.Hubs
{
    [Authorize]
    public class ChatHub(ILogger<ChatHub> logger) : Hub<IChatHub>
    {
        private readonly ILogger<ChatHub> _logger = logger;

        public override async Task OnConnectedAsync()
        {
            var userName = Context.User?.Identity?.Name;

            if (userName != null)
            {
                await Clients.All.ReceiveMessage("Server", $"{userName} joined the chat!");
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
                await Clients.All.ReceiveMessage(string.Empty, $"{userName} left the chat!");
            }
            else
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        public async Task SendMessageToCaller(string user, string message)
        {
            await Clients.Caller.ReceiveMessage(user, message);
        }
    }
}
