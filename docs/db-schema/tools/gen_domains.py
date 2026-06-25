import json, re
from collections import defaultdict

import os as _os
SP = _os.environ.get("SCHEMA_WORKDIR", _os.path.dirname(_os.path.abspath(__file__)))
m = json.load(open(f"{SP}/model.json"))
tables = m['tables']
rels = m['rels']
full2table = {k: v['table'] for k, v in tables.items()}
table2full = {v: k for k, v in full2table.items()}

META = {"repo":"nightscout/nocturne","branch":"main",
        "commit_short":"e7937ef","commit_date":"2026-06-25"}

DOMAINS = [
 ("Tenancy & Membership", [
    "tenants","tenant_members","tenant_member_roles","tenant_roles","member_invites",
    "membership_requests","tenant_alert_settings","tenant_audit_config",
    "tenant_data_retention_config","tenant_demo_config","settings","platform_settings"]),
 ("Identity & Authentication", [
    "subjects","subject_roles","subject_oidc_identities","subject_avatars","roles",
    "refresh_tokens","recovery_codes","totp_credentials","passkey_credentials",
    "oidc_providers","auth_audit_log"]),
 ("OAuth Server", [
    "oauth_clients","oauth_authorization_codes","oauth_device_codes","oauth_grants",
    "oauth_refresh_tokens"]),
 ("Glucose & Vitals (v4)", [
    "sensor_glucose","meter_glucose","calibrations","bg_checks",
    "compression_low_suggestions","heart_rates","step_counts","body_weights"]),
 ("Insulin & Therapy (v4)", [
    "boluses","bolus_calculations","basal_injections","temp_basals","carb_intakes",
    "basal_schedules","carb_ratio_schedules","sensitivity_schedules",
    "target_range_schedules","therapy_settings","patient_insulins"]),
 ("Devices & Status Snapshots (v4)", [
    "devices","device_events","device_status_extras","patient_devices","patient_records",
    "aps_snapshots","pump_snapshots","uploader_snapshots","notes"]),
 ("Food", [
    "foods","treatment_foods","connector_food_entries","user_food_favorites"]),
 ("Alerts", [
    "alert_rules","alert_rule_channels","alert_instances","alert_deliveries",
    "alert_excursions","alert_invites","alert_condition_timers","alert_custom_sounds",
    "alert_tracker_state"]),
 ("Trackers", [
    "tracker_definitions","tracker_instances","tracker_presets",
    "tracker_notification_thresholds","state_spans"]),
 ("Connectors & Migration", [
    "connector_configurations","data_source_metadata","migration_runs","migration_sources",
    "linked_records","dedup_reconcile_state","decomposition_batches",
    "discrepancy_analyses","discrepancy_details"]),
 ("Audit & Event Logs", [
    "mutation_audit_log","read_access_log","system_events"]),
 ("Platform & Misc", [
    "DataProtectionKeys","clock_faces","coach_mark_states","in_app_notifications",
    "timezone_timeline","chat_identity_directory","chat_identity_pending_links"]),
]

# sanity: every table assigned exactly once
assigned = [t for _, ts in DOMAINS for t in ts]
allt = set(full2table.values())
miss = allt - set(assigned)
dup = [x for x in assigned if assigned.count(x) > 1]
assert not miss, f"unassigned: {miss}"
assert not dup, f"dup: {set(dup)}"

table_domain = {}
for d, ts in DOMAINS:
    for t in ts: table_domain[t] = d

def ttype(coltype):
    c = coltype.lower()
    if c.startswith('character varying'):
        mm = re.search(r'\((\d+)\)', c); return 'varchar' + (mm.group(1) if mm else '')
    repl = {'uuid':'uuid','text':'text','boolean':'bool','integer':'int','bigint':'bigint',
            'smallint':'smallint','double precision':'double','real':'real','numeric':'numeric',
            'bytea':'bytea','jsonb':'jsonb','json':'json','date':'date','interval':'interval','inet':'inet'}
    if c.startswith('timestamp'): return 'timestamptz'
    if c in repl: return repl[c]
    return re.sub(r'[^a-zA-Z0-9_]', '_', c)

# fk props per dependent entity + edges
fk_props = defaultdict(set)
edges = []  # principal_table, dependent_table, fkcols, kind
for r in rels:
    dep, pr = r['dependent'], r['principal']
    if dep not in tables or pr not in tables: continue
    for p in r['fk']: fk_props[dep].add(p)
    edges.append((full2table[pr], full2table[dep], r['fk'], r['kind']))

def table_block(tname, only_keys=False):
    full = table2full[tname]
    info = tables[full]
    pk = set(info['pk']); fks = fk_props.get(full, set())
    lines = [f'  "{tname}" {{']
    for col in info['cols']:
        marks = []
        if col['prop'] in pk: marks.append('PK')
        if col['prop'] in fks: marks.append('FK')
        if only_keys and not marks:   # compact mode: keys only
            continue
        line = f'    {ttype(col["type"])} {col["col"]}'
        if marks: line += ' ' + ','.join(marks)
        lines.append(line)
    lines.append('  }')
    return '\n'.join(lines)

