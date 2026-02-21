# Legal Pages Design

**Date:** 2026-02-21
**Status:** Approved

## Overview

Create Terms of Service and Privacy Policy pages for Nocturne. These are informational legal pages for an open-source, self-hosted diabetes management application with third-party data connectors.

## Requirements

- Open source application with no warranty disclaimer
- No data collection/transmission without explicit user consent
- Third-party connector disclaimers (Dexcom, Libre, Glooko, Tidepool, etc.)
- Not responsible for third-party service terms or data practices
- No consent tracking needed (self-hosted deployment model)
- Friendly, approachable tone (not legalese)

## File Structure

```
src/Web/packages/app/src/routes/
├── (legal)/
│   ├── +layout.svelte          # Shared layout for legal pages
│   ├── terms/
│   │   └── +page.svelte        # Terms of Service content
│   └── privacy/
│       └── +page.svelte        # Privacy Policy content
```

**Route Group Pattern:**
- `(legal)` is a SvelteKit route group (parentheses don't affect URLs)
- URLs remain clean: `/terms` and `/privacy`
- Shared layout applies automatically to all pages in the group
- No login required to access these pages

## Layout Design

### (legal)/+layout.svelte

**Header:**
- Nocturne logo and name (links to `/`)
- Minimal, clean design
- Subtle bottom border

**Content Area:**
- Centered container: `max-w-3xl mx-auto`
- Tailwind prose classes for typography
- Proper heading hierarchy (h1, h2, h3)
- Comfortable reading experience

**Footer:**
- Navigation: "← Back to Login" or "← Back to Home"
- Last updated date (hardcoded, manually updated)
- Subtle top border

**Characteristics:**
- No sidebar, no app navigation
- No authentication required
- Mobile responsive
- Inherits theme (light/dark mode)

## Content Structure

### Terms of Service

1. **Introduction**
   - What Nocturne is
   - Self-hosted, open-source nature
   - Welcoming tone

2. **No Warranty / AS-IS**
   - Software provided "AS IS"
   - No guarantees or warranties
   - Not medical advice, not a medical device
   - Standard open-source disclaimers (friendly version)

3. **Third-Party Data Connectors**
   - List connector types (Dexcom, Libre, Glooko, Tidepool, etc.)
   - We don't control third-party services
   - Users subject to third-party terms
   - Data accuracy depends on sources
   - Not liable for third-party issues

4. **Your Data and Privacy**
   - No data collection without consent
   - Self-hosted = you control data
   - Link to Privacy Policy

5. **Acceptable Use**
   - Personal diabetes management
   - No illegal use
   - Don't abuse service

6. **Changes to Terms**
   - May update terms
   - Continued use = acceptance

### Privacy Policy

1. **Introduction**
   - Self-hosted application
   - You control where data lives

2. **Data We DON'T Collect**
   - No analytics by default
   - No tracking
   - No data selling (we don't have access)

3. **Data Storage**
   - Stored in your own database
   - Your hosting provider's policies apply
   - Browser local storage usage

4. **Third-Party Services**
   - Connector services (Dexcom, Libre, etc.) have own privacy policies
   - We don't control their data practices
   - Credentials stored encrypted in your database

5. **Optional Analytics/Telemetry**
   - Future: opt-in error reporting
   - Never enabled by default
   - What would be collected if enabled

6. **Your Rights**
   - You own your data
   - Export/delete anytime (your database)
   - Full control over hosting

## Styling & Design System

**Typography:**
- Tailwind prose utilities
- Existing font weights and hierarchy
- Proper heading structure (h1 → h2 → h3)
- Comfortable line-height for reading

**Colors:**
- `bg-background` and `text-foreground`
- `text-muted-foreground` for secondary text
- `border-border` for dividers
- `text-primary` for links with hover states
- Inherits theme system

**Spacing:**
- Container: `max-w-3xl mx-auto px-4 py-8`
- Prose defaults for section spacing
- Consistent header/footer padding

**Components:**
- Use existing Button component for navigation
- No custom components needed
- Standard HTML + Tailwind classes

**Responsive:**
- Full-width on mobile with padding
- Centered max-width on desktop
- Touch-friendly targets

## Navigation & Links

**Inbound:**
- Registration page already references `/terms` and `/privacy` (lines 328-334)
- Optional: add to app footer (future enhancement)

**Outbound:**
- Footer provides "← Back to Login" link
- Cross-links between Terms and Privacy Policy
- All links to main site pages

**Accessibility:**
- Descriptive link text
- Proper focus states
- Keyboard navigable
- Semantic HTML structure

**Meta:**
- Proper `<title>` tags for each page
- No meta descriptions needed (internal pages)

## Implementation Notes

- No tracking or consent acceptance required
- No database changes needed
- No API endpoints required
- Purely frontend pages with static content
- Content is versioned via git (manual updates)
- No special authentication or authorization
- Works for both logged-in and anonymous users

## Success Criteria

- [x] Registration page links work (no 404s)
- [x] Terms of Service covers all disclaimers
- [x] Privacy Policy explains data practices
- [x] Pages are readable and well-formatted
- [x] Consistent with Nocturne design system
- [x] Mobile responsive
- [x] Accessible via keyboard navigation
- [x] Friendly, approachable tone
