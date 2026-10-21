// Rational functions: parse, analyze exactly, and graph. Shared by rational_functions_lab.html
// and rational_functions_teacher.html, so everything lives inside RF (no globals that clash
// with the pages' own num / fmt / esc).
//
//   RF.parse(text) → { rat } | { error }        rat = { n, d } polynomials, coefficients low → high
//   RF.analyze(rat) → { f, zeros, vas, holes, end, ... }
//   RF.graph(el, opts) → { draw(curves), reset() }   interactive SVG graph with zoom and a readout
//   RF.mini(svg, an, win)                         small static graph (teacher thumbnails)
const RF = (() => {
  // ── Polynomials (arrays of coefficients, constant term first) ──
  const trim = p => { const q = p.slice(); const s = Math.max(1, ...q.map(Math.abs)); while (q.length > 1 && Math.abs(q[q.length - 1]) < 1e-12 * s) q.pop(); return q; };
  const deg = p => trim(p).length - 1;
  const isZero = p => trim(p).every(c => Math.abs(c) < 1e-14);
  const add = (a, b) => trim(Array.from({ length: Math.max(a.length, b.length) }, (_, i) => (a[i] || 0) + (b[i] || 0)));
  const scale = (a, k) => a.map(c => c * k);
  const mul = (a, b) => {
    const r = new Array(a.length + b.length - 1).fill(0);
    a.forEach((x, i) => b.forEach((y, j) => { r[i + j] += x * y; }));
    return trim(r);
  };
  const peval = (p, x) => p.reduceRight((acc, c) => acc * x + c, 0);
  // Divide p by (x − c), dropping the remainder
  const deflate = (p, c) => {
    const n = p.length - 1, q = new Array(n).fill(0);
    let acc = 0;
    for (let i = n; i >= 1; i--) { acc = acc * c + p[i]; q[i - 1] = acc; }
    return q.length ? q : [0];
  };
  // Long division a = q·b + r
  function divide(a, b) {
    let r = trim(a).slice();
    const db = deg(b), lb = trim(b)[db];
    const q = new Array(Math.max(1, deg(a) - db + 1)).fill(0);
    for (let k = deg(a) - db; k >= 0; k--) {
      const c = (r[k + db] || 0) / lb;
      q[k] = c;
      for (let j = 0; j <= db; j++) r[k + j] -= c * b[j];
    }
    return { q: trim(q), r: trim(r.slice(0, Math.max(1, db))) };
  }

  // Real roots with multiplicities (Durand–Kerner on the monic polynomial, then clustered)
  function realRoots(p) {
    p = trim(p);
    const n = p.length - 1;
    if (n < 1) return [];
    const lead = p[n], a = p.map(c => c / lead);
    let zr = [], zi = [];
    for (let k = 0; k < n; k++) { const ang = 2 * Math.PI * k / n + 0.4; const rad = 1 + Math.max(...a.slice(0, n).map(Math.abs)); zr.push(rad * Math.cos(ang)); zi.push(rad * Math.sin(ang)); }
    for (let it = 0; it < 800; it++) {
      let moved = 0;
      for (let i = 0; i < n; i++) {
        // p(z)
        let pr = 1, pi = 0;
        for (let k = n - 1; k >= 0; k--) { const t = pr * zr[i] - pi * zi[i] + a[k]; pi = pr * zi[i] + pi * zr[i]; pr = t; }
        // Π (z − zj)
        let qr = 1, qi = 0;
        for (let j = 0; j < n; j++) if (j !== i) { const dr = zr[i] - zr[j], di = zi[i] - zi[j]; const t = qr * dr - qi * di; qi = qr * di + qi * dr; qr = t; }
        const den = qr * qr + qi * qi || 1e-300;
        const cr = (pr * qr + pi * qi) / den, ci = (pi * qr - pr * qi) / den;
        zr[i] -= cr; zi[i] -= ci; moved = Math.max(moved, Math.hypot(cr, ci));
      }
      if (moved < 1e-14) break;
    }
    const reals = [];
    for (let i = 0; i < n; i++) if (Math.abs(zi[i]) < 1e-4 * (1 + Math.abs(zr[i]))) reals.push(zr[i]);
    reals.sort((x, y) => x - y);
    const out = [];
    reals.forEach(x => {
      const last = out[out.length - 1];
      if (last && Math.abs(x - last.sum / last.m) < 2e-3 * (1 + Math.abs(x))) { last.sum += x; last.m++; }
      else out.push({ sum: x, m: 1 });
    });
    return out.map(c => ({ x: snap(c.sum / c.m), m: c.m }));
  }
  // Snap a numerical root to a nearby simple fraction (denominators up to 12)
  function snap(x) {
    for (let q = 1; q <= 12; q++) { const p = Math.round(x * q); if (Math.abs(x - p / q) < 1e-5) return p / q; }
    return x;
  }

  // ── Parser: numbers, x, + − · / ^ (integer powers), parentheses, implicit multiplication ──
  function tokenize(s) {
    const toks = [];
    let i = 0;
    while (i < s.length) {
      const c = s[i];
      if (/\s/.test(c)) { i++; continue; }
      if (/[0-9.]/.test(c)) {
        const m = s.slice(i).match(/^(\d+\.?\d*|\.\d+)/);
        if (!m) throw new Error(`Bad number near "${s.slice(i, i + 4)}"`);
        toks.push({ type: 'num', v: parseFloat(m[0]) }); i += m[0].length; continue;
      }
      if (c === 'x') { toks.push({ type: 'x' }); i++; continue; }
      if ('+-*/^()'.includes(c)) { toks.push({ type: 'op', v: c }); i++; continue; }
      throw new Error(/[a-z]/.test(c) ? `Only x is allowed as a letter (found "${c}")` : `Unexpected character "${c}"`);
    }
    return toks;
  }
  const MAXDEG = 14;
  const R = {
    num: v => ({ n: [v], d: [1] }),
    x: () => ({ n: [0, 1], d: [1] }),
    neg: a => ({ n: scale(a.n, -1), d: a.d }),
    add: (a, b) => ({ n: add(mul(a.n, b.d), mul(b.n, a.d)), d: mul(a.d, b.d) }),
    sub: (a, b) => R.add(a, R.neg(b)),
    mul: (a, b) => ({ n: mul(a.n, b.n), d: mul(a.d, b.d) }),
    div: (a, b) => { if (isZero(b.n)) throw new Error('Division by zero'); return { n: mul(a.n, b.d), d: mul(a.d, b.n) }; },
    pow(a, b) {
      if (deg(b.n) > 0 || deg(b.d) > 0) throw new Error('Exponents must be whole numbers, like x^2');
      const k = b.n[0] / b.d[0];
      if (!Number.isInteger(k) || Math.abs(k) > 8) throw new Error('Exponents must be whole numbers from −8 to 8');
      let r = R.num(1);
      for (let i = 0; i < Math.abs(k); i++) r = R.mul(r, a);
      return k < 0 ? R.div(R.num(1), r) : r;
    },
  };
  function parse(raw) {
    let s = String(raw || '').trim().toLowerCase();
    s = s.replace(/^(y|r\(x\)|f\(x\))\s*=\s*/, '').replace(/\*\*/g, '^').replace(/[−–]/g, '-').replace(/[·×]/g, '*').replace(/²/g, '^2').replace(/³/g, '^3');
    if (!s) return { error: 'Type a function of x, for example (x - 1)/(x + 2)' };
    try {
      const toks = tokenize(s);
      let pos = 0;
      const peek = () => toks[pos];
      const isOp = (t, v) => t && t.type === 'op' && t.v === v;
      const expect = v => { if (!isOp(peek(), v)) throw new Error(`Missing "${v}"`); pos++; };
      const startsPrimary = t => t && (t.type === 'num' || t.type === 'x' || isOp(t, '('));
      const check = r => { if (deg(r.n) > MAXDEG || deg(r.d) > MAXDEG) throw new Error('That function is too long: use fewer factors'); return r; };
      function expr() {
        let n = term();
        while (isOp(peek(), '+') || isOp(peek(), '-')) { const op = toks[pos++].v; const r = term(); n = check(op === '+' ? R.add(n, r) : R.sub(n, r)); }
        return n;
      }
      function term() {
        let n = unary();
        for (;;) {
          const t = peek();
          if (isOp(t, '*')) { pos++; n = check(R.mul(n, unary())); }
          else if (isOp(t, '/')) { pos++; n = check(R.div(n, unary())); }
          else if (startsPrimary(t)) n = check(R.mul(n, power()));
          else return n;
        }
      }
      function unary() {
        if (isOp(peek(), '-')) { pos++; return R.neg(unary()); }
        if (isOp(peek(), '+')) { pos++; return unary(); }
        return power();
      }
      function power() {
        const base = primary();
        if (isOp(peek(), '^')) { pos++; return check(R.pow(base, unary())); }
        return base;
      }
      function primary() {
        const t = peek();
        if (!t) throw new Error('The expression ends too early');
        if (t.type === 'num') { pos++; return R.num(t.v); }
        if (t.type === 'x') { pos++; return R.x(); }
        if (isOp(t, '(')) { pos++; const e = expr(); expect(')'); return e; }
        throw new Error(`Unexpected "${t.v}"`);
      }
      const rat = expr();
      if (pos < toks.length) throw new Error(`Unexpected "${toks[pos].v ?? 'x'}"`);
      // Make the denominator's leading coefficient 1
      const d = trim(rat.d), l = d[d.length - 1];
      return { rat: { n: scale(trim(rat.n), 1 / l), d: scale(d, 1 / l) } };
    } catch (e) { return { error: e.message }; }
  }

  // ── Analysis: zeros, vertical asymptotes, holes, end behavior ──
  function analyze(rat) {
    const n = trim(rat.n), d = trim(rat.d);
    const f = x => peval(n, x) / peval(d, x);
    const rn = isZero(n) ? [] : realRoots(n), rd = realRoots(d);
    const multIn = (list, x) => { const r = list.find(c => Math.abs(c.x - x) < 2e-3 * (1 + Math.abs(x))); return r ? r.m : 0; };
    const vas = [], holes = [];
    rd.forEach(({ x, m }) => {
      const mn = multIn(rn, x);
      if (mn >= m) {
        // Cancel (x − c)^m from both and evaluate what is left
        let a = n, b = d;
        for (let i = 0; i < m; i++) { a = deflate(a, x); b = deflate(b, x); }
        holes.push({ x, y: snap(peval(a, x) / peval(b, x)) });
      } else vas.push({ x, m: m - mn });
    });
    const zeros = rn.filter(({ x }) => !multIn(rd, x)).map(({ x, m }) => ({ x, m }));
    const dn = isZero(n) ? -Infinity : deg(n), dd = deg(d);
    let end;
    if (dn < dd) end = { kind: 'zero', y: 0 };
    else if (dn === dd) end = { kind: 'ha', y: snap(n[dn] / d[dd]) };
    else if (dn === dd + 1) { const { q } = divide(n, d); end = { kind: 'slant', m: snap(q[1] || 0), b: snap(q[0] || 0) }; }
    else end = { kind: 'unbounded' };
    const y0 = f(0);
    return { n, d, f, degN: dn, degD: dd, zeros, vas, holes, end, yInt: holes.some(h => h.x === 0) || !isFinite(y0) ? null : y0 };
  }

  // ── Graphs ──
  function niceStep(range, n) {
    const raw = range / n, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
    return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * p;
  }
  const fmtTick = v => { const r = parseFloat(v.toPrecision(6)); return Math.abs(r) < 1e-10 ? '0' : String(r); };
  // curves: [{ an, color, faint?, label? }], lines: [{ m, b, color, dash }]
  function svgPlot(curves, win, W, H, o = {}) {
    const M = o.margin || { l: 36, r: 10, t: 10, b: 24 };
    const X = v => M.l + (v - win.x0) / (win.x1 - win.x0) * (W - M.l - M.r);
    const Y = v => H - M.b - (v - win.y0) / (win.y1 - win.y0) * (H - M.t - M.b);
    const id = 'c' + Math.random().toString(36).slice(2, 8);
    let s = `<defs><clipPath id="${id}"><rect x="${M.l}" y="${M.t}" width="${W - M.l - M.r}" height="${H - M.t - M.b}"/></clipPath></defs>`;
    if (!o.bare) {
      const xs = niceStep(win.x1 - win.x0, W / 70), ys = niceStep(win.y1 - win.y0, H / 50);
      for (let v = Math.ceil(win.x0 / xs) * xs; v <= win.x1 + 1e-9; v += xs) s += `<line class="grid" x1="${X(v)}" x2="${X(v)}" y1="${M.t}" y2="${H - M.b}"/><text x="${X(v)}" y="${H - M.b + 15}" text-anchor="middle">${fmtTick(v)}</text>`;
      for (let v = Math.ceil(win.y0 / ys) * ys; v <= win.y1 + 1e-9; v += ys) s += `<line class="grid" x1="${M.l}" x2="${W - M.r}" y1="${Y(v)}" y2="${Y(v)}"/><text x="${M.l - 5}" y="${Y(v) + 4}" text-anchor="end">${fmtTick(v)}</text>`;
    }
    if (win.x0 < 0 && win.x1 > 0) s += `<line class="axis" x1="${X(0)}" x2="${X(0)}" y1="${M.t}" y2="${H - M.b}"/>`;
    if (win.y0 < 0 && win.y1 > 0) s += `<line class="axis" x1="${M.l}" x2="${W - M.r}" y1="${Y(0)}" y2="${Y(0)}"/>`;
    let g = '';
    (o.lines || []).forEach(L => { g += `<line x1="${X(win.x0)}" y1="${Y(L.m * win.x0 + L.b)}" x2="${X(win.x1)}" y2="${Y(L.m * win.x1 + L.b)}" stroke="${L.color}" stroke-width="1.6" stroke-dasharray="${L.dash || '6 5'}"/>`; });
    curves.forEach(c => {
      const an = c.an, col = c.color || 'var(--accent)';
      if (c.asymptotes !== false && !c.faint) {
        an.vas.forEach(v => { g += `<line x1="${X(v.x)}" x2="${X(v.x)}" y1="${M.t}" y2="${H - M.b}" stroke="var(--red)" stroke-width="1.4" stroke-dasharray="6 5"/>`; });
        if (an.end.kind === 'ha' || an.end.kind === 'zero') g += `<line x1="${M.l}" x2="${W - M.r}" y1="${Y(an.end.y)}" y2="${Y(an.end.y)}" stroke="var(--amber)" stroke-width="1.4" stroke-dasharray="6 5"/>`;
        if (an.end.kind === 'slant') g += `<line x1="${X(win.x0)}" y1="${Y(an.end.m * win.x0 + an.end.b)}" x2="${X(win.x1)}" y2="${Y(an.end.m * win.x1 + an.end.b)}" stroke="var(--amber)" stroke-width="1.4" stroke-dasharray="6 5"/>`;
      }
      // Sample between the vertical asymptotes so the curve never jumps across one
      const cuts = an.vas.map(v => v.x).filter(x => x > win.x0 && x < win.x1).sort((a, b) => a - b);
      const edges = [win.x0, ...cuts, win.x1], N = Math.max(200, W * 1.5), big = (win.y1 - win.y0) * 20;
      let path = '';
      for (let k = 0; k < edges.length - 1; k++) {
        const a = edges[k], b = edges[k + 1], pad = k > 0 || k < edges.length - 2 ? (win.x1 - win.x0) * 1e-5 : 0;
        const lo = k > 0 ? a + pad : a, hi = k < edges.length - 2 ? b - pad : b;
        const steps = Math.max(20, Math.round(N * (hi - lo) / (win.x1 - win.x0)));
        let pen = false;
        for (let i = 0; i <= steps; i++) {
          const x = lo + (hi - lo) * i / steps, y = an.f(x);
          if (!isFinite(y) || Math.abs(y) > big) { pen = false; continue; }
          path += `${pen ? 'L' : 'M'}${X(x).toFixed(1)},${Y(y).toFixed(1)}`; pen = true;
        }
      }
      g += `<path d="${path}" fill="none" stroke="${col}" stroke-width="${c.faint ? 1.5 : 2.4}" ${c.faint ? 'stroke-dasharray="3 4" opacity=".7"' : ''} stroke-linejoin="round"/>`;
      if (!c.faint) an.holes.forEach(h => { g += `<circle cx="${X(h.x)}" cy="${Y(h.y)}" r="${o.bare ? 3 : 5}" fill="var(--bg)" stroke="${col}" stroke-width="2"/>`; });
    });
    s += `<g clip-path="url(#${id})">${g}</g>`;
    return { svg: s, X, Y, M };
  }

  // Interactive graph: zoom buttons, drag-free; a readout follows the pointer.
  function graph(el, opts) {
    const W = opts.width || 640, H = opts.height || 380, home = { ...opts.win };
    let win = { ...home }, curves = [], lines = [];
    el.classList.add('rf-graph');
    el.innerHTML = `<svg class="plot" viewBox="0 0 ${W} ${H}"></svg>
      <div class="rf-bar"><button class="btn" data-z="out">− Zoom out</button><button class="btn" data-z="in">+ Zoom in</button><button class="btn" data-z="home">Reset</button>
      <span class="rf-read"></span></div>`;
    const svg = el.querySelector('svg'), read = el.querySelector('.rf-read');
    let P = null;
    function draw(cs, ls) {
      if (cs) curves = cs; if (ls) lines = ls;
      P = svgPlot(curves, win, W, H, { lines, margin: { l: 44, r: 12, t: 12, b: 26 } });
      svg.innerHTML = P.svg;
    }
    el.querySelector('.rf-bar').addEventListener('click', e => {
      const z = e.target.dataset.z;
      if (!z) return;
      if (z === 'home') win = { ...home };
      else {
        const k = z === 'out' ? 2 : 0.5, cx = (win.x0 + win.x1) / 2, cy = (win.y0 + win.y1) / 2;
        win = { x0: cx - (cx - win.x0) * k, x1: cx + (win.x1 - cx) * k, y0: cy - (cy - win.y0) * k, y1: cy + (win.y1 - cy) * k };
      }
      draw();
    });
    svg.addEventListener('pointermove', e => {
      if (!P || !curves.length) return;
      const r = svg.getBoundingClientRect(), px = (e.clientX - r.left) / r.width * W;
      const x = win.x0 + (px - P.M.l) / (W - P.M.l - P.M.r) * (win.x1 - win.x0);
      const an = curves.find(c => !c.faint)?.an;
      if (!an || x < win.x0 || x > win.x1) { read.textContent = ''; return; }
      const y = an.f(x);
      read.textContent = `x = ${fmtTick(+x.toPrecision(4))}   y = ${isFinite(y) ? fmtTick(+y.toPrecision(4)) : 'undefined'}`;
    });
    svg.addEventListener('pointerleave', () => { read.textContent = ''; });
    return { draw, reset() { win = { ...home }; draw(); } };
  }

  // A window that shows the interesting part of a function
  function autoWin(an) {
    const xs = [...an.zeros, ...an.vas, ...an.holes].map(p => p.x);
    const lo = Math.min(-6, ...xs) - 2, hi = Math.max(6, ...xs) + 2;
    const ref = an.end.kind === 'ha' || an.end.kind === 'zero' ? an.end.y : 0;
    const span = Math.max(8, Math.abs(ref) * 2.5);
    return { x0: lo, x1: hi, y0: ref - span, y1: ref + span };
  }
  function mini(svg, an, color, W = 220, H = 150) {
    const win = autoWin(an);
    svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
    svg.innerHTML = svgPlot([{ an, color }], win, W, H, { bare: true, margin: { l: 4, r: 4, t: 4, b: 4 } }).svg;
  }

  // Readable formula of a list of x-values, e.g. "−4, 1"
  const list = xs => xs.map(v => fmtTick(+v.toPrecision(6))).join(', ');
  return { parse, analyze, graph, mini, autoWin, list, divide, snap };
})();
