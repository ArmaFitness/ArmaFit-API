using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/plans/{planId:int}/workouts/{workoutId:int}/exercises")]
public class WorkoutExercisesController(AppDbContext db) : ControllerBase
{
    /// <summary>List the exercises of a workout, in order.</summary>
    [HttpGet]
    [ProducesResponseType<List<WorkoutExerciseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<WorkoutExerciseDto>>> GetAll(int planId, int workoutId)
    {
        if (!await WorkoutExists(planId, workoutId)) return WorkoutNotFound(planId, workoutId);

        var items = await db.WorkoutExercises.Include(e => e.Exercise).Where(e => e.WorkoutId == workoutId)
            .OrderBy(e => e.OrderIndex).ThenBy(e => e.Id).ToListAsync();
        return items.Select(WorkoutExerciseDto.From).ToList();
    }

    /// <summary>Get one exercise of a workout.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<WorkoutExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutExerciseDto>> Get(int planId, int workoutId, int id)
    {
        var item = await Find(planId, workoutId, id);
        if (item == null) return ItemNotFound(workoutId, id);

        return WorkoutExerciseDto.From(item);
    }

    /// <summary>Add an exercise to a workout with its number of sets.</summary>
    [HttpPost]
    [ProducesResponseType<WorkoutExerciseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkoutExerciseDto>> Create(int planId, int workoutId, WorkoutExerciseRequest req)
    {
        if (!await WorkoutExists(planId, workoutId)) return WorkoutNotFound(planId, workoutId);

        var exercise = await db.Exercises.FindAsync(req.ExerciseId);
        if (exercise == null) return ExerciseMissing(req.ExerciseId);

        var item = new WorkoutExercise
        {
            WorkoutId = workoutId,
            Exercise = exercise,
            Sets = req.Sets,
            Reps = req.Reps,
            WeightKg = req.WeightKg,
            OrderIndex = req.OrderIndex,
        };
        db.WorkoutExercises.Add(item);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { planId, workoutId, id = item.Id }, WorkoutExerciseDto.From(item));
    }

    /// <summary>Update an exercise of a workout.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<WorkoutExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<WorkoutExerciseDto>> Update(int planId, int workoutId, int id, WorkoutExerciseRequest req)
    {
        var item = await Find(planId, workoutId, id);
        if (item == null) return ItemNotFound(workoutId, id);

        var exercise = await db.Exercises.FindAsync(req.ExerciseId);
        if (exercise == null) return ExerciseMissing(req.ExerciseId);

        item.Exercise = exercise;
        item.ExerciseId = exercise.Id;
        item.Sets = req.Sets;
        item.Reps = req.Reps;
        item.WeightKg = req.WeightKg;
        item.OrderIndex = req.OrderIndex;
        await db.SaveChangesAsync();

        return WorkoutExerciseDto.From(item);
    }

    /// <summary>Remove an exercise from a workout.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int planId, int workoutId, int id)
    {
        var item = await Find(planId, workoutId, id);
        if (item == null) return ItemNotFound(workoutId, id);

        db.WorkoutExercises.Remove(item);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Task<bool> WorkoutExists(int planId, int workoutId) =>
        db.Workouts.AnyAsync(w => w.Id == workoutId && w.WorkoutPlanId == planId);

    private Task<WorkoutExercise?> Find(int planId, int workoutId, int id) =>
        db.WorkoutExercises.Include(e => e.Exercise)
            .FirstOrDefaultAsync(e => e.Id == id && e.WorkoutId == workoutId && e.Workout!.WorkoutPlanId == planId);

    private ObjectResult WorkoutNotFound(int planId, int workoutId) =>
        Problem($"Workout {workoutId} not found in plan {planId}.", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult ItemNotFound(int workoutId, int id) =>
        Problem($"Exercise entry {id} not found in workout {workoutId}.", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult ExerciseMissing(int exerciseId) =>
        Problem($"Exercise {exerciseId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);
}
