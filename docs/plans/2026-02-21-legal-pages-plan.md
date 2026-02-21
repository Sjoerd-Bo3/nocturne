# Legal Pages Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create Terms of Service and Privacy Policy pages for Nocturne using SvelteKit route groups.

**Architecture:** SvelteKit route group `(legal)` with shared layout for consistent styling. Two content pages (terms, privacy) with no authentication required. Purely static content with friendly, approachable tone.

**Tech Stack:** SvelteKit 2, Svelte 5 (runes), Tailwind CSS 4, lucide-svelte icons

---

## Task 1: Create Route Group Directory Structure

**Files:**
- Create: `src/Web/packages/app/src/routes/(legal)/` (directory)
- Create: `src/Web/packages/app/src/routes/(legal)/terms/` (directory)
- Create: `src/Web/packages/app/src/routes/(legal)/privacy/` (directory)

**Step 1: Create directories**

```bash
mkdir -p src/Web/packages/app/src/routes/\(legal\)/terms
mkdir -p src/Web/packages/app/src/routes/\(legal\)/privacy
```

**Step 2: Verify structure**

Run: `ls -la src/Web/packages/app/src/routes/\(legal\)/`

Expected: Two directories: `terms/` and `privacy/`

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/\(legal\)/
git commit -m "feat: create legal pages directory structure"
```

---

## Task 2: Create Shared Legal Page Layout

**Files:**
- Create: `src/Web/packages/app/src/routes/(legal)/+layout.svelte`

**Step 1: Create layout file**

Create `src/Web/packages/app/src/routes/(legal)/+layout.svelte`:

```svelte
<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { ArrowLeft } from "lucide-svelte";

  let { children } = $props();
</script>

<svelte:head>
  <meta name="robots" content="noindex" />
</svelte:head>

<div class="min-h-screen flex flex-col bg-background">
  <!-- Header -->
  <header class="border-b border-border/40">
    <div class="container mx-auto px-4 py-4">
      <a href="/" class="flex items-center gap-2.5 hover:opacity-80 transition-opacity">
        <div class="w-8 h-8 rounded-lg bg-primary/20 flex items-center justify-center">
          <span class="text-primary font-bold">N</span>
        </div>
        <span class="text-lg font-semibold">Nocturne</span>
      </a>
    </div>
  </header>

  <!-- Content -->
  <main class="flex-1 container mx-auto px-4 py-8">
    <article class="max-w-3xl mx-auto prose prose-slate dark:prose-invert">
      {@render children()}
    </article>
  </main>

  <!-- Footer -->
  <footer class="border-t border-border/40 bg-muted/30">
    <div class="container mx-auto px-4 py-6 flex flex-col md:flex-row justify-between items-center gap-4">
      <Button variant="ghost" href="/auth/login" class="gap-2">
        <ArrowLeft class="w-4 h-4" />
        Back to Login
      </Button>
      <p class="text-sm text-muted-foreground">
        Last updated: February 21, 2026
      </p>
    </div>
  </footer>
</div>
```

**Step 2: Verify file exists**

Run: `cat src/Web/packages/app/src/routes/\(legal\)/+layout.svelte | head -5`

Expected: File shows script tag with imports

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/\(legal\)/+layout.svelte
git commit -m "feat: add legal pages shared layout"
```

---

## Task 3: Create Terms of Service Page

**Files:**
- Create: `src/Web/packages/app/src/routes/(legal)/terms/+page.svelte`

**Step 1: Create Terms of Service page**

Create `src/Web/packages/app/src/routes/(legal)/terms/+page.svelte`:

