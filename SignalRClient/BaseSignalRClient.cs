using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SignalRClient;

public abstract class BaseSignalRClient<TOptions> : IAsyncDisposable
    where TOptions : SignalRClientOptions
{
    protected readonly HubConnection _connection;
    protected readonly ILogger _logger;

    protected BaseSignalRClient(
        IAuthenticationProvider authProvider,
        IOptions<TOptions> options,
        ILogger logger)
    {
        _logger = logger;
        var settings = options.Value;

        _connection = new HubConnectionBuilder()
            .WithUrl(settings.Url,
                httpOptions =>
                {
                    httpOptions.AccessTokenProvider = async () => await authProvider.GetAccessTokenAsync();
                })
            .WithAutomaticReconnect(settings.RetryPolicy)
            .Build();

        // 注册基础事件
        RegisterBaseEvents();
        // 留给子类实现具体的业务事件绑定
        OnRegisterHubEvents();
    }

    private void RegisterBaseEvents()
    {
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += OnReconnected;
        _connection.Closed += OnClosed;
    }

    // 默认实现，子类可 override 覆盖
    protected virtual Task OnReconnecting(Exception? ex)
    {
        _logger.LogWarning("SignalR Reconnecting: {Msg}", ex?.Message);
        return Task.CompletedTask;
    }

    protected virtual Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR Reconnected. New ID: {Id}", connectionId);
        return Task.CompletedTask;
    }

    protected virtual Task OnClosed(Exception? ex)
    {
        _logger.LogError("SignalR Connection Closed: {Msg}", ex?.Message);
        return Task.CompletedTask;
    }

    // 子类必须实现此方法来绑定具体的 Hub 消息映射
    protected abstract void OnRegisterHubEvents();

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection.State == HubConnectionState.Disconnected)
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("SignalR Connected. ID: {Id}", _connection.ConnectionId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}