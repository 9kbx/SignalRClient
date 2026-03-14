using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SignalRServerSample.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        Console.WriteLine($"send message|{Context.ConnectionId} : {user} : {message}");
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
