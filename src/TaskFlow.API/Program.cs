using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TaskFlow.Application;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Infrastructure;
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
var key = Encoding.ASCII.GetBytes(jwtSettings.Get<JwtSettings>()?.Secret ?? "");
builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
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

// Add Application Layer (CQRS)
builder.Services.AddApplication();

// FluentValidation automatic model validation for controller input models
// Requires FluentValidation.AspNetCore package. Validators are registered in the Application layer.
builder.Services.AddFluentValidationAutoValidation();

// Add Infrastructure Layer (Database, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

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

// Ensure database is created and seed data (development reset flow)
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
    }
}

app.Run();