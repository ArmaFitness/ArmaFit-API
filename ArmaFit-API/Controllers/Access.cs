using System.Security.Claims;
using ArmaFit_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

public static class Access
{
    /// <summary>Id of the logged-in user, taken from the access token.</summary>
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);

    /// <summary>The athletes whose data the user may read and change: themselves and, for a trainer, their active athletes.</summary>
    public static IQueryable<int> AccessibleAthleteIds(this AppDbContext db, ClaimsPrincipal user)
    {
        var me = user.UserId();
        return db.Users
            .Where(u => u.Id == me || db.TrainerAthletes.Any(t =>
                t.TrainerId == me && t.AthleteId == u.Id && t.Status == InvitationStatus.Active))
            .Select(u => u.Id);
    }

    public static Task<bool> CanAccessAthlete(this AppDbContext db, ClaimsPrincipal user, int athleteId) =>
        db.AccessibleAthleteIds(user).ContainsAsync(athleteId);

    public static ObjectResult Forbidden(this ControllerBase c, string detail = "You do not have access to this resource.") =>
        c.Problem(detail, statusCode: StatusCodes.Status403Forbidden);
}

/// <summary>Guard for routes under api/plans/{planId}: 404 if the plan does not exist, 403 if the caller may not access it.</summary>
public class PlanAccessAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    // Runs before the [ApiController] validation filter, so someone else's plan answers 403 rather than 400.
    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = (ControllerBase)context.Controller;
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var planId = int.Parse((string)context.RouteData.Values["planId"]!);

        var athleteId = await db.WorkoutPlans.Where(p => p.Id == planId).Select(p => (int?)p.AthleteId).FirstOrDefaultAsync();
        if (athleteId == null)
            context.Result = controller.Problem($"Plan {planId} not found.", statusCode: StatusCodes.Status404NotFound);
        else if (!await db.CanAccessAthlete(controller.User, athleteId.Value))
            context.Result = controller.Forbidden();
        else
            await next();
    }
}
