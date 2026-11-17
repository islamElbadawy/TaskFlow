using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TaskFlow.Application;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Infrastructure;
using TaskFlow.API.Hubs;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
            }
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

// FluentValidation automatic model validation for controller input models
// Requires FluentValidation.AspNetCore package. Validators are registered in the Application layer.
builder.Services.AddFluentValidationAutoValidation();

// Add Infrastructure Layer (Database, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add SignalR
builder.Services.AddSignalR();
// Register real-time notifier implementation (uses NotificationHub)
builder.Services.AddSingleton<TaskFlow.Application.Common.Interfaces.IRealTimeNotifier, TaskFlow.API.Services.RealTimeNotifier>();

var app = builder.Build();

// Configure the HTTP request pipeline
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

// Development reset flow: drops and recreates the database, then seeds it.
// Opt-in only, and never outside Development, e.g.:
//   dotnet run -- --Seed:ResetDatabase=true
//   dotnet run -- --Seed:ResetDatabase=true --Seed:RunSmokeTestAndExit=true
var resetDatabase = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Seed:ResetDatabase");
if (app.Environment.IsDevelopment() && !resetDatabase)
{
    // Normal dev start: create the database if missing and seed it once (the seeder skips when users exist).
    // Replace with migrations (dotnet ef database update / a migration bundle) before deploying anywhere.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Data.Context.ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Data.Seeding.DataSeeder>().SeedAsync();
}
if (resetDatabase)
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetService<TaskFlow.Infrastructure.Data.Seeding.DataSeeder>();
    if (seeder != null)
    {
        // Reset database
        var db = scope.ServiceProvider.GetRequiredService<TaskFlow.Infrastructure.Data.Context.ApplicationDbContext>();
        db.Database.EnsureDeleted();
        // EnsureCreated is sufficient for development reset flow when migrations are not applied
        db.Database.EnsureCreated();
        await seeder.SeedAsync();
        // Save seed details to repo file
        var seedMd = System.IO.Path.Combine(System.AppContext.BaseDirectory, "SEEDS.md");
        var seedContent = "# Seeded Data\n\nAdmin: admin@taskflow.dev (Password: P@ssw0rd) - Id: 11111111-1111-1111-1111-111111111111\nManager: manager@taskflow.dev (Password: P@ssw0rd) - Id: 22222222-2222-2222-2222-222222222222\nUser: user@taskflow.dev (Password: P@ssw0rd) - Id: 33333333-3333-3333-3333-333333333333\n\nTasks:\n- Setup project: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\n- Write docs: bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\n";
        System.IO.File.WriteAllText(seedMd, seedContent);

        // Integration test: create a notification and verify persistence
        if (app.Configuration.GetValue<bool>("Seed:RunSmokeTestAndExit"))
        {
        try
        {
            var notifier = scope.ServiceProvider.GetRequiredService<TaskFlow.Application.Common.Interfaces.INotificationService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TaskFlow.Domain.Interfaces.IUnitOfWork>();
            var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            await notifier.CreateNotificationAsync(adminId, "Integration test notification", TaskFlow.Domain.Enums.NotificationType.TaskAssigned, null);

            var created = await unitOfWork.Notifications.FirstOrDefaultAsync(n => n.UserId == adminId);
            var testMd = System.IO.Path.Combine(System.AppContext.BaseDirectory, "SEED_TEST.md");
            if (created != null)
            {
                System.IO.File.WriteAllText(testMd, $"Notification created: Id={created.Id}, Message={created.Message}, UserId={created.UserId}");
                Console.WriteLine("Integration test succeeded: notification persisted.");
            }
            else
            {
                System.IO.File.WriteAllText(testMd, "Notification not found after creation.");
                Console.WriteLine("Integration test failed: notification not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Integration test error: {ex.Message}");
        }
        // Exit after running the quick integration test to avoid keeping the web host running during this check
        Environment.Exit(0);
        }
    }
}

app.Run();