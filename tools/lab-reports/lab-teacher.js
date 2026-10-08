// Shared engine for the lab reports teacher pages (<topic>/<lab>_teacher.html).
//
// A teacher page loads lab-teacher.css and this file, then calls LabTeacher({...}) with only the
// parts that are specific to its experiment. The engine does the rest: Google Sheet connection,
// one report per group (latest version), consolidated page, answers side by side, group detail,
// Projector slides, student link, import (with upload to the sheet), JSON/CSV export and demo.
//
// The lab object:
//   id, title, icon, studentPage, tagline        identity; studentPage is relative to the teacher page
//   questions: [{ id, title, get: s => text, chart?: 'errors' | 'pred' | … }]
//   errors: { key: 'label' }                     error-source checkboxes (chart for chart: 'errors')
//   analyze(s) → object                          derived values for one report, merged into the group G
//   finish?(GROUPS)                              cross-group values (e.g. class medians), after analyze
//   checks(G) → [[text, 'red' | ''], …]          data checks (not grades)
//   summaryHtml                                  static HTML for the summary plots (may include #legend)
//   renderSummary(on)                            fills #tiles and draws the summary plots
//   resultsHead, resultsRow(G)                   lab columns of the results table (between names and checks)
//   questionChart?(q) → html                     charts for questions other than 'errors'
//   answerPrefix?(q, s) → text                   bold text before an answer (e.g. the chosen prediction)
//   sections?: [{ id, title, render(el) }]       extra sections after the results (e.g. screenshots)
//   titleExtra?() → text                         appended to the title slide's subtitle
//   slides(on) → [{ html, draw?, cls? }]         lab slides between the title and the results table
//   detail(G) → html, drawDetail?(G)             body of the group dialog
//   computed(G) → object                         values recomputed here, added to the JSON export
//   csv: { head: [...], rows: G => [[...], …] }  CSV export
//   demo() → [report, …]                         demo class (session DEMO)
//   backendVersion?                              minimum Apps Script version this lab needs

// ── Helpers shared with the lab pages ───────────────────────
const num = s => { const v = parseFloat(String(s ?? '').trim().replace(',', '.')); return isFinite(v) ? v : NaN; };
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
function fmt(v, sig = 3) {
  if (!isFinite(v)) return '—';
  if (v === 0) return '0';
  const a = Math.abs(v);
  if (a >= 1e5 || a < 1e-3) return v.toExponential(sig - 1);
  return String(parseFloat(v.toPrecision(sig)));
}
const median = a => { const s = [...a].sort((x, y) => x - y), n = s.length; return n ? (n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2) : NaN; };
const norm = s => String(s ?? '').trim().toLowerCase();
const words = s => String(s || '').trim().split(/\s+/).filter(Boolean).length;
function niceStep(range, n) {
  const raw = range / n, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
  return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * p;
}
function linfit(pts) {
  const n = pts.length;
  if (n < 2) return null;
  let sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0;
  pts.forEach(p => { sx += p.x; sy += p.y; sxx += p.x * p.x; sxy += p.x * p.y; syy += p.y * p.y; });
  const Sxx = sxx - sx * sx / n, Sxy = sxy - sx * sy / n, Syy = syy - sy * sy / n;
  if (Sxx <= 0) return null;
  const k = Sxy / Sxx, b = (sy - k * sx) / n;
  const ssr = pts.reduce((a, p) => a + (p.y - k * p.x - b) ** 2, 0);
  const s2 = n > 2 ? ssr / (n - 2) : NaN;
  return { n, k, b, r2: Syy > 0 ? 1 - ssr / Syy : 1, sk: Math.sqrt(s2 / Sxx), sb: Math.sqrt(s2 * sxx / (n * Sxx)) };
}
const COLORS = ['#4e79a7', '#f28e2b', '#e15759', '#76b7b2', '#59a14f', '#edc948', '#b07aa1', '#ff9da7', '#9c755f', '#17becf', '#bcbd22', '#8c6d31'];

