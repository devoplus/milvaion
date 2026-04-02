namespace Suvari.ScheduledTasks.Core.Integrations.REMVision;

public class TokenResponse
{
    public string Access_token { get; set; }
    public string Token_type { get; set; }
    public int    Expires_in { get; set; }
    public string Refresh_token { get; set; }
    public string UserName { get; set; }
    public string Issued { get; set; }
    public string Expires { get; set; }
}

public class BaseResponse<T>
{
    public T      Data { get; set; }
    public bool   Success { get; set; }
    public string Message { get; set; }
}

public class StoreResponse
{
    public int        Id { get; set; }
    public string     Name { get; set; }
    public string     Lat { get; set; }
    public string     Lng { get; set; }
    public int        TZOffset { get; set; }
    public bool       SaleIntegrated { get; set; }
    public string     PartnerId { get; set; }
    public int        BrandId { get; set; }
    public string     Brand { get; set; }
    public DateTime   CreatedDate { get; set; }
    public bool       HasEntranceRects { get; set; }
    public bool       HasDoorDirectionRects { get; set; }
    public int        CameraCount { get; set; }
    public string     Country { get; set; }
    public int        CountryId { get; set; }
    public int        CountyId { get; set; }
    public string     County { get; set; }
    public string     City { get; set; }
    public int        CityId { get; set; }
    public bool       Archived { get; set; }
    public int        PeopleCapacity { get; set; }
    public Demography Demography { get; set; }
    public bool       HideFromReport { get; set; }
    public int        OccupancyThreshold { get; set; }
    public bool       HasDismissPhoto { get; set; }
    public int        CurrencyId { get; set; }
    public string     CurrencyName { get; set; }
    public string     CurrencySymbol { get; set; }
    public Products   Products { get; set; }
    public float      LatDouble { get; set; }
    public float      LngDouble { get; set; }
    public object[]   Devices { get; set; }
}

public class Demography
{
    public string Name { get; set; }
    public int    PopulationTotal { get; set; }
    public string Age { get; set; }
    public float  HouseHold { get; set; }
    public string Marital { get; set; }
    public string Ses { get; set; }
    public int    TotalSale { get; set; }
    public int    TotalEstate { get; set; }
    public string Education { get; set; }
    public float  HouseIncome { get; set; }
    public float  HouseIncomeTotal { get; set; }
    public float  ECommerce { get; set; }
    public int    CountyId { get; set; }
}

public class Products
{
    public bool CheckoutProduct { get; set; }
    public bool PeopleCountingProduct { get; set; }
    public bool PeopleCountingGenderAgeProduct { get; set; }
    public bool PersonnelTrackingProduct { get; set; }
    public bool InstoreAnalyticsProduct { get; set; }
}

public class LineEnteranceCountResponse
{
    public LineData[] Data { get; set; }
    public string     Message { get; set; }
    public bool       Success { get; set; }
}

public class LineData
{
    public string   Name { get; set; }
    public object[] Serial { get; set; }
}

