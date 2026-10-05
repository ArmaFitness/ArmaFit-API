using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<TrainerAthlete> TrainerAthletes => Set<TrainerAthlete>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<WorkoutLogSet> WorkoutLogSets => Set<WorkoutLogSet>();
}
