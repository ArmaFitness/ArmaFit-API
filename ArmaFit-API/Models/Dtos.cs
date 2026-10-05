using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ArmaFit_API.Data;

namespace ArmaFit_API.Models;

// ---------- Auth ----------

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required, MinLength(6)] string Password,
    [Required, MaxLength(100)] string FullName,
    UserRole Role = UserRole.Athlete,
    DateOnly? DateOfBirth = null,
    BiologicalSex? Sex = null,
    [Range(50, 250)] decimal? HeightCm = null,
    ActivityLevel? ActivityLevel = null);

public record LoginRequest([Required] string Email, [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

/// <param name="AccessToken">JWT for the Authorization: Bearer header. Carries the user's id (sub) and role.</param>
/// <param name="RefreshToken">Exchanged at auth/refresh for a new pair; each one works only once.</param>
/// <param name="User">The logged-in user.</param>
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(
    int Id, string Email, string FullName, UserRole Role,
    DateOnly? DateOfBirth, BiologicalSex? Sex, decimal? HeightCm, ActivityLevel? ActivityLevel, DateTime CreatedAt)
{
    public static UserDto From(User u) =>
        new(u.Id, u.Email, u.FullName, u.Role, u.DateOfBirth, u.Sex, u.HeightCm, u.ActivityLevel, u.CreatedAt);
}

// ---------- Invitations ----------

public record InvitationCreateRequest([Required, EmailAddress] string TrainerEmail);

public record InvitationDto(
    int Id, int TrainerId, string TrainerName, int AthleteId, string AthleteName,
    InvitationStatus Status, DateTime InvitedAt, DateTime? RespondedAt)
{
    [JsonPropertyName("_links")]
    public Dictionary<string, Link>? Links { get; init; }

    public static InvitationDto From(TrainerAthlete t) =>
        new(t.Id, t.TrainerId, t.Trainer!.FullName, t.AthleteId, t.Athlete!.FullName, t.Status, t.InvitedAt, t.RespondedAt);
}

// ---------- Exercises ----------

public record ExerciseDto(int Id, string Name, string? Description);

// ---------- Plans ----------

public record PlanCreateRequest(
    [Required, MaxLength(100)] string Name,
    [MaxLength(1000)] string? Description,
    [Range(1, int.MaxValue)] int AthleteId);

public record PlanUpdateRequest([Required, MaxLength(100)] string Name, [MaxLength(1000)] string? Description);

public record PlanDto(int Id, string Name, string? Description, int CreatedBy, int AthleteId, DateTime CreatedAt, DateTime UpdatedAt)
{
    [JsonPropertyName("_links")]
    public Dictionary<string, Link>? Links { get; init; }

    public static PlanDto From(WorkoutPlan p) =>
        new(p.Id, p.Name, p.Description, p.CreatedBy, p.AthleteId, p.CreatedAt, p.UpdatedAt);
}

// ---------- Workouts ----------

/// <param name="Name">Workout name, e.g. "Push".</param>
/// <param name="DayNumber">Day of the week: 1 = Monday ... 7 = Sunday.</param>
public record WorkoutRequest([Required, MaxLength(100)] string Name, [Range(1, 7)] int DayNumber);

public record WorkoutDto(int Id, int WorkoutPlanId, string Name, int DayNumber)
{
    [JsonPropertyName("_links")]
    public Dictionary<string, Link>? Links { get; init; }

    public static WorkoutDto From(Workout w) => new(w.Id, w.WorkoutPlanId, w.Name, w.DayNumber);
}

// ---------- Workout exercises ----------

public record WorkoutExerciseRequest(
    [Range(1, int.MaxValue)] int ExerciseId,
    [Range(1, 20)] int Sets,
    [Range(1, 100)] int? Reps,
    [Range(0, 1000)] decimal? WeightKg,
    [Range(0, 100)] int? OrderIndex);

public record WorkoutExerciseDto(
    int Id, int WorkoutId, int ExerciseId, string ExerciseName, int Sets, int? Reps, decimal? WeightKg, int? OrderIndex)
{
    [JsonPropertyName("_links")]
    public Dictionary<string, Link>? Links { get; init; }

    public static WorkoutExerciseDto From(WorkoutExercise e) =>
        new(e.Id, e.WorkoutId, e.ExerciseId, e.Exercise!.Name, e.Sets, e.Reps, e.WeightKg, e.OrderIndex);
}

// ---------- Workout logs & progress ----------

public record LogSetRequest(
    [Range(1, int.MaxValue)] int ExerciseId,
    [Range(1, 50)] int SetNumber,
    [Range(0, 1000)] decimal WeightKg,
    [Range(1, 200)] int Reps);

public record WorkoutLogCreateRequest(
    [Range(1, int.MaxValue)] int WorkoutId,
    [MaxLength(1000)] string? Notes,
    [Required, MinLength(1)] List<LogSetRequest> Sets);

public record LogSetDto(int ExerciseId, int SetNumber, decimal WeightKg, int Reps);

public record WorkoutLogDto(int Id, int UserId, int WorkoutId, DateTime LoggedAt, string? Notes, List<LogSetDto> Sets)
{
    public static WorkoutLogDto From(WorkoutLog l) =>
        new(l.Id, l.UserId, l.WorkoutId, l.LoggedAt, l.Notes,
            l.Sets.Select(s => new LogSetDto(s.ExerciseId, s.SetNumber, s.WeightKg, s.Reps)).ToList());
}

/// <param name="Date">When the session was logged.</param>
/// <param name="MaxWeightKg">Heaviest set of the session.</param>
/// <param name="VolumeKg">Sum of weight × reps over all sets of the session.</param>
public record ProgressPointDto(DateTime Date, decimal MaxWeightKg, decimal VolumeKg);

public record ProgressDto(int ExerciseId, string ExerciseName, List<ProgressPointDto> Points);
