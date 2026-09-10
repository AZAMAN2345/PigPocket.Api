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

builder.Services.AddControllers();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
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

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is missing.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
