using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="PasskeyLoginCompleteRequest"/> for the passkey login completion (WebAuthn assertion verification) step.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>AssertionResponseJson must be non-empty (contains the WebAuthn assertion response).</description></item>
/// </list>
/// </remarks>
/// <seealso cref="PasskeyLoginCompleteRequest"/>
/// <seealso cref="PasskeyController"/>
public class PasskeyLoginCompleteRequestValidator : AbstractValidator<PasskeyLoginCompleteRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasskeyLoginCompleteRequestValidator"/> class
    /// and configures all validation rules for passkey login completion.
    /// </summary>
    public PasskeyLoginCompleteRequestValidator()
    {
        RuleFor(x => x.AssertionResponseJson).NotEmpty();
    }
}
