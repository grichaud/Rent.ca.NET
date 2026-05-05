namespace Rent.Web.Features.Admin.Validators;

public class CreateRentSpecialRequest
{
    public Guid PropertyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateRentSpecialRequest
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public bool IsActive { get; set; }
}
