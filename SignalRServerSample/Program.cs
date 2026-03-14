using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SignalRServerSample.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// JWT 配置
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;
var accessTokenExpiry = int.Parse(builder.Configuration["Jwt:AccessTokenExpiryMinutes"] ?? "30");
var refreshTokenExpiry = int.Parse(builder.Configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT auth failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// 内存中的 refresh token 存储
var refreshTokenStore = new ConcurrentDictionary<string, (string Username, DateTime Expiry)>();

string GenerateAccessToken(string username)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var claims = new[] { new Claim(ClaimTypes.Name, username) };
    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(accessTokenExpiry),
        signingCredentials: creds
    );
    return new JwtSecurityTokenHandler().WriteToken(token);
}

string GenerateRefreshToken(string username)
{
    var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    refreshTokenStore[token] = (username, DateTime.UtcNow.AddDays(refreshTokenExpiry));
    return token;
}

// 登录接口
app.MapPost(
        "/api/v1/auth/login",
        (LoginRequest req) =>
        {
            Console.WriteLine($"login attempt: {req.Username}");
            var users = app.Configuration.GetSection("Users").Get<UserConfig[]>() ?? [];
            var user = users.FirstOrDefault(u =>
                u.Username == req.Username && u.Password == req.Password
            );

            if (user is null)
                return Results.Unauthorized();

            var accessToken = GenerateAccessToken(req.Username);
            var refreshToken = GenerateRefreshToken(req.Username);
            return Results.Ok(new { accessToken, refreshToken });
        }
    )
    .AllowAnonymous();

// 刷新 Token 接口
app.MapPost(
        "/api/v1/auth/refresh",
        (RefreshRequest req) =>
        {
            Console.WriteLine($"refresh attempt: {req.RefreshToken}");

            if (
                !refreshTokenStore.TryGetValue(req.RefreshToken, out var data)
                || data.Expiry < DateTime.UtcNow
            )
            {
                refreshTokenStore.TryRemove(req.RefreshToken, out _);
                return Results.Unauthorized();
            }

            refreshTokenStore.TryRemove(req.RefreshToken, out _);
            var accessToken = GenerateAccessToken(data.Username);
            var refreshToken = GenerateRefreshToken(data.Username);
            return Results.Ok(new { accessToken, refreshToken });
        }
    )
    .AllowAnonymous();

// SignalR Hub
app.MapHub<ChatHub>("/chat");

app.Run();

record LoginRequest(string Username, string Password);

record RefreshRequest(string RefreshToken);

record UserConfig(string Username, string Password);
