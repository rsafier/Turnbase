using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System;

namespace Turnbase.Tests
{
#pragma warning disable CS0618 // Suppress obsolete warning for ISystemClock
    public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        private static int _connectionCounter = 0;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Allow anonymous access for testing purposes
            // Use a static counter to assign player IDs, resetting for each test run if needed
            var playerNumber = Interlocked.Increment(ref _connectionCounter);
            // Ensure only two players for testing by cycling between 1 and 2
            var adjustedPlayerNumber = (playerNumber % 2 == 0) ? 2 : 1;
            var userId = $"TestConnection_Player{adjustedPlayerNumber}";
            Console.WriteLine($"Assigning user ID: {userId} for connection attempt {playerNumber}");
            var claims = new[] { new Claim(ClaimTypes.Name, userId) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
#pragma warning restore CS0618 // Restore warning after class definition

    public static class TestAuthenticationExtensions
    {
        public static AuthenticationBuilder AddTestAuth(this AuthenticationBuilder builder, Action<AuthenticationSchemeOptions> configureOptions)
        {
            return builder.AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TestScheme", configureOptions);
        }
    }
}
