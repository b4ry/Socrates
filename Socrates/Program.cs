using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Socrates.Constants;
using Socrates.Hubs;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
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

builder.Services.AddSignalR();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>(builder.Configuration.GetSection(ConfigurationPropertyNames.HubPattern).Get<string>()!);

app.Run();