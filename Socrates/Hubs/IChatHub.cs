namespace Socrates.Hubs
{
    public interface IChatHub
    {
        public Task ReceiveMessage(string user, string message);
    }
}
