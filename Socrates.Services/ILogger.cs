namespace Socrates.Services
{
    public interface ILogger
    {
        public void LogError(Exception exception, string message);
        public void LogError(string message);
    }
}
