using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SignalRClient;

namespace SignalRClientSample;

public class JwtAuthenticationProvider(IHttpClientFactory httpClientFactory, IConfiguration config)
    : IAuthenticationProvider
{
    private string? _accessToken;
    private string? _refreshToken;
    private DateTime? _tokenExpiry;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string?> GetAccessTokenAsync()
    {
        // --- 1. 第一层检查（非锁定）：如果缓存有效，直接返回 ---
        if (IsTokenValid())
        {
            return _accessToken;
        }

        // --- 2. 尝试获取锁 ---
        await _lock.WaitAsync();
        try
        {
            // --- 3. 第二层检查（锁定后）：防止多个线程排队进入后重复请求 ---
            if (IsTokenValid())
            {
                return _accessToken;
            }

            if (!string.IsNullOrEmpty(_refreshToken))
            {
                try
                {
                    return await RefreshTokenAsync();
                }
                catch
                {
                    _refreshToken = null;
                }
            }

            return await LoginAsync();
        }
        finally
        {
            // --- 4. 释放锁 ---
            _lock.Release();
        }
    }

    private bool IsTokenValid()
    {
        // 预留 30 秒缓冲
        return !string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddSeconds(30);
    }

    private async Task<string> LoginAsync()
    {
        Console.WriteLine("Logging in to get tokens...");
        var client = httpClientFactory.CreateClient();
        var res = await client.PostAsJsonAsync($"{config["Auth:FetchTokenUrl"]}", new
        {
            username = config["Auth:Username"],
            password = config["Auth:Password"]
        });

        res.EnsureSuccessStatusCode();
        var data = await res.Content.ReadFromJsonAsync<JsonElement>();

        UpdateCache(data);
        return _accessToken!;
    }

    private async Task<string> RefreshTokenAsync()
    {
        Console.WriteLine("Refreshing access token...");
        var client = httpClientFactory.CreateClient();
        var res = await client.PostAsJsonAsync($"{config["Auth:RefreshTokenUrl"]}", new
        {
            refreshToken = _refreshToken
        });

        if (!res.IsSuccessStatusCode) throw new Exception("Refresh failed");

        var data = await res.Content.ReadFromJsonAsync<JsonElement>();
        UpdateCache(data);
        return _accessToken!;
    }

    private void UpdateCache(JsonElement data)
    {
        _accessToken = data.GetProperty("accessToken").GetString();
        _refreshToken = data.GetProperty("refreshToken").GetString();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(_accessToken);
        _tokenExpiry = jwtToken.ValidTo;
    }
}