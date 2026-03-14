using Microsoft.AspNetCore.SignalR.Client;

namespace SignalRClientSample;

public class RandomRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // 如果重试时间已经超过 1 小时，可能真的出大问题了，返回 null 停止
        if (retryContext.ElapsedTime > TimeSpan.FromHours(1)) return null;

        // 逻辑：前 10 次每 5 秒重试，之后每 30 秒重试
        return TimeSpan.FromSeconds(retryContext.PreviousRetryCount < 10 ? 5 : 30);
    }
}