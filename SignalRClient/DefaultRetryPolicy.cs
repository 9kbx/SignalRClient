using Microsoft.AspNetCore.SignalR.Client;

namespace Pandax.SignalRClient;

// 提供一个简单的默认策略，避免空指针
public class DefaultRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount < 5 ? TimeSpan.FromSeconds(5) : null;
}
