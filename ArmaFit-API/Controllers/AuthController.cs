using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

// No tokens or sessions: login just returns the user, and logout happens on the client (it forgets the user).
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db) : ControllerBase
{
    private static readonly PasswordHasher<User> Hasher = new();

    /// <summary>Register a new athlete or trainer.</summary>
    [HttpPost("register")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest req)
    {
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

    /// <summary>Log in with email and password. Returns the user.</summary>
    [HttpPost("login")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive ||
            Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            return Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        return UserDto.From(user);
    }
}
