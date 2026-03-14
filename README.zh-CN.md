# Pandax.SignalRClient 中文文档

SignalRClient 是一个对 ASP.NET Core SignalR Client 的轻量封装，适合在 .NET 后台服务、Worker、控制台程序中快速构建稳定的 SignalR 客户端。

NuGet 包名：`Pandax.SignalRClient`

[English README](./README.md)

## 特性

- 基于 `BaseSignalRClient<TOptions>` 统一封装 `HubConnection`
- 通过 `IAuthenticationProvider` 注入访问令牌
- 支持自定义重连策略
- 适合放入依赖注入和后台服务生命周期中管理
- 默认提供一个基础重连策略 `DefaultRetryPolicy`

## 适用场景

- 后台服务持续监听 Hub 消息
- 控制台程序连接 SignalR 服务端
- 需要统一管理连接、鉴权、重连的业务客户端
- 需要按业务拆分多个 Hub Client 类型

## 安装

```bash
dotnet add package Pandax.SignalRClient
```

## 核心类型说明

- `BaseSignalRClient<TOptions>`：封装连接创建、基础事件注册、启动和释放逻辑
- `SignalRClientOptions`：基础配置项，包含 `Url` 和 `RetryPolicy`
- `IAuthenticationProvider`：访问令牌提供器
- `DefaultRetryPolicy`：默认重连策略，最多重试 5 次，每次间隔 5 秒

## 推荐接入方式

推荐按下面的顺序接入：

1. 实现 `IAuthenticationProvider`
2. 定义一个继承自 `SignalRClientOptions` 的业务配置类
3. 定义一个继承自 `BaseSignalRClient<TOptions>` 的业务客户端
4. 通过依赖注入注册客户端
5. 在 `BackgroundService` 或程序入口中启动客户端

## 1. 实现鉴权提供器

```csharp
using Pandax.SignalRClient;

public sealed class JwtAuthenticationProvider : IAuthenticationProvider
{
    public Task<string?> GetAccessTokenAsync()
    {
        return Task.FromResult<string?>("your-jwt-token");
    }
}
```

如果你的令牌来自刷新接口、缓存或本地存储，也可以在这里自行封装。

## 2. 定义客户端配置

```csharp
using Pandax.SignalRClient;

public sealed class ChatClientOptions : SignalRClientOptions
{
}
```

## 3. 定义业务客户端

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pandax.SignalRClient;

public interface IChatClient
{
    Task StartAsync(CancellationToken ct = default);
    Task SendMessageAsync(string user, string message);
    event Action<string, string>? OnMessageReceived;
}

public sealed class ChatClient(
    IAuthenticationProvider authenticationProvider,
    IOptions<ChatClientOptions> options,
    ILogger<ChatClient> logger)
    : BaseSignalRClient<ChatClientOptions>(authenticationProvider, options, logger), IChatClient
{
    public event Action<string, string>? OnMessageReceived;

    protected override void OnRegisterHubEvents()
    {
        _connection.On<string, string>("ReceiveMessage", (user, message) =>
        {
            OnMessageReceived?.Invoke(user, message);
        });
    }

    public Task SendMessageAsync(string user, string message)
    {
        return _connection.InvokeAsync("SendMessage", user, message);
    }
}
```

这里的关键点是：

- 在 `OnRegisterHubEvents()` 中绑定业务消息
- 通过 `_connection.InvokeAsync()` 调用服务端 Hub 方法
- 公共连接、鉴权、重连逻辑由基类统一处理

## 4. 注册到依赖注入

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class ChatClientServiceCollectionExtensions
{
    public static IServiceCollection AddChatClient(
        this IServiceCollection services,
        Action<ChatClientOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IChatClient, ChatClient>();
        return services;
    }
}
```

## 5. 在宿主中启动

```csharp
using Microsoft.Extensions.Hosting;
using Pandax.SignalRClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthenticationProvider, JwtAuthenticationProvider>();
builder.Services.AddChatClient(options =>
{
    options.Url = builder.Configuration["SignalR:HubUrl"]
        ?? throw new InvalidOperationException("Missing configuration: SignalR:HubUrl");
});

builder.Services.AddHostedService<ChatWorker>();

await builder.Build().RunAsync();
```

## 后台服务示例

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class ChatWorker(IChatClient chatClient, ILogger<ChatWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        chatClient.OnMessageReceived += (user, message) =>
        {
            logger.LogInformation("收到来自 {User} 的消息: {Message}", user, message);
        };

        await chatClient.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await chatClient.SendMessageAsync("ConsoleApp", $"Heartbeat {DateTime.Now:O}");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

## 配置项说明

基础配置如下：

- `Url`：SignalR Hub 地址
- `RetryPolicy`：自定义重连策略

配置示例：

```json
{
  "SignalR": {
    "HubUrl": "https://localhost:5001/chatHub"
  }
}
```

## 自定义重连策略

库默认使用 `DefaultRetryPolicy`，即最多重试 5 次，每次间隔 5 秒。

如果你想替换成自己的策略，可以这样写：

```csharp
using Microsoft.AspNetCore.SignalR.Client;

public sealed class CustomRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        return retryContext.PreviousRetryCount < 10
            ? TimeSpan.FromSeconds(3)
            : null;
    }
}
```

然后在注册时指定：

```csharp
builder.Services.AddChatClient(options =>
{
    options.Url = builder.Configuration["SignalR:HubUrl"]!;
    options.RetryPolicy = new CustomRetryPolicy();
});
```

## 示例工程

- `SignalRClientSample`：客户端示例
- `SignalRServerSample`：服务端示例

你可以先启动服务端示例，再启动客户端示例，快速验证连接、收发消息和重连行为。

## 发布说明

仓库已配置 GitHub Actions 自动发布流程：

- 在 GitHub 上发布正式 Release 后自动触发
- 只发布 `SignalRClient` 库，并以 `Pandax.SignalRClient` 包名推送
- pre-release 不会推送
- 会同时推送 `.nupkg` 和 `.snupkg`

## 仓库信息

- 仓库地址：[github.com/9kbx/SignalRClient](https://github.com/9kbx/SignalRClient)
- 许可证：MIT
