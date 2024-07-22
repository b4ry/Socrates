namespace Socrates.Hubs
{
    public interface IChatHub
    {
        public Task ReceiveMessage(string user, string message);
        public Task UserJoinsChat(string user);
        public Task UserLogsOut(string user);
        public Task GetUsers(IEnumerable<string> users);
        public Task GetAsymmetricPublicKey(string publicKey);
    }
}
