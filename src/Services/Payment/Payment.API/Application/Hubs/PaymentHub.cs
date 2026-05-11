using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Payment.API.Application.Hubs
{
    [Authorize]
    public class PaymentHub : Hub
    {
        private readonly ILogger<PaymentHub> _logger;

        public PaymentHub(ILogger<PaymentHub> logger)
        {
            _logger = logger;
        }

        public async Task WatchPayment(Guid paymentId)
        {
            var groupName = paymentId.ToString();
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} started watching payment {PaymentId}", Context.ConnectionId, paymentId);
            
            // Confirm successful subscription to the client
            await Clients.Caller.SendAsync("Subscribed", paymentId);
        }

        public async Task UnwatchPayment(Guid paymentId)
        {
            var groupName = paymentId.ToString();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("Client {ConnectionId} stopped watching payment {PaymentId}", Context.ConnectionId, paymentId);
        }

        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to PaymentHub: {ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(exception, "Client disconnected from PaymentHub: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
