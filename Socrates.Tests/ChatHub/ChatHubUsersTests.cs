using Microsoft.AspNetCore.SignalR;
using Moq;
using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using Socrates.Hubs;
using StackExchange.Redis;

namespace Socrates.Tests.ChatHub
{
    public class ChatHubUsersTests
    {
        private const string _username = "testUsername";
        private const string _otherUsername = "testUser";
        private const string _encryptedMessage = "encryptedString";

        private readonly Hubs.ChatHub _hub;

        private readonly Mock<IChatHub> _mockCaller;
        private readonly Mock<IChatHub> _mockClient;
        private readonly Mock<IChatHub> _mockOthers;
        private readonly Mock<IAssymmetricEncryption> _mockRsa;
        private readonly Mock<ISymmetricEncryption> _mockAes;
        private readonly Mock<Services.ILogger> _mockLogger;

        public ChatHubUsersTests()
        {
            // Arrange
            _mockRsa = new();
            _mockAes = new();
            _mockLogger = new();

            Mock<IConnectionMultiplexer> redis = new();
            TestHelper.MockRedisDatabase(redis, [new(_otherUsername, "testValue")]);

            Mock<HubCallerContext> mockContext = TestHelper.MockContext(_username);

            TestHelper.MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out _mockCaller, out _mockClient, out _mockOthers);

            _hub = new Hubs.ChatHub(_mockLogger.Object, redis.Object, _mockRsa.Object, _mockAes.Object)
            {
                Context = mockContext.Object,
                Clients = mockClients.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldSendUsersToCaller_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            _mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(_encryptedMessage));

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockCaller.Verify(caller => caller.GetUsers(It.IsAny<IEnumerable<string>>()), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldSendEncryptedMessageWithClientKeyToClient_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            _mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(_encryptedMessage));

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockAes.Verify(aes => aes.EncryptMessage(It.IsAny<string>(), _otherUsername), Times.Once);
            _mockClient.Verify(client => client.ReceiveMessage(MessageSourceNames.Server, _encryptedMessage), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldNotifyOthersThatUserJoinsChat_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            _mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(_encryptedMessage));

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockOthers.Verify(others => others.UserJoinsChat(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldSendEncryptedMessageWithClientKeyToClient_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            _mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(_encryptedMessage));

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _mockAes.Verify(aes => aes.EncryptMessage(It.IsAny<string>(), _otherUsername), Times.Once);
            _mockClient.Verify(client => client.ReceiveMessage(MessageSourceNames.Server, _encryptedMessage), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_ShouldNotSendEncryptedMessageToDisconnectingClient_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            _mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult(_encryptedMessage));

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _mockAes.Verify(aes => aes.EncryptMessage(It.IsAny<string>(), _username), Times.Never);
        }
    }
}
