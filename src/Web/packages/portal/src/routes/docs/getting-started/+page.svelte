<script lang="ts">
    import { Button } from "@nocturne/app/ui/button";
    import { ArrowRight } from "@lucide/svelte";
</script>

<div class="max-w-3xl">
    <h1 class="text-4xl font-bold tracking-tight mb-4">Quick Start</h1>
    <p class="text-lg text-muted-foreground mb-8">
        Get Nocturne up and running in just a few minutes.
    </p>

    <h2 class="text-2xl font-bold mt-8 mb-4">Prerequisites</h2>
    <p class="mb-4">Before you begin, make sure you have:</p>
    <ul class="list-disc list-inside space-y-2 text-muted-foreground mb-8">
        <li>Docker and Docker Compose installed</li>
        <li>A server or VPS with at least 1 GB RAM</li>
        <li>A domain name (recommended) or static IP</li>
    </ul>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 1: Create your project</h2>
    <pre class="p-4 rounded-lg bg-muted/50 border border-border/60 overflow-x-auto text-sm mb-8"><code>mkdir nocturne && cd nocturne</code></pre>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 2: Deploy</h2>
    <p class="text-muted-foreground mb-4">
        Follow one of our installation guides to set up the Docker Compose stack and
        configure your environment variables:
    </p>
    <div class="not-prose mb-8">
        <Button href="/docs/installation" class="gap-2">
            View Installation Guides
            <ArrowRight class="w-4 h-4" />
        </Button>
    </div>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 3: Access Your Instance</h2>
    <p class="text-muted-foreground mb-4">
        Once the containers are running, access your Nocturne instance at:
    </p>
    <pre
        class="p-4 rounded-lg bg-muted/50 border border-border/60 overflow-x-auto text-sm"><code>https://your-server-ip:8443</code></pre>

    <h2 class="text-2xl font-bold mt-8 mb-4">Next Steps</h2>
    <ul class="list-disc list-inside space-y-2 text-muted-foreground">
        <li>Configure your data connectors (Dexcom, LibreLinkUp, etc.)</li>
        <li>Set up HTTPS with a reverse proxy</li>
        <li>Configure your CGM app to point to your new instance</li>
    </ul>

    <div class="mt-12 flex gap-4 not-prose">
        <Button href="/docs/installation" variant="outline" class="gap-2">
            Full Installation Guide
            <ArrowRight class="w-4 h-4" />
        </Button>
    </div>
</div>
