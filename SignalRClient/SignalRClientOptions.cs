using Microsoft.AspNetCore.SignalR.Client;

namespace SignalRClient;

public class SignalRClientOptions
{
    public string Url { get; set; } = string.Empty;

    // 允许用户传入自定义重试策略，如果不传则可以使用默认值
    public IRetryPolicy RetryPolicy { get; set; } = new DefaultRetryPolicy();
}