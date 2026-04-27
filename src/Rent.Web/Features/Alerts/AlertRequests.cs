using Rent.Web.Domain;

namespace Rent.Web.Features.Alerts;

public class CreateAlertRequest
{
    public string? City { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? BedroomsMin { get; set; }

    /// <summary>
    /// Tri-state: "true" / "false" / null/"" = "any". Form posts strings; we parse to bool? in the handler.
    /// </summary>
    public string? PetsAllowed { get; set; }

    public AlertFrequency Frequency { get; set; } = AlertFrequency.Daily;
}
