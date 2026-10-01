namespace TodoX.Api.Services;

/// <summary>page/limit parsing and paging math (api-contract §4.3), matching the Node parseInt rules.</summary>
public static class Pagination
{
    public static int ParsePage(string? page) => throw new NotImplementedException();

    public static int ParseLimit(string? limit) => throw new NotImplementedException();

    public static int TotalPages(int totalCount, int limit) => throw new NotImplementedException();

    public static int Skip(int page, int limit) => throw new NotImplementedException();
}
