using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="PasskeyRegisterCompleteRequest"/> for the passkey registration completion (WebAuthn attestation verification) step.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>AttestationResponseJson must be non-empty (contains the WebAuthn attestation response).</description></item>
/// <item><description>Label, when provided, is capped at 200 characters (user-facing credential name).</description></item>
/// </list>
/// </remarks>
/// <seealso cref="PasskeyRegisterCompleteRequest"/>
/// <seealso cref="PasskeyController"/>
public class PasskeyRegisterCompleteRequestValidator : AbstractValidator<PasskeyRegisterCompleteRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PasskeyRegisterCompleteRequestValidator"/> class
    /// and configures all validation rules for passkey registration completion.
    /// </summary>
    public PasskeyRegisterCompleteRequestValidator()
    {
        RuleFor(x => x.AttestationResponseJson).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(200).When(x => x.Label is not null);
    }
}
