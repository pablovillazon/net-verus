namespace Verus.Companies.Application.Validation;

/// <summary>
/// Raised when a request violates a validation rule. The API layer translates it into a
/// 400 Bad Request ValidationProblemDetails; the application layer knows nothing about HTTP.
/// </summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(IDictionary<string, string[]> errors)
        : base(string.Join(" ", errors.SelectMany(e => e.Value)))
    {
        Errors = errors;
    }

    public RequestValidationException(string member, string message)
        : this(new Dictionary<string, string[]> { [member] = [message] })
    {
    }

    public IDictionary<string, string[]> Errors { get; }
}
