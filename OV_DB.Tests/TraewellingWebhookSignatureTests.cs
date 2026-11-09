using System;
using System.Security.Cryptography;
using System.Text;
using OV_DB.Controllers;
using Xunit;

namespace OV_DB.Tests;

/// <summary>
/// The webhook endpoint is anonymous; the HMAC signature is the only thing between it and anyone
/// who can guess a webhook id.
/// </summary>
public class TraewellingWebhookSignatureTests
{
    private const string Secret = "per-webhook-secret";
    private const string Body = "{\"event\":\"checkin_create\",\"status\":{\"id\":1}}";

    private static string Sign(string secret, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    [Fact]
    public void AcceptsTheSignatureTraewellingSends()
    {
        Assert.True(TraewellingController.IsValidSignature(Secret, Body, Sign(Secret, Body)));
    }

    [Theory]
    [InlineData("other-secret", Body)]
    [InlineData(Secret, "{\"event\":\"checkin_delete\",\"status\":{\"id\":1}}")]
    public void RejectsASignatureForAnotherSecretOrBody(string signingSecret, string signedBody)
    {
        Assert.False(TraewellingController.IsValidSignature(Secret, Body, Sign(signingSecret, signedBody)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("deadbeef")]
    public void RejectsMissingOrMalformedSignatures(string header)
    {
        Assert.False(TraewellingController.IsValidSignature(Secret, Body, header));
    }
}
