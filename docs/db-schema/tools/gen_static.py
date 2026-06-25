import json, re, html
from collections import defaultdict

import os as _os
SP = _os.environ.get("SCHEMA_WORKDIR", _os.path.dirname(_os.path.abspath(__file__)))
m = json.load(open(f"{SP}/model.json"))
tables = m['tables']; rels = m['rels']
full2table = {k: v['table'] for k, v in tables.items()}
table2full = {v: k for k, v in full2table.items()}
exec(open(_os.path.join(_os.path.dirname(_os.path.abspath(__file__)),"_domains.py")).read())  # DOMAINS

META = {"repo":_os.environ.get("SCHEMA_REPO","nightscout/nocturne"),"branch":_os.environ.get("SCHEMA_BRANCH","main"),"commit_short":_os.environ.get("SCHEMA_COMMIT","e7937ef"),"commit_date":_os.environ.get("SCHEMA_DATE","2026-06-25")}

def ttype(coltype):
    c = coltype.lower()
    if c.startswith('character varying'):
        mm = re.search(r'\((\d+)\)', c); return 'varchar' + (f'({mm.group(1)})' if mm else '')
    if c.startswith('timestamp'): return 'timestamptz'
    repl={'uuid':'uuid','text':'text','boolean':'bool','integer':'int','bigint':'bigint','smallint':'smallint',
          'double precision':'double','real':'real','numeric':'numeric','bytea':'bytea','jsonb':'jsonb',
          'json':'json','date':'date','interval':'interval','inet':'inet'}
    return repl.get(c, c)

fk_props = defaultdict(set); refs_out=defaultdict(list); refs_in=defaultdict(list)
for r in rels:
    dep,pr=r['dependent'],r['principal']
    if dep not in tables or pr not in tables: continue
    for p in r['fk']: fk_props[dep].add(p)
    refs_out[full2table[dep]].append({"cols":r['fk'],"to":full2table[pr]})
    refs_in[full2table[pr]].append({"from":full2table[dep],"cols":r['fk']})

table_domain={}
for d,ts in DOMAINS:
    for t in ts: table_domain[t]=d

def E(s): return html.escape(str(s))
def anchor(d): return re.sub(r'[^a-z0-9]+','-',d.lower()).strip('-')

def load_svg(i):
    s=open(f"{SP}/blk_{i}.svg",encoding="utf-8").read()
    s=s.replace("background-color: white;","")
    # keep viewBox; drop fixed width so CSS controls it; keep style max-width override later
    s=re.sub(r'(<svg\b[^>]*?)\swidth="[^"]*"', r'\1', s, count=1)
    s=re.sub(r'(<svg\b[^>]*?)\sstyle="[^"]*"', r'\1', s, count=1)
    return s

ntab=len(tables); ncol=sum(len(v['cols']) for v in tables.values())
nfk=len({(r['dependent'],r['principal'],tuple(r['fk'])) for r in rels if r['dependent'] in tables and r['principal'] in tables})

def cols_table(tname):
    full=table2full[tname]; info=tables[full]; pk=set(info['pk']); fks=fk_props.get(full,set())
    rows=[]
    for c in info['cols']:
        marks=''
        if c['prop'] in pk: marks+='<span class="kp pk">PK</span>'
        if c['prop'] in fks: marks+='<span class="kp fk">FK</span>'
        req='<span class="req">*</span>' if c['required'] else ''
        rows.append(f'<tr><td class="cn">{E(c["col"])}{req}{marks}</td><td class="ct">{E(ttype(c["type"]))}</td></tr>')
    out=refs_out.get(tname,[]); inn=refs_in.get(tname,[])
    rel=''
    if out:
        rel+='<div class="rl"><span class="rh">References &rarr;</span>'+ ''.join(
            f'<div><code>{E(", ".join(r["cols"]))}</code> &rarr; <b>{E(r["to"])}</b></div>' for r in out)+'</div>'
    if inn:
        rel+='<div class="rl"><span class="rh">&larr; Referenced by</span>'+ ''.join(
            f'<div><b>{E(r["from"])}</b> <code>({E(", ".join(r["cols"]))})</code></div>' for r in inn)+'</div>'
    return (f'<details class="tbl"><summary>{E(tname)} <span class="cc">{len(info["cols"])}</span></summary>'
            f'<table class="cols">{"".join(rows)}</table>{rel}</details>')

