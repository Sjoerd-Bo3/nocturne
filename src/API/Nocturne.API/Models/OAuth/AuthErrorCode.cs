using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// Standard error codes returned by the authentication flow,
/// passed as the ?error= query parameter to /auth/error.
/// Covers both OAuth 2.0 RFC 6749 codes and Nocturne-specific codes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuthErrorCode>))]
public enum AuthErrorCode
{
    [EnumMember(Value = "invalid_state"), JsonStringEnumMemberName("invalid_state")]
    InvalidState,

    [EnumMember(Value = "invalid_intent"), JsonStringEnumMemberName("invalid_intent")]
    InvalidIntent,

    [EnumMember(Value = "identity_already_linked"), JsonStringEnumMemberName("identity_already_linked")]
    IdentityAlreadyLinked,

    [EnumMember(Value = "access_denied"), JsonStringEnumMemberName("access_denied")]
    AccessDenied,

    [EnumMember(Value = "invalid_request"), JsonStringEnumMemberName("invalid_request")]
    InvalidRequest,

    [EnumMember(Value = "unauthorized_client"), JsonStringEnumMemberName("unauthorized_client")]
    UnauthorizedClient,

    [EnumMember(Value = "unsupported_response_type"), JsonStringEnumMemberName("unsupported_response_type")]
    UnsupportedResponseType,

    [EnumMember(Value = "invalid_scope"), JsonStringEnumMemberName("invalid_scope")]
    InvalidScope,

    [EnumMember(Value = "server_error"), JsonStringEnumMemberName("server_error")]
    ServerError,

    [EnumMember(Value = "temporarily_unavailable"), JsonStringEnumMemberName("temporarily_unavailable")]
    TemporarilyUnavailable,

    [EnumMember(Value = "callback_failed"), JsonStringEnumMemberName("callback_failed")]
    CallbackFailed,

    [EnumMember(Value = "provider_error"), JsonStringEnumMemberName("provider_error")]
    ProviderError,

    [EnumMember(Value = "oidc_disabled"), JsonStringEnumMemberName("oidc_disabled")]
    OidcDisabled,
}
