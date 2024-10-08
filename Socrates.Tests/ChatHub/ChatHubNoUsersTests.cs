﻿using Microsoft.AspNetCore.SignalR;
using Moq;
using Socrates.Constants;
using Socrates.Encryption.Interfaces;
using Socrates.Hubs;
using StackExchange.Redis;

namespace Socrates.Tests.ChatHub
{
    public class ChatHubNoUsersTests
    {
        private const string _username = "testUsername";

        private readonly Hubs.ChatHub _hub;

        private readonly Mock<IDatabase> _mockRedisDb;
        private readonly Mock<IChatHub> _mockCaller;
        private readonly Mock<IAssymmetricEncryption> _mockRsa;
        private readonly Mock<ISymmetricEncryption> _mockAes;
        private readonly Mock<Services.ILogger> _mockLogger;

        public ChatHubNoUsersTests()
        {
            // Arrange
            _mockRsa = new();
            _mockAes = new();
            _mockLogger = new();

            Mock<IConnectionMultiplexer> redis = new();
            _mockRedisDb = TestHelper.MockRedisDatabase(redis, []);

            Mock<HubCallerContext> mockContext = TestHelper.MockContext(_username);

            TestHelper.MockClients(out Mock<IHubCallerClients<IChatHub>> mockClients, out _mockCaller);

            _hub = new Hubs.ChatHub(_mockLogger.Object, redis.Object, _mockRsa.Object, _mockAes.Object)
            {
                Context = mockContext.Object,
                Clients = mockClients.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldAddUserToConnectedUsersEntriesInRedisDatabase_WhenContextHasCorrectIdentityWithUsername()
        {
            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockRedisDb.Verify(db => db.HashSetAsync(
                    Redis.ConnectedUsersKey,
                    _username,
                    It.IsAny<RedisValue>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ), Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_ShouldSendRSAPublicKeyToCaller_WhenContextHasCorrectIdentityWithUsername()
        {
            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockCaller.Verify(caller => caller.GetAsymmetricPublicKey(It.IsAny<string>()), Times.Once);
        }
    }
}
