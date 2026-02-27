<script lang="ts">
    import "../app.css";
    import SiteHeader from "$lib/components/SiteHeader.svelte";
    import SiteFooter from "$lib/components/SiteFooter.svelte";

    let { children } = $props();
</script>

<div
    class="min-h-screen flex flex-col bg-linear-to-br from-background via-background to-primary/5"
>
    <SiteHeader />

    <main class="flex-1">
        {@render children()}
    </main>

    <SiteFooter />
</div>
