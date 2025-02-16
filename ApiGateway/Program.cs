using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Yarp.ReverseProxy.Configuration;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using Yarp.ReverseProxy.Transforms; // ✅ Ensure you have this import
using Microsoft.IdentityModel.Logging;


var builder = WebApplication.CreateBuilder(args);
IdentityModelEventSource.ShowPII = true;
IdentityModelEventSource.LogCompleteSecurityArtifact = true;

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// JWT Settings from configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);
Console.WriteLine($"🔑 API Gateway JWT Key Length: {key.Length}, {key}, {jwtSettings["Key"]}");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    { // Auth Service URL
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],  // "InvestWise"
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],  // "InvestWiseUsers"
            ValidateLifetime = true,  // Ensures token is still valid
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"❌ Authentication Failed: {context.Exception}");
                if (context.Exception.InnerException != null)
                {
                    Console.WriteLine($"➡ Inner Exception: {context.Exception.InnerException}");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("✅ Token Validated Successfully");
                var jsonWebToken = context.SecurityToken as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;

                if (jsonWebToken != null)
                {
                    Console.WriteLine($"📜 Token Type: JsonWebToken");
                    foreach (var claim in jsonWebToken.Claims)
                    {
                        Console.WriteLine($"{claim.Type}: {claim.Value}");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ SecurityToken is not JsonWebToken, continuing...");
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

// Reverse Proxy Configuration with Authorization
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        [
            new RouteConfig
            {
                RouteId = "auth_service",
                ClusterId = "auth",
                Match = new RouteMatch { Path = "/api/auth/{**catch-all}" }
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
                    { "auth_backend", new DestinationConfig { Address = "http://auth-service:8080" } }
                }
            },
            new ClusterConfig
            {
                ClusterId = "dashboard",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    { "dashboard_backend", new DestinationConfig { Address = "http://dashboard-service:9000"} }
                }
            }
        ]
    )
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(async transformContext =>
        {
            var authHeader = transformContext.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader))
            {
                Console.WriteLine($"🔑 Forwarding Authorization Header: {authHeader}");
                transformContext.ProxyRequest.Headers.Remove("Authorization");
                transformContext.ProxyRequest.Headers.Add("Authorization", authHeader.ToString());
            }
        });
    });

// Enable Swagger for API Documentation
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InvestWise ApiGateway API",
        Version = "v1"
    });
});

builder.Services.AddControllers();
Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InvestWise ApiGateway v1");
    });
}

// Debug Incoming Request Headers
app.Use(async (context, next) =>
{
    Console.WriteLine("📥 Incoming Request:");
    Console.WriteLine($"➡ Path: {context.Request.Path}");
    Console.WriteLine($"➡ Method: {context.Request.Method}");

    foreach (var header in context.Request.Headers)
    {
        Console.WriteLine($"📝 Header: {header.Key} = {header.Value}");
    }

    await next();
});

// Enforce Authentication & Authorization Middleware
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapControllers();
app.Run();
