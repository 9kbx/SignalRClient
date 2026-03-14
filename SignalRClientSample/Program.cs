using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SignalRClient;
using SignalRClientSample;

var builder = Host.CreateApplicationBuilder(args);

// 1. 注册基础组件
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAuthenticationProvider, JwtAuthenticationProvider>();

// 2. 注册 Chat 客户端为单例
builder.Services.AddChatClient(o =>
{
    o.Url =
        builder.Configuration["SignalR:HubUrl"]
        ?? throw new InvalidOperationException("Missing configuration: SignalR:HubUrl");
    o.RetryPolicy = new RandomRetryPolicy();
});

// 3. 注册一个后台任务来运行客户端
builder.Services.AddHostedService<ChatWorker>();

var host = builder.Build();
await host.RunAsync();
