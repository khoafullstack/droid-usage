using DroidUsage.Models;

namespace DroidUsage.Services.Abstractions;

public interface IUsageApiClient
{
    Task<UsageFetchResult> FetchBillingLimitsAsync(CancellationToken cancellationToken = default);
}
