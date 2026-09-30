namespace ArmaFit_API.Models.Queries;

public class PlanSearchQuery
{
    public int? AthleteId { get; set; }
    public int? CreatedBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
