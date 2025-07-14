using Hai.PristineListing.Core;

namespace Hai.PristineListing.Aggregator;

public class PLAggregator
{
    public virtual async Task<PLCoreAggregation> DownloadAndAggregate(PLCoreInput input)
    {
        return new PLCoreAggregation();
    }
}