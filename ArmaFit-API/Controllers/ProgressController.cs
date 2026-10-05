using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/athletes/{athleteId:int}/progress")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ProgressController(AppDbContext db) : ControllerBase
{
    /// <summary>Progress chart data: one point per logged session for each exercise. Open to the athlete and their active trainers.</summary>
    [HttpGet]
    [ProducesResponseType<List<ProgressDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ProgressDto>>> Get(int athleteId, int? exerciseId)
    {
        if (!await db.Users.AnyAsync(u => u.Id == athleteId && u.Role == UserRole.Athlete))
            return Problem($"Athlete {athleteId} not found.", statusCode: StatusCodes.Status404NotFound);
        if (!await db.CanAccessAthlete(User, athleteId)) return this.Forbidden();

        var sets = await db.WorkoutLogSets
            .Where(s => s.WorkoutLog!.UserId == athleteId && (exerciseId == null || s.ExerciseId == exerciseId))
            .Select(s => new { s.ExerciseId, s.Exercise!.Name, s.WorkoutLogId, s.WorkoutLog!.LoggedAt, s.WeightKg, s.Reps })
            .ToListAsync();

        return sets
            .GroupBy(s => (s.ExerciseId, s.Name))
            .OrderBy(g => g.Key.Name)
            .Select(g => new ProgressDto(g.Key.ExerciseId, g.Key.Name, g
                .GroupBy(s => (s.WorkoutLogId, s.LoggedAt))
                .OrderBy(session => session.Key.LoggedAt)
                .Select(session => new ProgressPointDto(
                    session.Key.LoggedAt,
                    session.Max(s => s.WeightKg),
                    session.Sum(s => s.WeightKg * s.Reps)))
                .ToList()))
            .ToList();
    }
}
