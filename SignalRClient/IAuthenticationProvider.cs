namespace Pandax.SignalRClient;

public interface IAuthenticationProvider
{
    Task<string?> GetAccessTokenAsync();
}
