namespace Rent.Web.Domain;

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string? City { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? BedroomsMin { get; set; }
    public decimal? BathroomsMin { get; set; }
    public bool? PetsAllowed { get; set; }

    public AlertFrequency Frequency { get; set; } = AlertFrequency.Daily;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastSentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = default!;
}
