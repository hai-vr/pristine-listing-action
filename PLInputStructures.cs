public class PLInput
{
    public PLInputListingData listingData;
    public List<PLProducts> products;
}

public class PLInputListingData
{
    public string name;
    public string author;
    public string url;
    public string id;
}

public class PLProducts
{
    public string repository;
}