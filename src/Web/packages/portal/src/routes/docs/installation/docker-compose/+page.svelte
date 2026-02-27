<script lang="ts">
    import SystemRequirements from "$lib/components/docs/SystemRequirements.svelte";
    import DockerComposeYaml from "$lib/components/docs/DockerComposeYaml.svelte";
    import EnvVarReference from "$lib/components/docs/EnvVarReference.svelte";
    import VerificationSteps from "$lib/components/docs/VerificationSteps.svelte";
    import NextSteps from "$lib/components/docs/NextSteps.svelte";
    import { Info } from "@lucide/svelte";
</script>

<div class="max-w-3xl">
    <h1 class="text-4xl font-bold tracking-tight mb-4">Docker Compose</h1>
    <p class="text-lg text-muted-foreground mb-8">
        Deploy Nocturne on any server with Docker Compose from the command line.
    </p>

    <h2 class="text-2xl font-bold mt-8 mb-4">Prerequisites</h2>
    <ul class="list-disc list-inside space-y-2 text-muted-foreground mb-8">
        <li>A Linux server, VPS, or Raspberry Pi with SSH access</li>
        <li>Docker Engine 20.10+ and Docker Compose 2.0+ installed</li>
        <li>A domain name (recommended) or static IP address</li>
    </ul>

    <SystemRequirements />

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 1: Create a project directory</h2>
    <pre class="p-3 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-8"><code>mkdir nocturne && cd nocturne</code></pre>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 2: Create configuration files</h2>
    <p class="text-muted-foreground mb-4">
        Create a <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded">docker-compose.yml</code>
        and <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded">.env</code> file in your
        project directory. The minimal configuration includes PostgreSQL, the Nocturne API, and
        Watchtower for automatic updates.
    </p>

    <DockerComposeYaml minimal />

    <div class="p-4 rounded-lg border border-blue-500/30 bg-blue-500/5 mb-8 not-prose">
        <div class="flex items-start gap-3">
            <Info class="w-5 h-5 text-blue-500 mt-0.5 shrink-0" />
            <p class="text-sm text-muted-foreground">
                <strong class="text-blue-700 dark:text-blue-400">Connectors are optional.</strong>
                The minimal compose above runs the core Nocturne stack. To add data source connectors
                (Dexcom, LibreLinkUp, etc.), add the connector services to your compose file.
                See the <a href="/docs/configuration" class="text-primary hover:underline">Configuration Guide</a>.
            </p>
        </div>
    </div>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 3: Configure environment variables</h2>
    <p class="text-muted-foreground mb-4">
        Edit the <code class="text-xs bg-muted/50 px-1.5 py-0.5 rounded">.env</code> file and set
        your values. At minimum, you must change the database password and API secret:
    </p>
    <EnvVarReference coreOnly />

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 4: Start the services</h2>
    <pre class="p-3 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-8"><code>docker compose up -d</code></pre>
    <p class="text-muted-foreground mb-8">
        Docker will pull the required images and start all services. This may take a few minutes
        on first run.
    </p>

    <h2 class="text-2xl font-bold mt-8 mb-4">Step 5: Verify the installation</h2>
    <VerificationSteps />

    <h2 class="text-2xl font-bold mt-8 mb-4">Updating</h2>
    <p class="text-muted-foreground mb-4">
        Watchtower is included in the default compose and will automatically check for image updates
        once per day. To update manually:
    </p>
    <pre class="p-3 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-8"><code>docker compose pull && docker compose up -d</code></pre>

    <h2 class="text-2xl font-bold mt-8 mb-4">Troubleshooting</h2>
    <p class="text-muted-foreground mb-4">
        If you encounter issues, check the logs for error details:
    </p>
    <pre class="p-3 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-4"><code># View all service logs
docker compose logs

# View logs for a specific service
docker compose logs nocturne-api

# Follow logs in real-time
docker compose logs -f</code></pre>
    <p class="text-muted-foreground mb-8">
        To start fresh, stop all services and remove volumes:
    </p>
    <pre class="p-3 rounded-lg bg-muted/50 border border-border/60 text-sm overflow-x-auto mb-8"><code>docker compose down -v</code></pre>

    <h2 class="text-2xl font-bold mt-8 mb-4">Next Steps</h2>
    <NextSteps />
</div>
