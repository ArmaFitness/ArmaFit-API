using ArmaFit_API.Data;
using ArmaFit_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArmaFit_API.Controllers;

[ApiController]
[Route("api/exercises")]
public class ExercisesController(AppDbContext db) : ControllerBase
{
    // pagination optional
    /// <summary>List the exercise catalog (used to pick an exerciseId for a workout).</summary>
    [HttpGet]
    [ProducesResponseType<List<ExerciseDto>>(StatusCodes.Status200OK)]
    public async Task<List<ExerciseDto>> GetAll() =>
        await db.Exercises.OrderBy(e => e.Name).Select(e => new ExerciseDto(e.Id, e.Name, e.Description)).ToListAsync();
}
