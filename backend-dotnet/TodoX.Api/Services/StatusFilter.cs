namespace TodoX.Api.Services;

/// <summary>Maps the UI filter value to a stored status (api-contract §4.2).</summary>
public static class StatusFilter
{
    /// <summary>
    /// "active" and "completed" (with a d) map to a status; anything else, including the stored
    /// value "complete" and wrong-case values, means no status condition.
    /// </summary>
    public static string? ToStatus(string? filter) => filter switch
    {
        "active" => "active",
        "completed" => "complete",
        _ => null,
    };
}
