using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Microsoft.IdentityModel.Logging;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
    IdentityModelEventSource.LogCompleteSecurityArtifact = true;
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});


var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);
Console.WriteLine($"🔑 API Gateway JWT Key Length: {key.Length}");


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication Failure: {context.Exception.GetType().Name} - {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token Validation Succeeded");
                var jsonWebToken = context.SecurityToken as Microsoft.IdentityModel.JsonWebTokens.JsonWebToken;
                if (jsonWebToken != null)
                {
                    Console.WriteLine($"Validated Token Claims: [sub={jsonWebToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value}, iss={jsonWebToken.Issuer}, aud={jsonWebToken.Audiences.FirstOrDefault()}]");
                }
                return Task.CompletedTask;
            }
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
            var authHeaderExists = transformContext.HttpContext.Request.Headers.ContainsKey("Authorization");
            if (authHeaderExists)
            {
                Console.WriteLine("Forwarding Authorization Header");
            }
        });
    });


builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "InvestWise API Gateway",
        Version = "v1"
    });
});

builder.Services.AddControllers();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InvestWise API Gateway v1");
    });
}


app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

// Middleware pipeline setup
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapControllers();
app.Run();
