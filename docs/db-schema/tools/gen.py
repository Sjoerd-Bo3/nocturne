import json, re

import os as _os
SP = _os.environ.get("SCHEMA_WORKDIR", _os.path.dirname(_os.path.abspath(__file__)))
m = json.load(open(f"{SP}/model.json"))
tables = m['tables']        # entity full -> {table, cols, pk, indexes}
rels = m['rels']

full2table = {k: v['table'] for k, v in tables.items()}

# map column type to a compact mermaid-friendly token
def t(coltype):
    c = coltype.lower()
    if c == 'uuid': return 'uuid'
    if c.startswith('character varying'):
        mm = re.search(r'\((\d+)\)', c); return f'varchar' + (f'_{mm.group(1)}' if mm else '')
    if c == 'text': return 'text'
    if c.startswith('timestamp'): return 'timestamptz'
    if c == 'boolean': return 'bool'
    if c in ('integer','int'): return 'int'
    if c == 'bigint': return 'bigint'
    if c == 'smallint': return 'smallint'
    if c in ('double precision','real'): return 'double'
    if c == 'numeric' or c.startswith('numeric'): return 'numeric'
    if c == 'bytea': return 'bytea'
    if c == 'jsonb': return 'jsonb'
    if c == 'json': return 'json'
    if c == 'date': return 'date'
    if c == 'interval': return 'interval'
    if c == 'inet': return 'inet'
    if 'integer[]' in c or c.endswith('[]'): return c.replace(' ', '_')
    return re.sub(r'[^a-zA-Z0-9_]', '_', c)

# Build per-table FK property set for marking columns
fk_props = {}   # table entity full -> set(prop names that are FK)
edges = []      # (principal_table, dependent_table, label, kind)
for r in rels:
    dep = r['dependent']; pr = r['principal']
    if dep not in tables or pr not in tables:
        continue
    for p in r['fk']:
        fk_props.setdefault(dep, set()).add(p)
    dep_t = full2table[dep]; pr_t = full2table[pr]
    edges.append((pr_t, dep_t, r['fk'], r['required'], r['kind']))

def mermaid_table_block(full, info):
    name = info['table']
    pk = set(info['pk'])
    fks = fk_props.get(full, set())
    lines = [f'  "{name}" {{']
    for col in info['cols']:
        typ = t(col['type'])
        key = ''
        marks = []
        if col['prop'] in pk: marks.append('PK')
        if col['prop'] in fks: marks.append('FK')
        key = ','.join(marks)
        # mermaid: type name [key] ["comment"]
        line = f'    {typ} {col["col"]}'
        if key: line += f' {key}'
        lines.append(line)
    lines.append('  }')
    return '\n'.join(lines)

# ---- FULL diagram ----
out = ['erDiagram']
for full, info in sorted(tables.items(), key=lambda kv: kv[1]['table']):
    out.append(mermaid_table_block(full, info))
# relationships (dedupe)
seen = set()
for pr_t, dep_t, fk, req, kind in edges:
    card = '||--o{' if kind == 'many' else '||--o|'
    lbl = ','.join(fk) if fk else 'fk'
    key = (pr_t, dep_t, lbl)
    if key in seen: continue
    seen.add(key)
    out.append(f'  "{pr_t}" {card} "{dep_t}" : "{lbl}"')
open(f"{SP}/schema.full.mmd", "w").write('\n'.join(out) + '\n')

# ---- relationships-only overview (no columns) ----
ov = ['erDiagram']
# include only tables that participate in an edge to keep it lighter? include all as empty entities
edge_tables = set()
seen = set()
relines = []
for pr_t, dep_t, fk, req, kind in edges:
    card = '||--o{' if kind == 'many' else '||--o|'
    lbl = ','.join(fk) if fk else 'fk'
    key = (pr_t, dep_t, lbl)
    if key in seen: continue
    seen.add(key)
    edge_tables.add(pr_t); edge_tables.add(dep_t)
    relines.append(f'  "{pr_t}" {card} "{dep_t}" : "{lbl}"')
for tname in sorted(edge_tables):
    ov.append(f'  "{tname}" {{\n  }}')
ov += relines
open(f"{SP}/schema.overview.mmd", "w").write('\n'.join(ov) + '\n')

print("full mmd lines:", len(out))
print("overview tables:", len(edge_tables), "edges:", len(relines))
print("total columns:", sum(len(v['cols']) for v in tables.values()))
print("isolated tables (no FK edges):", sorted(set(full2table.values()) - edge_tables))
