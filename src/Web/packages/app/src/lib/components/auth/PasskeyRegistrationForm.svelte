<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Fingerprint, Loader2 } from "lucide-svelte";

  interface Props {
    onRegister: (username: string, displayName: string) => Promise<void>;
    disabled?: boolean;
    isRegistering?: boolean;
  }

  let {
    onRegister,
    disabled = false,
    isRegistering = false,
  }: Props = $props();

  let displayName = $state("");
  let username = $state("");

  const canRegister = $derived(
    displayName.trim().length > 0 && username.trim().length > 0,
  );

  async function handleSubmit() {
    if (!canRegister) return;
    await onRegister(username.trim(), displayName.trim());
  }
</script>

<div class="space-y-4">
  <div class="space-y-2">
    <Label for="display-name">Display name</Label>
    <Input
      id="display-name"
      type="text"
      placeholder="Your name"
      bind:value={displayName}
      disabled={disabled || isRegistering}
    />
    <p class="text-xs text-muted-foreground">
      This is how you will appear to others.
    </p>
  </div>

  <div class="space-y-2">
    <Label for="pk-username">Username</Label>
    <Input
      id="pk-username"
      type="text"
      placeholder="your-username"
      bind:value={username}
      disabled={disabled || isRegistering}
    />
    <p class="text-xs text-muted-foreground">
      A unique identifier for your account.
    </p>
  </div>

  <Button
    class="w-full"
    size="lg"
    disabled={!canRegister || disabled || isRegistering}
    onclick={handleSubmit}
  >
    {#if isRegistering}
      <Loader2 class="mr-2 h-5 w-5 animate-spin" />
      Waiting for passkey...
    {:else}
      <Fingerprint class="mr-2 h-5 w-5" />
      Create account with passkey
    {/if}
  </Button>
</div>
