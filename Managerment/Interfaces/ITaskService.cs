using Managerment.DTO;
using Managerment.Services;

namespace Managerment.Interfaces
{
    public interface ITaskService
    {
        Task<ServiceResult<List<object>>> GetAllTasksAsync();
        Task<ServiceResult<List<object>>> GetMyTasksAsync(int userId);
        Task<ServiceResult<object>> GetTaskByIdAsync(int id);
        Task<ServiceResult<object>> CreateTaskAsync(TaskCreateDTO dto);
        Task<ServiceResult<object>> UpdateTaskAsync(int id, TaskUpdateDTO dto);
        Task<ServiceResult<object>> AssignTaskAsync(TaskAssignDTO dto);
        Task<ServiceResult<object>> DeleteTaskAsync(int id);
        Task<ServiceResult<List<object>>> GetAllUsersAsync();
        Task<ServiceResult<object>> SearchTasksAsync(TaskFilterDTO filter);
    }
}
