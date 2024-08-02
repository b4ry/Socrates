using Microsoft.AspNetCore.SignalR;
using Moq;
using Socrates.Hubs;
using StackExchange.Redis;
using System.Security.Claims;

namespace Socrates.Tests
{
    internal static class TestHelper
    {
        public static void MockClients(
            out Mock<IHubCallerClients<IChatHub>> mockClients,
            out Mock<IChatHub> mockCaller,
            out Mock<IChatHub> mockClient,
            out Mock<IChatHub> mockOthers)
        {
            mockClients = new Mock<IHubCallerClients<IChatHub>>();

            mockCaller = new();
            mockClients.Setup(clients => clients.Caller).Returns(mockCaller.Object);

            mockClient = new();
            mockClients.Setup(clients => clients.Client(It.IsAny<string>())).Returns(mockClient.Object);

            mockOthers = new();
            mockClients.Setup(clients => clients.Others).Returns(mockOthers.Object);
        }

        public static void MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out Mock<IChatHub> mockCaller)
        {
            mockClients = new Mock<IHubCallerClients<IChatHub>>();

            mockCaller = new();
            mockClients.Setup(clients => clients.Caller).Returns(mockCaller.Object);
        }

        public static Mock<IDatabase> MockRedisDatabase(Mock<IConnectionMultiplexer> redis, HashEntry[] dbRecords)
        {
            var mockDatabase = new Mock<IDatabase>();

            mockDatabase.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .Returns(Task.FromResult(dbRecords));
            redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDatabase.Object);

            return mockDatabase;
        }

        public static Mock<HubCallerContext> MockContext(string username)
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
