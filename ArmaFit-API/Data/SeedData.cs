using Microsoft.AspNetCore.Identity;

namespace ArmaFit_API.Data;

public static class SeedData
{
    public const string DefaultPassword = "Password123!";

    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var now = DateTime.UtcNow;
        var hash = new PasswordHasher<User>().HashPassword(null!, DefaultPassword);

        User NewUser(string email, string name, UserRole role, BiologicalSex sex, DateOnly born, decimal height, ActivityLevel level) =>
            new() { Email = email, PasswordHash = hash, FullName = name, Role = role, Sex = sex, DateOfBirth = born, HeightCm = height, ActivityLevel = level, CreatedAt = now.AddDays(-90) };

        var tomas = NewUser("tomas@armafit.lt", "Tomas Petraitis", UserRole.Trainer, BiologicalSex.Male, new(1988, 3, 12), 182.5m, ActivityLevel.VeryActive);
        var rasa = NewUser("rasa@armafit.lt", "Rasa Kazlauskienė", UserRole.Trainer, BiologicalSex.Female, new(1991, 7, 4), 168m, ActivityLevel.VeryActive);
        var jonas = NewUser("jonas@armafit.lt", "Jonas Jonaitis", UserRole.Athlete, BiologicalSex.Male, new(2000, 5, 20), 180m, ActivityLevel.ModeratelyActive);
        var egle = NewUser("egle@armafit.lt", "Eglė Vaitkutė", UserRole.Athlete, BiologicalSex.Female, new(2002, 11, 2), 165m, ActivityLevel.LightlyActive);
        var lukas = NewUser("lukas@armafit.lt", "Lukas Stankevičius", UserRole.Athlete, BiologicalSex.Male, new(1998, 1, 15), 185.5m, ActivityLevel.VeryActive);
        var greta = NewUser("greta@armafit.lt", "Greta Paulauskaitė", UserRole.Athlete, BiologicalSex.Female, new(2001, 9, 30), 170m, ActivityLevel.ModeratelyActive);
        db.Users.AddRange(tomas, rasa, jonas, egle, lukas, greta);

        db.TrainerAthletes.AddRange(
            new TrainerAthlete { Trainer = tomas, Athlete = jonas, Status = InvitationStatus.Active, InvitedAt = now.AddDays(-60), RespondedAt = now.AddDays(-59) },
            new TrainerAthlete { Trainer = tomas, Athlete = egle, Status = InvitationStatus.Active, InvitedAt = now.AddDays(-45), RespondedAt = now.AddDays(-44) },
            new TrainerAthlete { Trainer = rasa, Athlete = lukas, Status = InvitationStatus.Active, InvitedAt = now.AddDays(-40), RespondedAt = now.AddDays(-40) },
            new TrainerAthlete { Trainer = rasa, Athlete = greta, Status = InvitationStatus.Pending, InvitedAt = now.AddDays(-2) },
            new TrainerAthlete { Trainer = tomas, Athlete = lukas, Status = InvitationStatus.Declined, InvitedAt = now.AddDays(-50), RespondedAt = now.AddDays(-48) },
            new TrainerAthlete { Trainer = rasa, Athlete = jonas, Status = InvitationStatus.Ended, InvitedAt = now.AddDays(-120), RespondedAt = now.AddDays(-119), EndedAt = now.AddDays(-65) });

        var bench = new Exercise { Name = "Bench Press", Description = "Barbell press lying on a flat bench." };
        var squat = new Exercise { Name = "Back Squat", Description = "Barbell squat with the bar on the upper back." };
        var deadlift = new Exercise { Name = "Deadlift", Description = "Lift the barbell from the floor to hip level." };
        var ohp = new Exercise { Name = "Overhead Press", Description = "Standing barbell press from shoulders to overhead." };
        var row = new Exercise { Name = "Barbell Row", Description = "Bent-over row pulling the bar to the lower chest." };
        var pullUp = new Exercise { Name = "Pull-up", Description = "Bodyweight pull from a dead hang until chin over the bar." };
        var rdl = new Exercise { Name = "Romanian Deadlift", Description = "Hip hinge with slightly bent knees, targets hamstrings." };
        var lunge = new Exercise { Name = "Dumbbell Lunge", Description = "Alternating forward lunges holding dumbbells." };
        db.Exercises.AddRange(bench, squat, deadlift, ohp, row, pullUp, rdl, lunge);

