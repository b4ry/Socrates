using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Socrates.Constants;
using Socrates.Hubs;
using StackExchange.Redis;
using System.Security.Claims;

namespace Socrates.Tests
{
    public class ChatHubTests
    {
        private readonly Mock<ILogger<ChatHub>> _logger;
        private readonly Mock<IConnectionMultiplexer> _redis;

        public ChatHubTests()
        {
            _logger = new Mock<ILogger<ChatHub>>();
            _redis = new Mock<IConnectionMultiplexer>();
        }

        [Fact]
        public async void OnConnectedAsync_ShouldAddUserToConnectedUsersEntriesInRedisDatabase_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            var username = "testUsername";
            Mock<IDatabase> mockRedisDb = MockRedisDatabase([]);
            Mock<HubCallerContext> mockContext = MockContext(username);
            MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out Mock<IChatHub> mockCaller);

            var hub = new ChatHub(_logger.Object, _redis.Object)
            {
                Context = mockContext.Object,
                Clients = mockClients.Object
            };

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockRedisDb.Verify(db => db.HashSetAsync(
                    Redis.ConnectedUsersKey,
                    username,
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ), Times.Once);
        }

        [Fact]
        public async void OnConnectedAsync_ShouldSendRSAPublicKeyToTheCaller_WhenContextHasCorrectIdentityWithUsername()
        {
            // Arrange
            var username = "testUsername";
            Mock<IDatabase> mockRedisDb = MockRedisDatabase([]);
            Mock<HubCallerContext> mockContext = MockContext(username);
            MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out Mock<IChatHub> mockCaller);

            var hub = new ChatHub(_logger.Object, _redis.Object)
            {
                Context = mockContext.Object,
                Clients = mockClients.Object
            };

            // Act
            await hub.OnConnectedAsync();

            // Assert
            mockCaller.Verify(client => client.GetAsymmetricPublicKey(It.IsAny<string>()), Times.Once);
        }

        private static void MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out Mock<IChatHub> mockClientProxy)
        {
            mockClients = new Mock<IHubCallerClients<IChatHub>>();
            mockClientProxy = new();
            mockClients.Setup(clients => clients.Caller).Returns(mockClientProxy.Object);
        }

        private Mock<IDatabase> MockRedisDatabase(HashEntry[] dbRecords)
        {
            var mockDatabase = new Mock<IDatabase>();

            mockDatabase.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(dbRecords));
            _redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            return mockDatabase;
        }

        private static Mock<IHubCallerClients<IChatHub>> MockHubCallerClients()
        {
            var mockClients = new Mock<IHubCallerClients<IChatHub>>();

            return mockClients;
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