```svelte
<svelte:head>
  <title>Terms of Service - Nocturne</title>
</svelte:head>

<h1>Terms of Service</h1>

<p class="lead">
  Welcome to Nocturne. These terms explain your rights and responsibilities when using this open-source diabetes data management platform.
</p>

<h2>1. Introduction</h2>

<p>
  Nocturne is an open-source, self-hosted platform for managing your diabetes data with full Nightscout API compatibility. It's built by the diabetes community, for the diabetes community.
</p>

<p>
  Because Nocturne is self-hosted, you have full control over your installation, your data, and how you use the software. These terms apply to the Nocturne software itself, not to any specific hosted instance.
</p>

<h2>2. No Warranty - Provided "AS IS"</h2>

<p>
  Nocturne is provided <strong>"AS IS"</strong> without any warranties or guarantees of any kind, either express or implied. This means:
</p>

<ul>
  <li><strong>No medical advice:</strong> Nocturne is not a medical device and does not provide medical advice. Always consult with healthcare professionals for medical decisions.</li>
  <li><strong>No reliability guarantee:</strong> We don't guarantee the software will work perfectly, be error-free, or be available at all times.</li>
  <li><strong>No fitness guarantee:</strong> We don't warrant that Nocturne is suitable for any particular purpose or meets your specific needs.</li>
  <li><strong>Use at your own risk:</strong> You assume all risk when using Nocturne. We are not liable for any harm, data loss, or issues that may arise.</li>
</ul>

<p>
  This is standard for open-source software. You're free to inspect the code, modify it, and use it as you see fit, but we can't guarantee outcomes.
</p>

<h2>3. Third-Party Data Connectors</h2>

<p>
  Nocturne integrates with various third-party diabetes data services, including:
</p>

<ul>
  <li>Dexcom</li>
  <li>FreeStyle Libre</li>
  <li>Glooko</li>
  <li>Tidepool</li>
  <li>Nightscout</li>
  <li>And others</li>
</ul>

<p>
  <strong>Important disclaimers about third-party connectors:</strong>
</p>

<ul>
  <li><strong>We don't control these services:</strong> Third-party services have their own terms of service, privacy policies, and data practices. You are responsible for reviewing and agreeing to their terms.</li>
  <li><strong>Data accuracy:</strong> The accuracy of your data in Nocturne depends on the accuracy of data provided by these third-party sources. We cannot guarantee the accuracy of third-party data.</li>
  <li><strong>Service availability:</strong> Third-party services may change their APIs, terms, or shut down at any time. We are not responsible for service interruptions or changes to third-party integrations.</li>
  <li><strong>Your agreements:</strong> By connecting a third-party service to Nocturne, you agree that you have the right to access that data and are in compliance with that service's terms.</li>
  <li><strong>No liability:</strong> We are not liable for any issues, data loss, or problems arising from third-party services or connectors.</li>
</ul>

<h2>4. Your Data and Privacy</h2>

<p>
  Nocturne is designed with privacy as a core principle:
</p>

<ul>
  <li><strong>Self-hosted:</strong> Your data lives in your own database on your own server. We don't have access to it.</li>
  <li><strong>No data collection:</strong> The Nocturne software does not send data to us without your explicit consent.</li>
  <li><strong>You're in control:</strong> You decide where to host Nocturne, who has access, and how your data is used.</li>
</ul>

<p>
  For more details, see our <a href="/privacy">Privacy Policy</a>.
</p>

<h2>5. Acceptable Use</h2>

<p>
  When using Nocturne, you agree to:
</p>

<ul>
  <li>Use the software for lawful purposes only</li>
  <li>Not use it to harm others or violate their rights</li>
  <li>Not abuse the software or use it in ways that could damage the project or community</li>
  <li>Comply with all applicable laws and regulations</li>
</ul>

<p>
  If you're hosting Nocturne for others (family, friends, community), you're responsible for ensuring acceptable use by those users.
</p>

<h2>6. Changes to These Terms</h2>

<p>
  We may update these terms from time to time to reflect changes in the software, legal requirements, or best practices. When we do:
</p>

<ul>
  <li>The updated terms will be posted at this URL</li>
  <li>The "Last updated" date at the bottom of the page will change</li>
  <li>Continued use of Nocturne after changes constitutes acceptance of the new terms</li>
</ul>

<p>
  For significant changes, we'll announce them in the project's GitHub repository.
</p>

<h2>7. Open Source License</h2>

<p>
  Nocturne is released under the MIT License. This means you're free to:
</p>

<ul>
  <li>Use the software for any purpose</li>
  <li>Modify the source code</li>
  <li>Distribute copies</li>
  <li>Sublicense or sell copies</li>
</ul>

<p>
  The MIT License provides the software "as is" without warranty. See the LICENSE file in the project repository for full details.
</p>

<h2>8. Contact</h2>

<p>
  Nocturne is an open-source project. For questions, issues, or contributions:
</p>

<ul>
  <li>GitHub: <a href="https://github.com/nightscout/nocturne" target="_blank" rel="noopener noreferrer">github.com/nightscout/nocturne</a></li>
  <li>Issues: Use GitHub Issues for bug reports and feature requests</li>
  <li>Community: Join discussions in GitHub Discussions</li>
</ul>

<hr />

<p class="text-sm text-muted-foreground">
  These terms are effective as of February 21, 2026.
</p>
```