        WorkoutExercise Ex(Exercise e, int order, int sets, int? reps, decimal? kg) =>
            new() { Exercise = e, OrderIndex = order, Sets = sets, Reps = reps, WeightKg = kg };

        var push = new Workout { Name = "Push", DayNumber = 1, Exercises = [Ex(bench, 1, 4, 8, 60m), Ex(ohp, 2, 3, 10, 35m)] };
        var pull = new Workout { Name = "Pull", DayNumber = 3, Exercises = [Ex(row, 1, 4, 8, 50m), Ex(pullUp, 2, 3, 8, null)] };
        var legs = new Workout { Name = "Legs", DayNumber = 5, Exercises = [Ex(squat, 1, 4, 6, 80m), Ex(rdl, 2, 3, 10, 60m)] };
        var fullBody = new Workout { Name = "Full Body A", DayNumber = 2, Exercises = [Ex(squat, 1, 3, 10, 30m), Ex(bench, 2, 3, 10, 25m), Ex(row, 3, 3, 10, 20m)] };
        var heavy = new Workout { Name = "Heavy Day", DayNumber = 1, Exercises = [Ex(deadlift, 1, 5, 5, 140m), Ex(squat, 2, 5, 5, 120m)] };
        var upper = new Workout { Name = "Upper Body", DayNumber = 6, Exercises = [Ex(ohp, 1, 4, 6, 40m), Ex(pullUp, 2, 4, 6, null)] };
        var lower = new Workout { Name = "Lower Body", DayNumber = 4, Exercises = [Ex(lunge, 1, 3, 12, 10m), Ex(rdl, 2, 3, 10, 30m)] };

        db.WorkoutPlans.AddRange(
            new WorkoutPlan { Name = "Push Pull Legs", Description = "Three-day split focused on building strength.", Creator = tomas, Athlete = jonas, CreatedAt = now.AddDays(-58), UpdatedAt = now.AddDays(-58), Workouts = [push, pull, legs] },
            new WorkoutPlan { Name = "Beginner Full Body", Description = "Light full body routine to learn the main lifts.", Creator = tomas, Athlete = egle, CreatedAt = now.AddDays(-43), UpdatedAt = now.AddDays(-43), Workouts = [fullBody] },
            new WorkoutPlan { Name = "Strength Block", Description = "Heavy low-rep block for deadlift and squat.", Creator = rasa, Athlete = lukas, CreatedAt = now.AddDays(-39), UpdatedAt = now.AddDays(-30), Workouts = [heavy] },
            new WorkoutPlan { Name = "Weekend Upper", Description = "Extra upper body session on Saturdays.", Creator = jonas, Athlete = jonas, CreatedAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20), Workouts = [upper] },
            new WorkoutPlan { Name = "Home Strength", Description = "Lower body workout with dumbbells at home.", Creator = greta, Athlete = greta, CreatedAt = now.AddDays(-10), UpdatedAt = now.AddDays(-10), Workouts = [lower] });

        WorkoutLogSet Set(Exercise e, int number, decimal kg, int reps) =>
            new() { Exercise = e, SetNumber = number, WeightKg = kg, Reps = reps };

        db.WorkoutLogs.AddRange(
            new WorkoutLog { User = jonas, Workout = push, LoggedAt = now.AddDays(-21), Notes = "First session, bench felt fine.", Sets = [Set(bench, 1, 60m, 8), Set(bench, 2, 60m, 7), Set(ohp, 1, 35m, 10)] },
            new WorkoutLog { User = jonas, Workout = push, LoggedAt = now.AddDays(-14), Notes = "Added 2.5 kg to bench.", Sets = [Set(bench, 1, 62.5m, 8), Set(bench, 2, 62.5m, 8), Set(ohp, 1, 37.5m, 9)] },
            new WorkoutLog { User = jonas, Workout = push, LoggedAt = now.AddDays(-7), Notes = "New bench PR.", Sets = [Set(bench, 1, 65m, 8), Set(bench, 2, 65m, 6), Set(ohp, 1, 37.5m, 10)] },
            new WorkoutLog { User = egle, Workout = fullBody, LoggedAt = now.AddDays(-5), Notes = "Focused on squat depth.", Sets = [Set(squat, 1, 30m, 10), Set(squat, 2, 30m, 10)] },
            new WorkoutLog { User = lukas, Workout = heavy, LoggedAt = now.AddDays(-3), Notes = "Grip started failing on the last deadlift set.", Sets = [Set(deadlift, 1, 140m, 5), Set(deadlift, 2, 145m, 4)] });

        db.SaveChanges();
    }
}
