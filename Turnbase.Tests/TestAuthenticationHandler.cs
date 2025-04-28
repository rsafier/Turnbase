using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System;

namespace Turnbase.Tests
{
    public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TimeProvider timeProvider)
            : base(options, logger, encoder, timeProvider)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Allow anonymous access for testing purposes
            // Use a deterministic identifier for the connection
            var connectionId = Context.Items.TryGetValue("ConnectionId", out var id) ? id?.ToString() : $"TestConnection_Player{Context.Items.Count + 1}";
            Console.WriteLine($"Assigning user ID: {connectionId} for authentication");
            var claims = new[] { new Claim(ClaimTypes.Name, connectionId ?? "Unknown") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public static class TestAuthenticationExtensions
    {
        public static AuthenticationBuilder AddTestAuth(this AuthenticationBuilder builder, Action<AuthenticationSchemeOptions> configureOptions)
        {
            return builder.AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", configureOptions);
        }
    }
}
