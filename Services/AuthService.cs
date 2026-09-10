using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Auth;
using PigPocket.Api.Models;
using System.Security.Cryptography;
using System.Text;

namespace PigPocket.Api.Services;

public class AuthService(IMongoDatabase database, JwtService jwt, AuthEmailService emailService, IConfiguration configuration)
{
    private readonly IMongoCollection<User> users = database.GetCollection<User>("Users");
    private readonly PasswordHasher<User> hasher = new();
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string CodeHash(Guid id, string purpose, string code) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!), Encoding.UTF8.GetBytes($"{id}:{purpose}:{code}")));
    private static AuthException InvalidCode() => new(400, "invalid_code", "The code is incorrect, expired, or has reached its attempt limit. Request a new code.");
    public Task EnsureIndexesAsync() => users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
        Builders<User>.IndexKeys.Ascending(u => u.Email), new CreateIndexOptions { Unique = true }));

    public async Task<object> Register(SignUpDTO dto)
    {
        var user = new User { Id = Guid.NewGuid(), Email = Normalize(dto.Email), FirstName = dto.FirstName.Trim(), LastName = dto.LastName.Trim() };
        user.PasswordHash = hasher.HashPassword(user, dto.Password);
        try { await users.InsertOneAsync(user); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { throw new AuthException(409, "account_exists", "An account with this email already exists. Log in or resend your verification code."); }
        await SendCode(user.Email, false);
        return new { email = user.Email, requiresEmailVerification = true, expiresIn = 600, resendAfter = 45 };
    }

    public async Task SendCode(string email, bool reset)
    {
        var user = await users.Find(u => u.Email == Normalize(email)).FirstOrDefaultAsync();
        if (user is null || (!reset && user.IsEmailVerified)) return;
        var field = reset ? "PasswordReset" : "Verification";
        var now = DateTime.UtcNow;
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        var challenge = new EmailChallenge { Hash = CodeHash(user.Id, field, code), SentAt = now, ExpiresAt = now.AddMinutes(10) };
        var f = Builders<User>.Filter;
        var filter = f.Eq(u => u.Id, user.Id) & (f.Eq(field, (EmailChallenge?)null) | f.Lte(field + ".SentAt", now.AddSeconds(-45)));
        if (!reset) filter &= f.Eq(u => u.IsEmailVerified, false);
        var result = await users.UpdateOneAsync(filter, Builders<User>.Update.Set(field, challenge));
        if (result.ModifiedCount == 0) return; // Same generic response for cooldown and unknown accounts.
        try { await emailService.SendCode(user.Email, code, reset); }
        catch
        {
            await users.UpdateOneAsync(f.Eq(u => u.Id, user.Id) & f.Eq(field + ".Hash", challenge.Hash), Builders<User>.Update.Set(field, (EmailChallenge?)null));
            throw;
        }
    }

    private async Task<(User User, FilterDefinition<User> Filter)> CheckCode(VerifyEmailDTO dto, bool reset)
    {
        var field = reset ? "PasswordReset" : "Verification";
        var f = Builders<User>.Filter;
        var filter = f.Eq(u => u.Email, Normalize(dto.Email)) & f.Gt(field + ".ExpiresAt", DateTime.UtcNow) & f.Lt(field + ".Attempts", 5);
        if (!reset) filter &= f.Eq(u => u.IsEmailVerified, false);
        var user = await users.FindOneAndUpdateAsync(filter, Builders<User>.Update.Inc(field + ".Attempts", 1),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After });
        if (user is null) throw InvalidCode();
        var challenge = (reset ? user.PasswordReset : user.Verification)!;
        var expected = CodeHash(user.Id, field, dto.Code);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(challenge.Hash))) throw InvalidCode();
        return (user, f.Eq(u => u.Id, user.Id) & f.Eq(field + ".Hash", challenge.Hash) & f.Lte(field + ".Attempts", 5) & f.Gt(field + ".ExpiresAt", DateTime.UtcNow));
    }

    private (AuthSession Session, string Refresh) NewSession()
    {
        var refresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        return (new AuthSession { Id = Guid.NewGuid().ToString(), RefreshHash = Hash(refresh), ExpiresAt = DateTime.UtcNow.AddDays(30) }, refresh);
    }
    private AuthResponseDto Response(User user, AuthSession session, string refresh) => new() {
        Token = jwt.GenerateToken(user, session.Id), RefreshToken = refresh,
        ExpiresIn = configuration.GetValue<int>("Jwt:ExpiresInMinutes") * 60,
        UserId = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName
    };

    public async Task<AuthResponseDto> VerifyEmail(VerifyEmailDTO dto)
    {
        var (user, filter) = await CheckCode(dto, false);
        var (session, refresh) = NewSession();
        var result = await users.UpdateOneAsync(filter & Builders<User>.Filter.Eq(u => u.IsEmailVerified, false),
            Builders<User>.Update.Set(u => u.IsEmailVerified, true).Set(u => u.Verification, null).Push(u => u.Sessions, session));
        if (result.ModifiedCount == 0) throw InvalidCode();
        return Response(user, session, refresh);
    }
    public async Task<AuthResponseDto> Login(LoginDTO dto)
    {
        var user = await users.Find(u => u.Email == Normalize(dto.Email)).FirstOrDefaultAsync();
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            throw new AuthException(401, "invalid_credentials", "Invalid email or password.");
        if (!user.IsEmailVerified) throw new AuthException(403, "email_not_verified", "Verify your email before logging in.");
        var (session, refresh) = NewSession();
        var result = await users.UpdateOneAsync(u => u.Id == user.Id && u.PasswordHash == user.PasswordHash,
            Builders<User>.Update.Push(u => u.Sessions, session));
        if (result.ModifiedCount == 0) throw new AuthException(401, "invalid_credentials", "Please log in again.");
        return Response(user, session, refresh);
    }
    public async Task ResetPassword(ResetPasswordDTO dto)
    {
        var (user, filter) = await CheckCode(dto, true);
        var result = await users.UpdateOneAsync(filter, Builders<User>.Update
            .Set(u => u.PasswordHash, hasher.HashPassword(user, dto.NewPassword))
            .Set(u => u.PasswordReset, null).Set(u => u.Verification, null).Set(u => u.Sessions, new List<AuthSession>()));
        if (result.ModifiedCount == 0) throw InvalidCode();
    }
    public async Task<AuthResponseDto> Refresh(string token)
    {
        var hash = Hash(token);
        var f = Builders<User>.Filter;
        var filter = f.Eq(u => u.IsEmailVerified, true) & f.ElemMatch(u => u.Sessions, s => s.RefreshHash == hash && s.ExpiresAt > DateTime.UtcNow);
        var refresh = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
        var user = await users.FindOneAndUpdateAsync(filter, Builders<User>.Update.Set("Sessions.$.RefreshHash", Hash(refresh)));
        if (user is null) throw new AuthException(401, "invalid_session", "Your session expired. Please log in again.");
        return Response(user, user.Sessions.Single(s => s.RefreshHash == hash), refresh);
    }
    public Task Logout(Guid id, string sid) => users.UpdateOneAsync(u => u.Id == id,
        Builders<User>.Update.PullFilter(u => u.Sessions, s => s.Id == sid));
    public async Task<bool> IsSessionActive(Guid id, string sid) => await users.Find(
        Builders<User>.Filter.Eq(u => u.Id, id) & Builders<User>.Filter.Eq(u => u.IsEmailVerified, true) &
        Builders<User>.Filter.ElemMatch(u => u.Sessions, s => s.Id == sid && s.ExpiresAt > DateTime.UtcNow)).AnyAsync();
}
