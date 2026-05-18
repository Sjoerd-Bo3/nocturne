using FluentValidation;
using Nocturne.API.Controllers.Authentication;

namespace Nocturne.API.Validators.Auth;

/// <summary>
/// Validates <see cref="RecoveryVerifyRequest"/> for the account recovery verification endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>Username is required and capped at 200 characters.</description></item>
/// <item><description>Code is required and capped at 50 characters.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="RecoveryVerifyRequest"/>
/// <seealso cref="PasskeyController"/>
public class RecoveryVerifyRequestValidator : AbstractValidator<RecoveryVerifyRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryVerifyRequestValidator"/> class
    /// and configures all validation rules for recovery verification.
    /// </summary>
    public RecoveryVerifyRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
