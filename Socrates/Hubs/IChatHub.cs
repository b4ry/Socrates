namespace Socrates.Hubs
{
    public interface IChatHub
    {
        public Task ReceiveMessage(string user, string message);
        public Task NewUserJoinedChat(string user);
        public Task UserLoggedOut(string user);
        public Task GetUsers(IEnumerable<string> users);
    }
}
