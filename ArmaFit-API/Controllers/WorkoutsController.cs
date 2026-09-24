using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/plans/{planId:int}/workouts")]
public class WorkoutsController(AppDbContext db) : ControllerBase
{
    /// <summary>List the workouts of a plan, ordered by day.</summary>
    [HttpGet]
    [ProducesResponseType<List<WorkoutDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkoutDto>>> GetAll(int planId)
    {
        if (!await db.WorkoutPlans.AnyAsync(p => p.Id == planId))
            return Problem($"Plan {planId} not found.", statusCode: StatusCodes.Status404NotFound);

        var workouts = await db.Workouts.Where(w => w.WorkoutPlanId == planId)
            .OrderBy(w => w.DayNumber).ThenBy(w => w.Id).ToListAsync();
        return workouts.Select(WorkoutDto.From).ToList();
    }

    /// <summary>Get one workout of a plan.</summary>
    [HttpGet("{workoutId:int}")]
    [ProducesResponseType<WorkoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDto>> Get(int planId, int workoutId)
    {
        var workout = await Find(planId, workoutId);
        if (workout == null) return WorkoutNotFound(planId, workoutId);

        return WorkoutDto.From(workout);
    }

    /// <summary>Create a workout in a plan and assign it a day.</summary>
    [HttpPost]
    [ProducesResponseType<WorkoutDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDto>> Create(int planId, WorkoutRequest req)
    {
        if (!await db.WorkoutPlans.AnyAsync(p => p.Id == planId))
            return Problem($"Plan {planId} not found.", statusCode: StatusCodes.Status404NotFound);

        var workout = new Workout { WorkoutPlanId = planId, Name = req.Name, DayNumber = req.DayNumber };
        db.Workouts.Add(workout);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { planId, workoutId = workout.Id }, WorkoutDto.From(workout));
    }

    /// <summary>Update a workout's name and day.</summary>
    [HttpPut("{workoutId:int}")]
    [ProducesResponseType<WorkoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDto>> Update(int planId, int workoutId, WorkoutRequest req)
    {
        var workout = await Find(planId, workoutId);
        if (workout == null) return WorkoutNotFound(planId, workoutId);

        workout.Name = req.Name;
        workout.DayNumber = req.DayNumber;
        await db.SaveChangesAsync();

        return WorkoutDto.From(workout);
    }

    /// <summary>Delete a workout together with its exercises and logs.</summary>
    [HttpDelete("{workoutId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int planId, int workoutId)
    {
        var workout = await Find(planId, workoutId);
        if (workout == null) return WorkoutNotFound(planId, workoutId);

        db.Workouts.Remove(workout);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Task<Workout?> Find(int planId, int workoutId) =>
        db.Workouts.FirstOrDefaultAsync(w => w.Id == workoutId && w.WorkoutPlanId == planId);

    private ObjectResult WorkoutNotFound(int planId, int workoutId) =>
        Problem($"Workout {workoutId} not found in plan {planId}.", statusCode: StatusCodes.Status404NotFound);
}
