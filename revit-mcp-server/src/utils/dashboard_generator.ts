/**
 * Dashboard Generator — Builds a self-contained HTML file for BIM compliance reporting.
 *
 * The output is a single HTML file with:
 *  - Chart.js via CDN for visualizations
 *  - Inline CSS with dark glassmorphism theme
 *  - 5 pages: Overview, Compliance, Schedules, Issues, Analytics
 *  - Client-side search/filter/sort on all tables
 *  - Print-friendly CSS media queries
 */

// ── Types ──────────────────────────────────────────────────────

export interface ComplianceIssue {
    elementId: string;
    elementName: string;
    category: string;
    level: string;
    severity: "critical" | "warning" | "info";
    rule: string;
    message: string;
    suggestion: string;
}

export interface CategorySummary {
    category: string;
    totalElements: number;
    passCount: number;
    warnCount: number;
    failCount: number;
    complianceScore: number;
    parameterFillRate: number;
    missingParams: Record<string, number>;
}

export interface ElementRow {
    id: string;
    name: string;
    category: string;
    typeName: string;
    level: string;
    mark: string;
    status: "pass" | "warning" | "fail";
    missingParams: string[];
    issues: string[];
    [key: string]: unknown;
}

export interface DashboardData {
    projectName: string;
    generatedAt: string;
    bepVersion: string;
    midpVersion: string;
    overallScore: number;
    totalElements: number;
    totalPass: number;
    totalWarn: number;
    totalFail: number;
    categories: CategorySummary[];
    elements: ElementRow[];
    issues: ComplianceIssue[];
    levelDistribution: Record<string, number>;
    configuredCategories: string[];
}

// ── Generator ──────────────────────────────────────────────────

