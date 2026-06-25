import json, re, html
from collections import defaultdict

import os as _os
SP = _os.environ.get("SCHEMA_WORKDIR", _os.path.dirname(_os.path.abspath(__file__)))
m = json.load(open(f"{SP}/model.json"))
tables = m['tables']; rels = m['rels']
full2table = {k: v['table'] for k, v in tables.items()}
table2full = {v: k for k, v in full2table.items()}

# domains (same as gen_domains.py)
exec(open(_os.path.join(_os.path.dirname(_os.path.abspath(__file__)),"_domains.py")).read())  # defines DOMAINS

META = {
  "repo": _os.environ.get("SCHEMA_REPO","nightscout/nocturne"),
  "branch": _os.environ.get("SCHEMA_BRANCH","main"),
  "commit": _os.environ.get("SCHEMA_COMMIT_FULL","e7937ef9d3a43e541eb0b3f499619f8b83d1ae07"),
  "commit_short": _os.environ.get("SCHEMA_COMMIT","e7937ef"),
  "commit_date": _os.environ.get("SCHEMA_DATE","2026-06-25"),
  "commit_msg": _os.environ.get("SCHEMA_MSG",""),
}

def ttype(coltype):
    c = coltype.lower()
    if c.startswith('character varying'):
        mm = re.search(r'\((\d+)\)', c); return 'varchar' + (f'({mm.group(1)})' if mm else '')
    if c.startswith('timestamp'): return 'timestamptz'
    repl={'uuid':'uuid','text':'text','boolean':'bool','integer':'int','bigint':'bigint',
          'smallint':'smallint','double precision':'double','real':'real','numeric':'numeric',
          'bytea':'bytea','jsonb':'jsonb','json':'json','date':'date','interval':'interval','inet':'inet'}
    return repl.get(c, c)

fk_props = defaultdict(set)
refs_out = defaultdict(list)  # table -> [(cols, principal_table)]
refs_in  = defaultdict(list)  # table -> [(dependent_table, cols)]
for r in rels:
    dep, pr = r['dependent'], r['principal']
    if dep not in tables or pr not in tables: continue
    for p in r['fk']: fk_props[dep].add(p)
    dep_t, pr_t = full2table[dep], full2table[pr]
    refs_out[dep_t].append({"cols": r['fk'], "to": pr_t})
    refs_in[pr_t].append({"from": dep_t, "cols": r['fk']})

table_domain = {}
for d, ts in DOMAINS:
    for t in ts: table_domain[t] = d

model = {}
for full, info in tables.items():
    tname = info['table']; pk=set(info['pk']); fks=fk_props.get(full,set())
    cols=[]
    for c in info['cols']:
        cols.append({"name": c['col'], "type": ttype(c['type']),
                     "pk": c['prop'] in pk, "fk": c['prop'] in fks,
                     "req": c['required']})
    model[tname] = {"domain": table_domain[tname], "cols": cols,
                    "out": refs_out.get(tname, []), "in": refs_in.get(tname, [])}

# load SVGs: blk_0 = domain map, blk_1..N = domains in order
def load_svg(i):
    s = open(f"{SP}/blk_{i}.svg", encoding="utf-8").read()
    s = s.replace("background-color: white;", "")
    return s

domain_svgs = []
for i,(d,ts) in enumerate(DOMAINS):
    domain_svgs.append({"name": d, "tables": ts, "svg": load_svg(i+1)})
map_svg = load_svg(0)

payload = {"meta": META, "model": model, "domains": [{"name":d["name"],"tables":d["tables"]} for d in domain_svgs]}

# Build HTML
def jsstr(s):  # safe embed of arbitrary string into <script> via JSON
    return json.dumps(s)

svgs_js = "{\n" + ",\n".join(
    [f'  "map": {jsstr(map_svg)}'] +
    [f'  "d{i}": {jsstr(d["svg"])}' for i,d in enumerate(domain_svgs)]
) + "\n}"

ntab=len(model); ncol=sum(len(v['cols']) for v in model.values())
nfk=len({(r['dependent'],r['principal'],tuple(r['fk'])) for r in rels if r['dependent'] in tables and r['principal'] in tables})

