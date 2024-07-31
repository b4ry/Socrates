using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using Socrates.Hubs;
using StackExchange.Redis;
using System.Security.Claims;

namespace Socrates.Tests
{
    public class ChatHubTests
    {
        private readonly Mock<ILogger<ChatHub>> _logger;
        private const string _username = "testUsername";

        public ChatHubTests()
        {
            _logger = new Mock<ILogger<ChatHub>>();
        }

        [Fact]
        public async void OnConnectedAsync_ShouldAddUserToConnectedUsersEntriesInRedisDatabase_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            ArrangeInitialData(
                out Mock<IDatabase> mockRedisDb,
                out ChatHub hub,
                out Mock<IChatHub> mockCaller,
                out Mock<IChatHub> mockClient,
                out Mock<IChatHub> mockOthers,
                out Mock<IRSAEncryption> mockRsa,
                out Mock<IAESEncryption> mockAes,
                []
            );

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockRedisDb.Verify(db => db.HashSetAsync(
                    Redis.ConnectedUsersKey,
                    _username,
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ), Times.Once);
        }

        [Fact]
        public async void OnConnectedAsync_ShouldSendRSAPublicKeyToCaller_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            ArrangeInitialData(
                out Mock<IDatabase> mockRedisDb,
                out ChatHub hub,
                out Mock<IChatHub> mockCaller,
                out Mock<IChatHub> mockClient,
                out Mock<IChatHub> mockOthers,
                out Mock<IRSAEncryption> mockRsa,
                out Mock<IAESEncryption> mockAes,
                []
            );

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockCaller.Verify(caller => caller.GetAsymmetricPublicKey(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async void OnConnectedAsync_ShouldSendUsersToCaller_WhenContextHasCorrectIdentityWithUsernameAndThereAreOthersConnected()
        {
            // Arrange
            ArrangeInitialData(
                out Mock<IDatabase> mockRedisDb,
                out ChatHub hub,
                out Mock<IChatHub> mockCaller,
                out Mock<IChatHub> mockClient,
                out Mock<IChatHub> mockOthers,
                out Mock<IRSAEncryption> mockRsa,
                out Mock<IAESEncryption> mockAes,
                [new("testUser", "testValue")]
            );

            mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult("encryptedString"));

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockCaller.Verify(caller => caller.GetUsers(It.IsAny<IEnumerable<string>>()), Times.Once);
        }

        [Fact]
        public async void OnConnectedAsync_ShouldSendEncryptedWithClientKeyMessageToClient_WhenContextHasCorrectIdentityWithUsernameAndThereAreOthersConnected()
        {
            // Arrange
            ArrangeInitialData(
                out Mock<IDatabase> mockRedisDb,
                out ChatHub hub,
                out Mock<IChatHub> mockCaller,
                out Mock<IChatHub> mockClient,
                out Mock<IChatHub> mockOthers,
                out Mock<IRSAEncryption> mockRsa,
                out Mock<IAESEncryption> mockAes,
                [new("testUser", "testValue")]
            );

            mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult("encryptedString"));

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockAes.Verify(aes => aes.EncryptMessage(It.IsAny<string>(), "testUser"), Times.Once);
            mockClient.Verify(client => client.ReceiveMessage(MessageSourceNames.Server, "encryptedString"), Times.Once);
        }

        [Fact]
        public async void OnConnectedAsync_ShouldNotifyOthersThatUserJoinsChat_WhenContextHasCorrectIdentityWithUsernameAndThereAreOthersConnected()
        {
            // Arrange
            ArrangeInitialData(
                out Mock<IDatabase> mockRedisDb,
                out ChatHub hub,
                out Mock<IChatHub> mockCaller,
                out Mock<IChatHub> mockClient,
                out Mock<IChatHub> mockOthers,
                out Mock<IRSAEncryption> mockRsa,
                out Mock<IAESEncryption> mockAes,
                [new("testUser", "testValue")]
            );

            mockAes.Setup(aes => aes.EncryptMessage(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.FromResult("encryptedString"));

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockOthers.Verify(others => others.UserJoinsChat(It.IsAny<string>()), Times.Once);
        }


        private void ArrangeInitialData(
            out Mock<IDatabase> mockRedisDb,
            out ChatHub hub,
            out Mock<IChatHub> mockCaller,
            out Mock<IChatHub> mockClient,
            out Mock<IChatHub> mockOthers,
            out Mock<IRSAEncryption> mockRsa,
            out Mock<IAESEncryption> mockAes,
            HashEntry[] hashEntries
        )
        {
            mockRsa = new();
            mockAes = new();
            Mock<IConnectionMultiplexer> redis = new();
            mockRedisDb = MockRedisDatabase(redis, hashEntries);
            Mock<HubCallerContext> mockContext = MockContext(_username);
            MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out mockCaller, out mockClient, out mockOthers);

            hub = new ChatHub(_logger.Object, redis.Object, mockRsa.Object, mockAes.Object)
            {
                Context = mockContext.Object,
                Clients = mockClients.Object
            };
        }

        private static void MockClients(
            out Mock<IHubCallerClients<IChatHub>> mockClients,
            out Mock<IChatHub> mockCaller,
            out Mock<IChatHub> mockClient,
            out Mock<IChatHub> mockOthers
        )
        {
            mockClients = new Mock<IHubCallerClients<IChatHub>>();

            mockCaller = new();
            mockClients.Setup(clients => clients.Caller).Returns(mockCaller.Object);

            mockClient = new();
            mockClients.Setup(clients => clients.Client(It.IsAny<string>())).Returns(mockClient.Object);

            mockOthers = new();
            mockClients.Setup(clients => clients.Others).Returns(mockOthers.Object);
        }

        private static Mock<IDatabase> MockRedisDatabase(Mock<IConnectionMultiplexer> redis, HashEntry[] dbRecords)
        {
            var mockDatabase = new Mock<IDatabase>();

            mockDatabase.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(dbRecords));
            redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            return mockDatabase;
        }

        private static Mock<HubCallerContext> MockContext(string username)
        {
            var mockContext = new Mock<HubCallerContext>();
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username)
            };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            mockContext.Setup(context => context.User).Returns(claimsPrincipal);

            return mockContext;
        }
    }
}
