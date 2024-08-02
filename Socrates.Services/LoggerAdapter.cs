using Microsoft.Extensions.Logging;

namespace Socrates.Services
{
    public class LoggerAdapter<T> : ILogger
    {
        private readonly ILogger<T> _logger;

        public LoggerAdapter(ILogger<T> logger)
        {
            _logger = logger;
        }

        public void LogError(Exception exception, string message)
        {
            _logger.LogError(exception, message);
        }

        public void LogError(string message)
        {
            _logger.LogError(message);
        }
    }
}
