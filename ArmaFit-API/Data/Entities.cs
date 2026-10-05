using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Data;

public enum UserRole { Trainer, Athlete }

public enum BiologicalSex { Male, Female, Other }

public enum ActivityLevel { Sedentary, LightlyActive, ModeratelyActive, VeryActive, ExtremelyActive }

public enum InvitationStatus { Pending, Active, Declined, Ended }

[Index(nameof(Email), IsUnique = true)]
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Athlete;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateOnly? DateOfBirth { get; set; }
    public BiologicalSex? Sex { get; set; }
    [Precision(5, 2)] public decimal? HeightCm { get; set; }
    public ActivityLevel? ActivityLevel { get; set; } = Data.ActivityLevel.ModeratelyActive;
}

// One row per login. The access token carries the session id, so revoking the row invalidates both tokens.
[Index(nameof(RefreshTokenHash), IsUnique = true)]
public class Session
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

[Index(nameof(TrainerId), nameof(AthleteId), IsUnique = true)]
public class TrainerAthlete
{
    public int Id { get; set; }
    public int TrainerId { get; set; }
    public User? Trainer { get; set; }
    public int AthleteId { get; set; }
    public User? Athlete { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class Exercise
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public class WorkoutPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int CreatedBy { get; set; }
    [ForeignKey(nameof(CreatedBy))] public User? Creator { get; set; }
    public int AthleteId { get; set; }
    public User? Athlete { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<Workout> Workouts { get; set; } = [];
}

[Index(nameof(WorkoutPlanId), nameof(DayNumber))]
public class Workout
{
    public int Id { get; set; }
    public int WorkoutPlanId { get; set; }
    public WorkoutPlan? WorkoutPlan { get; set; }
    public string Name { get; set; } = "";
    public int DayNumber { get; set; }
    public List<WorkoutExercise> Exercises { get; set; } = [];
}

[Index(nameof(WorkoutId), nameof(OrderIndex))]
public class WorkoutExercise
{
    public int Id { get; set; }
    public int WorkoutId { get; set; }
    public Workout? Workout { get; set; }
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }
    public int Sets { get; set; }
    public int? Reps { get; set; }
    [Precision(6, 2)] public decimal? WeightKg { get; set; }
    public int? OrderIndex { get; set; }
}

public class WorkoutLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int WorkoutId { get; set; }
    public Workout? Workout { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<WorkoutLogSet> Sets { get; set; } = [];
}

[Index(nameof(WorkoutLogId), nameof(ExerciseId), nameof(SetNumber), IsUnique = true)]
public class WorkoutLogSet
{
    public int Id { get; set; }
    public int WorkoutLogId { get; set; }
    public WorkoutLog? WorkoutLog { get; set; }
    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }
    public int SetNumber { get; set; }
    [Precision(6, 2)] public decimal WeightKg { get; set; }
    public int Reps { get; set; }
}
