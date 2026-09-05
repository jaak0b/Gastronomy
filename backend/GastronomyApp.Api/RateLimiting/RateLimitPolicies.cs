using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using GastronomyApp.Api.Auth;
using GastronomyApp.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace GastronomyApp.Api.RateLimiting;

public sealed record RateLimitPolicyNames
{
  public string PerDevice { get; } = "per-device";

  public string PerAddress { get; } = "per-address";
}

public sealed class RateLimitPolicies
{
  private const int DeviceRequestsPerMinute = 600;
  private const int AddressRequestsPerMinute = 20;
  private const string UnknownPartitionKey = "unknown";
  private readonly DeviceClaimTypes _claimTypes = new();

  private readonly RateLimitPolicyNames _policyNames = new();

  public void Configure(RateLimiterOptions options)
  {
    options.AddPolicy(_policyNames.PerDevice,
                      httpContext => Partition(DevicePartitionKeyFor(httpContext), DeviceRequestsPerMinute));

    options.AddPolicy(_policyNames.PerAddress,
                      httpContext => Partition(AddressPartitionKeyFor(httpContext), AddressRequestsPerMinute));

    options.OnRejected = async (context, cancellationToken) =>
                         {
                           context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                           context.HttpContext.Response.ContentType = "application/json";

                           ApiError error = new()
                                            {
                                              Code = "TooManyRequests",
                                              MessageKey = "session.tooManyRequests",
                                              Parameters = new Dictionary<string, string>()
                                            };

                           await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(error,
                                                                                                  new JsonSerializerOptions
                                                                                                  {
                                                                                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                                                                                                  }),
                                                                         cancellationToken);
                         };
  }

  private RateLimitPartition<string> Partition(string partitionKey, int permitLimit)
  {
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
                                                    _ => new()
                                                         {
                                                           PermitLimit = permitLimit,
                                                           Window = TimeSpan.FromMinutes(1),
                                                           QueueLimit = 0,
                                                           QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                                           AutoReplenishment = true
                                                         });
  }

  private string DevicePartitionKeyFor(HttpContext httpContext)
  {
    var deviceId = httpContext.User.FindFirst(_claimTypes.DeviceId)?.Value;

    return deviceId ?? AddressPartitionKeyFor(httpContext);
  }

  private string AddressPartitionKeyFor(HttpContext httpContext)
  {
    return httpContext.Connection.RemoteIpAddress?.ToString()
           ?? string.Create(CultureInfo.InvariantCulture, $"{UnknownPartitionKey}");
  }
}
