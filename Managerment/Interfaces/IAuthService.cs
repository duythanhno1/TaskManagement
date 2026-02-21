using Managerment.DTO;
using Managerment.Services;

namespace Managerment.Interfaces
{
    public interface IAuthService
    {
        Task<ServiceResult<object>> RegisterAsync(RegisterDTO dto);
        Task<ServiceResult<object>> LoginAsync(LoginDTO dto);
        Task<ServiceResult<object>> RefreshTokenAsync(string refreshToken);
        Task<ServiceResult<object>> RevokeTokenAsync(int userId);
    }
}
