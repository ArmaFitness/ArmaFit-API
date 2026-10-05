using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/workout-logs")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class WorkoutLogsController(AppDbContext db) : ControllerBase
{
    /// <summary>List the workout logs you can access, newest first: your own, or (for a trainer) those of your active athletes.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<WorkoutLogDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<WorkoutLogDto>> GetAll(int? athleteId, int? workoutId, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var athletes = db.AccessibleAthleteIds(User);
        var query = db.WorkoutLogs.AsNoTracking().Include(l => l.Sets).Where(l => athletes.Contains(l.UserId));
        if (athleteId != null) query = query.Where(l => l.UserId == athleteId);
        if (workoutId != null) query = query.Where(l => l.WorkoutId == workoutId);

        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(l => l.LoggedAt).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var result = new PagedResult<WorkoutLogDto>(logs.Select(WorkoutLogDto.From).ToList(), page, pageSize, total);
        return result with { Links = this.PageLinks(page, result.TotalPages) };
    }

    /// <summary>Athlete logs a completed workout from one of their plans, with weight and reps per set.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Athlete))]
    [ProducesResponseType<WorkoutLogDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkoutLogDto>> Create(WorkoutLogCreateRequest req)
    {
        if (req.Sets.Any(s => s is null))
        {
            ModelState.AddModelError(nameof(req.Sets), "Sets must not contain null entries.");
            return ValidationProblem();
        }

        var me = User.UserId();
        var workout = await db.Workouts.Include(w => w.WorkoutPlan).Include(w => w.Exercises)
            .FirstOrDefaultAsync(w => w.Id == req.WorkoutId);
        if (workout == null)
            return Problem($"Workout {req.WorkoutId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);
        if (workout.WorkoutPlan!.AthleteId != me)
            return this.Forbidden($"Workout {req.WorkoutId} is not in one of your plans.");

        var plannedExerciseIds = workout.Exercises.Select(e => e.ExerciseId).ToHashSet();
        if (req.Sets.Any(s => !plannedExerciseIds.Contains(s.ExerciseId)))
            return Problem("Every logged exercise must be part of the workout.", statusCode: StatusCodes.Status422UnprocessableEntity);
        if (req.Sets.DistinctBy(s => (s.ExerciseId, s.SetNumber)).Count() != req.Sets.Count)
            return Problem("Set numbers must be unique per exercise.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var log = new WorkoutLog
        {
            UserId = me,
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
