using System.Security.Claims;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProjectDoomsdayServer.Application.Files;
using ProjectDoomsdayServer.Application.Ports.Repositories;
using ProjectDoomsdayServer.Domain.Configuration;
using ProjectDoomsdayServer.Infrastructure;
using ProjectDoomsdayServer.Infrastructure.Files;
using ProjectDoomsdayServer.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
builder.Services.AddSingleton<IFileRepository, MongoDbFileRepository>();
builder.Services.AddScoped<IFilesService, FilesService>();

#region AWS
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.Configure<S3Config>(builder.Configuration.GetSection("S3"));
#endregion AWS

builder.Services.Configure<MongoDbConfig>(builder.Configuration.GetSection("MongoDB"));

// Register infrastructure services (including S3FileStorage, etc.)
builder.Services.AddInfrastructureServices();

// To use S3FileStorage instead of LocalFileStorage, swap the following:
// builder.Services.AddSingleton<IFileStorage, S3FileStorage>();

builder.Services.AddExceptionHandler<FileNotFoundExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Project Doomsday Server API", Version = "v1" });
    c.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        }
    );
    c.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

var authEnabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");
if (authEnabled)
{
    builder
        .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = builder.Configuration["Authentication:Cognito:Authority"];
            o.Audience = builder.Configuration["Authentication:Cognito:Audience"];
            o.RequireHttpsMetadata = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = false, // Access tokens don't validate the audience, that's a frontend concern
                ValidateLifetime = true,
                // Cognito puts user ID in "sub"
                NameClaimType = "sub",
            };
            o.MapInboundClaims = false;
            var expectedClientId = builder.Configuration["Authentication:Cognito:Audience"];

            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tokenUse = context.Principal?.FindFirstValue("token_use");
                    if (tokenUse != "access")
                    {
                        context.Fail("Only access tokens are accepted.");
                        return Task.CompletedTask;
                    }

                    var clientId = context.Principal?.FindFirstValue("client_id");
                    if (clientId != expectedClientId)
                    {
                        context.Fail("Token was not issued for this app client.");
                        return Task.CompletedTask;
                    }

                    return Task.CompletedTask;
                },
            };
        });
}

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