let LAB = null;
let CFG = { session: '', names: false };
const CONN = { api: '', key: '' };   // shared by all labs, set on ../teacher.html
const RAW = new Map();               // reports from any source, keyed by id + submittedAt
let IN_SHEET = null;                 // keys seen in the sheet on the last successful load
let GROUPS = [];
const ON_STATE = {};

const label = G => 'Group ' + (G.s.group || '?') + (CFG.session ? '' : ` (${G.s.session || '—'})`);
const who = G => (G.s.students || []).filter(Boolean).join(' & ');
const flagsHtml = G => G.checks.map(([t, c]) => `<span class="flag ${c}">${esc(t)}</span>`).join('');
const rawKey = s => (s.id || '') + '|' + (s.submittedAt || '');
function bar(name, n) {
  const max = Math.max(1, GROUPS.length);
  return `<div class="bar"><span>${esc(name)}</span><div class="track"><div class="fill" style="width:${n / max * 100}%"></div></div><b>${n}</b></div>`;
}
// Pieces for group detail dialogs
const detailAnswer = (title, text) => `<div class="ans"><div class="who" style="cursor:default">${esc(title)}</div>${String(text || '').trim() ? `<div class="text">${esc(text)}</div>` : '<div class="empty">No answer</div>'}</div>`;
const sentTile = G => `<div class="tile"><div class="k">Sent</div><div class="v" style="font-size:1rem">${G.s.submittedAt ? new Date(G.s.submittedAt).toLocaleString() : '—'}</div><div class="s">${G.versions > 1 ? G.versions + ' versions, showing the latest' : 'session ' + esc(G.s.session || '—')}</div></div>`;
const errorsTitle = c => 'Error sources: ' + ((c.errors || []).map(e => LAB.errors[e] || e).join(', ') || 'none chosen');
function download(name, text, type) {
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([text], { type }));
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}
const $ = id => document.getElementById(id);

