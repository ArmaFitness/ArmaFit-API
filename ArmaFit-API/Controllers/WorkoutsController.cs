using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/plans/{planId:int}/workouts")]
[PlanAccess]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class WorkoutsController(AppDbContext db) : ControllerBase
{
    /// <summary>List the workouts of a plan, ordered by day.</summary>
    [HttpGet]
    [ProducesResponseType<List<WorkoutDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkoutDto>>> GetAll(int planId)
    {
        var workouts = await db.Workouts.Where(w => w.WorkoutPlanId == planId)
            .OrderBy(w => w.DayNumber).ThenBy(w => w.Id).ToListAsync();
        return workouts.Select(w => WithLinks(WorkoutDto.From(w))).ToList();
    }

    /// <summary>Get one workout of a plan.</summary>
    [HttpGet("{workoutId:int}")]
    [ProducesResponseType<WorkoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDto>> Get(int planId, int workoutId)
    {
        var workout = await Find(planId, workoutId);
        if (workout == null) return WorkoutNotFound(planId, workoutId);

        return WithLinks(WorkoutDto.From(workout));
    }

    /// <summary>Create a workout in a plan and assign it a day.</summary>
    [HttpPost]
    [ProducesResponseType<WorkoutDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDto>> Create(int planId, WorkoutRequest req)
    {
        var workout = new Workout { WorkoutPlanId = planId, Name = req.Name, DayNumber = req.DayNumber };
        db.Workouts.Add(workout);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { planId, workoutId = workout.Id }, WithLinks(WorkoutDto.From(workout)));
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

        return WithLinks(WorkoutDto.From(workout));
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

    private WorkoutDto WithLinks(WorkoutDto w) => w with
    {
        Links = new()
        {
            ["self"] = this.Link(nameof(Get), "Workouts", new { planId = w.WorkoutPlanId, workoutId = w.Id }),
            ["update"] = this.Link(nameof(Update), "Workouts", new { planId = w.WorkoutPlanId, workoutId = w.Id }, "PUT"),
            ["delete"] = this.Link(nameof(Delete), "Workouts", new { planId = w.WorkoutPlanId, workoutId = w.Id }, "DELETE"),
            ["plan"] = this.Link("Get", "Plans", new { id = w.WorkoutPlanId }),
            ["exercises"] = this.Link("GetAll", "WorkoutExercises", new { planId = w.WorkoutPlanId, workoutId = w.Id }),
        }
    };

    private Task<Workout?> Find(int planId, int workoutId) =>
        db.Workouts.FirstOrDefaultAsync(w => w.Id == workoutId && w.WorkoutPlanId == planId);

    private ObjectResult WorkoutNotFound(int planId, int workoutId) =>
        Problem($"Workout {workoutId} not found in plan {planId}.", statusCode: StatusCodes.Status404NotFound);
}
