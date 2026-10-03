using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/workout-logs")]
public class WorkoutLogsController(AppDbContext db) : ControllerBase
{
    /// <summary>Log a completed workout from the athlete's plan, with weight and reps per set.</summary>
    [HttpPost]
    [ProducesResponseType<WorkoutLogDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkoutLogDto>> Create(WorkoutLogCreateRequest req)
    {
        if (req.Sets.Any(s => s is null))
        {
            ModelState.AddModelError(nameof(req.Sets), "Sets must not contain null entries.");
            return ValidationProblem();
        }

        var workout = await db.Workouts.Include(w => w.WorkoutPlan).Include(w => w.Exercises)
            .FirstOrDefaultAsync(w => w.Id == req.WorkoutId);
        if (workout == null)
            return Problem($"Workout {req.WorkoutId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);
        if (workout.WorkoutPlan!.AthleteId != req.UserId)
            return Problem($"Workout {req.WorkoutId} is not in a plan of user {req.UserId}.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var plannedExerciseIds = workout.Exercises.Select(e => e.ExerciseId).ToHashSet();
        if (req.Sets.Any(s => !plannedExerciseIds.Contains(s.ExerciseId)))
            return Problem("Every logged exercise must be part of the workout.", statusCode: StatusCodes.Status422UnprocessableEntity);
        if (req.Sets.DistinctBy(s => (s.ExerciseId, s.SetNumber)).Count() != req.Sets.Count)
            return Problem("Set numbers must be unique per exercise.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var log = new WorkoutLog
        {
            UserId = req.UserId,
            WorkoutId = workout.Id,
            Notes = req.Notes,
            Sets = req.Sets.Select(s => new WorkoutLogSet
            {
                ExerciseId = s.ExerciseId,
                SetNumber = s.SetNumber,
                WeightKg = s.WeightKg,
                Reps = s.Reps,
            }).ToList(),
        };
        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, WorkoutLogDto.From(log));
    }
}