// ── Page skeleton ───────────────────────────────────────────
function buildPage() {
  document.body.innerHTML = `
<div class="top">
<header>
  <a class="pill-btn" href="../teacher.html" style="text-decoration:none">← All labs</a>
  <h1>📊 <span>${esc(LAB.title)}</span> · Lab Reports</h1>
  <div class="ctl">Session <input type="text" id="session" placeholder="all"></div>
  <button class="pill-btn" id="refresh">⟳ Refresh</button>
  <button class="pill-btn" id="auto">Live: off</button>
  <button class="pill-btn" id="names">Names: hidden</button>
  <button class="pill-btn" id="present" title="Present the results as full-screen slides">▶ Projector</button>
  <button class="pill-btn" id="theme-toggle">☀ Light</button>
  <button class="pill-btn" id="setup-toggle">⚙ Setup &amp; data</button>
  <span id="fetch-state"></span>
</header>
<nav class="jump" id="jump"></nav>
</div>

<main>
  <section id="setup" class="setup-grid" hidden>
    <div class="setup-head"><h2>⚙ Setup &amp; data</h2><button class="pill-btn" id="setup-close">Close</button></div>
    <div class="card setup">
      <h3>1 · Connection</h3>
      <div class="note" id="conn-state"></div>
    </div>
    <div class="card setup">
      <h3>2 · Student link for a session</h3>
      <div class="form-row"><label for="link-session">Session code</label><input type="text" id="link-session" placeholder="e.g. 10A or 10A-oct08"></div>
      <div class="link-box">
        <div style="display:grid;gap:8px">
          <input type="text" id="student-link" readonly class="mono">
          <div class="btns"><button class="btn" id="copy-link">Copy link</button><a class="btn" id="open-link" target="_blank" rel="noopener">Open student page</a></div>
          <p class="muted" style="font-size:.8rem">Share it in your class chat or by email. Each pair opens it on one computer. Without the class link, students can still download their report as a file and hand it in.</p>
        </div>
      </div>
    </div>
    <div class="card setup">
      <h3>3 · Data</h3>
      <div class="btns">
        <label class="btn" for="import">⬆ Import report files (.json)</label><input type="file" id="import" accept=".json,application/json" multiple hidden>
        <button class="btn" id="export-json">⬇ Download all reports (JSON)</button>
        <button class="btn" id="export">⬇ Results table (CSV)</button>
        <button class="btn" id="demo">Load demo class</button>
        <button class="btn" id="clear">Clear loaded reports</button>
      </div>
      <p class="muted" style="font-size:.8rem">Imported files are also uploaded to your Google Sheet (if it doesn't have them yet), so the sheet keeps every report. "Download all reports" saves the complete reports for the current session, with every answer, in one file you can hand over for evaluation.</p>
      <span class="muted" id="data-msg"></span>
    </div>
  </section>

  <div id="empty" class="empty-state" hidden>
    <p><b>No reports yet.</b></p>
    <p>Students' reports for this lab will appear here. Open <b>⚙ Setup &amp; data</b> to get the student link, import report files, or load a demo class.</p>
  </div>

  <div id="report" style="display:grid;gap:22px">
    <h2 class="sec" id="sec-summary">Class summary</h2>
    <div class="tiles" id="tiles"></div>
    ${LAB.summaryHtml}

    <h2 class="sec" id="sec-results">Results by group</h2>
    <div class="card">
      <div class="table-wrap"><table class="t" id="results"></table></div>
      <p class="muted" style="font-size:.78rem;margin-top:8px">Values are recomputed from each group's raw measurements. Data checks point out things to look at; they are not grades. Click a row to see the full report.</p>
    </div>
    ${(LAB.sections || []).map(sec => `<h2 class="sec" id="sec-${sec.id}">${esc(sec.title)}</h2><div id="${sec.id}"></div>`).join('')}

    <h2 class="sec" id="sec-answers">Answers</h2>
    <div id="questions" style="display:grid;gap:26px"></div>
  </div>
</main>

<div id="show" hidden>
  <div class="slide" id="slide"></div>
  <div class="show-bar">
    <button class="pill-btn" id="show-prev" title="Previous (←)">◀</button>
    <span id="show-count"></span>
    <div class="track"><i id="show-progress"></i></div>
    <button class="pill-btn" id="show-next" title="Next (→, space)">▶</button>
    <button class="pill-btn" id="show-exit" title="Exit (Esc)">✕ Exit</button>
  </div>
</div>

<dialog id="dlg">
  <div class="dlg-head"><h2 id="dlg-title"></h2><button class="pill-btn" id="dlg-prev">◀</button><button class="pill-btn" id="dlg-next">▶</button><button class="pill-btn" id="dlg-close">Close</button></div>
  <div class="dlg-body" id="dlg-body"></div>
</dialog>

<dialog id="lightbox"><img alt=""><div class="cap"></div></dialog>`;
}

// ── Groups: latest report per (session, group), recomputed from raw data ──
function buildGroups() {
  const sess = norm(CFG.session);
  const latest = new Map(), versions = new Map();
  [...RAW.values()].forEach(s => {
    if (sess && norm(s.session) !== sess) return;
    const key = norm(s.session) + '|' + norm(s.group);
    versions.set(key, (versions.get(key) || 0) + 1);
    const cur = latest.get(key);
    if (!cur || String(s.submittedAt) > String(cur.submittedAt)) latest.set(key, s);
  });
  GROUPS = [...latest.entries()].map(([key, s]) =>
    Object.assign({ key, s, versions: versions.get(key), on: ON_STATE[key] ?? true }, LAB.analyze(s))
  ).sort((a, b) => (num(a.s.group) - num(b.s.group)) || norm(a.s.group).localeCompare(norm(b.s.group)));
  GROUPS.forEach((G, i) => { G.color = COLORS[i % COLORS.length]; });
  if (LAB.finish) LAB.finish(GROUPS);
  GROUPS.forEach(G => { G.checks = LAB.checks(G); });
}

