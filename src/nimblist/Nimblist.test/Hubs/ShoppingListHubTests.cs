using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Nimblist.api.Hubs;
using Xunit;

namespace Nimblist.test.Hubs
{
    public class ShoppingListHubTests
    {
        private const string ConnectionId = "conn-test-123";

        /// <summary>
        /// Creates a fully wired ShoppingListHub with mock Groups, Context, and Clients.
        /// </summary>
        private static (ShoppingListHub Hub, Mock<IGroupManager> MockGroups) CreateHub()
        {
            var mockGroups = new Mock<IGroupManager>();
            mockGroups
                .Setup(g => g.AddToGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockGroups
                .Setup(g => g.RemoveFromGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(ConnectionId);

            var mockClients = new Mock<IHubCallerClients>();

            var hub = new ShoppingListHub();
            hub.Context = mockContext.Object;
            hub.Groups = mockGroups.Object;
            hub.Clients = mockClients.Object;

            return (hub, mockGroups);
        }

        // ── JoinListGroup ────────────────────────────────────────────────────

        [Fact]
        public async Task JoinListGroup_CallsAddToGroupAsync_WithListId()
        {
            var (hub, mockGroups) = CreateHub();
            var listId = "list-guid-abc";

            await hub.JoinListGroup(listId);

            mockGroups.Verify(
                g => g.AddToGroupAsync(
                    ConnectionId,
                    $"list_{listId}",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task JoinListGroup_DoesNothing_WhenListIdIsNullOrEmpty(string? listId)
        {
            var (hub, mockGroups) = CreateHub();

            await hub.JoinListGroup(listId!);

            mockGroups.Verify(
                g => g.AddToGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── LeaveListGroup ───────────────────────────────────────────────────

        [Fact]
        public async Task LeaveListGroup_CallsRemoveFromGroupAsync_WithListId()
        {
            var (hub, mockGroups) = CreateHub();
            var listId = "list-guid-xyz";

            await hub.LeaveListGroup(listId);

            mockGroups.Verify(
                g => g.RemoveFromGroupAsync(
                    ConnectionId,
                    $"list_{listId}",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task LeaveListGroup_DoesNothing_WhenListIdIsNullOrEmpty(string? listId)
        {
            var (hub, mockGroups) = CreateHub();

            await hub.LeaveListGroup(listId!);

            mockGroups.Verify(
                g => g.RemoveFromGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // ── OnConnectedAsync / OnDisconnectedAsync ───────────────────────────

        [Fact]
        public async Task OnConnectedAsync_CompletesSuccessfully()
        {
            // OnConnectedAsync calls base.OnConnectedAsync() which requires a hub context.
            // We verify it runs without throwing.
            var mockGroups = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(ConnectionId);
            var mockClients = new Mock<IHubCallerClients>();

            var hub = new ShoppingListHub();
            hub.Context = mockContext.Object;
            hub.Groups = mockGroups.Object;
            hub.Clients = mockClients.Object;

            // Should complete without throwing
            await hub.OnConnectedAsync();
        }

        [Fact]
        public async Task OnDisconnectedAsync_CompletesSuccessfully_WithNullException()
        {
            var mockGroups = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(ConnectionId);
            var mockClients = new Mock<IHubCallerClients>();

            var hub = new ShoppingListHub();
            hub.Context = mockContext.Object;
            hub.Groups = mockGroups.Object;
            hub.Clients = mockClients.Object;

            await hub.OnDisconnectedAsync(null);
        }

        [Fact]
        public async Task OnDisconnectedAsync_CompletesSuccessfully_WithException()
        {
            var mockGroups = new Mock<IGroupManager>();
            var mockContext = new Mock<HubCallerContext>();
            mockContext.Setup(c => c.ConnectionId).Returns(ConnectionId);
            var mockClients = new Mock<IHubCallerClients>();

            var hub = new ShoppingListHub();
            hub.Context = mockContext.Object;
            hub.Groups = mockGroups.Object;
            hub.Clients = mockClients.Object;

            var ex = new InvalidOperationException("Transport error");
            await hub.OnDisconnectedAsync(ex);
        }
    }
}
