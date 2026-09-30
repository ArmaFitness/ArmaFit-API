using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController(AppDbContext db) : ControllerBase
{
    /// <summary>List workout plans, optionally filtered by athlete or creator.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PlanDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<PlanDto>> GetAll(int? athleteId, int? createdBy, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = db.WorkoutPlans.AsQueryable();
        //these will be useful for filtering
        //if (athleteId != null) query = query.Where(p => p.AthleteId == athleteId);
        //if (createdBy != null) query = query.Where(p => p.CreatedBy == createdBy);

        var total = await query.CountAsync();
        var plans = await query.OrderBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<PlanDto>(plans.Select(PlanDto.From).ToList(), page, pageSize, total);
    }

    /// <summary>Get one workout plan.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> Get(int id)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);

        return PlanDto.From(plan);
    }

    /// <summary>Create a plan. An athlete creates one for themselves; a trainer for one of their active athletes.</summary>
    [HttpPost]
    [ProducesResponseType<PlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PlanDto>> Create(PlanCreateRequest req)
    {
        var creator = await db.Users.FindAsync(req.CreatedBy);
        if (creator == null)
            return Problem($"User {req.CreatedBy} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var athlete = await db.Users.FindAsync(req.AthleteId);
        if (athlete == null || athlete.Role != UserRole.Athlete)
            return Problem($"Athlete {req.AthleteId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

        if (creator.Role == UserRole.Athlete && creator.Id != athlete.Id)
            return Problem("Athletes can only create plans for themselves.", statusCode: StatusCodes.Status422UnprocessableEntity);

        if (creator.Role == UserRole.Trainer && !await db.TrainerAthletes.AnyAsync(t =>
                t.TrainerId == creator.Id && t.AthleteId == athlete.Id && t.Status == InvitationStatus.Active))
            return Problem("Trainer has no active link with this athlete.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var plan = new WorkoutPlan { Name = req.Name, Description = req.Description, CreatedBy = creator.Id, AthleteId = athlete.Id };
        db.WorkoutPlans.Add(plan);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = plan.Id }, PlanDto.From(plan));
    }

    /// <summary>Update a plan's name and description.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> Update(int id, PlanUpdateRequest req)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);

        plan.Name = req.Name;
        plan.Description = req.Description;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return PlanDto.From(plan);
    }

    /// <summary>Delete a plan together with its workouts, their exercises and logs.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);

        db.WorkoutPlans.Remove(plan);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private ObjectResult PlanNotFound(int id) =>
        Problem($"Plan {id} not found.", statusCode: StatusCodes.Status404NotFound);
}
