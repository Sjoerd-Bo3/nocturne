using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="PasskeyLoginOptionsRequest"/> for the passkey login options (WebAuthn challenge) endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Username is required and capped at 200 characters.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="PasskeyLoginOptionsRequest"/>
/// <seealso cref="PasskeyController"/>
public class PasskeyLoginOptionsRequestValidator : AbstractValidator<PasskeyLoginOptionsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasskeyLoginOptionsRequestValidator"/> class
    /// and configures all validation rules for passkey login options.
    /// </summary>
    public PasskeyLoginOptionsRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
    }
}
