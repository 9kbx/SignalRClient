using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SignalRServerSample.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string targetUser, string message)
    {
        var sender =
            Context.User?.FindFirstValue(ClaimTypes.Name)
            ?? Context.UserIdentifier
            ?? throw new HubException("Unauthorized user.");

        if (string.IsNullOrWhiteSpace(targetUser))
        {
            throw new HubException("Target user is required.");
        }

        Console.WriteLine(
            $"send message|connection:{Context.ConnectionId}|from:{sender}|to:{targetUser}|msg:{message}"
        );

        await Clients.User(targetUser).SendAsync("ReceiveMessage", sender, message);
    }
}
