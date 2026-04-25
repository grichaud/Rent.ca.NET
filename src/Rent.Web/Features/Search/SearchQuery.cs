using Rent.Web.Domain;

namespace Rent.Web.Features.Search;

public class SearchQuery
{
    public string CitySlug { get; set; } = string.Empty;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? Bedrooms { get; set; }
    public PropertyType? Type { get; set; }
    public bool PetsAllowed { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class PropertyCard
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string CitySlug { get; set; } = string.Empty;
    public string? Neighbourhood { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public decimal? FromPrice { get; set; }
    public int MinBedrooms { get; set; }
    public decimal MinBathrooms { get; set; }
    public PropertyType PropertyType { get; set; }
    public ListingTier Tier { get; set; }
    public bool IsVerified { get; set; }
    public bool PetsAllowed { get; set; }
    public bool Furnished { get; set; }
}

public class SearchResult
{
    public City? City { get; set; }
    public IReadOnlyList<PropertyCard> Properties { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
