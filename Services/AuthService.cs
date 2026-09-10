using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Auth;
using PigPocket.Api.Models;

namespace PigPocket.Api.Services;

public class AuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly JwtService _jwtService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthService(
        IMongoDatabase database,
        JwtService jwtService)
    {
        _users = database.GetCollection<User>("Users");
        _jwtService = jwtService;
    }

    public async Task<AuthResponseDto?> Register(SignUpDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var existingUser = await _users
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync();

        if (existingUser is not null)
        {
            return null;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

        await _users.InsertOneAsync(user);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponseDto?> Login(LoginDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _users
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return null;
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return CreateAuthResponse(user);
    }

    private AuthResponseDto CreateAuthResponse(User user)
    {
        return new AuthResponseDto
        {
            Token = _jwtService.GenerateToken(user),
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }
}