// ── Consolidated page ───────────────────────────────────────
function render() {
  buildGroups();
  const empty = !GROUPS.length;
  $('empty').hidden = !empty;
  $('report').hidden = empty;
  $('jump').innerHTML = empty ? '' :
    '<a href="#sec-summary">Summary</a><a href="#sec-results">Results</a>' +
    (LAB.sections || []).map(sec => `<a href="#sec-${sec.id}">${esc(sec.title)}</a>`).join('') +
    LAB.questions.map((q, i) => `<a href="#q-${q.id}">Q${i + 1} · ${esc(q.title.split(/[?:…]/)[0])}</a>`).join('');
  if (empty) return;
  LAB.renderSummary(GROUPS.filter(G => G.on));
  const legend = $('legend');
  if (legend) legend.innerHTML = GROUPS.map((G, i) => `<span class="chip ${G.on ? '' : 'off'}" data-i="${i}"><i style="background:${G.color}"></i>${esc(label(G))}</span>`).join('') +
    '<span class="chip" data-all>Show all</span>';
  renderResults();
  (LAB.sections || []).forEach(sec => sec.render($(sec.id)));
  renderQuestions();
  if (!$('show').hidden) buildSlides();
}

function renderResults() {
  $('results').innerHTML = `<thead><tr>
      <th>Group</th>${CFG.names ? '<th>Students</th>' : ''}${LAB.resultsHead}<th>Data checks</th>
    </tr></thead><tbody>` + GROUPS.map((G, i) => `<tr class="click" data-i="${i}">
        <td><span class="dot" style="background:${G.color}"></span><b>${esc(label(G))}</b>${G.versions > 1 ? ` <span class="muted" title="Sent ${G.versions} times; showing the latest">×${G.versions}</span>` : ''}</td>
        ${CFG.names ? `<td>${esc(who(G))}</td>` : ''}
        ${LAB.resultsRow(G)}
        <td>${flagsHtml(G)}</td></tr>`).join('') + '</tbody>';
}

function renderQuestions() {
  $('questions').innerHTML = LAB.questions.map((q, qi) => {
    const chart = questionChart(q);
    const cards = GROUPS.map((G, i) => answerCard(q, G, i)).join('');
    return `<section class="question"><div class="qt" id="q-${q.id}"><b>Q${qi + 1}</b>${esc(q.title)}</div>${chart ? `<div class="card">${chart}</div>` : ''}
      ${cards ? `<div class="answers">${cards}</div>` : ''}</section>`;
  }).join('');
}
// Bar chart of choices for the questions that have one
function questionChart(q) {
  if (q.chart === 'errors') {
    const counts = {}; GROUPS.forEach(G => (G.s.conclusion?.errors || []).forEach(e => { counts[e] = (counts[e] || 0) + 1; }));
    return `<div class="bars">${Object.keys(LAB.errors).sort((a, b) => (counts[b] || 0) - (counts[a] || 0)).map(e => bar(LAB.errors[e], counts[e] || 0)).join('')}</div>`;
  }
  return q.chart && LAB.questionChart ? LAB.questionChart(q) : '';
}
// One group's answer to a question ('' when an optional answer is empty)
function answerCard(q, G, i) {
  const text = String(q.get(G.s) || '').trim();
  const prefix = LAB.answerPrefix ? LAB.answerPrefix(q, G.s) : '';
  const pre = prefix ? `<b>${esc(prefix)}</b> ` : '';
  if (q.chart === 'errors' && !text) return '';
  return `<div class="ans">
    <div class="who" data-i="${i}" title="Open full report"><span class="dot" style="background:${G.color}"></span>${esc(label(G))}${CFG.names ? ' · ' + esc(who(G)) : ''}</div>
    ${text || pre ? `<div class="text">${pre}${esc(text)}</div>` : '<div class="empty">No answer</div>'}</div>`;
}

// ── Presentation: full-screen slides of the class results ──
const ANSWERS_PER_SLIDE = 6;
let SLIDES = [], SLIDE = 0, enteredFullscreen = false;