**Step 2: Verify file exists**

Run: `wc -l src/Web/packages/app/src/routes/\(legal\)/terms/+page.svelte`

Expected: Shows line count (should be ~150+ lines)

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/\(legal\)/terms/+page.svelte
git commit -m "feat: add Terms of Service page content"
```

---

## Task 4: Create Privacy Policy Page

**Files:**
- Create: `src/Web/packages/app/src/routes/(legal)/privacy/+page.svelte`

**Step 1: Create Privacy Policy page**

Create `src/Web/packages/app/src/routes/(legal)/privacy/+page.svelte`:

```svelte
<svelte:head>
  <title>Privacy Policy - Nocturne</title>
</svelte:head>

<h1>Privacy Policy</h1>

<p class="lead">
  Your privacy matters. This policy explains how Nocturne handles your data and protects your privacy.
</p>

<h2>1. Introduction</h2>

<p>
  Nocturne is a self-hosted, open-source diabetes data management platform. This means <strong>you control where your data lives</strong>. Unlike cloud-based services, Nocturne runs on your own server or hosting provider, and your data stays in your own database.
</p>

<p>
  This privacy policy explains what data Nocturne collects (spoiler: very little), how it's stored, and your rights.
</p>

<h2>2. Data We DON'T Collect</h2>

<p>
  Because Nocturne is self-hosted, we (the Nocturne project developers) do not have access to your data. Specifically:
</p>

<ul>
  <li><strong>No analytics:</strong> Nocturne does not send usage analytics or telemetry to us by default.</li>
  <li><strong>No tracking:</strong> We don't track your usage, browsing, or behavior.</li>
  <li><strong>No data collection:</strong> Your glucose readings, treatments, meals, and health data never leave your server unless you choose to share them.</li>
  <li><strong>No selling data:</strong> We don't sell your data because we don't have access to it in the first place.</li>
  <li><strong>No cloud storage:</strong> Nocturne doesn't store your data in our cloud or servers. It's all on your infrastructure.</li>
</ul>

<h2>3. Data Storage</h2>

<p>
  All your Nocturne data is stored in your own database on your own server or hosting provider. This includes:
</p>

<ul>
  <li>Glucose readings (blood sugar, CGM data)</li>
  <li>Treatments (insulin, medications)</li>
  <li>Meals and nutrition data</li>
  <li>Activity and exercise logs</li>
  <li>User account information</li>
  <li>Settings and preferences</li>
</ul>

<p>
  <strong>Where your data lives:</strong>
</p>

<ul>
  <li><strong>Your database:</strong> All persistent data is stored in a PostgreSQL database that you control.</li>
  <li><strong>Your hosting provider:</strong> Your hosting provider's privacy policy and security practices apply to your data. Choose a provider you trust.</li>
  <li><strong>Browser local storage:</strong> Some temporary data (like UI preferences) may be stored in your browser's local storage. This never leaves your device.</li>
</ul>

<h2>4. Third-Party Services and Connectors</h2>

<p>
  Nocturne can connect to third-party diabetes data services to import your glucose and treatment data. When you use these integrations:
</p>

<ul>
  <li><strong>Third-party privacy policies apply:</strong> Services like Dexcom, FreeStyle Libre, Glooko, and Tidepool have their own privacy policies. Review them before connecting.</li>
  <li><strong>We don't control third parties:</strong> We cannot control what data these services collect about you or how they use it.</li>
  <li><strong>Credentials storage:</strong> Your API keys and login credentials for third-party services are stored <strong>encrypted</strong> in your database. Only your Nocturne instance can decrypt them.</li>
  <li><strong>Data transfer:</strong> When you connect to a third-party service, data flows between that service and your Nocturne instance. We (the Nocturne developers) do not see or have access to this data.</li>
</ul>

<p>
  <strong>Third-party services you might connect:</strong>
</p>

<ul>
  <li>Dexcom (CGM data)</li>
  <li>FreeStyle Libre (CGM data)</li>
  <li>Glooko (multi-source data aggregation)</li>
  <li>Tidepool (open diabetes data platform)</li>
  <li>Nightscout (diabetes data platform)</li>
  <li>Others via community plugins</li>
