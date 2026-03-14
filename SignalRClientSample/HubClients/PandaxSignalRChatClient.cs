using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pandax.SignalRClient;

namespace SignalRClientSample.HubClients;

public interface IChatClient
{
    Task StartAsync(CancellationToken ct = default);

    Task SendMessageAsync(string user, string message);

    // 定义一个事件或回调，供外部订阅接收到的消息
    event Action<string, string>? OnMessageReceived;
}

public class ChatClientOptions : SignalRClientOptions { }

public class PandaxSignalRChatClient(
    IAuthenticationProvider auth,
    IOptions<ChatClientOptions> options,
    ILogger<PandaxSignalRChatClient> logger
) : BaseSignalRClient<ChatClientOptions>(auth, options, logger), IChatClient
{
    public event Action<string, string>? OnMessageReceived;

    protected override void OnRegisterHubEvents()
    {
        // 绑定业务特有的消息
        _connection.On<string, string>(
            "ReceiveMessage",
            (user, message) =>
            {
                OnMessageReceived?.Invoke(user, message);
            }
        );
    }

    public async Task SendMessageAsync(string user, string message)
    {
        await _connection.InvokeAsync("SendMessage", user, message);
    }

    protected override async Task OnClosed(Exception? ex)
    {
        await base.OnClosed(ex);

        // 如果是因为授权失败导致的关闭
        if (ex?.Message.Contains("401") == true)
        {
            _logger.LogCritical("Authentication failed consistently. Stopping reconnect.");
            // 这里可以触发一个外部事件，通知 UI 弹出登录窗口
        }
        else
        {
            // 比如：如果是非正常关闭，等待 30 秒后尝试手动重启连接
            _logger.LogInformation("Attempting manual restart...");
            await Task.Delay(TimeSpan.FromSeconds(30));
            await StartAsync();
        }
    }
}