# dedupe edges
def dom_diagram(domain, tlist):
    tset = set(tlist)
    out = ['```mermaid', 'erDiagram']
    for tname in tlist:
        out.append(table_block(tname))
    seen = set()
    for pr_t, dep_t, fk, kind in edges:
        if pr_t in tset and dep_t in tset:
            lbl = ','.join(fk) if fk else 'fk'
            k = (pr_t, dep_t, lbl)
            if k in seen: continue
            seen.add(k)
            card = '||--o{' if kind == 'many' else '||--o|'
            out.append(f'  "{pr_t}" {card} "{dep_t}" : "{lbl}"')
    out.append('```')
    return '\n'.join(out)

def cross_refs(domain, tlist):
    tset = set(tlist)
    rows = []
    seen = set()
    for pr_t, dep_t, fk, kind in edges:
        # FK from a table in this domain -> table in another domain
        if dep_t in tset and pr_t not in tset:
            k = (dep_t, pr_t, ','.join(fk))
            if k in seen: continue
            seen.add(k)
            rows.append((dep_t, ','.join(fk) or 'fk', pr_t, table_domain.get(pr_t,'?')))
    return rows

# Domain map: count edges between domains
dom_edges = defaultdict(int)
for pr_t, dep_t, fk, kind in edges:
    dp, dd = table_domain[pr_t], table_domain[dep_t]
    if dp != dd:
        dom_edges[(dd, dp)] += 1  # dependent-domain references principal-domain

# Build README
abbr = {d: ''.join(w[0] for w in re.findall(r'[A-Za-z0-9]+', d))[:4].upper() for d,_ in DOMAINS}
md = []
md.append("# Nocturne Database Schema\n")
md.append("> Entity-relationship reference for the Nocturne PostgreSQL database, generated "
          "from the EF Core model snapshot on the upstream `nightscout/nocturne@main` "
          f"branch (commit `{META['commit_short']}`, {META['commit_date']}).\n")
md.append("**[Open `schema.html`](./schema.html)** for the interactive explorer "
          "(search, pan/zoom, per-table column &amp; relationship detail) &mdash; a single "
          "self-contained file you can open in any browser or share directly.\n")
md.append("On mobile / the iOS Files preview (which disables JavaScript), use "
          "**[`schema-static.html`](./schema-static.html)** instead &mdash; a no-JavaScript "
          "build that renders everywhere, with tap-to-expand tables and per-domain ER diagrams.\n")
md.append(f"**{len(allt)} tables**, **{sum(len(v['cols']) for v in tables.values())} columns**, "
          f"**{len(set((p,d,tuple(f)) for p,d,f,k in edges))} foreign-key relationships**, "
          f"grouped into **{len(DOMAINS)} functional domains**.\n")
md.append("All tenant-scoped tables carry a `tenant_id` column and are protected by PostgreSQL "
          "Row Level Security (`FORCE ROW LEVEL SECURITY`). Tables use snake_case names; new rows "
          "use UUID v7 primary keys. Timestamps are `timestamp with time zone`.\n")
md.append("## Contents\n")
md.append("- [Domain map](#domain-map)")
for d,_ in DOMAINS:
    anchor = re.sub(r'[^a-z0-9 ]','',d.lower()).replace(' ','-')
    md.append(f"- [{d}](#{anchor})")
md.append("\nThe complete single-diagram source (all 93 tables in one ER diagram) is in "
          "[`schema.full.mmd`](./schema.full.mmd).\n")

# Domain map
md.append("## Domain map\n")
md.append("High-level view of how the domains reference one another (arrows point from the "
          "domain holding the foreign key to the domain it references; numbers are distinct FK paths).\n")
md.append("```mermaid")
md.append("flowchart LR")
for d,_ in DOMAINS:
    md.append(f'  {abbr[d]}["{d}"]')
seen=set()
for (dd, dp), n in sorted(dom_edges.items(), key=lambda x:-x[1]):
    md.append(f'  {abbr[dd]} -->|{n}| {abbr[dp]}')
md.append("```\n")

for d, tlist in DOMAINS:
    md.append(f"## {d}\n")
    md.append(f"Tables: " + ", ".join(f"`{t}`" for t in tlist) + "\n")
    md.append(dom_diagram(d, tlist))
    md.append("")
    xr = cross_refs(d, tlist)
    if xr:
        md.append("**Cross-domain references**\n")
        md.append("| Table | Column(s) | References | Domain |")
        md.append("|---|---|---|---|")
        for dep_t, fk, pr_t, pdom in sorted(xr):
            md.append(f"| `{dep_t}` | `{fk}` | `{pr_t}` | {pdom} |")
        md.append("")

open(f"{SP}/README.md", "w").write('\n'.join(md) + '\n')
print("domains:", len(DOMAINS), "all assigned OK")
print("README bytes:", len('\n'.join(md)))
print("cross-domain edge groups:", len(dom_edges))