</ul>

<h2>5. Optional Analytics and Error Reporting</h2>

<p>
  In the future, Nocturne may offer optional, opt-in features for:
</p>

<ul>
  <li><strong>Error reporting:</strong> Send crash reports and error logs to help us improve Nocturne</li>
  <li><strong>Anonymous usage analytics:</strong> Aggregated, anonymized usage statistics</li>
</ul>

<p>
  If we add these features:
</p>

<ul>
  <li>They will be <strong>opt-in only</strong> - disabled by default</li>
  <li>You'll have clear control to enable or disable them</li>
  <li>We'll clearly explain what data would be collected</li>
  <li>Data will be anonymized and aggregated when possible</li>
  <li>No health data or personally identifiable information will be included</li>
</ul>

<p>
  <strong>Current status:</strong> As of this writing, Nocturne does not include any analytics or error reporting features.
</p>

<h2>6. Your Rights and Data Control</h2>

<p>
  Because you self-host Nocturne, you have complete control over your data:
</p>

<ul>
  <li><strong>You own your data:</strong> All data belongs to you. We make no claims to it.</li>
  <li><strong>Export anytime:</strong> You can export your data at any time. It's just a PostgreSQL database - you have full access.</li>
  <li><strong>Delete anytime:</strong> You can delete any or all of your data whenever you want.</li>
  <li><strong>Control access:</strong> You decide who can access your Nocturne instance and what they can see.</li>
  <li><strong>Move your data:</strong> You can migrate your database to a different server or hosting provider at any time.</li>
  <li><strong>Backup your data:</strong> You're responsible for backing up your database. We recommend regular backups.</li>
</ul>

<h2>7. Security</h2>

<p>
  Nocturne is designed with security best practices:
</p>

<ul>
  <li><strong>Encryption:</strong> API keys and sensitive credentials are stored encrypted in the database.</li>
  <li><strong>HTTPS recommended:</strong> We strongly recommend running Nocturne behind HTTPS to encrypt data in transit.</li>
  <li><strong>Authentication:</strong> User accounts are protected with password hashing (bcrypt).</li>
  <li><strong>Open source:</strong> The code is open for security review by the community.</li>
</ul>

<p>
  <strong>Your responsibility:</strong> As a self-hosted application, you're responsible for:
</p>

<ul>
  <li>Keeping your server and dependencies updated</li>
  <li>Using strong passwords</li>
  <li>Securing your hosting environment</li>
  <li>Configuring HTTPS/SSL certificates</li>
  <li>Regular backups</li>
</ul>

<h2>8. Children's Privacy</h2>

<p>
  Nocturne is often used to manage diabetes data for children. Because the software is self-hosted:
</p>

<ul>
  <li>Parents/guardians control all data for their children</li>
  <li>No data is sent to us (the developers)</li>
  <li>All data stays within the family's or caregiver's control</li>
</ul>

<p>
  If you're hosting Nocturne for your child, you have full control and responsibility for their data privacy and security.
</p>

<h2>9. Changes to This Privacy Policy</h2>

<p>
  We may update this privacy policy from time to time. When we do:
</p>

<ul>
  <li>The updated policy will be posted at this URL</li>
  <li>The "Last updated" date will change</li>
  <li>Major changes will be announced in the GitHub repository</li>
</ul>

<p>
  Because Nocturne is self-hosted, privacy policy changes don't affect data you've already collected - it stays under your control regardless.
</p>

<h2>10. Contact</h2>

<p>
  For questions about this privacy policy or Nocturne's privacy practices:
</p>

<ul>
  <li>GitHub: <a href="https://github.com/nightscout/nocturne" target="_blank" rel="noopener noreferrer">github.com/nightscout/nocturne</a></li>
  <li>Open an issue or discussion on GitHub</li>
</ul>

<p>
  For questions about your specific Nocturne instance's privacy and data handling, contact your instance administrator (which might be you if you're self-hosting).
</p>

<h2>Summary</h2>

<p>
  The key point: <strong>Nocturne is self-hosted, which means you control your data</strong>. We (the developers) don't collect it, don't have access to it, and can't sell it or share it. Your privacy is protected by design.
</p>

<p>
  See also: <a href="/terms">Terms of Service</a>