function buildSlides() {
  const on = GROUPS.filter(G => G.on);
  SLIDES = [{ cls: 'title', html: `
    <h1>${LAB.icon} <span>${esc(LAB.title)}</span></h1>
    <div class="sub">${CFG.session ? 'Session ' + esc(CFG.session) + ' · ' : ''}${GROUPS.length} group report${GROUPS.length === 1 ? '' : 's'}${LAB.titleExtra ? esc(LAB.titleExtra()) : ''}</div>
    <div class="sub">${esc(LAB.tagline)}</div>` },
    ...LAB.slides(on),
    { html: `<h2>Results by group</h2><div class="table-wrap"><table class="t">${$('results').innerHTML}</table></div>` }];
  LAB.questions.forEach((q, qi) => {
    const cards = GROUPS.map((G, i) => answerCard(q, G, i)).filter(Boolean);
    const chart = questionChart(q);
    const title = `<h2><span class="qn">Q${qi + 1}</span>${esc(q.title)}`;
    if (chart) SLIDES.push({ html: `${title}</h2><div class="card">${chart}</div>` });
    if (chart && !cards.length) return;
    const pages = Math.max(1, Math.ceil(cards.length / ANSWERS_PER_SLIDE));
    for (let p = 0; p < pages; p++) {
      const part = cards.slice(p * ANSWERS_PER_SLIDE, (p + 1) * ANSWERS_PER_SLIDE);
      SLIDES.push({ html: `${title}${pages > 1 ? `<span class="page">${p + 1}/${pages}</span>` : ''}</h2>
        ${part.length ? `<div class="answers">${part.join('')}</div>` : '<div class="sub">No answers yet.</div>'}
        <div class="sub" style="margin-top:auto">Click an answer to enlarge it.</div>` });
    }
  });
  showSlide(Math.min(SLIDE, SLIDES.length - 1));
}

function showSlide(i) {
  SLIDE = Math.max(0, Math.min(SLIDES.length - 1, i));
  const sl = SLIDES[SLIDE], el = $('slide');
  el.className = 'slide ' + (sl.cls || '');
  el.innerHTML = sl.html;
  el.scrollTop = 0;
  if (sl.draw) sl.draw();
  $('show-count').textContent = (SLIDE + 1) + ' / ' + SLIDES.length;
  $('show-progress').style.width = ((SLIDE + 1) / SLIDES.length * 100) + '%';
}

function openShow() {
  if (!GROUPS.length) { alert('There are no reports to present yet.'); return; }
  $('show').hidden = false;
  document.body.classList.add('presenting');
  SLIDE = 0;
  buildSlides();
  enteredFullscreen = false;
  if (document.documentElement.requestFullscreen) {
    document.documentElement.requestFullscreen().then(() => { enteredFullscreen = true; }).catch(() => {});
  }
}
function closeShow() {
  $('show').hidden = true;
  document.body.classList.remove('presenting');
  if (document.fullscreenElement) document.exitFullscreen().catch(() => {});
}

// ── Full report of one group (dialog) ───────────────────────
let DLG_I = 0;
function openDetail(i) {
  DLG_I = (i + GROUPS.length) % GROUPS.length;
  const G = GROUPS[DLG_I];
  $('dlg-title').innerHTML = `<span class="dot" style="background:${G.color}"></span>${esc(label(G))}${CFG.names ? ' · ' + esc(who(G)) : ''}`;
  $('dlg-body').innerHTML = LAB.detail(G);
  if (LAB.drawDetail) LAB.drawDetail(G);
  if (!$('dlg').open) $('dlg').showModal();
}

// Click any image with class "zoomable" to see it full size
function openLightbox(img) {
  const box = $('lightbox');
  box.querySelector('img').src = img.src;
  box.querySelector('.cap').textContent = img.alt || '';
  if (!box.open) box.showModal();
}

