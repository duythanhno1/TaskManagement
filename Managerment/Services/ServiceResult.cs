namespace Managerment.Services
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }

        public static ServiceResult<T> Ok(T data, string message = null)
        {
            return new ServiceResult<T> { Success = true, StatusCode = 200, Data = data, Message = message };
        }

        public static ServiceResult<T> Created(T data, string message = null)
        {
            return new ServiceResult<T> { Success = true, StatusCode = 201, Data = data, Message = message };
        }

        public static ServiceResult<T> NotFound(string message = "Not found.")
        {
            return new ServiceResult<T> { Success = false, StatusCode = 404, Message = message };
        }

        public static ServiceResult<T> BadRequest(string message = "Bad request.")
        {
            return new ServiceResult<T> { Success = false, StatusCode = 400, Message = message };
        }

        public static ServiceResult<T> Conflict(string message = "Conflict.")
        {
            return new ServiceResult<T> { Success = false, StatusCode = 409, Message = message };
        }

        public static ServiceResult<T> Error(string message = "Internal server error.", int statusCode = 500)
        {
            return new ServiceResult<T> { Success = false, StatusCode = statusCode, Message = message };
        }
    }
}
