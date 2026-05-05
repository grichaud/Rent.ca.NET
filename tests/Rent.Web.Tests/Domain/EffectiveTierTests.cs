using Rent.Web.Domain;
using Xunit;

namespace Rent.Web.Tests.Domain;

public class EffectiveTierTests
{
    [Fact]
    public void Property_returns_raw_tier_when_no_expiration()
    {
        var p = new Property { Tier = ListingTier.Featured, TierExpiresAt = null };
        Assert.Equal(ListingTier.Featured, p.EffectiveTier());
    }

    [Fact]
    public void Property_returns_raw_tier_when_expiration_in_future()
    {
        var p = new Property
        {
            Tier = ListingTier.Featured,
            TierExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        };
        Assert.Equal(ListingTier.Featured, p.EffectiveTier());
    }

    [Fact]
    public void Property_returns_Limited_when_expired()
    {
        var p = new Property
        {
            Tier = ListingTier.Featured,
            TierExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        Assert.Equal(ListingTier.Limited, p.EffectiveTier());
    }

    [Fact]
    public void Landlord_returns_Limited_when_expired()
    {
        var l = new LandlordProfile
        {
            Tier = ListingTier.Promoted,
            TierExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        Assert.Equal(ListingTier.Limited, l.EffectiveTier());
    }

    [Fact]
    public void ActiveSpecial_returns_first_active_within_window()
    {
        var p = new Property
        {
            RentSpecials = new List<RentSpecial>
            {
                new() { IsActive = false, Title = "inactive" },
                new() { IsActive = true, Title = "expired", EndDate = DateTimeOffset.UtcNow.AddHours(-1) },
                new() { IsActive = true, Title = "current" }
            }
        };
        Assert.Equal("current", p.ActiveSpecial()?.Title);
    }

    [Fact]
    public void ActiveSpecial_returns_null_when_none_active()
    {
        var p = new Property
        {
            RentSpecials = new List<RentSpecial>
            {
                new() { IsActive = false, Title = "inactive" }
            }
        };
        Assert.Null(p.ActiveSpecial());
    }

    [Fact]
    public void ActiveSpecial_skips_specials_before_StartDate()
    {
        var p = new Property
        {
            RentSpecials = new List<RentSpecial>
            {
                new() { IsActive = true, Title = "future", StartDate = DateTimeOffset.UtcNow.AddHours(1) }
            }
        };
        Assert.Null(p.ActiveSpecial());
    }
}
