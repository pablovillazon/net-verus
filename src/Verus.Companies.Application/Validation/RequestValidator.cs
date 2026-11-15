using System.ComponentModel.DataAnnotations;

namespace Verus.Companies.Application.Validation;

/// <summary>
/// Runs DataAnnotations on a request inside the application layer. ASP.NET Core validates the same
/// attributes at the HTTP boundary; validating here too means the service enforces its own rules no
/// matter who calls it (controller, background job, test).
/// </summary>
internal static class RequestValidator
{
    public static void ValidateOrThrow(object request)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            return;
        }

        var errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty)
                .Select(member => (Member: member, Message: r.ErrorMessage ?? "The value is invalid.")))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).Distinct().ToArray());

        throw new RequestValidationException(errors);
    }
}