parts=[]
parts.append(f'''<!DOCTYPE html><html lang="en"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Nocturne DB Schema &middot; {META["repo"]} @ {META["branch"]}</title>
<style>
:root{{--bg:#0b0e14;--panel:#11151f;--line:#222a3a;--line2:#2c364a;--txt:#cdd6e6;--muted:#7e8aa3;
--accent:#6ea8fe;--accent2:#8b6efe;--pk:#f2c14e;--fk:#6ee7b7;--chip:#1c2433}}
*{{box-sizing:border-box}}
body{{margin:0;background:var(--bg);color:var(--txt);font:15px/1.5 ui-sans-serif,system-ui,"Segoe UI",Roboto,Arial;-webkit-text-size-adjust:100%}}
a{{color:var(--accent);text-decoration:none}}
.wrap{{max-width:920px;margin:0 auto;padding:16px 14px 64px}}
header h1{{font-size:19px;margin:0 0 4px}} .moon{{color:var(--accent2)}}
.badge{{display:inline-block;background:var(--chip);border:1px solid var(--line2);border-radius:999px;padding:5px 11px;font-size:12px;color:var(--muted);margin:8px 0}}
.badge b{{color:var(--txt)}} .dot{{color:var(--fk)}}
.stats{{display:flex;flex-wrap:wrap;gap:8px;margin:6px 0 14px}}
.stat{{background:var(--chip);border:1px solid var(--line2);border-radius:8px;padding:5px 11px;font-size:13px}}
.stat b{{color:var(--accent)}}
.note{{background:var(--panel);border:1px solid var(--line);border-left:3px solid var(--accent);border-radius:8px;padding:10px 13px;font-size:13px;color:var(--muted);margin:0 0 18px}}
h2{{font-size:17px;margin:30px 0 4px;padding-top:8px;border-top:1px solid var(--line)}}
.toc{{columns:2;-webkit-columns:2;gap:14px;margin:0 0 8px;padding:0;list-style:none}}
.toc li{{margin:3px 0;font-size:14px}} .toc .n{{color:var(--muted);font-size:12px}}
.tlist-note{{color:var(--muted);font-size:12.5px;margin:4px 0 10px}}
details.dia>summary,details.tbl>summary{{cursor:pointer;padding:8px 10px;background:var(--panel);border:1px solid var(--line);border-radius:8px;margin-top:8px;font-weight:600;list-style:none}}
details.dia>summary::-webkit-details-marker,details.tbl>summary::-webkit-details-marker{{display:none}}
details.dia>summary::before,details.tbl>summary::before{{content:"\\25B8 ";color:var(--muted)}}
details[open].dia>summary::before,details[open].tbl>summary::before{{content:"\\25BE "}}
details.tbl{{margin:6px 0}}
.tbl summary{{font-family:ui-monospace,Menlo,monospace;font-size:13px}}
.cc{{float:right;color:var(--muted);font-weight:500;font-size:11px;background:var(--chip);padding:1px 7px;border-radius:999px}}
table.cols{{width:100%;border-collapse:collapse;font-family:ui-monospace,Menlo,monospace;font-size:12.5px;margin:6px 0}}
table.cols td{{padding:4px 8px;border-bottom:1px solid var(--line);vertical-align:top}}
td.ct{{color:var(--muted);text-align:right;white-space:nowrap}}
.kp{{font-size:9px;font-weight:700;padding:1px 5px;border-radius:4px;margin-left:6px}}
.kp.pk{{background:rgba(242,193,78,.16);color:var(--pk)}} .kp.fk{{background:rgba(110,231,183,.16);color:var(--fk)}}
.req{{color:#ff8a8a;margin-left:3px}}
.rl{{font-size:12.5px;margin:6px 0 2px;color:var(--muted)}} .rl .rh{{display:block;color:var(--txt);font-weight:600;margin-top:6px;font-size:11px;text-transform:uppercase;letter-spacing:.5px}}
.rl code{{color:var(--pk)}} .rl b{{color:var(--accent);font-weight:600}}
.svgbox{{overflow:auto;-webkit-overflow-scrolling:touch;background:#fff;border:1px solid var(--line2);border-radius:8px;margin:8px 0;max-height:75vh}}
.svgbox svg{{display:block;height:auto}}
.dia .svgbox{{margin-top:6px}}
.map .svgbox svg{{width:100%}}
footer{{margin-top:40px;color:var(--muted);font-size:12px;border-top:1px solid var(--line);padding-top:14px}}
</style></head><body><div class="wrap">
<header>
<h1><span class="moon">&#9790;</span> Nocturne &mdash; Database Schema</h1>
<div class="badge"><span class="dot">&#9679;</span> <b>{META["repo"]}</b> @ <b>{META["branch"]}</b> &middot; <b>{META["commit_short"]}</b> &middot; {META["commit_date"]}</div>
<div class="stats"><div class="stat"><b>{ntab}</b> tables</div><div class="stat"><b>{ncol}</b> columns</div><div class="stat"><b>{nfk}</b> FKs</div><div class="stat"><b>{len(DOMAINS)}</b> domains</div></div>
</header>
<div class="note">Tap a table to expand its columns and relationships. Each domain has a "Show ER diagram" toggle &mdash; the diagram canvas scrolls/pinch-zooms. This page needs no JavaScript or internet, so it renders anywhere (including the iOS Files preview).</div>
''')

# domain map
parts.append('<div class="map"><h2 id="domain-map">Domain map</h2>')
parts.append('<div class="tlist-note">How the domains reference one another (label = number of distinct FK paths).</div>')
parts.append(f'<div class="svgbox">{load_svg(0)}</div></div>')

# TOC
parts.append('<h2 id="contents">Domains</h2><ul class="toc">')
for d,ts in DOMAINS:
    parts.append(f'<li><a href="#{anchor(d)}">{E(d)}</a> <span class="n">({len(ts)})</span></li>')
parts.append('</ul>')

# per domain
for i,(d,ts) in enumerate(DOMAINS):
    parts.append(f'<h2 id="{anchor(d)}">{E(d)} <span class="cc">{len(ts)} tables</span></h2>')
    for t in ts:
        parts.append(cols_table(t))
    parts.append(f'<details class="dia"><summary>Show ER diagram</summary><div class="svgbox">{load_svg(i+1)}</div></details>')

parts.append(f'<footer>Generated from the EF Core model snapshot on <b>{META["repo"]}@{META["branch"]}</b> '
             f'(commit {META["commit_short"]}, {META["commit_date"]}). '
             f'{ntab} tables &middot; {ncol} columns &middot; {nfk} foreign keys. No-JavaScript static build.</footer>')
parts.append('</div></body></html>')

open(f"{SP}/schema-static.html","w",encoding="utf-8").write("".join(parts))
import os
print("wrote schema-static.html", os.path.getsize(f"{SP}/schema-static.html"), "bytes")