// ── Google Sheet ────────────────────────────────────────────
async function load() {
  const fstate = $('fetch-state');
  if (!CONN.api || !CONN.key) { fstate.textContent = RAW.size ? 'Not connected: showing saved reports' : 'Not connected'; render(); return; }
  fstate.textContent = 'Loading…';
  try {
    const u = new URL(CONN.api);
    u.searchParams.set('key', CONN.key);
    u.searchParams.set('lab', LAB.id);
    const j = await (await fetch(u)).json();
    if (!j.ok) throw new Error(j.error || 'server error');
    j.submissions.forEach(addRaw);
    IN_SHEET = new Set(j.submissions.map(rawKey));
    cacheRaw();
    fstate.textContent = 'Updated ' + new Date().toLocaleTimeString();
    if (LAB.backendVersion && (j.version || 1) < LAB.backendVersion)
      fstate.textContent += ' · ⚠ Update your Apps Script (see the Lab Reports teacher page) so reports with screenshots can be saved';
  } catch (e) {
    fstate.textContent = '⚠ ' + (e.message || 'Could not load') + (RAW.size ? ' (showing saved reports)' : '');
  }
  render();
}
function addRaw(s) { if (s && s.lab === LAB.id) RAW.set(rawKey(s), s); }
function cacheRaw() { try { localStorage.setItem('labdash_cache_' + LAB.id, JSON.stringify([...RAW.values()])); } catch (e) {} }
function saveCfg() { try { localStorage.setItem('labdash_' + LAB.id, JSON.stringify(CFG)); } catch (e) {} }

// ── Setup panel ─────────────────────────────────────────────
function showSetup(on) {
  $('setup').hidden = !on;
  $('setup-toggle').classList.toggle('on', on);
  if (on) { renderLink(); window.scrollTo({ top: 0 }); }
}
function renderLink() {
  const u = new URL(LAB.studentPage, location.href);
  const sess = $('link-session').value.trim();
  if (sess) u.searchParams.set('session', sess);
  if (CONN.api) u.searchParams.set('api', CONN.api);
  const link = u.toString();
  $('student-link').value = link;
  $('open-link').href = link;
}
const tag = () => CFG.session ? '_' + CFG.session.replace(/[^\w-]+/g, '-') : '';

// Imported files (e.g. a group that couldn't send) are also uploaded to the Google Sheet,
// so the sheet stays the complete record. Reports already in the sheet are skipped.
async function importFiles(files) {
  const dataMsg = $('data-msg');
  const imported = [];
  for (const file of files) {
    try {
      const d = JSON.parse(await file.text());
      (Array.isArray(d) ? d : d.reports || [d]).forEach(r => {
        if (r.lab !== LAB.id) return;
        const { computed, receivedAt, ...report } = r;
        addRaw(report); imported.push(report);
      });
    } catch (err) {}
  }
  cacheRaw();
  const n = imported.length;
  const msg = `Imported ${n} report${n === 1 ? '' : 's'}.`;
  dataMsg.textContent = msg;
  render();
  if (!n) return;
  if (!CONN.api || !CONN.key) {
    dataMsg.textContent = msg + ` Not connected to a Google Sheet, so ${n === 1 ? 'it is' : 'they are'} only saved in this browser.`;
    return;
  }
  await load();
  if (IN_SHEET === null) { dataMsg.textContent = msg + ` Could not reach the Google Sheet, so ${n === 1 ? 'it was' : 'they were'} not uploaded. Try importing again later.`; return; }
  const missing = imported.filter(r => !IN_SHEET.has(rawKey(r)));
  if (!missing.length) { dataMsg.textContent = msg + ` ${n === 1 ? 'It is' : 'They are'} already in your Google Sheet.`; return; }
  if (!confirm(`Upload ${missing.length} imported report${missing.length === 1 ? '' : 's'} to your Google Sheet, so ${missing.length === 1 ? 'it is' : 'they are'} kept with the others?`)) {
    dataMsg.textContent = msg + ` Not uploaded: ${missing.length === 1 ? 'it is' : 'they are'} only saved in this browser.`;
    return;
  }
  dataMsg.textContent = 'Uploading…';
  let sent = 0;
  for (const r of missing) {
    try {
      const j = await (await fetch(CONN.api, { method: 'POST', body: JSON.stringify(r) })).json();
      if (j.ok) sent++;
    } catch (err) {}
  }
  await load();
  dataMsg.textContent = sent === missing.length
    ? `${msg} Uploaded ${sent} to your Google Sheet ✓`
    : `${msg} Uploaded ${sent} of ${missing.length}; the rest are only saved in this browser. Import the file again to retry.`;
}

