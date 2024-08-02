using Microsoft.AspNetCore.SignalR;
using Moq;
using Socrates.Encryption.Interfaces;
using Socrates.Hubs;
using StackExchange.Redis;

namespace Socrates.Tests.ChatHub
{
    public class ChatHubNoContext
    {
        private readonly Hubs.ChatHub _hub;
        private readonly Mock<Services.ILogger> _mockLogger;

        public ChatHubNoContext()
        {
            // Arrange
            Mock<IAssymmetricEncryption> mockRsa = new();
            Mock<ISymmetricEncryption> mockAes = new();
            _mockLogger = new();

            Mock<IConnectionMultiplexer> redis = new();

            TestHelper.MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients);

            _hub = new Hubs.ChatHub(_mockLogger.Object, redis.Object, mockRsa.Object, mockAes.Object)
            {
                Context = null,
                Clients = mockClients.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldLogError_WhenNoContext()
        {
            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockLogger.Verify(logger => logger.LogError("User without an identity name!"));
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldLogError_WhenExceptionOccurs()
        {
            // Arrange
            var exception = new Exception("testException");

            // Act
            await _hub.OnDisconnectedAsync(exception);

            // Assert
            _mockLogger.Verify(logger => logger.LogError(exception, "Exception occured!"));
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldLogError_WhenNoContext()
        {
            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _mockLogger.Verify(logger => logger.LogError("User without an identity name!"));
        }
    }
}
