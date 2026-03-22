<script lang="ts">
	import type { PageData, ActionData } from "./$types";

	let { data, form }: { data: PageData; form: ActionData } = $props();
</script>

{#if form?.success}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4">
		<h1 class="text-2xl font-bold">Connected</h1>
		<p class="text-muted-foreground">Your chat account is now linked to Nocturne. You can close this tab.</p>
	</div>
{:else}
	<div class="flex flex-col items-center justify-center min-h-screen gap-4">
		<h1 class="text-2xl font-bold">Connect Chat Account</h1>
		<p class="text-muted-foreground">
			Allow the Nocturne bot to access your glucose data and send you alerts?
		</p>
		{#if form?.error}
			<p class="text-destructive">{form.error}</p>
		{/if}
		<form method="POST">
			<input type="hidden" name="state" value={data.state} />
			<button
				type="submit"
				class="px-6 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
			>
				Authorize
			</button>
		</form>
	</div>
{/if}
