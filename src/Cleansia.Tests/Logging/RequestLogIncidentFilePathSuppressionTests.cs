using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// The incident file is the whole subject in one response — identity, orders, dispute text, the
/// trail — as PDF bytes, and the request names the subject and an order in its path. Both stay out of
/// every host's request log through the <c>gdpr/</c> rule that already covers the JSON export beside
/// it; this pins that the new route falls under it rather than assuming so.
/// </summary>
public sealed class RequestLogIncidentFilePathSuppressionTests
{
    [Theory]
    [InlineData("/api/v1/AdminGdpr/incident-file/user-1")]
    [InlineData("/api/v1/admingdpr/incident-file/user-1")]
    public void The_Incident_File_Route_Is_Suppressed_On_Every_Host(string route)
    {
        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware)
        {
            var suppressed = (bool)middleware
                .GetMethod("IsSensitivePath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new PathString(route)])!;

            Assert.True(suppressed, $"{route} must be in IsSensitivePath on {middleware.FullName}");
        }
    }
}
