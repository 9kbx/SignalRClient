using Microsoft.Extensions.DependencyInjection;
using SignalRClientSample.HubClients;

namespace SignalRClientSample;

// 封装一个扩展方法，让注册过程变得极其简单
public static class PandaxSignalRClientExtensions
{
    public static IServiceCollection AddChatClient(
        this IServiceCollection services,
        Action<ChatClientOptions> configure
    )
    {
        services.Configure(configure);
        services.AddSingleton<IChatClient, PandaxSignalRChatClient>();
        return services;
    }

    // public static IServiceCollection AddOrderClient(this IServiceCollection services,
    //     Action<OrderClientOptions> configure)
    // {
    //     services.Configure(configure);
    //     services.AddSingleton<IOrderClient, SignalROrderClient>();
    //     return services;
    // }
}
