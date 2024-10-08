using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Socrates.Constants;
using Socrates.Encryption;
using Socrates.Encryption.Interfaces;
using Socrates.Hubs;
using Socrates.Services;
using StackExchange.Redis;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

var jwtIssuer = builder.Configuration.GetSection(ConfigurationPropertyNames.JwtIssuer).Get<string>();
var jwtKey = builder.Configuration.GetSection(ConfigurationPropertyNames.JwtKey).Get<string>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!)),
        NameClaimType = ClaimTypes.NameIdentifier // to ensure User.Identity.Name is not null
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Request.Headers.TryGetValue("Authorization", out StringValues accessToken);

            if (!string.IsNullOrEmpty(accessToken))
            { 
                var token = Regex.Match(accessToken.First()!,
                    builder.Configuration.GetSection(ConfigurationPropertyNames.RegexBearerTokenPattern).Get<string>()!).Value;

                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSignalR()
    .AddMessagePackProtocol()
    .AddStackExchangeRedis("127.0.0.1:6379", o =>
    {
        o.ConnectionFactory = async writer =>
        {
            var config = new ConfigurationOptions
            {
                AbortOnConnectFail = false
            };
            config.EndPoints.Add(IPAddress.Loopback, 0);
            config.SetDefaultPorts();
    
            var connection = await ConnectionMultiplexer.ConnectAsync(config, writer);
    
            connection.ConnectionFailed += (_, e) =>
            {
                Console.WriteLine("Connection to Redis failed.");
            };
    
            if (!connection.IsConnected)
            {
                Console.WriteLine("Did not connect to Redis.");
            }
            else
            {
                Console.Write($"Successfully connected to Redis: {connection.Configuration} as {connection.ClientName}.");
            }
    
            return connection;
        };
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("127.0.0.1:6379"));
builder.Services.AddSingleton<IAssymmetricEncryption, RSAEncryption>();
builder.Services.AddScoped<Socrates.Services.ILogger, LoggerAdapter<ChatHub>>();
builder.Services.AddScoped<ISymmetricEncryption, AESEncryption>();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>(builder.Configuration.GetSection(ConfigurationPropertyNames.HubPattern).Get<string>()!);

app.Run();