namespace MlxPep.Core;

/// <summary>
/// Generic result type for operations that may succeed or fail with semantic error handling.
/// Provides explicit error context instead of silent failures.
/// </summary>
/// <typeparam name="T">The type of data returned on success.</typeparam>
public record Result<T>(bool Success, T? Data = default, string? Error = null)
{
    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static Result<T> Ok(T data) => new(true, data, null);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result<T> Fail(string error) => new(false, default, error);

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result<T> Fail(Exception ex) => new(false, default, $"{ex.GetType().Name}: {ex.Message}");
}
