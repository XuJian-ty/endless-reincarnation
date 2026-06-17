using System.Net.Http.Json;

internal sealed class OnlineDungeonClient
{
    private readonly HttpClient _httpClient;

    public OnlineDungeonClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool TryCreateInstance(
        string dungeonServerUrl,
        CreateDungeonInstanceRequest request,
        out DungeonInstanceInfo? instance,
        out string error)
    {
        instance = null;
        error = string.Empty;

        string normalizedServerUrl = NormalizeServerUrl(dungeonServerUrl);
        if (string.IsNullOrWhiteSpace(normalizedServerUrl))
        {
            error = "联机副本服务地址为空";
            return false;
        }

        try
        {
            HttpResponseMessage response = _httpClient.PostAsJsonAsync($"{normalizedServerUrl}/api/dungeons", request)
                .GetAwaiter()
                .GetResult();
            DungeonApiResponse<DungeonInstanceInfo>? apiResponse = response.Content
                .ReadFromJsonAsync<DungeonApiResponse<DungeonInstanceInfo>>()
                .GetAwaiter()
                .GetResult();

            if (!response.IsSuccessStatusCode || apiResponse == null || !apiResponse.success || apiResponse.data == null)
            {
                error = apiResponse?.message ?? string.Empty;
                if (string.IsNullOrWhiteSpace(error))
                    error = $"联机副本服务返回 HTTP {(int)response.StatusCode}";
                return false;
            }

            instance = apiResponse.data;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryJoinInstance(
        string dungeonServerUrl,
        string instanceId,
        JoinDungeonInstanceRequest request,
        out string error)
    {
        error = string.Empty;

        string normalizedServerUrl = NormalizeServerUrl(dungeonServerUrl);
        string normalizedInstanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedServerUrl) || string.IsNullOrWhiteSpace(normalizedInstanceId))
        {
            error = "联机副本服务地址或副本标识为空";
            return false;
        }

        try
        {
            HttpResponseMessage response = _httpClient.PostAsJsonAsync(
                    $"{normalizedServerUrl}/api/dungeons/{Uri.EscapeDataString(normalizedInstanceId)}/participants/join",
                    request)
                .GetAwaiter()
                .GetResult();
            DungeonApiResponse<DungeonInstanceInfo>? apiResponse = response.Content
                .ReadFromJsonAsync<DungeonApiResponse<DungeonInstanceInfo>>()
                .GetAwaiter()
                .GetResult();

            if (response.IsSuccessStatusCode && apiResponse != null && apiResponse.success)
                return true;

            error = apiResponse?.message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(error))
                error = $"联机副本服务返回 HTTP {(int)response.StatusCode}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryCloseInstance(
        string dungeonServerUrl,
        string instanceId,
        string reason,
        out string error)
    {
        error = string.Empty;

        string normalizedServerUrl = NormalizeServerUrl(dungeonServerUrl);
        string normalizedInstanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedServerUrl) || string.IsNullOrWhiteSpace(normalizedInstanceId))
        {
            error = "联机副本服务地址或副本标识为空";
            return false;
        }

        try
        {
            HttpResponseMessage response = _httpClient.PostAsJsonAsync(
                    $"{normalizedServerUrl}/api/dungeons/{Uri.EscapeDataString(normalizedInstanceId)}/close",
                    new CloseDungeonInstanceRequest { reason = reason })
                .GetAwaiter()
                .GetResult();
            DungeonApiResponse<DungeonInstanceInfo>? apiResponse = response.Content
                .ReadFromJsonAsync<DungeonApiResponse<DungeonInstanceInfo>>()
                .GetAwaiter()
                .GetResult();

            if (response.IsSuccessStatusCode && apiResponse != null && apiResponse.success)
                return true;

            error = apiResponse?.message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(error))
                error = $"联机副本服务返回 HTTP {(int)response.StatusCode}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string NormalizeServerUrl(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
    }

}
