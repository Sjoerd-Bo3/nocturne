import re, json, sys

import os as _os
SP = _os.environ.get("SCHEMA_WORKDIR", _os.path.dirname(_os.path.abspath(__file__)))
src = open(f"{SP}/snapshot.cs", encoding="utf-8-sig").read()

# Split into top-level `modelBuilder.Entity("Name", b =>` blocks by brace matching.
blocks = []
for m in re.finditer(r'modelBuilder\.Entity\(\s*"([^"]+)"\s*,\s*b\s*=>', src):
    name = m.group(1)
    # find the opening brace of the lambda body after the match
    i = src.index('{', m.end())
    depth = 0
    j = i
    while j < len(src):
        c = src[j]
        if c == '{': depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                break
        j += 1
    body = src[i:j+1]
    blocks.append((name, body))

def short(fullname):
    # strip namespace + trailing "Entity"
    n = fullname.split('.')[-1]
    return n

tables = {}      # entity fullname -> dict(table, cols=[], pk=[], indexes=[])
owned = {}       # owned entity fullname -> info (no own table; merged)
rels = []        # (principal_full, dependent_full, fk_cols, required, principal_nav, dependent_nav, kind)

for name, body in blocks:
    is_def = '.ToTable(' in body or 'b.Property<' in body
    has_rel = 'b.HasOne(' in body or 'b.HasMany(' in body
    # A block can be definition-only, relationship-only, or owned-type def.
    if '.ToTable(' in body:
        # table definition
        cols = []
        for pm in re.finditer(r'b\.Property<([^>]+)>\("([^"]+)"\)(.*?)(?=\n\s*b\.(?:Property<|HasKey|HasIndex|HasAlternateKey|ToTable|HasOne|HasMany|OwnsOne|OwnsMany|Navigation)|\Z)', body, re.S):
            cstype, prop, attrs = pm.group(1), pm.group(2), pm.group(3)
            colname = None
            cm = re.search(r'HasColumnName\("([^"]+)"\)', attrs)
            colname = cm.group(1) if cm else prop
            tm = re.search(r'HasColumnType\("([^"]+)"\)', attrs)
            coltype = tm.group(1) if tm else cstype
            required = '.IsRequired()' in attrs
            cols.append({'prop': prop, 'col': colname, 'type': coltype, 'required': required, 'cs': cstype})
        pk = []
        km = re.search(r'b\.HasKey\(([^;]+)\);', body)
        if km:
            pk = re.findall(r'"([^"]+)"', km.group(1))
        idxs = []
        for im in re.finditer(r'b\.HasIndex\(([^)]*)\)', body):
            idxs.append(re.findall(r'"([^"]+)"', im.group(1)))
        tm = re.search(r'\.ToTable\("([^"]+)"', body)
        table = tm.group(1) if tm else short(name)
        tables.setdefault(name, {'table': table, 'cols': cols, 'pk': pk, 'indexes': idxs})

for name, body in blocks:
    # relationships
    # pattern: b.HasOne("Principal"[, "Nav"]) .WithMany/WithOne(...) .HasForeignKey("..."...) ... .IsRequired()
    for rm in re.finditer(r'b\.HasOne\(\s*"([^"]+)"(?:\s*,\s*(?:"([^"]+)"|null))?\s*\)(.*?)(?=\n\s*b\.HasOne\(|\n\s*b\.OwnsOne|\n\s*b\.OwnsMany|\n\s*b\.Navigation|\Z)', body, re.S):
        principal, nav, rest = rm.group(1), rm.group(2), rm.group(3)
        fk = []
        fkm = re.search(r'HasForeignKey\(([^)]*)\)', rest)
        if fkm:
            fk = re.findall(r'"([^"]+)"', fkm.group(1))
        required = '.IsRequired()' in rest
        withmany = '.WithMany(' in rest
        rels.append({'principal': principal, 'dependent': name, 'fk': fk,
                     'required': required, 'kind': 'many' if withmany else 'one'})

out = {'tables': tables, 'rels': rels}
json.dump(out, open(f"{SP}/model.json", "w"), indent=1)
print("tables:", len(tables))
print("rels:", len(rels))
print("sample tables:", [v['table'] for v in list(tables.values())[:8]])
# detect dependents referencing principals not in tables (owned types etc.)
tnames = set(tables.keys())
missing_p = sorted({r['principal'] for r in rels if r['principal'] not in tnames})
print("principals not in tables:", missing_p[:20], "..." if len(missing_p)>20 else "")
