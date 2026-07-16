using Rent.Web.Domain;

namespace Rent.Web.Features.LandlordManage.Pages.Listings;

public class ListingFormInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PropertyType PropertyType { get; set; } = PropertyType.Apartment;

    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public string StreetAddress { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string? Neighbourhood { get; set; }

    public bool PetsAllowed { get; set; }
    public bool Furnished { get; set; }

    public LeaseTerm? LeaseTerm { get; set; } = Domain.LeaseTerm.OneYear;
    public string? ParkingType { get; set; }
    public int? YearBuilt { get; set; }
    public int? TotalFloors { get; set; }

    public List<Guid> AmenityIds { get; set; } = [];

    public List<UnitInput> Units { get; set; } = [new UnitInput()];

    public IFormFileCollection? NewImages { get; set; }
}

public class UnitInput
{
    /// <summary>Null para unidades nuevas. En Edit identifica la fila existente a actualizar.</summary>
    public Guid? Id { get; set; }

    public int Bedrooms { get; set; } = 1;
    public decimal Bathrooms { get; set; } = 1;
    public int? SqFt { get; set; }
    public decimal Price { get; set; }
    public DateOnly? AvailableDate { get; set; }
    public int AvailableUnits { get; set; } = 1;
}
