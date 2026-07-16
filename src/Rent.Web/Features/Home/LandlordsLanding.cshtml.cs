using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Rent.Web.Features.Home;

[AllowAnonymous]
public class LandlordsLandingModel : PageModel
{
    public IReadOnlyList<PricingTier> Tiers { get; } = new[]
    {
        new PricingTier(
            NameKey:        "Landlords_TierLimited",
            PriceKey:       "Landlords_TierLimitedPrice",
            PeriodKey:      "Landlords_TierLimitedPeriod",
            BodyKey:        "Landlords_TierLimitedBody",
            CtaKey:         "Landlords_TierLimitedCta",
            BadgeKey:       null,
            Variant:        TierVariant.Standard,
            Features: new[]
            {
                new TierFeature("Landlords_TierLimitedF1", true),
                new TierFeature("Landlords_TierLimitedF2", true),
                new TierFeature("Landlords_TierLimitedF3", true),
                new TierFeature("Landlords_TierLimitedF4", true),
                new TierFeature("Landlords_TierLimitedF5", false),
                new TierFeature("Landlords_TierLimitedF6", false),
                new TierFeature("Landlords_TierLimitedF7", false),
                new TierFeature("Landlords_TierLimitedF8", false),
            }),
        new PricingTier(
            NameKey:        "Landlords_TierPromoted",
            PriceKey:       "Landlords_TierPromotedPrice",
            PeriodKey:      "Landlords_TierPromotedPeriod",
            BodyKey:        "Landlords_TierPromotedBody",
            CtaKey:         "Landlords_TierPromotedCta",
            BadgeKey:       "Landlords_TierPromotedBadge",
            Variant:        TierVariant.Promoted,
            Features: new[]
            {
                new TierFeature("Landlords_TierPromotedF1", true),
                new TierFeature("Landlords_TierPromotedF2", true),
                new TierFeature("Landlords_TierPromotedF3", true),
                new TierFeature("Landlords_TierPromotedF4", true),
                new TierFeature("Landlords_TierPromotedF5", true),
                new TierFeature("Landlords_TierPromotedF6", false),
                new TierFeature("Landlords_TierPromotedF7", false),
                new TierFeature("Landlords_TierPromotedF8", false),
            }),
        new PricingTier(
            NameKey:        "Landlords_TierFeatured",
            PriceKey:       "Landlords_TierFeaturedPrice",
            PeriodKey:      "Landlords_TierFeaturedPeriod",
            BodyKey:        "Landlords_TierFeaturedBody",
            CtaKey:         "Landlords_TierFeaturedCta",
            BadgeKey:       "Landlords_TierFeaturedBadge",
            Variant:        TierVariant.Featured,
            Features: new[]
            {
                new TierFeature("Landlords_TierFeaturedF1", true),
                new TierFeature("Landlords_TierFeaturedF2", true),
                new TierFeature("Landlords_TierFeaturedF3", true),
                new TierFeature("Landlords_TierFeaturedF4", true),
                new TierFeature("Landlords_TierFeaturedF5", true),
                new TierFeature("Landlords_TierFeaturedF6", true),
                new TierFeature("Landlords_TierFeaturedF7", true),
                new TierFeature("Landlords_TierFeaturedF8", true),
            }),
    };

    public IReadOnlyList<Testimonial> Testimonials { get; } = new[]
    {
        new Testimonial("Landlords_T1Quote", "Landlords_T1Author", "Landlords_T1Company", "J"),
        new Testimonial("Landlords_T2Quote", "Landlords_T2Author", "Landlords_T2Company", "P"),
        new Testimonial("Landlords_T3Quote", "Landlords_T3Author", "Landlords_T3Company", "M"),
    };

    public IReadOnlyList<FaqItem> FaqItems { get; } = new[]
    {
        new FaqItem("Landlords_FaqQ1", "Landlords_FaqA1"),
        new FaqItem("Landlords_FaqQ2", "Landlords_FaqA2"),
        new FaqItem("Landlords_FaqQ3", "Landlords_FaqA3"),
        new FaqItem("Landlords_FaqQ4", "Landlords_FaqA4"),
        new FaqItem("Landlords_FaqQ5", "Landlords_FaqA5"),
        new FaqItem("Landlords_FaqQ6", "Landlords_FaqA6"),
        new FaqItem("Landlords_FaqQ7", "Landlords_FaqA7"),
        new FaqItem("Landlords_FaqQ8", "Landlords_FaqA8"),
    };

    public void OnGet() { }
}

public enum TierVariant { Standard, Promoted, Featured }

public sealed record PricingTier(
    string NameKey,
    string PriceKey,
    string PeriodKey,
    string BodyKey,
    string CtaKey,
    string? BadgeKey,
    TierVariant Variant,
    IReadOnlyList<TierFeature> Features);

public sealed record TierFeature(string LabelKey, bool Included);

public sealed record Testimonial(string QuoteKey, string AuthorKey, string CompanyKey, string AvatarLetter);

public sealed record FaqItem(string QuestionKey, string AnswerKey);
