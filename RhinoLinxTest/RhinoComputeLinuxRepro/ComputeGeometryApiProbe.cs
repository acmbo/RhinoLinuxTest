using System.Net;
using System.Text;
using System.Text.Json;

namespace RhinoComputeLinuxRepro;

/// <summary>
/// Calls the public Rhino.Compute Mesh.CreateFromBox endpoint without loading Rhino locally.
/// </summary>
public static class ComputeGeometryApiProbe
{
    public const string CreateFromBoxPath = "/rhino/geometry/mesh/createfrombox-boundingbox_int_int_int";

    // This is Compute's JSON-array argument convention: BoundingBox, xCount, yCount, zCount.
    private const string CreateFromBoxPayload =
        "[{\"Min\":{\"X\":0,\"Y\":0,\"Z\":0},\"Max\":{\"X\":10,\"Y\":20,\"Z\":30}},1,1,1]";

    public static async Task<ComputeApiCallResult> CreateMeshFromBoxAsync(
        Uri computeBaseUri,
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(computeBaseUri);

        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        try
        {
            var endpoint = new Uri(computeBaseUri, CreateFromBoxPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(CreateFromBoxPayload, Encoding.UTF8, "application/json")
            };
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var serializedMeshReturned = false;
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(responseBody);
                serializedMeshReturned = document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(data.GetString());
            }

            return new ComputeApiCallResult(
                endpoint,
                response.StatusCode,
                responseBody,
                response.IsSuccessStatusCode && serializedMeshReturned);
        }
        finally
        {
            if (ownsClient)
            {
                httpClient.Dispose();
            }
        }
    }
}

public sealed record ComputeApiCallResult(
    Uri Endpoint,
    HttpStatusCode StatusCode,
    string ResponseBody,
    bool SerializedMeshReturned);
