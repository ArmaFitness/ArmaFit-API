using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/exercises")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ExercisesController(AppDbContext db) : ControllerBase
{
    /// <summary>List the exercise catalog (used to pick an exerciseId for a workout).</summary>
    [HttpGet]
    [ProducesResponseType<List<ExerciseDto>>(StatusCodes.Status200OK)]
    public async Task<List<ExerciseDto>> GetAll(int page = 1, int pageSize = 20) 
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return await db.Exercises.OrderBy(e => e.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(e => new ExerciseDto(e.Id, e.Name, e.Description)).ToListAsync();
    }
}