</p>

<hr />

<p class="text-sm text-muted-foreground">
  This privacy policy is effective as of February 21, 2026.
</p>
```

**Step 2: Verify file exists**

Run: `wc -l src/Web/packages/app/src/routes/\(legal\)/privacy/+page.svelte`

Expected: Shows line count (should be ~200+ lines)

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/\(legal\)/privacy/+page.svelte
git commit -m "feat: add Privacy Policy page content"
```

---

## Task 5: Test Pages Manually

**Files:**
- Test: Browser verification of all routes

**Step 1: Start development server**

Run: `cd src/Web/packages/app && pnpm run dev`

Expected: Dev server starts on http://localhost:5173

**Step 2: Test Terms of Service page**

1. Navigate to: http://localhost:5173/terms
2. Verify:
   - Page loads without errors
   - Title shows "Terms of Service - Nocturne"
   - Header shows Nocturne logo and name
   - All content sections render correctly
   - Footer shows "Back to Login" button
   - Footer shows last updated date
   - Page is responsive (resize browser)
   - Links to Privacy Policy work

**Step 3: Test Privacy Policy page**

1. Navigate to: http://localhost:5173/privacy
2. Verify:
   - Page loads without errors
   - Title shows "Privacy Policy - Nocturne"
   - Header shows Nocturne logo and name
   - All content sections render correctly
   - Footer shows "Back to Login" button
   - Footer shows last updated date
   - Page is responsive (resize browser)
   - Links to Terms of Service work

**Step 4: Test from registration page**

1. Navigate to: http://localhost:5173/auth/register
2. Scroll to bottom
3. Verify:
   - Links to "Terms of Service" and "Privacy Policy" exist
   - Clicking them navigates to correct pages (no 404)
   - Can navigate back to registration

**Step 5: Test dark mode**

1. Toggle dark mode (if available in app)
2. Visit /terms and /privacy
3. Verify: Pages render correctly in dark mode

**Step 6: Stop dev server**

Press Ctrl+C to stop the development server

---

## Task 6: Verify Type Safety and Build

**Files:**
- Test: Type checking and production build

**Step 1: Run type checking**

Run: `cd src/Web/packages/app && pnpm run check`

Expected: No type errors related to legal pages

**Step 2: Run production build**

Run: `cd src/Web/packages/app && pnpm run build`

Expected: Build completes successfully

**Step 3: Verify routes in build output**

Run: `ls -la src/Web/packages/app/.svelte-kit/output/client/_app/immutable/`

Expected: Build artifacts exist (no need to verify specific files for static pages)

---

## Task 7: Final Commit and Cleanup

**Files:**
- Review: All created files

**Step 1: Check git status**

Run: `git status`

Expected: All files committed, working directory clean

**Step 2: Review commit history**

Run: `git log --oneline -5`

Expected: Shows all commits from this implementation:
- feat: create legal pages directory structure
- feat: add legal pages shared layout
- feat: add Terms of Service page content
- feat: add Privacy Policy page content

**Step 3: Create summary commit (if needed)**

If any uncommitted changes exist:

```bash
git add -A
git commit -m "chore: finalize legal pages implementation"
```

**Step 4: Verify registration page links work**

Final verification that the original issue is resolved:

1. Start dev server: `cd src/Web/packages/app && pnpm run dev`
2. Navigate to: http://localhost:5173/auth/register
3. Click "Terms of Service" link at bottom
4. Verify: No 404, page loads correctly
5. Go back, click "Privacy Policy" link
6. Verify: No 404, page loads correctly
7. Stop dev server

---

## Success Criteria

- ✅ Route group `(legal)` created with shared layout
- ✅ `/terms` page accessible and renders correctly
- ✅ `/privacy` page accessible and renders correctly
- ✅ Registration page links work (no 404 errors)
- ✅ Pages use consistent Nocturne styling
- ✅ Pages are mobile responsive
- ✅ Dark mode works correctly
- ✅ Type checking passes
- ✅ Production build succeeds
- ✅ All content sections included per design
- ✅ Friendly, approachable tone throughout
- ✅ All third-party disclaimers included

## Notes

- No backend changes required
- No database migrations needed
- No API endpoints created
- No translation files needed (English only for legal docs)
- No consent tracking or acceptance logic
- Pages accessible without authentication
- Content can be updated by editing Svelte files directly
