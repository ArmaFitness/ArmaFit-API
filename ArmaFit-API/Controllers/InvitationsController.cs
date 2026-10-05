using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/invitations")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class InvitationsController(AppDbContext db) : ControllerBase
{
    /// <summary>List your own invitations, e.g. a trainer's pending invitations or active athletes.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<InvitationDto>>(StatusCodes.Status200OK)]
    public async Task<PagedResult<InvitationDto>> GetAll(int? trainerId, int? athleteId, InvitationStatus? status, int page = 1, int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var me = User.UserId();
        var query = db.TrainerAthletes.AsNoTracking().Include(t => t.Trainer).Include(t => t.Athlete)
            .Where(t => t.TrainerId == me || t.AthleteId == me);
        if (trainerId != null) query = query.Where(t => t.TrainerId == trainerId);
        if (athleteId != null) query = query.Where(t => t.AthleteId == athleteId);
        if (status != null) query = query.Where(t => t.Status == status);

        var total = await query.CountAsync();
        var invitations = await query.OrderByDescending(t => t.InvitedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var result = new PagedResult<InvitationDto>(invitations.Select(i => WithLinks(InvitationDto.From(i))).ToList(), page, pageSize, total);
        return result with { Links = this.PageLinks(page, result.TotalPages) };
    }

    /// <summary>Athlete sends an invitation to a trainer (found by email).</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Athlete))]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InvitationDto>> Create(InvitationCreateRequest req)
    {
        var athlete = (await db.Users.FindAsync(User.UserId()))!;

        var email = req.TrainerEmail.Trim().ToLowerInvariant();
        var trainer = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.Role == UserRole.Trainer);
        if (trainer == null)
            return Problem($"Trainer '{email}' does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

        var invitation = await db.TrainerAthletes.FirstOrDefaultAsync(t => t.TrainerId == trainer.Id && t.AthleteId == athlete.Id);
        if (invitation is { Status: InvitationStatus.Pending or InvitationStatus.Active })
            return Problem("An invitation to this trainer is already pending or active.", statusCode: StatusCodes.Status409Conflict);

        // (trainer, athlete) is unique, so a declined or ended invitation is re-opened instead of duplicated.
        if (invitation == null)
        {
            invitation = new TrainerAthlete { Trainer = trainer, Athlete = athlete };
            db.TrainerAthletes.Add(invitation);
        }
        invitation.Status = InvitationStatus.Pending;
        invitation.InvitedAt = DateTime.UtcNow;
        invitation.RespondedAt = null;
        invitation.EndedAt = null;
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, WithLinks(InvitationDto.From(invitation)));
    }

    /// <summary>Trainer accepts a pending invitation that was sent to them.</summary>
    [HttpPost("{id:int}/accept")]
    [Authorize(Roles = nameof(UserRole.Trainer))]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationDto>> Accept(int id)
    {
        var invitation = await db.TrainerAthletes.Include(t => t.Trainer).Include(t => t.Athlete).FirstOrDefaultAsync(t => t.Id == id);
        if (invitation == null)
            return Problem($"Invitation {id} not found.", statusCode: StatusCodes.Status404NotFound);
        if (invitation.TrainerId != User.UserId())
            return this.Forbidden("This invitation was sent to another trainer.");
        if (invitation.Status != InvitationStatus.Pending)
            return Problem("Only pending invitations can be accepted.", statusCode: StatusCodes.Status409Conflict);

        invitation.Status = InvitationStatus.Active;
        invitation.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return WithLinks(InvitationDto.From(invitation));
    }

    // "accept" is only offered to the invited trainer, while the invitation is pending.
    private InvitationDto WithLinks(InvitationDto i)
    {
        var links = new Dictionary<string, Link> { ["athlete-plans"] = PlansFor(i.AthleteId) };
        if (i.Status == InvitationStatus.Pending && i.TrainerId == User.UserId())
            links["accept"] = this.Link(nameof(Accept), "Invitations", new { id = i.Id }, "POST");
        return i with { Links = links };
    }

    private Link PlansFor(int athleteId) =>
        new(Url.Action("GetAll", "Plans", new { athleteId })!, "GET");
}
