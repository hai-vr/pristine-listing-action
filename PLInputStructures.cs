namespace Hai.PristineListing;

public class PLInput
{
    public PLInputListingData listingData;
    public List<PLProduct> products;
}

public class PLInputListingData
{
    public string name;
    public string author;
    public string url;
    public string id;
}

public class PLProduct
{
    public string repository;
}