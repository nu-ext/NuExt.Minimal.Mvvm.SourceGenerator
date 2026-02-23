using System.Threading;
using System.Threading.Tasks;

namespace WpfAppSample.Services
{
    public interface IAuthService
    {
        Task<bool> IsUserLockedAsync(string userName, CancellationToken ct);
        Task<bool> LoginAsync(string userName, string password, bool rememberMe, CancellationToken ct);
    }
}
