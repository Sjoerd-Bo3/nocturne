<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Check, Copy, ShieldCheck } from "lucide-svelte";

  interface Props {
    codes: string[];
    onContinue: () => void;
    continueLabel?: string;
  }

  let { codes, onContinue, continueLabel = "Continue" }: Props = $props();

  let codesCopied = $state(false);

  async function copyRecoveryCodes() {
    const text = codes.join("\n");
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      // Clipboard API not available — treat as copied anyway
    }
    codesCopied = true;
  }
</script>

<div class="space-y-4">
  <div class="space-y-3">
    <div class="flex items-center gap-2">
      <ShieldCheck class="h-5 w-5 text-primary" />
      <h3 class="font-medium">Recovery Codes</h3>
    </div>
    <p class="text-sm text-muted-foreground">
      Save these recovery codes in a safe place. If you lose access to your
      passkey, you can use one of these codes to sign in. Each code can only be
      used once.
    </p>

    {#if codes.length > 0}
      <div class="grid grid-cols-2 gap-2 rounded-lg border bg-muted/50 p-4">
        {#each codes as code}
          <code
            class="rounded bg-background px-2 py-1 text-center text-sm font-mono"
          >
            {code}
          </code>
        {/each}
      </div>

      <Button
        variant={codesCopied ? "outline" : "default"}
        class="w-full"
        onclick={copyRecoveryCodes}
      >
        {#if codesCopied}
          <Check class="mr-2 h-4 w-4" />
          Codes copied
        {:else}
          <Copy class="mr-2 h-4 w-4" />
          Copy recovery codes
        {/if}
      </Button>

      {#if !codesCopied}
        <p class="text-center text-xs text-muted-foreground">
          You must copy your recovery codes before continuing.
        </p>
      {/if}
    {:else}
      <p class="text-sm text-muted-foreground">
        No recovery codes were returned. You can generate new ones later from
        your account settings.
      </p>
    {/if}
  </div>

  <Button
    class="w-full"
    size="lg"
    disabled={codes.length > 0 && !codesCopied}
    onclick={onContinue}
  >
    {continueLabel}
  </Button>
</div>
