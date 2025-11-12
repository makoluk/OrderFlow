namespace OrderFlow.Shared.Http;

public class BaseResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? TraceId { get; set; }
    public string[]? Errors { get; set; }

    public static BaseResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message ?? "OK" };

    public static BaseResponse<T> Fail(string message, params string[] errors) =>
        new() { Success = false, Message = message, Errors = errors };
}

