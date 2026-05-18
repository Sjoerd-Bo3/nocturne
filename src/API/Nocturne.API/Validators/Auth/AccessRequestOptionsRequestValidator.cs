using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="AccessRequestOptionsRequest"/> for the access request options (WebAuthn challenge) endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>DisplayName is required and capped at 255 characters.</description></item>
/// <item><description>Message is capped at 500 characters (optional note to the tenant owner).</description></item>
/// </list>
/// </remarks>
/// <seealso cref="AccessRequestOptionsRequest"/>
/// <seealso cref="PasskeyController"/>
public class AccessRequestOptionsRequestValidator : AbstractValidator<AccessRequestOptionsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessRequestOptionsRequestValidator"/> class
    /// and configures all validation rules for access request options.
    /// </summary>
    public AccessRequestOptionsRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Message).MaximumLength(500);
    }
}
