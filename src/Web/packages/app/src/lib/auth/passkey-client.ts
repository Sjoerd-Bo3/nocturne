/**
 * Passkey Client
 *
 * Client-side functions for WebAuthn passkey authentication.
 * These call the /api/auth/passkey/* endpoints directly through the SvelteKit proxy
 * so that the challenge cookie round-trips between the browser and the backend.
 *
 * The WebAuthn ceremony (navigator.credentials.create/get) must happen in the browser,
 * so these cannot be server-side remote functions.
 */

import {
  startAuthentication,
  startRegistration,
} from "@simplewebauthn/browser";
import type {
  PublicKeyCredentialCreationOptionsJSON,
  PublicKeyCredentialRequestOptionsJSON,
} from "@simplewebauthn/browser";

// ============================================================================
// Types
// ============================================================================

export interface PasskeyLoginResult {
  success: boolean;
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  refreshExpiresIn?: number;
  error?: string;
}

export interface PasskeyRegistrationResult {
  success: boolean;
  credentialId?: string;
  recoveryCodes?: string[];
  error?: string;
}

export interface RecoveryCodeResult {
  success: boolean;
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  refreshExpiresIn?: number;
  error?: string;
}

// ============================================================================
// Discoverable Login (no username required)
// ============================================================================

/**
 * Perform a discoverable (resident key) passkey login.
 * The browser will show a credential picker for all registered passkeys.
 */
export async function discoverableLogin(): Promise<PasskeyLoginResult> {
  // Step 1: Get assertion options from the server
  const optionsResponse = await fetch("/api/auth/passkey/login/discoverable/options", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({}),
  });

  if (!optionsResponse.ok) {
    const body = await optionsResponse.text();
    return { success: false, error: body || "Failed to get login options" };
  }

  const options: PublicKeyCredentialRequestOptionsJSON = await optionsResponse.json();

  // Step 2: Perform the WebAuthn ceremony in the browser
  let assertionResponse;
  try {
    assertionResponse = await startAuthentication({ optionsJSON: options });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Authentication was cancelled";
    return { success: false, error: message };
  }

  // Step 3: Send the assertion response to the server for verification
  // The challenge cookie is automatically included by the browser
  const verifyResponse = await fetch("/api/auth/passkey/login/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ assertionResponseJson: JSON.stringify(assertionResponse) }),
  });

  if (!verifyResponse.ok) {
    const body = await verifyResponse.text();
    return { success: false, error: body || "Login verification failed" };
  }

  return await verifyResponse.json();
}

// ============================================================================
// Non-Discoverable Login (username required)
// ============================================================================

/**
 * Perform a non-discoverable passkey login for a specific username.
 * The server provides allowed credential IDs for the given user.
 */
export async function usernameLogin(username: string): Promise<PasskeyLoginResult> {
  // Step 1: Get assertion options for this user
  const optionsResponse = await fetch("/api/auth/passkey/login/options", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username }),
  });

  if (!optionsResponse.ok) {
    const body = await optionsResponse.text();
    return { success: false, error: body || "Failed to get login options" };
  }

  const options: PublicKeyCredentialRequestOptionsJSON = await optionsResponse.json();

  // Step 2: Perform the WebAuthn ceremony
  let assertionResponse;
  try {
    assertionResponse = await startAuthentication({ optionsJSON: options });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Authentication was cancelled";
    return { success: false, error: message };
  }

  // Step 3: Verify with the server
  const verifyResponse = await fetch("/api/auth/passkey/login/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ assertionResponseJson: JSON.stringify(assertionResponse) }),
  });

  if (!verifyResponse.ok) {
    const body = await verifyResponse.text();
    return { success: false, error: body || "Login verification failed" };
  }

  return await verifyResponse.json();
}

// ============================================================================
// Registration
// ============================================================================

/**
 * Register a new passkey for a user.
 * The subjectId comes from the current session (for existing users)
 * or from the invite acceptance flow (for new users).
 */
export async function registerPasskey(
  subjectId: string,
  username: string,
  label?: string
): Promise<PasskeyRegistrationResult> {
  // Step 1: Get registration options from the server
  const optionsResponse = await fetch("/api/auth/passkey/register/options", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ subjectId, username }),
  });

  if (!optionsResponse.ok) {
    const body = await optionsResponse.text();
    return { success: false, error: body || "Failed to get registration options" };
  }

  const options: PublicKeyCredentialCreationOptionsJSON = await optionsResponse.json();

  // Step 2: Perform the WebAuthn ceremony
  let attestationResponse;
  try {
    attestationResponse = await startRegistration({ optionsJSON: options });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Registration was cancelled";
    return { success: false, error: message };
  }

  // Step 3: Complete registration with the server
  const verifyResponse = await fetch("/api/auth/passkey/register/complete", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      attestationResponseJson: JSON.stringify(attestationResponse),
      label: label || undefined,
    }),
  });

  if (!verifyResponse.ok) {
    const body = await verifyResponse.text();
    return { success: false, error: body || "Registration verification failed" };
  }

  return await verifyResponse.json();
}

// ============================================================================
// Recovery Code
// ============================================================================

/**
 * Authenticate using a recovery code instead of a passkey.
 */
export async function verifyRecoveryCode(
  username: string,
  code: string
): Promise<RecoveryCodeResult> {
  const response = await fetch("/api/auth/passkey/recovery/verify", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, code }),
  });

  if (!response.ok) {
    const body = await response.text();
    return { success: false, error: body || "Recovery code verification failed" };
  }

  return await response.json();
}

// ============================================================================
// Invite Registration
// ============================================================================

/**
 * Accept an invite and register a passkey in one flow.
 * This creates the user account from the invite token and registers their first passkey.
 */
export async function acceptInviteWithPasskey(
  token: string,
  username: string,
  displayName: string,
  label?: string
): Promise<PasskeyRegistrationResult> {
  // Step 1: Accept the invite and create the user account
  const acceptResponse = await fetch(`/api/auth/passkey/invite/accept`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ token, username, displayName }),
  });

  if (!acceptResponse.ok) {
    const body = await acceptResponse.text();
    return { success: false, error: body || "Failed to accept invite" };
  }

  const acceptResult: { subjectId: string } = await acceptResponse.json();

  // Step 2: Register a passkey for the new user
  return await registerPasskey(acceptResult.subjectId, username, label);
}
