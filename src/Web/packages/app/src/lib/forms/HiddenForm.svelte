<script lang="ts" module>
	export interface HiddenFormResult {
		ok: boolean;
		result?: unknown;
		error?: string;
	}
</script>

<script lang="ts">
	import { tick } from 'svelte';
	import { serializeFormData } from './serialize';

	interface Props {
		/** The form() remote function (e.g., createTherapySettings or updateTherapySettings.for(id)) */
		remote: any;
		/** Reactive data getter — called at submit time to capture current values */
		data: () => Record<string, unknown>;
	}

	let { remote, data }: Props = $props();

	let formEl = $state<HTMLFormElement | null>(null);
	let entries = $state<Array<[string, string]>>([]);
	let pendingResolve: ((value: HiddenFormResult) => void) | null = null;

	/**
	 * Submit the hidden form programmatically.
	 * Returns a promise that resolves when the remote function completes.
	 */
	export async function submit(): Promise<HiddenFormResult> {
		// Serialize current data into form entries
		entries = serializeFormData(data());

		// Wait for Svelte to render the hidden inputs
		await tick();

		return new Promise<HiddenFormResult>((resolve) => {
			pendingResolve = resolve;
			formEl?.requestSubmit();
		});
	}

	// Build the enhance attributes from the remote function
	const formAttrs = $derived(
		remote.enhance(async ({ submit: doSubmit }: { submit: () => Promise<void> }) => {
			try {
				await doSubmit();
				const result = remote.result;
				if (pendingResolve) {
					pendingResolve({ ok: true, result });
					pendingResolve = null;
				}
			} catch (err: unknown) {
				const message = err instanceof Error ? err.message : String(err);
				if (pendingResolve) {
					pendingResolve({ ok: false, error: message });
					pendingResolve = null;
				}
			}
		})
	);
</script>

<form bind:this={formEl} {...formAttrs} class="hidden">
	{#each entries as [name, value]}
		<input type="hidden" {name} {value} />
	{/each}
</form>
