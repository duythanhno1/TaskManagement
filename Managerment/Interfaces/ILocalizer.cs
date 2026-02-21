namespace Managerment.Interfaces
{
    public interface ILocalizer
    {
        /// <summary>
        /// Get localized message by key. Supports format args: Get("task.assigned", userName)
        /// </summary>
        string Get(string key, params object[] args);
    }
}
