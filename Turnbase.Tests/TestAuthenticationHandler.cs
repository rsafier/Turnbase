using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Turnbase.Tests
{
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

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Allow anonymous access for testing purposes
            // Use a unique identifier for the connection since we can't access ConnectionId directly
            var connectionId = Context.Items.ContainsKey("ConnectionId") ? Context.Items["ConnectionId"].ToString() : "TestConnection_" + Guid.NewGuid().ToString();
            var claims = new[] { new Claim(ClaimTypes.Name, connectionId) };
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
