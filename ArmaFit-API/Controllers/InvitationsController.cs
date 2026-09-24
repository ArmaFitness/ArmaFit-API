using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/invitations")]
public class InvitationsController(AppDbContext db) : ControllerBase
{
    /// <summary>List invitations, e.g. a trainer's pending invitations or active athletes.</summary>
    [HttpGet]
    [ProducesResponseType<List<InvitationDto>>(StatusCodes.Status200OK)]
    public async Task<List<InvitationDto>> GetAll(int? trainerId, int? athleteId, InvitationStatus? status)
    {
        IQueryable<TrainerAthlete> query = db.TrainerAthletes.Include(t => t.Trainer).Include(t => t.Athlete);
        if (trainerId != null) query = query.Where(t => t.TrainerId == trainerId);
        if (athleteId != null) query = query.Where(t => t.AthleteId == athleteId);
        if (status != null) query = query.Where(t => t.Status == status);

        var invitations = await query.OrderByDescending(t => t.InvitedAt).ToListAsync();
        return invitations.Select(InvitationDto.From).ToList();
    }

    /// <summary>Athlete sends an invitation to a trainer (found by email).</summary>
    [HttpPost]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InvitationDto>> Create(InvitationCreateRequest req)
    {
        var athlete = await db.Users.FirstOrDefaultAsync(u => u.Id == req.AthleteId && u.Role == UserRole.Athlete);
        if (athlete == null)
            return Problem($"Athlete {req.AthleteId} does not exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

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

        return StatusCode(StatusCodes.Status201Created, InvitationDto.From(invitation));
    }

    /// <summary>Trainer accepts a pending invitation.</summary>
    [HttpPost("{id:int}/accept")]
    [ProducesResponseType<InvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationDto>> Accept(int id)
    {
        var invitation = await db.TrainerAthletes.Include(t => t.Trainer).Include(t => t.Athlete).FirstOrDefaultAsync(t => t.Id == id);
        if (invitation == null)
            return Problem($"Invitation {id} not found.", statusCode: StatusCodes.Status404NotFound);
        if (invitation.Status != InvitationStatus.Pending)
            return Problem("Only pending invitations can be accepted.", statusCode: StatusCodes.Status409Conflict);

        invitation.Status = InvitationStatus.Active;
        invitation.RespondedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return InvitationDto.From(invitation);
    }
}
