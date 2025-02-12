using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = jwtSettings["Authority"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuerSigningKey = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            new RouteConfig
            {
                RouteId = "auth_service",
                ClusterId = "auth",
                Match = new RouteMatch {Path = "/api/auth/{**catch-all}"}
            },
            new RouteConfig
            {
                RouteId = "dashboard_service",
                ClusterId = "dashboard",
                Match = new RouteMatch { Path = "/api/dashboard/{**catch-all}" },
                AuthorizationPolicy = "Authenticated"
            }
        ],
        [
            new ClusterConfig
            {
                ClusterId = "auth",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    {"auth_backend", new DestinationConfig { Address = "http://localhost:5144" } }
                }
            },
            new ClusterConfig
            {
                ClusterId = "dashboard",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    {"dashboard_backend", new DestinationConfig { Address = "http://localhost:6000"} }
                }
            }
        ]
    );

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();