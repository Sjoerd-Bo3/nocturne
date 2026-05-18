using FluentValidation;
using Nocturne.API.Controllers.V4.PlatformAdmin;

namespace Nocturne.API.Validators.Admin;

/// <summary>
/// Validates <see cref="ApproveAccessRequestRequest"/> for the platform admin access request approval endpoint.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>At least one role or one direct permission must be granted when approving an access request.</description></item>
/// </list>
/// </remarks>
/// <seealso cref="ApproveAccessRequestRequest"/>
/// <seealso cref="AccessRequestController"/>
public class ApproveAccessRequestRequestValidator : AbstractValidator<ApproveAccessRequestRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApproveAccessRequestRequestValidator"/> class
    /// and configures all validation rules for access request approval.
    /// </summary>
    public ApproveAccessRequestRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.RoleIds.Count > 0 || (x.DirectPermissions != null && x.DirectPermissions.Count > 0))
            .WithMessage("At least one role or direct permission is required.");
    }
}
