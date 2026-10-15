namespace DroidUsage.Models;

public sealed record UsageFetchResult(
    bool IsSuccess,
    BillingLimitsResponse? Data,
    string? PlanTier,
    string? ErrorMessage);