export function generateDashboardHtml(data: DashboardData): string {
    const jsonData = JSON.stringify(data);
    const ts = data.generatedAt;

    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>BIM Dashboard — ${esc(data.projectName)}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<style>${getCSS()}</style>
</head>
<body>
<nav id="sidebar">
  <div class="logo">🏗️ BIM Dashboard</div>
  <div class="nav-links">
    <a class="nav-link active" data-page="overview" onclick="showPage('overview')">📊 Overview</a>
    <a class="nav-link" data-page="compliance" onclick="showPage('compliance')">✅ Compliance</a>
    <a class="nav-link" data-page="schedules" onclick="showPage('schedules')">📋 Schedules</a>
    <a class="nav-link" data-page="issues" onclick="showPage('issues')">⚠️ Issues</a>
    <a class="nav-link" data-page="analytics" onclick="showPage('analytics')">📈 Analytics</a>
  </div>
  <div class="nav-footer">
    <small>Generated: ${esc(ts)}</small>
  </div>
</nav>
<main id="content">
  <!-- Overview -->
  <section id="page-overview" class="page active">
    <h1>Project Overview</h1>
    <p class="subtitle">${esc(data.projectName)} — BEP v${esc(data.bepVersion)} · MIDP v${esc(data.midpVersion)}</p>
    <div class="cards">
      <div class="card accent">${scoreGauge(data.overallScore)}<span class="card-label">Overall Score</span></div>
      <div class="card"><span class="card-value">${data.totalElements}</span><span class="card-label">Total Elements</span></div>
      <div class="card pass"><span class="card-value">${data.totalPass}</span><span class="card-label">✅ Pass</span></div>
      <div class="card warn"><span class="card-value">${data.totalWarn}</span><span class="card-label">⚠️ Warnings</span></div>
      <div class="card fail"><span class="card-value">${data.totalFail}</span><span class="card-label">❌ Failures</span></div>
    </div>
    <div class="chart-row">
      <div class="chart-box"><canvas id="catBarChart"></canvas></div>
      <div class="chart-box"><canvas id="levelPieChart"></canvas></div>
    </div>
  </section>

  <!-- Compliance -->
  <section id="page-compliance" class="page">
    <h1>BEP/MIDP Compliance</h1>
    <div class="table-wrap">
      <table id="complianceTable">
        <thead><tr><th>Category</th><th>Elements</th><th>Pass</th><th>Warn</th><th>Fail</th><th>Score</th><th>Param Fill</th></tr></thead>
        <tbody>${data.categories.map(c => `<tr>
          <td>${esc(c.category)}</td><td>${c.totalElements}</td>
          <td class="pass-cell">${c.passCount}</td><td class="warn-cell">${c.warnCount}</td><td class="fail-cell">${c.failCount}</td>
          <td>${badge(c.complianceScore)}</td><td>${c.parameterFillRate}%</td>
        </tr>`).join("")}</tbody>
      </table>
    </div>
    <h2>Missing Parameters by Category</h2>
    <div class="chart-row"><div class="chart-box wide"><canvas id="paramRadar"></canvas></div></div>
  </section>

  <!-- Schedules -->
  <section id="page-schedules" class="page">
    <h1>Element Schedule</h1>
    <div class="toolbar">
      <input type="text" id="scheduleSearch" placeholder="🔍 Search elements..." oninput="filterSchedule()">
      <select id="catFilter" onchange="filterSchedule()"><option value="">All Categories</option>${data.configuredCategories.map(c => `<option>${esc(c)}</option>`).join("")}</select>
      <select id="statusFilter" onchange="filterSchedule()"><option value="">All Status</option><option value="pass">Pass</option><option value="warning">Warning</option><option value="fail">Fail</option></select>
      <button onclick="exportCSV()">⬇ Export CSV</button>
    </div>
    <div class="table-wrap">
      <table id="scheduleTable">
        <thead><tr><th onclick="sortTable(0)">ID ↕</th><th onclick="sortTable(1)">Name ↕</th><th onclick="sortTable(2)">Category ↕</th><th onclick="sortTable(3)">Type ↕</th><th onclick="sortTable(4)">Level ↕</th><th onclick="sortTable(5)">Mark</th><th onclick="sortTable(6)">Status ↕</th><th>Issues</th></tr></thead>
        <tbody id="scheduleBody"></tbody>
      </table>
    </div>
    <div id="scheduleCount" class="count-label"></div>
  </section>

  <!-- Issues -->
  <section id="page-issues" class="page">
    <h1>Issues & Recommendations</h1>
    <div class="toolbar">
      <select id="sevFilter" onchange="filterIssues()"><option value="">All Severities</option><option value="critical">Critical</option><option value="warning">Warning</option><option value="info">Info</option></select>
    </div>
    <div id="issuesList"></div>
  </section>

  <!-- Analytics -->
  <section id="page-analytics" class="page">
    <h1>Analytics</h1>
    <div class="chart-row">
      <div class="chart-box"><canvas id="catDoughnut"></canvas></div>
      <div class="chart-box"><canvas id="scoreBar"></canvas></div>
    </div>
    <div class="chart-row">
      <div class="chart-box wide"><canvas id="levelBar"></canvas></div>
    </div>
  </section>
</main>

<script>
const D=${jsonData};
${getJS()}
</script>
</body>
</html>`;
}

// ── Helpers ─────────────────────────────────────────────────────

function esc(s: string): string {
    return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function scoreGauge(score: number): string {
    const color = score >= 80 ? "#22c55e" : score >= 60 ? "#eab308" : "#ef4444";
    return `<span class="gauge" style="--score:${score};--color:${color}">${score}%</span>`;
}

function badge(score: number): string {
    const cls = score >= 80 ? "pass" : score >= 60 ? "warn" : "fail";
    return `<span class="badge ${cls}">${score}%</span>`;
}

// ── Inline CSS ──────────────────────────────────────────────────

function getCSS(): string {
    return `
*{margin:0;padding:0;box-sizing:border-box}
:root{--bg:#0f172a;--bg2:#1e293b;--card:#1e293b;--border:#334155;--text:#e2e8f0;--text2:#94a3b8;--accent:#3b82f6;--pass:#22c55e;--warn:#eab308;--fail:#ef4444}
body{font-family:'Inter',sans-serif;background:linear-gradient(135deg,var(--bg),var(--bg2));color:var(--text);display:flex;min-height:100vh}
#sidebar{width:220px;background:rgba(15,23,42,.95);border-right:1px solid var(--border);padding:1.5rem 1rem;display:flex;flex-direction:column;position:fixed;height:100vh;backdrop-filter:blur(12px)}
.logo{font-size:1.2rem;font-weight:700;margin-bottom:2rem;color:var(--accent)}
.nav-links{flex:1;display:flex;flex-direction:column;gap:.25rem}
.nav-link{padding:.7rem 1rem;border-radius:8px;color:var(--text2);cursor:pointer;text-decoration:none;transition:all .2s}
.nav-link:hover,.nav-link.active{background:rgba(59,130,246,.15);color:var(--accent)}
.nav-footer{font-size:.7rem;color:var(--text2);border-top:1px solid var(--border);padding-top:1rem}
#content{margin-left:220px;flex:1;padding:2rem 2.5rem;max-width:1200px}
.page{display:none;animation:fadeIn .3s ease}
.page.active{display:block}
@keyframes fadeIn{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:none}}
h1{font-size:1.8rem;font-weight:700;margin-bottom:.3rem}
h2{font-size:1.2rem;font-weight:600;margin:1.5rem 0 .8rem;color:var(--text2)}
.subtitle{color:var(--text2);margin-bottom:1.5rem}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:1rem;margin-bottom:2rem}
.card{background:var(--card);border:1px solid var(--border);border-radius:12px;padding:1.2rem;text-align:center;backdrop-filter:blur(8px);transition:transform .2s}
.card:hover{transform:translateY(-3px)}
.card-value{display:block;font-size:2rem;font-weight:700}
.card-label{display:block;font-size:.8rem;color:var(--text2);margin-top:.3rem}
.card.accent{border-color:var(--accent)}
.card.pass .card-value{color:var(--pass)}.card.warn .card-value{color:var(--warn)}.card.fail .card-value{color:var(--fail)}
.gauge{display:block;font-size:2.5rem;font-weight:700;color:var(--color)}
.chart-row{display:grid;grid-template-columns:1fr 1fr;gap:1.5rem;margin-bottom:2rem}
.chart-box{background:var(--card);border:1px solid var(--border);border-radius:12px;padding:1.2rem;min-height:280px}
.chart-box.wide{grid-column:1/-1}
.table-wrap{overflow-x:auto;margin-bottom:1rem}
table{width:100%;border-collapse:collapse;font-size:.85rem}
th{background:var(--bg);padding:.7rem .6rem;text-align:left;cursor:pointer;position:sticky;top:0;border-bottom:2px solid var(--accent);user-select:none}
td{padding:.55rem .6rem;border-bottom:1px solid var(--border)}
tr:hover td{background:rgba(59,130,246,.06)}
.pass-cell{color:var(--pass)}.warn-cell{color:var(--warn)}.fail-cell{color:var(--fail)}
.badge{padding:2px 8px;border-radius:6px;font-size:.75rem;font-weight:600}
.badge.pass{background:rgba(34,197,94,.15);color:var(--pass)}
.badge.warn{background:rgba(234,179,8,.15);color:var(--warn)}
.badge.fail{background:rgba(239,68,68,.15);color:var(--fail)}
.toolbar{display:flex;gap:.7rem;margin-bottom:1rem;flex-wrap:wrap;align-items:center}
.toolbar input,.toolbar select{background:var(--bg);border:1px solid var(--border);color:var(--text);padding:.5rem .8rem;border-radius:8px;font-size:.85rem}
.toolbar input{flex:1;min-width:200px}
.toolbar button{background:var(--accent);color:#fff;border:none;padding:.5rem 1rem;border-radius:8px;cursor:pointer;font-size:.85rem;font-weight:500;transition:opacity .2s}
.toolbar button:hover{opacity:.85}
.count-label{font-size:.8rem;color:var(--text2)}
.issue-card{background:var(--card);border:1px solid var(--border);border-radius:10px;padding:1rem 1.2rem;margin-bottom:.8rem;transition:transform .15s}
.issue-card:hover{transform:translateX(4px)}
.issue-card.critical{border-left:4px solid var(--fail)}
.issue-card.warning{border-left:4px solid var(--warn)}
.issue-card.info{border-left:4px solid var(--accent)}
.issue-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:.4rem}
.issue-title{font-weight:600;font-size:.9rem}
.issue-meta{font-size:.75rem;color:var(--text2)}
.issue-msg{font-size:.85rem;color:var(--text2);margin-bottom:.3rem}
.issue-fix{font-size:.8rem;color:var(--accent);font-style:italic}
@media print{#sidebar{display:none}#content{margin-left:0}body{background:#fff;color:#000}.card,.chart-box,.issue-card{border-color:#ddd;background:#f9f9f9}}
@media(max-width:900px){#sidebar{display:none}#content{margin-left:0}}
`;
}

// ── Inline JS ───────────────────────────────────────────────────

function getJS(): string {
    return `
// Navigation
function showPage(id){
  document.querySelectorAll('.page').forEach(p=>p.classList.remove('active'));
  document.querySelectorAll('.nav-link').forEach(a=>a.classList.remove('active'));
  document.getElementById('page-'+id).classList.add('active');
  document.querySelector('[data-page="'+id+'"]').classList.add('active');
}

// Charts init
document.addEventListener('DOMContentLoaded',()=>{
  initCharts();
  renderSchedule(D.elements);
  renderIssues(D.issues);
});

function initCharts(){
  const cats=D.categories;
  // Category bar chart
  new Chart(document.getElementById('catBarChart'),{type:'bar',data:{
    labels:cats.map(c=>c.category),
    datasets:[
      {label:'Pass',data:cats.map(c=>c.passCount),backgroundColor:'#22c55e'},
      {label:'Warn',data:cats.map(c=>c.warnCount),backgroundColor:'#eab308'},
      {label:'Fail',data:cats.map(c=>c.failCount),backgroundColor:'#ef4444'}
    ]},options:{responsive:true,plugins:{title:{display:true,text:'Elements by Category',color:'#e2e8f0'}},scales:{x:{stacked:true,ticks:{color:'#94a3b8'}},y:{stacked:true,ticks:{color:'#94a3b8'}}}}});

  // Level pie
  const lvls=Object.entries(D.levelDistribution);
  new Chart(document.getElementById('levelPieChart'),{type:'doughnut',data:{
    labels:lvls.map(l=>l[0]),datasets:[{data:lvls.map(l=>l[1]),backgroundColor:['#3b82f6','#8b5cf6','#06b6d4','#22c55e','#eab308','#ef4444','#f97316','#ec4899','#14b8a6','#6366f1']}]
  },options:{responsive:true,plugins:{title:{display:true,text:'Elements by Level',color:'#e2e8f0'}}}});

  // Param radar
  if(cats.length>0){
    new Chart(document.getElementById('paramRadar'),{type:'radar',data:{
      labels:cats.map(c=>c.category),datasets:[{label:'Param Fill Rate %',data:cats.map(c=>c.parameterFillRate),borderColor:'#3b82f6',backgroundColor:'rgba(59,130,246,.15)',pointBackgroundColor:'#3b82f6'}]
    },options:{responsive:true,scales:{r:{min:0,max:100,ticks:{color:'#94a3b8'},grid:{color:'#334155'},pointLabels:{color:'#e2e8f0'}}},plugins:{title:{display:true,text:'Parameter Fill Rate by Category',color:'#e2e8f0'}}}});
  }

  // Analytics - category doughnut
  new Chart(document.getElementById('catDoughnut'),{type:'doughnut',data:{
    labels:cats.map(c=>c.category),datasets:[{data:cats.map(c=>c.totalElements),backgroundColor:['#3b82f6','#8b5cf6','#06b6d4','#22c55e','#eab308','#ef4444','#f97316','#ec4899','#14b8a6','#6366f1','#a855f7','#0ea5e9','#84cc16','#f43f5e','#d946ef']}]
  },options:{responsive:true,plugins:{title:{display:true,text:'Category Distribution',color:'#e2e8f0'}}}});

  // Analytics - score bar
  new Chart(document.getElementById('scoreBar'),{type:'bar',data:{
    labels:cats.map(c=>c.category),datasets:[{label:'Compliance Score %',data:cats.map(c=>c.complianceScore),backgroundColor:cats.map(c=>c.complianceScore>=80?'#22c55e':c.complianceScore>=60?'#eab308':'#ef4444')}]
  },options:{responsive:true,indexAxis:'y',plugins:{title:{display:true,text:'Compliance Scores',color:'#e2e8f0'}},scales:{x:{min:0,max:100,ticks:{color:'#94a3b8'}},y:{ticks:{color:'#94a3b8'}}}}});

  // Analytics - level bar
  new Chart(document.getElementById('levelBar'),{type:'bar',data:{
    labels:lvls.map(l=>l[0]),datasets:[{label:'Element Count',data:lvls.map(l=>l[1]),backgroundColor:'#3b82f6'}]
  },options:{responsive:true,plugins:{title:{display:true,text:'Elements per Level',color:'#e2e8f0'}},scales:{x:{ticks:{color:'#94a3b8'}},y:{ticks:{color:'#94a3b8'}}}}});
}

// Schedule table
let allRows=[];
function renderSchedule(elems){
  allRows=elems;
  const body=document.getElementById('scheduleBody');
  body.innerHTML=elems.map(e=>'<tr data-cat="'+e.category+'" data-status="'+e.status+'"><td>'+e.id+'</td><td>'+e.name+'</td><td>'+e.category+'</td><td>'+e.typeName+'</td><td>'+e.level+'</td><td>'+e.mark+'</td><td><span class="badge '+e.status+'">'+e.status.toUpperCase()+'</span></td><td>'+(e.issues.length?e.issues.join('; '):'—')+'</td></tr>').join('');
  document.getElementById('scheduleCount').textContent=elems.length+' elements shown';
}

function filterSchedule(){
  const q=document.getElementById('scheduleSearch').value.toLowerCase();
  const cat=document.getElementById('catFilter').value;
  const st=document.getElementById('statusFilter').value;
  const filtered=allRows.filter(e=>{
    if(cat&&e.category!==cat)return false;
    if(st&&e.status!==st)return false;
    if(q&&!(e.name.toLowerCase().includes(q)||e.id.toString().includes(q)||e.typeName.toLowerCase().includes(q)||e.mark.toLowerCase().includes(q)))return false;
    return true;
  });
  renderSchedule(filtered);
}

let sortDir=1,sortCol=-1;
function sortTable(col){
  if(sortCol===col)sortDir*=-1;else{sortCol=col;sortDir=1;}
  const keys=['id','name','category','typeName','level','mark','status'];
  allRows.sort((a,b)=>{
    const va=String(a[keys[col]]||'').toLowerCase(),vb=String(b[keys[col]]||'').toLowerCase();
    return va<vb?-sortDir:va>vb?sortDir:0;
  });
  renderSchedule(allRows);
}

function exportCSV(){
  const rows=[['ID','Name','Category','Type','Level','Mark','Status','Issues']];
  allRows.forEach(e=>rows.push([e.id,e.name,e.category,e.typeName,e.level,e.mark,e.status,e.issues.join('; ')]));
  const csv=rows.map(r=>r.map(c=>'"'+(c||'').replace(/"/g,'""')+'"').join(',')).join('\\n');
  const blob=new Blob([csv],{type:'text/csv'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='BIM-Schedule.csv';a.click();
}

// Issues
function renderIssues(issues){
  const c=document.getElementById('issuesList');
  c.innerHTML=issues.map(i=>'<div class="issue-card '+i.severity+'"><div class="issue-header"><span class="issue-title">'+i.rule+'</span><span class="badge '+({critical:'fail',warning:'warn',info:'pass'}[i.severity])+'">'+i.severity.toUpperCase()+'</span></div><div class="issue-meta">'+i.category+' · '+i.elementName+' ('+i.elementId+') · '+i.level+'</div><div class="issue-msg">'+i.message+'</div><div class="issue-fix">💡 '+i.suggestion+'</div></div>').join('');
}

function filterIssues(){
  const sev=document.getElementById('sevFilter').value;
  renderIssues(sev?D.issues.filter(i=>i.severity===sev):D.issues);
}
`;
}
