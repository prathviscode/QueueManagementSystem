using Microsoft.AspNetCore.SignalR;

namespace QueueManagementSystem.Hubs
{
    // FIX: Hub is intentionally left simple – the server pushes "ReceiveUpdate"
    // broadcasts from controllers via IHubContext<QueueHub>.
    // Display clients connect anonymously (AllowAnonymous on MapHub).
    // Staff/Admin connections work with or without a JWT token.
    public class QueueHub : Hub
    {
        // Optional: override OnConnectedAsync / OnDisconnectedAsync for logging
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
