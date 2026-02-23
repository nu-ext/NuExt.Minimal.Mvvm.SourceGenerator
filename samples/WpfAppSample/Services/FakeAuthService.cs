using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfAppSample.Services
{
    public sealed class FakeAuthService : IAuthService
    {
        public Task<bool> IsUserLockedAsync(string userName, CancellationToken ct)
            => ct.IsCancellationRequested 
            ? Task.FromCanceled<bool>(ct) 
            : Task.FromResult(string.Equals(userName, "locked", StringComparison.OrdinalIgnoreCase));

        public async Task<bool> LoginAsync(string userName, string password, bool rememberMe, CancellationToken ct)
        {
            await Task.Delay(500, ct);
            return (Matches(userName, "admin") && Matches(password, "admin"))
                || (Matches(userName, "user") && Matches(password, "pass"));

            static bool Matches(string input, string pattern)
            {
                return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
