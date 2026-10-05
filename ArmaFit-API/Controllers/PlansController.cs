using ArmaFit_API.Data;
using ArmaFit_API.Models;
using ArmaFit_API.Models.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/plans")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class PlansController(AppDbContext db) : ControllerBase
{
    /// <summary>List the workout plans you can access (your own, or those of your active athletes), optionally filtered by athlete or creator.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PlanDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<PlanDto>> GetAll([FromQuery] PlanSearchQuery q)
    {
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var page = Math.Max(q.Page, 1);

        var athletes = db.AccessibleAthleteIds(User);
        var query = db.WorkoutPlans.Where(p => athletes.Contains(p.AthleteId));
        if (q.AthleteId != null) query = query.Where(p => p.AthleteId == q.AthleteId);
        if (q.CreatedBy != null) query = query.Where(p => p.CreatedBy == q.CreatedBy);

        var total = await query.CountAsync();
        var plans = await query.OrderBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var result = new PagedResult<PlanDto>(plans.Select(p => WithLinks(PlanDto.From(p))).ToList(), page, pageSize, total);
        return result with { Links = this.PageLinks(page, result.TotalPages) };
    }

    /// <summary>Get one workout plan.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> Get(int id)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);
        if (!await db.CanAccessAthlete(User, plan.AthleteId)) return this.Forbidden();

        return WithLinks(PlanDto.From(plan));
    }

    /// <summary>Create a plan. An athlete creates one for themselves; a trainer for one of their active athletes.</summary>
    [HttpPost]
    [ProducesResponseType<PlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PlanDto>> Create(PlanCreateRequest req)
    {
        var athlete = await db.Users.FindAsync(req.AthleteId);
        if (athlete == null || athlete.Role != UserRole.Athlete)
            return Problem($"Athlete {req.AthleteId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

        if (!await db.CanAccessAthlete(User, athlete.Id))
            return this.Forbidden("You can only create plans for yourself or for your active athletes.");

        var plan = new WorkoutPlan { Name = req.Name, Description = req.Description, CreatedBy = User.UserId(), AthleteId = athlete.Id };
        db.WorkoutPlans.Add(plan);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = plan.Id }, WithLinks(PlanDto.From(plan)));
    }

    /// <summary>Update a plan's name and description.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<PlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDto>> Update(int id, PlanUpdateRequest req)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);
        if (!await db.CanAccessAthlete(User, plan.AthleteId)) return this.Forbidden();

        plan.Name = req.Name;
        plan.Description = req.Description;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return WithLinks(PlanDto.From(plan));
    }

    /// <summary>Delete a plan together with its workouts, their exercises and logs.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.WorkoutPlans.FindAsync(id);
        if (plan == null) return PlanNotFound(id);
        if (!await db.CanAccessAthlete(User, plan.AthleteId)) return this.Forbidden();

        db.WorkoutPlans.Remove(plan);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private PlanDto WithLinks(PlanDto p) => p with
    {
        Links = new()
        {
            ["self"] = this.Link(nameof(Get), "Plans", new { id = p.Id }),
            ["update"] = this.Link(nameof(Update), "Plans", new { id = p.Id }, "PUT"),
            ["delete"] = this.Link(nameof(Delete), "Plans", new { id = p.Id }, "DELETE"),
            ["workouts"] = this.Link("GetAll", "Workouts", new { planId = p.Id }),
            ["progress"] = this.Link("Get", "Progress", new { athleteId = p.AthleteId }),
        }
    };

    private ObjectResult PlanNotFound(int id) =>
        Problem($"Plan {id} not found.", statusCode: StatusCodes.Status404NotFound);
}
