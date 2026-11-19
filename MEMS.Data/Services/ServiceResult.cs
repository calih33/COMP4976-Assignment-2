namespace MEMS.Data.Services;

public enum ServiceStatus
{
    Ok,
    NotFound,
    Forbidden,
    Invalid,
}

/// <summary>
/// Outcome of a write operation. Admin shows <see cref="Errors"/> next to the form fields;
/// the API maps the status to 200/404/403/400 ProblemDetails.
/// </summary>
public class ServiceResult
{
    protected ServiceResult(ServiceStatus status, IReadOnlyDictionary<string, string[]>? errors = null)
    {
        Status = status;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public ServiceStatus Status { get; }

    /// <summary>Field name (e.g. "Name", "Photo") → messages. Empty unless <see cref="Status"/> is Invalid.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public bool Succeeded => Status == ServiceStatus.Ok;

    public static ServiceResult Ok() => new(ServiceStatus.Ok);
    public static ServiceResult NotFound() => new(ServiceStatus.NotFound);
    public static ServiceResult Forbidden() => new(ServiceStatus.Forbidden);
    public static ServiceResult Invalid(string field, string message) => new(ServiceStatus.Invalid, Single(field, message));
    public static ServiceResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ServiceStatus.Invalid, errors);

    protected static Dictionary<string, string[]> Single(string field, string message) => new() { [field] = [message] };
}

public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(ServiceStatus status, T? value, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(status, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value);
    public static new ServiceResult<T> NotFound() => new(ServiceStatus.NotFound, default);
    public static new ServiceResult<T> Forbidden() => new(ServiceStatus.Forbidden, default);
    public static new ServiceResult<T> Invalid(string field, string message) => new(ServiceStatus.Invalid, default, Single(field, message));
    public static new ServiceResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ServiceStatus.Invalid, default, errors);
}
