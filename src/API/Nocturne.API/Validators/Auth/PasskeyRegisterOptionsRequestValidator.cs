using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="PasskeyRegisterOptionsRequest"/> for the passkey registration options (WebAuthn challenge) endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Username is required and capped at 200 characters.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="PasskeyRegisterOptionsRequest"/>
/// <seealso cref="PasskeyController"/>
public class PasskeyRegisterOptionsRequestValidator : AbstractValidator<PasskeyRegisterOptionsRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasskeyRegisterOptionsRequestValidator"/> class
    /// and configures all validation rules for passkey registration options.
    /// </summary>
    public PasskeyRegisterOptionsRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
    }
}
