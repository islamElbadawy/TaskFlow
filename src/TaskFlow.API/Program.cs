using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Application;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Common;
using TaskFlow.API.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Requests MVC can't bind (malformed JSON, wrong types, missing required fields) get the same shape as validator
    // failures: 400 ValidationProblemDetails with title "Validation" and an errors object per field.
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest,
                title: "Validation", detail: "One or more validation errors occurred.");
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});
builder.Services.AddEndpointsApiExplorer();

// Errors: expected failures become ProblemDetails in the controllers (ResultExtensions), unexpected exceptions
// are logged and returned as a 500 ProblemDetails, and empty 4xx responses (401/403/404) get a ProblemDetails body.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSwaggerGen();

// Configure JWT Settings
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettings);

// Add JWT Authentication
// The signing secret is NOT stored in appsettings.json. Provide it via user-secrets in development
// (dotnet user-secrets set "JwtSettings:Secret" "<random 32+ byte value>") or the
// JwtSettings__Secret environment variable / a vault in other environments.
var key = Encoding.UTF8.GetBytes(jwtSettings.Get<JwtSettings>()?.Secret ?? "");
if (key.Length < 32)
    throw new InvalidOperationException(
        "JwtSettings:Secret is missing or shorter than 32 bytes. Set it with user-secrets or an environment variable.");
builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Get<JwtSettings>()?.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Get<JwtSettings>()?.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            // Deactivated or deleted users are rejected even with an unexpired token (one PK lookup per request).
            OnTokenValidated = ActiveUserTokenCheck.ValidateAsync
        };
    });

builder.Services.AddAuthorization();


// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddApplication();

// Add Infrastructure Layer (Database, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add SignalR
builder.Services.AddSignalR();
// Register real-time notifier implementation (uses NotificationHub)
builder.Services.AddSingleton<TaskFlow.Application.Common.Interfaces.IRealTimeNotifier, TaskFlow.API.Services.RealTimeNotifier>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Development database setup. Migrations are applied and the seed data inserted on every Development start
// (the seeder only adds records that are missing). Other environments apply migrations explicitly
// (dotnet ef database update / a migration bundle) and are never seeded.
// Opt-in reset, Development only: drops the database, re-applies migrations and reseeds, e.g.
//   dotnet run -- --Seed:ResetDatabase=true
//   dotnet run -- --Seed:ResetDatabase=true --Seed:RunSmokeTestAndExit=true
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Data.Context.ApplicationDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Data.Seeding.DataSeeder>();

    var resetDatabase = app.Configuration.GetValue<bool>("Seed:ResetDatabase");
    if (resetDatabase)
        await db.Database.EnsureDeletedAsync();

    await db.Database.MigrateAsync();
    await seeder.SeedAsync();

    // Quick end-to-end check after a reset: create a notification through the service and read it back.
    if (resetDatabase && app.Configuration.GetValue<bool>("Seed:RunSmokeTestAndExit"))
    {
        var succeeded = false;
        try
        {
            var notifier = scope.ServiceProvider.GetRequiredService<TaskFlow.Application.Common.Interfaces.INotificationService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TaskFlow.Domain.Interfaces.IUnitOfWork>();
            var adminId = TaskFlow.Infrastructure.Data.Seeding.DataSeeder.UserIds.Admin;
            var message = $"Integration test notification {Guid.NewGuid():N}";
            await notifier.CreateNotificationAsync(adminId, message, TaskFlow.Domain.Enums.NotificationType.TaskAssigned, null);

            var created = await unitOfWork.Notifications.FirstOrDefaultAsync(n => n.UserId == adminId && n.Message == message);
            succeeded = created != null;
            Console.WriteLine(succeeded
                ? $"Integration test succeeded: notification {created!.Id} persisted."
                : "Integration test failed: notification not found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Integration test error: {ex.Message}");
        }
        // Exit instead of keeping the web host running during this check.
        Environment.Exit(succeeded ? 0 : 1);
    }
}

app.Run();
// Makes the implicit Program class visible to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }
