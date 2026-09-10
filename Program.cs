using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PigPocket.Api.Configuration;
using PigPocket.Api.Services;
using PigPocket.Api.Services.Banking;
using PigPocket.Api.Services.FinanceEngine;
using PigPocket.Api.Services.Analytics;
using System.Text;

BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true).AddEnvironmentVariables();

builder.Services.AddControllers();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient<AuthEmailService>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:8081", "http://localhost:8082", "http://localhost:19006"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<KycService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<MerchantService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<BankingService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<SavingsGoalService>();
builder.Services.AddScoped<SavingsService>();
builder.Services.AddScoped<PeriodService>();
builder.Services.AddScoped<SavingsCalculator>();
builder.Services.AddScoped<SpendingCalculator>();
builder.Services.AddScoped<ProjectionCalculator>();
builder.Services.AddScoped<PeriodComparisonService>();
builder.Services.AddScoped<TrendService>();
builder.Services.AddScoped<CategoryAnalyticsService>();
builder.Services.AddScoped<MerchantAnalyticsService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<WrappedService>();

var monoSettings = builder.Configuration
    .GetRequiredSection("Mono")
    .Get<MonoSettings>();

if (monoSettings is null)
{
    throw new InvalidOperationException("Mono configuration is missing.");
}

if (string.IsNullOrWhiteSpace(monoSettings.BaseUrl))
{
    throw new InvalidOperationException("Mono:BaseUrl is missing.");
}

builder.Services.AddSingleton(monoSettings);

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32 || jwtKey.StartsWith("replace-"))
{
    throw new InvalidOperationException("Set Jwt:Key to a random secret of at least 32 bytes in local configuration or environment variables.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var sid = context.Principal?.FindFirst("sid")?.Value;
                if (!Guid.TryParse(id, out var userId) || sid is null ||
                    !await context.HttpContext.RequestServices.GetRequiredService<AuthService>().IsSessionActive(userId, sid))
                    context.Fail("Session has been revoked.");
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<MonoKycService>(client =>
{
    client.BaseAddress = new Uri(monoSettings.BaseUrl);
});

builder.Services.AddHttpClient<MonoService>(client =>
{
    client.BaseAddress = new Uri(monoSettings.BaseUrl);
});

var mongoDbSettings = builder.Configuration
    .GetRequiredSection("MongoDb")
    .Get<MongoDbSettings>();

if (mongoDbSettings is null)
{
    throw new InvalidOperationException("MongoDb configuration is missing.");
}

if (string.IsNullOrWhiteSpace(mongoDbSettings.ConnectionString))
{
    throw new InvalidOperationException("MongoDb:ConnectionString is missing.");
}

if (string.IsNullOrWhiteSpace(mongoDbSettings.DatabaseName))
{
    throw new InvalidOperationException("MongoDb:DatabaseName is missing.");
}

builder.Services.AddSingleton(mongoDbSettings);

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(mongoDbSettings.ConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(provider =>
{
    var client = provider.GetRequiredService<IMongoClient>();

    return client.GetDatabase(mongoDbSettings.DatabaseName);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthService>().EnsureIndexesAsync();
    var categoryService = scope.ServiceProvider.GetRequiredService<CategoryService>();
    await categoryService.EnsureDefaultCategoriesAsync();

    await scope.ServiceProvider
        .GetRequiredService<TransactionService>()
        .EnsureIndexesAsync();

    await scope.ServiceProvider
        .GetRequiredService<BankingService>()
        .EnsureIndexesAsync();

    await scope.ServiceProvider
        .GetRequiredService<BudgetService>()
        .EnsureIndexesAsync();

    await scope.ServiceProvider
        .GetRequiredService<SavingsGoalService>()
        .EnsureIndexesAsync();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (AuthException ex)
    {
        context.Response.StatusCode = ex.Status;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message, code = ex.Code });
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
