using Microsoft.AspNetCore.Authorization; // Namespace needed if you add [Authorize] later
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace Nimblist.api.Hubs // Adjust namespace if needed
{
    // TODO: Add [Authorize] attribute later if you only want authenticated users to connect
    // [Authorize]
    public class ShoppingListHub : Hub
    {
        // This method will be called by the React client when it starts viewing a list
        public async Task JoinListGroup(string listId)
        {
            if (string.IsNullOrWhiteSpace(listId)) return; // Basic validation

            string groupName = $"list_{listId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // Optional: Log who joined which group
            Console.WriteLine($"--> SignalR Client {Context.ConnectionId} joined group {groupName}");

            // Optional: Send a confirmation message back to the specific client (the caller)
            // await Clients.Caller.SendAsync("JoinedGroupConfirmation", listId);
        }

        // This method will be called by the React client when it stops viewing a list
        public async Task LeaveListGroup(string listId)
        {
            if (string.IsNullOrWhiteSpace(listId)) return;

            string groupName = $"list_{listId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            // Optional: Log who left which group
            Console.WriteLine($"--> SignalR Client {Context.ConnectionId} left group {groupName}");

            // Optional: Send confirmation back
            // await Clients.Caller.SendAsync("LeftGroupConfirmation", listId);
        }

        // --- Methods below are examples, NOT called by clients directly ---
        // These illustrate how your backend services/controllers will send messages later
        // using IHubContext<ShoppingListHub>

        // public async Task SendItemAddedMessage(string groupName, Item item) {
        //    await Clients.Group(groupName).SendAsync("ReceiveItemAdded", item);
        // }
        // public async Task SendItemToggledMessage(string groupName, Guid itemId, bool isChecked) {
        //     await Clients.Group(groupName).SendAsync("ReceiveItemToggled", new { ItemId = itemId, IsChecked = isChecked });
        // }
        // public async Task SendItemDeletedMessage(string groupName, Guid itemId) {
        //     await Clients.Group(groupName).SendAsync("ReceiveItemDeleted", itemId);
        // }


        // --- Connection Lifecycle Events (Optional) ---
        public override Task OnConnectedAsync()
        {
            // Optional: Log connections
            Console.WriteLine($"--> SignalR Client Connected: {Context.ConnectionId} | User: {Context.User?.Identity?.Name ?? "Anonymous"}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"--> SignalR Client Disconnected: {Context.ConnectionId} | Error: {exception?.Message}");
            // Note: Automatically removing users from groups on disconnect requires
            // tracking connections, which adds complexity. Often handled by client calling LeaveListGroup.
            return base.OnDisconnectedAsync(exception);
        }
    }
}