HTML = r'''<!DOCTYPE html>
<html lang="en"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Nocturne DB Schema &middot; __REPO__ @ __BRANCH__</title>
<style>
:root{
 --bg:#0b0e14; --panel:#11151f; --panel2:#161b27; --line:#222a3a; --line2:#2c364a;
 --txt:#cdd6e6; --muted:#7e8aa3; --accent:#6ea8fe; --accent2:#8b6efe;
 --pk:#f2c14e; --fk:#6ee7b7; --chip:#1c2433;
 --shadow:0 8px 30px rgba(0,0,0,.45);
}
*{box-sizing:border-box}
html,body{margin:0;height:100%}
body{background:var(--bg);color:var(--txt);font:14px/1.45 ui-sans-serif,system-ui,"Segoe UI",Roboto,Helvetica,Arial;overflow:hidden}
a{color:var(--accent);text-decoration:none}
#app{display:grid;grid-template-rows:auto 1fr;height:100vh;height:100dvh;width:100vw;max-width:100vw;overflow:hidden}
.menu-btn{display:none;background:var(--chip);border:1px solid var(--line2);color:var(--txt);width:38px;height:38px;border-radius:8px;font-size:18px;cursor:pointer;flex:0 0 auto}
.backdrop{display:none;position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:9}
.backdrop.show{display:block}
header{display:flex;align-items:center;gap:16px;padding:12px 18px;background:linear-gradient(180deg,#10141e,#0c0f17);border-bottom:1px solid var(--line);flex-wrap:wrap;min-width:0}
header .logo{font-weight:700;font-size:16px;letter-spacing:.3px}
header .logo .moon{color:var(--accent2)}
header .sub{color:var(--muted);font-size:12px}
header .spacer{flex:1}
.badge{display:inline-flex;align-items:center;gap:6px;background:var(--chip);border:1px solid var(--line2);border-radius:999px;padding:4px 10px;font-size:12px;color:var(--muted)}
.badge b{color:var(--txt);font-weight:600}
.badge .dot{width:7px;height:7px;border-radius:50%;background:var(--fk)}
.stats{display:flex;gap:8px}
.stat{background:var(--chip);border:1px solid var(--line2);border-radius:8px;padding:4px 10px;font-size:12px}
.stat b{color:var(--accent);font-size:14px}
main{display:grid;grid-template-columns:288px 1fr;min-height:0}
aside{background:var(--panel);border-right:1px solid var(--line);display:flex;flex-direction:column;min-height:0}
.search{padding:12px}
.search input{width:100%;background:var(--panel2);border:1px solid var(--line2);border-radius:8px;color:var(--txt);padding:9px 11px;outline:none;font-size:13px}
.search input:focus{border-color:var(--accent)}
.nav{overflow:auto;padding:0 8px 16px;min-height:0}
.domain{margin:6px 4px}
.domain>button{width:100%;text-align:left;background:transparent;border:0;color:var(--txt);padding:8px 10px;border-radius:8px;cursor:pointer;display:flex;align-items:center;gap:8px;font-size:13px;font-weight:600}
.domain>button:hover{background:var(--panel2)}
.domain>button.active{background:#17233b;color:#fff}
.domain .cnt{margin-left:auto;color:var(--muted);font-weight:500;font-size:11px;background:var(--chip);padding:1px 7px;border-radius:999px}
.tlist{list-style:none;margin:2px 0 2px;padding:0 0 0 6px}
.tlist li{padding:5px 10px;border-radius:6px;cursor:pointer;color:var(--muted);font-size:12.5px;display:flex;align-items:center;gap:7px}
.tlist li:hover{background:var(--panel2);color:var(--txt)}
.tlist li.hit{color:var(--txt)}
.tlist li .k{margin-left:auto;font-size:10px;color:var(--pk)}
.canvas-wrap{position:relative;min-width:0;overflow:hidden;background:
  radial-gradient(circle at 1px 1px, #1a2133 1px, transparent 0) 0 0/22px 22px, var(--bg)}
.toolbar{position:absolute;top:12px;left:12px;right:12px;display:flex;align-items:center;gap:10px;z-index:5;pointer-events:none}
.toolbar .title{pointer-events:auto;background:rgba(17,21,31,.85);border:1px solid var(--line2);border-radius:8px;padding:6px 12px;font-weight:600;backdrop-filter:blur(6px)}
.toolbar .title small{color:var(--muted);font-weight:500;margin-left:8px}
.zoomers{margin-left:auto;display:flex;gap:6px;pointer-events:auto}
.zoomers button{background:rgba(17,21,31,.85);border:1px solid var(--line2);color:var(--txt);width:34px;height:34px;border-radius:8px;cursor:pointer;font-size:16px;backdrop-filter:blur(6px)}
.zoomers button:hover{border-color:var(--accent);color:#fff}
.zoomers .lbl{min-width:54px;text-align:center;display:flex;align-items:center;justify-content:center;color:var(--muted);font-size:12px;background:rgba(17,21,31,.7);border-radius:8px}
#stage{position:absolute;inset:0;cursor:grab}
#stage.drag{cursor:grabbing}
#pan{transform-origin:0 0;will-change:transform}
#pan svg{display:block;max-width:none!important;height:auto}
.hint{position:absolute;bottom:12px;left:12px;color:var(--muted);font-size:11px;background:rgba(17,21,31,.7);padding:5px 10px;border-radius:6px;z-index:5}
/* table detail drawer */
.drawer{position:absolute;top:0;right:0;height:100%;width:360px;background:var(--panel);border-left:1px solid var(--line);transform:translateX(100%);transition:transform .18s ease;z-index:8;display:flex;flex-direction:column;box-shadow:var(--shadow)}
.drawer.open{transform:none}
.drawer header{background:var(--panel2);justify-content:space-between}
.drawer .tname{font-weight:700;font-size:15px;font-family:ui-monospace,SFMono-Regular,Menlo,monospace}
.drawer .dom{color:var(--muted);font-size:11px}
.drawer .close{background:transparent;border:0;color:var(--muted);font-size:20px;cursor:pointer}
.drawer .body{overflow:auto;padding:12px 14px}
.sec{margin:6px 0 14px}
.sec h4{margin:0 0 7px;color:var(--muted);font-size:11px;text-transform:uppercase;letter-spacing:.6px;font-weight:700}
table.cols{width:100%;border-collapse:collapse;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:12px}
table.cols td{padding:4px 6px;border-bottom:1px solid var(--line);vertical-align:top}
table.cols td.t{color:var(--muted)}
.kpill{font-size:9px;font-weight:700;padding:1px 5px;border-radius:4px;margin-left:5px;vertical-align:middle}
.kpill.pk{background:rgba(242,193,78,.16);color:var(--pk)}
.kpill.fk{background:rgba(110,231,183,.16);color:var(--fk)}
.req{color:#ff8a8a;margin-left:3px}
.rel{font-family:ui-monospace,monospace;font-size:12px;padding:4px 0;border-bottom:1px solid var(--line);cursor:pointer}
.rel:hover{color:#fff}
.rel .col{color:var(--pk)} .rel .arrow{color:var(--muted);margin:0 6px} .rel .tgt{color:var(--accent)}
.empty{color:var(--muted);font-size:12px;font-style:italic}
::-webkit-scrollbar{width:10px;height:10px}::-webkit-scrollbar-thumb{background:#202840;border-radius:8px}::-webkit-scrollbar-track{background:transparent}
@media (max-width:760px){
  header{gap:10px;padding:10px 12px}
  header .logo{font-size:15px}
  .menu-btn{display:inline-flex;align-items:center;justify-content:center}
  .stats{order:3;width:100%;gap:6px;flex-wrap:wrap}
  .badge{order:2;font-size:11px;flex:1 1 100%;justify-content:flex-start}
  .spacer{display:none}
  main{grid-template-columns:1fr}
  aside{position:fixed;top:0;left:0;height:100vh;height:100dvh;width:min(86vw,320px);z-index:10;
        transform:translateX(-100%);transition:transform .2s ease;box-shadow:var(--shadow)}
  aside.open{transform:none}
  .drawer{width:100%}
  .toolbar{top:8px;left:8px;right:8px}
  .hint{display:none}
}
</style></head>
<body><div id="app">
<header>
  <button class="menu-btn" id="menu" title="Menu" aria-label="Toggle table list">&#9776;</button>
  <div class="logo"><span class="moon">&#9790;</span> Nocturne <span style="color:var(--muted);font-weight:500">DB Schema</span></div>
  <div class="badge"><span class="dot"></span> <b>__REPO__</b> @ <b>__BRANCH__</b> &middot; <b>__CSHORT__</b> &middot; __CDATE__</div>
  <div class="spacer"></div>
  <div class="stats">
    <div class="stat"><b>__NTAB__</b> tables</div>
    <div class="stat"><b>__NCOL__</b> columns</div>
    <div class="stat"><b>__NFK__</b> FKs</div>
    <div class="stat"><b>__NDOM__</b> domains</div>
  </div>
</header>
<main>
  <aside>
    <div class="search"><input id="q" placeholder="Search tables &amp; columns&hellip;" autocomplete="off"></div>
    <div class="nav" id="nav"></div>
  </aside>
  <div class="canvas-wrap">
    <div class="toolbar">
      <div class="title" id="vtitle">Domain map</div>
      <div class="zoomers">
        <button id="zout" title="Zoom out">&minus;</button>
        <div class="lbl" id="zlbl">100%</div>
        <button id="zin" title="Zoom in">+</button>
        <button id="zfit" title="Fit">&#9633;</button>
      </div>
    </div>
    <div id="stage"><div id="pan"></div></div>
    <div class="hint">Scroll to zoom &middot; drag to pan &middot; click a table for details</div>
    <div class="drawer" id="drawer">
      <header><div><div class="tname" id="dname"></div><div class="dom" id="ddom"></div></div>
        <button class="close" id="dclose">&times;</button></header>
      <div class="body" id="dbody"></div>
    </div>
  </div>
</main>
<div class="backdrop" id="backdrop"></div>
</div>
<script>
const SVGS = __SVGS__;
const DATA = __DATA__;
const MODEL = DATA.model, DOMAINS = DATA.domains;
let cur = "map", scale = 1, tx = 0, ty = 0;

const pan = document.getElementById('pan'), stage = document.getElementById('stage');
const vtitle = document.getElementById('vtitle'), zlbl = document.getElementById('zlbl');

function applyT(){ pan.style.transform = `translate(${tx}px,${ty}px) scale(${scale})`; zlbl.textContent = Math.round(scale*100)+'%'; }
function vbOf(svg){
  const a=(svg.getAttribute('viewBox')||'').split(/[\s,]+/).map(Number);
  if(a.length===4 && a[2]>0 && a[3]>0) return {w:a[2], h:a[3]};
  return null;
}
function sizeSvg(svg){
  const vb=vbOf(svg); if(!vb) return null;
  svg.removeAttribute('width'); svg.removeAttribute('height');
  svg.style.width=vb.w+'px'; svg.style.height=vb.h+'px'; svg.style.maxWidth='none';
  return vb;
}
function show(key, title){
  cur = key; pan.innerHTML = SVGS[key] || '<div style="color:#7e8aa3;padding:24px">diagram unavailable</div>';
  vtitle.innerHTML = title;
  const svg = pan.querySelector('svg');
  if(svg) sizeSvg(svg);   // set explicit px size immediately so it is never zero-width
  fit();
  requestAnimationFrame(fit); setTimeout(fit,120); setTimeout(fit,450);
}
function fit(){
  const svg = pan.querySelector('svg'); if(!svg) return;
  const vb = sizeSvg(svg); if(!vb) return;
  const cw = stage.clientWidth||stage.offsetWidth||window.innerWidth;
  const ch = stage.clientHeight||stage.offsetHeight||(window.innerHeight-120);
  const pad = 28;
  scale = Math.min((cw-pad)/vb.w, (ch-pad)/vb.h, 1.4);
  if(!isFinite(scale)||scale<=0) scale = 1;
  tx = (cw - vb.w*scale)/2; ty = (ch - vb.h*scale)/2;
  applyT();
}
// pan/zoom
stage.addEventListener('wheel', e=>{
  e.preventDefault();
  const r = stage.getBoundingClientRect(); const mx = e.clientX-r.left, my = e.clientY-r.top;
  const f = Math.exp(-e.deltaY*0.0015); const ns = Math.min(Math.max(scale*f,0.08),6);
  tx = mx - (mx-tx)*(ns/scale); ty = my - (my-ty)*(ns/scale); scale = ns; applyT();
},{passive:false});
let dragging=false,sx,sy;
stage.addEventListener('mousedown',e=>{dragging=true;sx=e.clientX-tx;sy=e.clientY-ty;stage.classList.add('drag');});
window.addEventListener('mousemove',e=>{if(dragging){tx=e.clientX-sx;ty=e.clientY-sy;applyT();}});
window.addEventListener('mouseup',()=>{dragging=false;stage.classList.remove('drag');});
// touch: one finger pans, two fingers pinch-zoom
let tDrag=false, tsx=0, tsy=0, pinch=0, pcx=0, pcy=0;
function tdist(t){const dx=t[0].clientX-t[1].clientX,dy=t[0].clientY-t[1].clientY;return Math.hypot(dx,dy);}
stage.addEventListener('touchstart',e=>{
  const r=stage.getBoundingClientRect();
  if(e.touches.length===1){tDrag=true;tsx=e.touches[0].clientX-tx;tsy=e.touches[0].clientY-ty;}
  else if(e.touches.length===2){tDrag=false;pinch=tdist(e.touches);
    pcx=(e.touches[0].clientX+e.touches[1].clientX)/2-r.left;
    pcy=(e.touches[0].clientY+e.touches[1].clientY)/2-r.top;}
},{passive:true});
stage.addEventListener('touchmove',e=>{
  const r=stage.getBoundingClientRect();
  if(e.touches.length===2&&pinch){
    const nd=tdist(e.touches); const ns=Math.min(Math.max(scale*(nd/pinch),0.05),6);
    tx=pcx-(pcx-tx)*(ns/scale); ty=pcy-(pcy-ty)*(ns/scale); scale=ns; pinch=nd; applyT();
    e.preventDefault();
  } else if(e.touches.length===1&&tDrag){
    tx=e.touches[0].clientX-tsx; ty=e.touches[0].clientY-tsy; applyT(); e.preventDefault();
  }
},{passive:false});
stage.addEventListener('touchend',e=>{if(e.touches.length===0){tDrag=false;pinch=0;}});
document.getElementById('zin').onclick=()=>zoomBtn(1.25);
document.getElementById('zout').onclick=()=>zoomBtn(0.8);
document.getElementById('zfit').onclick=fit;
function zoomBtn(f){const cw=stage.clientWidth/2,ch=stage.clientHeight/2;const ns=Math.min(Math.max(scale*f,0.08),6);
  tx=cw-(cw-tx)*(ns/scale);ty=ch-(ch-ty)*(ns/scale);scale=ns;applyT();}

// sidebar (mobile drawer)
const aside = document.querySelector('aside'), backdrop = document.getElementById('backdrop');
const isMobile = ()=>window.matchMedia('(max-width:760px)').matches;
function openSidebar(){ aside.classList.add('open'); backdrop.classList.add('show'); }
function closeSidebar(){ aside.classList.remove('open'); backdrop.classList.remove('show'); }
document.getElementById('menu').onclick=()=>{ aside.classList.contains('open')?closeSidebar():openSidebar(); };
backdrop.onclick=closeSidebar;
const nav = document.getElementById('nav');
function buildNav(filter){
  filter = (filter||'').trim().toLowerCase();
  nav.innerHTML='';
  // domain map entry
  if(!filter){
    const d=document.createElement('div'); d.className='domain';
    d.innerHTML=`<button data-map="1" class="${cur==='map'?'active':''}">&#9737; Domain map</button>`;
    d.querySelector('button').onclick=()=>{show('map','Domain map');buildNav(document.getElementById('q').value);if(isMobile())closeSidebar();};
    nav.appendChild(d);
  }
  DOMAINS.forEach((dom,i)=>{
    let hits = dom.tables;
    if(filter){
      hits = dom.tables.filter(t=> t.toLowerCase().includes(filter) ||
        MODEL[t].cols.some(c=>c.name.toLowerCase().includes(filter)));
      if(!hits.length) return;
    }
    const wrap=document.createElement('div'); wrap.className='domain';
    const active = cur==='d'+i;
    wrap.innerHTML=`<button class="${active?'active':''}">${esc(dom.name)}<span class="cnt">${dom.tables.length}</span></button>`;
    wrap.querySelector('button').onclick=()=>{show('d'+i, esc(dom.name)+' <small>'+dom.tables.length+' tables</small>');buildNav(document.getElementById('q').value);if(isMobile())closeSidebar();};
    const ul=document.createElement('ul'); ul.className='tlist';
    (filter?hits:dom.tables).forEach(t=>{
      const li=document.createElement('li'); li.className='hit';
      const hasPk = MODEL[t].cols.some(c=>c.pk);
      li.innerHTML=`<span>${esc(t)}</span>`;
      li.onclick=()=>{ openTable(t); };
      ul.appendChild(li);
    });
    if(filter || active) wrap.appendChild(ul);
    nav.appendChild(wrap);
  });
}
document.getElementById('q').addEventListener('input',e=>buildNav(e.target.value));

// table drawer
const drawer=document.getElementById('drawer');
document.getElementById('dclose').onclick=()=>{drawer.classList.remove('open');clearHi();};
function openTable(t){
  const info=MODEL[t]; if(!info) return;
  const di = DOMAINS.findIndex(d=>d.name===info.domain);
  if(cur!=='d'+di){ show('d'+di, esc(info.domain)+' <small>'+DOMAINS[di].tables.length+' tables</small>'); buildNav(document.getElementById('q').value); }
  document.getElementById('dname').textContent=t;
  document.getElementById('ddom').textContent=info.domain;
  let h='<div class="sec"><h4>Columns ('+info.cols.length+')</h4><table class="cols">';
  info.cols.forEach(c=>{
    let k=''; if(c.pk)k+='<span class="kpill pk">PK</span>'; if(c.fk)k+='<span class="kpill fk">FK</span>';
    h+=`<tr><td>${esc(c.name)}${c.req?'<span class="req" title="required">*</span>':''}${k}</td><td class="t">${esc(c.type)}</td></tr>`;
  });
  h+='</table></div>';
  h+='<div class="sec"><h4>References &rarr; ('+info.out.length+')</h4>';
  if(info.out.length){ info.out.forEach(r=>{ h+=`<div class="rel" data-go="${esc(r.to)}"><span class="col">${esc(r.cols.join(', '))}</span><span class="arrow">&rarr;</span><span class="tgt">${esc(r.to)}</span></div>`; }); }
  else h+='<div class="empty">none</div>';
  h+='</div>';
  h+='<div class="sec"><h4>Referenced by &larr; ('+info.in.length+')</h4>';
  if(info.in.length){ info.in.forEach(r=>{ h+=`<div class="rel" data-go="${esc(r.from)}"><span class="tgt">${esc(r.from)}</span><span class="arrow">&rarr;</span><span class="col">${esc(r.cols.join(', '))}</span></div>`; }); }
  else h+='<div class="empty">none</div>';
  h+='</div>';
  document.getElementById('dbody').innerHTML=h;
  drawer.querySelectorAll('.rel').forEach(el=>el.onclick=()=>openTable(el.dataset.go));
  drawer.classList.add('open');
  if(isMobile()) closeSidebar();
  highlight(t);
}
let hiEl=null;
function clearHi(){ if(hiEl){hiEl.style.outline='';hiEl.style.outlineOffset='';hiEl=null;} }
function highlight(t){
  clearHi();
  const g=pan.querySelector('[id*="-entity-'+cssesc(t)+'-"]');
  if(g){ const box=g.querySelector('rect')||g; box.style.outline='3px solid var(--accent)'; box.style.outlineOffset='2px';
    hiEl=box;
    // center it
    const bb=g.getBoundingClientRect(), sr=stage.getBoundingClientRect();
    const cx=bb.left+bb.width/2-sr.left, cy=bb.top+bb.height/2-sr.top;
    tx += (sr.width/2 - cx); ty += (sr.height/2 - cy); applyT();
  }
}
function esc(s){return String(s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));}
function cssesc(s){return s.replace(/[^a-zA-Z0-9_-]/g,'\\$&');}

try{ buildNav(''); show('map','Domain map'); }
catch(err){ document.getElementById('nav').innerHTML='<div style="padding:14px;color:#ff8a8a">init error: '+esc(err.message)+'</div>'; }
window.addEventListener('resize',()=>{ if(!drawer.classList.contains('open')) fit(); });
window.addEventListener('load',()=>requestAnimationFrame(fit));
</script>
</body></html>'''

HTML = (HTML
  .replace("__SVGS__", svgs_js)
  .replace("__DATA__", json.dumps(payload))
  .replace("__REPO__", META["repo"]).replace("__BRANCH__", META["branch"])
  .replace("__CSHORT__", META["commit_short"]).replace("__CDATE__", META["commit_date"])
  .replace("__NTAB__", str(ntab)).replace("__NCOL__", str(ncol))
  .replace("__NFK__", str(nfk)).replace("__NDOM__", str(len(DOMAINS))))

open(f"{SP}/schema.html","w",encoding="utf-8").write(HTML)
print("wrote schema.html", len(HTML), "bytes; tables", ntab, "cols", ncol, "fks", nfk)
