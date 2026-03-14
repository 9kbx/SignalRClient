namespace SignalRClient;

public interface IAuthenticationProvider
{
    Task<string?> GetAccessTokenAsync();
}