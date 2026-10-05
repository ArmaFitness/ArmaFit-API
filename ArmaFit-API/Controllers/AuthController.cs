using System.Security.Cryptography;
using System.Text;
using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ArmaFit_API.Controllers;

// Login opens a session: a short-lived JWT access token plus a refresh token that is replaced on every refresh.
// The access token carries the session id, so logging out (revoking the session) invalidates both at once.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(AppDbContext db, SigningCredentials signing) : ControllerBase
{
    // The API issues tokens for itself, so this is both the issuer and the audience.
    public const string Issuer = "armafit-api";

    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>Register a new athlete or trainer.</summary>
    [HttpPost("register")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req)
    {
        if (req.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(req.DateOfBirth), "Date of birth cannot be in the future.");
            return ValidationProblem();
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Problem($"Email '{email}' is already registered.", statusCode: StatusCodes.Status409Conflict);

        var user = new User
        {
            Email = email,
            FullName = req.FullName,
            Role = req.Role,
            DateOfBirth = req.DateOfBirth,
            Sex = req.Sex,
            HeightCm = req.HeightCm,
            ActivityLevel = req.ActivityLevel ?? ActivityLevel.ModeratelyActive,
        };
        user.PasswordHash = Hasher.HashPassword(user, req.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, UserDto.From(user));
    }

    /// <summary>Log in with email and password. Returns an access token, a refresh token and the user.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive ||
            Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            return Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        var session = new Session { UserId = user.Id };
        db.Sessions.Add(session);
        return await IssueTokens(session, user);
    }

    /// <summary>Exchange a refresh token for a new access token and a new refresh token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var session = await FindSession(req.RefreshToken);
        if (session == null || !session.User!.IsActive)
            return Problem("Invalid or expired refresh token.", statusCode: StatusCodes.Status401Unauthorized);

        return await IssueTokens(session, session.User);
    }

    /// <summary>Log out: the session's access and refresh tokens stop working immediately.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        var session = await FindSession(req.RefreshToken);
        if (session != null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    // Gives the session a new refresh token (the previous one stops working) and signs an access token for it.
    private async Task<AuthResponse> IssueTokens(Session session, User user)
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        session.RefreshTokenHash = Hash(refreshToken);
        session.ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);
        await db.SaveChangesAsync();

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            Expires = DateTime.UtcNow.Add(AccessTokenLifetime),
            SigningCredentials = signing,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["role"] = user.Role.ToString(),
                ["sid"] = session.Id.ToString(),
            },
        });

        return new(accessToken, refreshToken, UserDto.From(user));
    }

    private Task<Session?> FindSession(string refreshToken)
    {
        var hash = Hash(refreshToken);
        return db.Sessions.Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow);
    }

    // Only the hash is stored, so a leaked database does not hand out usable refresh tokens.
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