function exportJson() {
  buildGroups();
  const out = {
    lab: LAB.id, session: CFG.session || 'all', exportedAt: new Date().toISOString(),
    reports: GROUPS.map(G => ({ ...G.s, computed: { ...LAB.computed(G), data_checks: G.checks.map(c => c[0]) } })),
  };
  download(`${LAB.id}_reports${tag()}.json`, JSON.stringify(out, null, 2), 'application/json');
}
function exportCsv() {
  buildGroups();
  const rows = GROUPS.flatMap(G => LAB.csv.rows(G).map(r => [...r, G.checks.map(x => x[0]).join('; ')]));
  const csv = [[...LAB.csv.head, 'data_checks'], ...rows].map(r => r.map(v => {
    const t = typeof v === 'number' ? (isFinite(v) ? String(+v.toPrecision(6)) : '') : String(v ?? '');
    return /[",\n]/.test(t) ? '"' + t.replace(/"/g, '""') + '"' : t;
  }).join(',')).join('\n');
  download(`${LAB.id}_results${tag()}.csv`, '﻿' + csv, 'text/csv');
}

// ── Theme (shared with the tools index) ─────────────────────
function applyTheme(light) {
  document.body.classList.toggle('light', light);
  $('theme-toggle').textContent = light ? '☽ Dark' : '☀ Light';
  try { localStorage.setItem('et_theme', light ? 'light' : 'dark'); } catch (e) {}
}

// ── Start ───────────────────────────────────────────────────
function LabTeacher(lab) {
  LAB = lab;
  try { Object.assign(CONN, JSON.parse(localStorage.getItem('labreports_connection') || '{}')); } catch (e) {}
  try { Object.assign(CFG, JSON.parse(localStorage.getItem('labdash_' + LAB.id) || '{}')); } catch (e) {}
  try { (JSON.parse(localStorage.getItem('labdash_cache_' + LAB.id) || '[]')).forEach(addRaw); } catch (e) {}
  buildPage();

  // Jump links: scroll so the target sits just below the sticky top bar, whatever its height
  $('jump').addEventListener('click', e => {
    const a = e.target.closest('a');
    if (!a) return;
    e.preventDefault();
    const el = document.querySelector(a.getAttribute('href'));
    const top = el.getBoundingClientRect().top + window.scrollY - document.querySelector('.top').offsetHeight - 12;
    window.scrollTo({ top, behavior: 'smooth' });
  });
  document.addEventListener('click', e => {
    const legendChip = e.target.closest('#legend .chip');
    if (legendChip) {
      if (legendChip.dataset.all != null) GROUPS.forEach(G => { ON_STATE[G.key] = true; });
      else { const G = GROUPS[+legendChip.dataset.i]; ON_STATE[G.key] = !G.on; }
      render();
      return;
    }
    const img = e.target.closest('img.zoomable');
    if (img) { openLightbox(img); return; }
    const row = e.target.closest('#results tr.click');
    if (row) { openDetail(+row.dataset.i); return; }
    const whoEl = e.target.closest('#report .who[data-i]');
    if (whoEl) openDetail(+whoEl.dataset.i);
  });
  $('lightbox').addEventListener('click', () => $('lightbox').close());

  // Presentation controls
  $('present').addEventListener('click', openShow);
  $('show-exit').addEventListener('click', closeShow);
  $('show-prev').addEventListener('click', () => showSlide(SLIDE - 1));
  $('show-next').addEventListener('click', () => showSlide(SLIDE + 1));
  $('slide').addEventListener('click', e => {
    const card = e.target.closest('.ans');
    if (!card) return;
    const was = card.classList.contains('zoom');
    document.querySelectorAll('#slide .ans.zoom').forEach(c => c.classList.remove('zoom'));
    if (!was) card.classList.add('zoom');
  });
  // Leaving full screen (Esc in the browser) also ends the presentation
  document.addEventListener('fullscreenchange', () => {
    if (!document.fullscreenElement && enteredFullscreen && !$('show').hidden) closeShow();
  });
  document.addEventListener('keydown', e => {
    if ($('show').hidden || $('lightbox').open) return;
    const zoomed = document.querySelector('#slide .ans.zoom');
    if (e.key === 'Escape') { if (zoomed) zoomed.classList.remove('zoom'); else closeShow(); return; }
    if (zoomed) zoomed.classList.remove('zoom');
    if (['ArrowRight', 'PageDown', ' ', 'Enter'].includes(e.key)) { e.preventDefault(); showSlide(SLIDE + 1); }
    else if (['ArrowLeft', 'PageUp', 'Backspace'].includes(e.key)) { e.preventDefault(); showSlide(SLIDE - 1); }
    else if (e.key === 'Home') showSlide(0);
    else if (e.key === 'End') showSlide(SLIDES.length - 1);
  });

  // Group dialog
  const dlg = $('dlg');
  $('dlg-close').addEventListener('click', () => dlg.close());
  $('dlg-prev').addEventListener('click', () => openDetail(DLG_I - 1));
  $('dlg-next').addEventListener('click', () => openDetail(DLG_I + 1));
  dlg.addEventListener('click', e => { if (e.target === dlg) dlg.close(); });
  dlg.addEventListener('keydown', e => {
    if (e.key === 'ArrowLeft') openDetail(DLG_I - 1);
    if (e.key === 'ArrowRight') openDetail(DLG_I + 1);
  });

  // Header controls
  $('refresh').addEventListener('click', load);
  let autoTimer = null;
  $('auto').addEventListener('click', e => {
    if (autoTimer) { clearInterval(autoTimer); autoTimer = null; } else { autoTimer = setInterval(load, 20000); load(); }
    e.target.textContent = 'Live: ' + (autoTimer ? 'on' : 'off');
    e.target.classList.toggle('on', !!autoTimer);
  });
  const sessEl = $('session');
  sessEl.value = CFG.session;
  sessEl.addEventListener('input', () => { CFG.session = sessEl.value.trim(); saveCfg(); render(); });
  const applyNames = () => { $('names').textContent = 'Names: ' + (CFG.names ? 'shown' : 'hidden'); $('names').classList.toggle('on', CFG.names); };
  $('names').addEventListener('click', () => { CFG.names = !CFG.names; saveCfg(); applyNames(); render(); });
  applyNames();

  // Setup panel
  $('setup-toggle').addEventListener('click', () => showSetup($('setup').hidden));
  $('setup-close').addEventListener('click', () => showSetup(false));
  $('conn-state').innerHTML = CONN.api && CONN.key
    ? 'Connected to your Google Sheet ✓ (change it on the <a href="../teacher.html" style="color:var(--accent)">Lab Reports teacher page</a>)'
    : 'Not connected to a Google Sheet yet. Set it up once, for all labs, on the <a href="../teacher.html" style="color:var(--accent)">Lab Reports teacher page</a>. Until then you can import report files.';
  $('link-session').value = CFG.session;
  $('link-session').addEventListener('input', renderLink);
  $('copy-link').addEventListener('click', async () => {
    try { await navigator.clipboard.writeText($('student-link').value); } catch (e) { $('student-link').select(); }
  });
  $('import').addEventListener('change', async e => { const files = [...e.target.files]; e.target.value = ''; await importFiles(files); });
  $('clear').addEventListener('click', () => {
    if (!confirm('Remove all loaded reports from this page? Reports in your Google Sheet are not affected.')) return;
    RAW.clear(); cacheRaw(); $('data-msg').textContent = 'Cleared.'; render();
  });
  $('export-json').addEventListener('click', exportJson);
  $('export').addEventListener('click', exportCsv);
  $('demo').addEventListener('click', () => {
    LAB.demo().forEach(addRaw);
    cacheRaw();
    CFG.session = 'DEMO'; sessEl.value = 'DEMO'; saveCfg();
    $('data-msg').textContent = 'Demo class loaded (session DEMO).';
    showSetup(false);
    render();
  });

  let savedTheme = null; try { savedTheme = localStorage.getItem('et_theme'); } catch (e) {}
  applyTheme(savedTheme === 'light');
  $('theme-toggle').addEventListener('click', () => { applyTheme(!document.body.classList.contains('light')); render(); });

  showSetup(!RAW.size && !CONN.api);
  load();
}
