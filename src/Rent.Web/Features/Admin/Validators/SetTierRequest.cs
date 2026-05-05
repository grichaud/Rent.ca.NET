using Rent.Web.Domain;

namespace Rent.Web.Features.Admin.Validators;

public class SetTierRequest
{
    public Guid TargetId { get; set; }
    public ListingTier Tier { get; set; } = ListingTier.Limited;
    public DateTimeOffset? ExpiresAt { get; set; }
}
