using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalRClientSample.HubClients;

namespace SignalRClientSample;

public class ChatWorker(IChatClient chatClient, ILogger<ChatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 订阅消息
        chatClient.OnMessageReceived += (user, msg) =>
        {
            logger.LogInformation("New Message from {User}: {Msg}", user, msg);
        };

        // 启动连接
        await chatClient.StartAsync(stoppingToken);

        // 循环发送测试消息
        while (!stoppingToken.IsCancellationRequested)
        {
            await chatClient.SendMessageAsync("ConsoleApp", $"Heartbeat {DateTime.Now}");
            await Task.Delay(5000, stoppingToken);
        }
    }
}