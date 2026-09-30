using System.Net;
using System.Text;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Cleansia.Core.Blobs.Abstractions;
using BlobContainerClient = Cleansia.Infra.Azure.Storage.Blobs.BlobContainerClient;

namespace Cleansia.Tests.Infrastructure;

/// <summary>
/// The read links a managed-identity host mints. The prod account refuses shared keys (E-4), so an
/// account-key SAS would be a link the storage service rejects: the client the factory builds from
/// <c>BlobContainerConfiguration:AccountUrl</c> must sign with a user-delegation key it asks the service
/// for. DEV never takes this path — its connection string carries the key — so nothing else runs it
/// before production does.
/// </summary>
public class BlobUserDelegationSasTests
{
    private const string BlobName = "2024/order-1/order-1_Before_20240101120000_ab12cd34.jpg";

    [Fact]
    public void A_managed_identity_client_signs_with_a_user_delegation_key()
    {
        var storage = new UserDelegationKeyEndpoint();
        var container = new global::Azure.Storage.Blobs.BlobContainerClient(
            new Uri("https://stcleansiaweuprod.blob.core.windows.net/order-photos"),
            new FixedTokenCredential(),
            new BlobClientOptions { Transport = new HttpClientTransport(storage), Retry = { MaxRetries = 0 } });

        var link = new BlobContainerClient(container)
            .GenerateSasUri(BlobName, TimeSpan.FromMinutes(15), ServedContentType.ForRecordedType("image/jpeg"));

        var query = QueryOf(link);
        Assert.Equal("the-identity-object-id", query["skoid"]);
        Assert.Equal("the-tenant-id", query["sktid"]);
        Assert.Equal("b", query["sr"]);
        Assert.Equal("r", query["sp"]);
        Assert.Equal("image/jpeg", query["rsct"]);
        Assert.False(string.IsNullOrEmpty(query["sig"]));

        var request = Assert.Single(storage.Requests);
        Assert.Contains("comp=userdelegationkey", request.Query, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> QueryOf(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => Uri.UnescapeDataString((parts.Length > 1 ? parts[1] : string.Empty).Replace('+', ' ')));

    private sealed class FixedTokenCredential : TokenCredential
    {
        private static readonly AccessToken Token = new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => Token;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Token);
    }

    private sealed class UserDelegationKeyEndpoint : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var now = DateTimeOffset.UtcNow;
            var body = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <UserDelegationKey>
                  <SignedOid>the-identity-object-id</SignedOid>
                  <SignedTid>the-tenant-id</SignedTid>
                  <SignedStart>{now.AddMinutes(-5).UtcDateTime:s}Z</SignedStart>
                  <SignedExpiry>{now.AddMinutes(15).UtcDateTime:s}Z</SignedExpiry>
                  <SignedService>b</SignedService>
                  <SignedVersion>2025-01-05</SignedVersion>
                  <Value>{Convert.ToBase64String(Encoding.UTF8.GetBytes("a-thirty-two-byte-signing-key!!!"))}</Value>
                </UserDelegationKey>
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }
}
