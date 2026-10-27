// Equipotential Lines: the four arrangements on the conductive paper, how to draw them, and a model of each.
// Shared by equipotential_lab.html (students) and equipotential_teacher.html.
// Coordinates are centimeters on the paper's own grid (PASCO PK-9025B): x 0–28 to the right, y 0–20 up.

const EQ_KEYS = ['A', 'B', 'C', 'D'];
const EQ_VOLTS = [1, 2, 3, 4, 5, 6, 7, 8];
const EQ_HIGH = 9;   // the red electrode, in volts (the black one is 0 V)

// Electrodes: dot (cx, cy, r), ring (cx, cy, r, w: tape width) or bar (x0..x1, y0..y1).
// pole: 0 for the black lead, EQ_HIGH for the red lead. label: where its badge goes (on the copper, so it never hides a point).
const EQ_ARR = {
  A: { name: 'Dot inside a ring', short: 'Ring', x0: 0, x1: 14, y0: 10, y1: 20, el: [
    { type: 'ring', cx: 6.5, cy: 14.5, r: 4, w: 0.5, pole: 0, label: [9.33, 17.33] },
    { type: 'dot', cx: 6.5, cy: 14.5, r: 0.5, pole: EQ_HIGH, label: [6.5, 14.5] },
  ] },
  B: { name: 'Two dots', short: 'Two dots', x0: 14, x1: 28, y0: 10, y1: 20, el: [
    { type: 'dot', cx: 17, cy: 15, r: 0.5, pole: 0, label: [17, 15] },
    { type: 'dot', cx: 23, cy: 15, r: 0.5, pole: EQ_HIGH, label: [23, 15] },
  ] },
  C: { name: 'Dot and a strip', short: 'Dot and strip', x0: 0, x1: 14, y0: 0, y1: 10, el: [
    { type: 'dot', cx: 4.5, cy: 5.5, r: 0.5, pole: EQ_HIGH, label: [4.5, 5.5] },
    { type: 'bar', x0: 10.75, x1: 11.25, y0: 2, y1: 9.5, pole: 0, label: [11, 5.75] },
  ] },
  D: { name: 'Two parallel strips', short: 'Parallel strips', x0: 14, x1: 28, y0: 0, y1: 10, el: [
    { type: 'bar', x0: 18, x1: 24, y0: 7, y1: 7.5, pole: EQ_HIGH, label: [21, 7.25] },
    { type: 'bar', x0: 18, x1: 24, y0: 2.5, y1: 3, pole: 0, label: [21, 2.75] },
  ] },
};
const EQ_ALL = { name: 'The whole sheet', x0: 0, x1: 28, y0: 0, y1: 20, el: EQ_KEYS.flatMap(k => EQ_ARR[k].el) };
const EQ_PLATE_GAP = 4.5;   // cm between the two strips of D (center to center)

// One color per voltage, readable on the dark paper
const EQ_VCOLOR = { 1: '#4c9aff', 2: '#22c3e6', 3: '#2fd18b', 4: '#9be24a', 5: '#f2d53c', 6: '#ffa53c', 7: '#ff6b4a', 8: '#ff5fae' };
const eqColor = v => EQ_VCOLOR[v] || '#ccc';
const EQ_PRED = '#c4a7ff';   // predicted field lines
const EQ_FIELD = '#ffffff';  // field lines after the rule

function eqInside(e, x, y, grow = 0) {
  if (e.type === 'dot') return Math.hypot(x - e.cx, y - e.cy) <= e.r + grow;
  if (e.type === 'ring') return Math.abs(Math.hypot(x - e.cx, y - e.cy) - e.r) <= e.w / 2 + grow;
  return x >= e.x0 - grow && x <= e.x1 + grow && y >= e.y0 - grow && y <= e.y1 + grow;
}

// ── Drawing ─────────────────────────────────────────────────
// Screen frame of an arrangement (or 'ALL'): S svg units per cm, a margin for the axis numbers
function eqFrame(key) {
  const A = key === 'ALL' ? EQ_ALL : EQ_ARR[key], S = 40, ml = 0.9, mb = 0.75, mt = 0.25, mr = 0.25;
  const W = (A.x1 - A.x0 + ml + mr) * S, H = (A.y1 - A.y0 + mt + mb) * S;
  return {
    A, S, W, H,
    X: x => (x - A.x0 + ml) * S, Y: y => (A.y1 - y + mt) * S,
    cm: (ux, uy) => [ux / S + A.x0 - ml, A.y1 + mt - uy / S],
    inside: (x, y) => x >= A.x0 && x <= A.x1 && y >= A.y0 && y <= A.y1,
  };
}
const r2 = v => Math.round(v * 100) / 100;

// A smooth path through the points of a stroke
function eqPathD(F, pts) {
  if (!pts.length) return '';
  const P = pts.map(([x, y]) => [F.X(x), F.Y(y)]);
  if (P.length < 3) return 'M' + P.map(p => p.map(r2).join(' ')).join(' L');
  let d = `M${r2(P[0][0])} ${r2(P[0][1])}`;
  for (let i = 1; i < P.length - 1; i++) {
    const mx = (P[i][0] + P[i + 1][0]) / 2, my = (P[i][1] + P[i + 1][1]) / 2;
    d += ` Q${r2(P[i][0])} ${r2(P[i][1])} ${r2(mx)} ${r2(my)}`;
  }
  const L = P[P.length - 1];
  return d + ` L${r2(L[0])} ${r2(L[1])}`;
}
// Arrowheads along a field line: one halfway and one at the end, pointing the way it was drawn
function eqArrows(F, pts, color, size = 11) {
  if (pts.length < 2) return '';
  const P = pts.map(([x, y]) => [F.X(x), F.Y(y)]);
  const cum = [0];
  for (let i = 1; i < P.length; i++) cum.push(cum[i - 1] + Math.hypot(P[i][0] - P[i - 1][0], P[i][1] - P[i - 1][1]));
  const total = cum[cum.length - 1];
  if (total < size * 1.5) return '';
  const at = s => {
    let i = 1; while (i < P.length - 1 && cum[i] < s) i++;
    const t = (s - cum[i - 1]) / ((cum[i] - cum[i - 1]) || 1);
    return [P[i - 1][0] + (P[i][0] - P[i - 1][0]) * t, P[i - 1][1] + (P[i][1] - P[i - 1][1]) * t];
  };
  const head = s => {
    const [x2, y2] = at(s), [x1, y1] = at(Math.max(0, s - size * 1.2));
    const L = Math.hypot(x2 - x1, y2 - y1) || 1, ux = (x2 - x1) / L, uy = (y2 - y1) / L, bx = x2 - ux * size, by = y2 - uy * size;
    return `<polygon points="${r2(x2)},${r2(y2)} ${r2(bx - uy * size * .5)},${r2(by + ux * size * .5)} ${r2(bx + uy * size * .5)},${r2(by - ux * size * .5)}" fill="${color}"/>`;
  };
  return head(total / 2 + size / 2) + head(total);
}

// Draws one arrangement (or 'ALL') into an <svg>.
// data: { points: [{p, v, x, y, color?}], equi: [{v, pts}], field: [pts], field2: [pts] }
// o: { paths, fine (small marks to place points precisely), selected: {p, v}, fieldFaint, model, small, dim, letters, coords }
function eqDraw(svg, key, data = {}, o = {}) {
  const F = eqFrame(key), A = F.A, S = F.S;
  svg.setAttribute('viewBox', `0 0 ${F.W} ${F.H}`);
  let s = `<rect x="${F.X(A.x0)}" y="${F.Y(A.y1)}" width="${(A.x1 - A.x0) * S}" height="${(A.y1 - A.y0) * S}" fill="#1c1d20" rx="4"/>`;
  // the paper's grid: faint lines and a cross at every centimeter
  let g = '', c = '';
  for (let x = Math.ceil(A.x0); x <= A.x1; x++) g += `M${F.X(x)} ${F.Y(A.y0)}V${F.Y(A.y1)}`;
  for (let y = Math.ceil(A.y0); y <= A.y1; y++) g += `M${F.X(A.x0)} ${F.Y(y)}H${F.X(A.x1)}`;
  const k = 0.13 * S;
  for (let x = Math.ceil(A.x0) + 1; x < A.x1; x++) for (let y = Math.ceil(A.y0) + 1; y < A.y1; y++) c += `M${F.X(x) - k} ${F.Y(y)}h${2 * k}M${F.X(x)} ${F.Y(y) - k}v${2 * k}`;
  s += `<path d="${g}" stroke="#fff" stroke-opacity=".07" stroke-width="1"/><path d="${c}" stroke="#fff" stroke-opacity="${o.small ? .3 : .5}" stroke-width="1.2"/>`;
  if (key === 'ALL') s += `<path d="M${F.X(14)} ${F.Y(0)}V${F.Y(20)}M${F.X(0)} ${F.Y(10)}H${F.X(28)}" stroke="#fff" stroke-opacity=".55" stroke-width="1.5"/>`;
  // axis numbers, every 2 cm as on the paper
  const step = o.small ? 4 : 2, fs = o.small ? 22 : 15;
  for (let x = Math.ceil(A.x0 / step) * step; x <= A.x1; x += step) s += `<text x="${F.X(x)}" y="${F.Y(A.y0) + 0.55 * S}" text-anchor="middle" font-size="${fs}" fill="var(--muted)">${x}</text>`;
  for (let y = Math.ceil(A.y0 / step) * step; y <= A.y1; y += step) s += `<text x="${F.X(A.x0) - 0.15 * S}" y="${F.Y(y) + fs * .35}" text-anchor="end" font-size="${fs}" fill="var(--muted)">${y}</text>`;
  if (o.letters && key === 'ALL') EQ_KEYS.forEach(kk => { const B = EQ_ARR[kk]; s += `<text x="${F.X(B.x0) + 10}" y="${F.Y(B.y1) + 26}" font-size="22" font-weight="800" fill="#fff" fill-opacity=".8">${kk}</text>`; });
  // the model's equipotential lines, dashed
  if (o.model && key !== 'ALL') {
    const segs = eqContours(key);
    EQ_VOLTS.forEach(v => {
      const d = segs.filter(q => q[4] === v).map(q => `M${r2(F.X(q[0]))} ${r2(F.Y(q[1]))}L${r2(F.X(q[2]))} ${r2(F.Y(q[3]))}`).join('');
      if (d) s += `<path d="${d}" stroke="${eqColor(v)}" stroke-width="1.6" stroke-dasharray="5 4" opacity=".75" fill="none"/>`;
    });
  }
  // electrodes: copper tape
  A.el.forEach(e => {
    const st = 'fill="#c9875b" stroke="#f0b48a" stroke-width="1"';
    if (e.type === 'dot') s += `<circle cx="${F.X(e.cx)}" cy="${F.Y(e.cy)}" r="${e.r * S}" ${st}/>`;
    else if (e.type === 'ring') s += `<circle cx="${F.X(e.cx)}" cy="${F.Y(e.cy)}" r="${e.r * S}" fill="none" stroke="#c9875b" stroke-width="${e.w * S}"/>`;
    else s += `<rect x="${F.X(e.x0)}" y="${F.Y(e.y1)}" width="${(e.x1 - e.x0) * S}" height="${(e.y1 - e.y0) * S}" ${st}/>`;
  });
  // electrode badges: which lead goes where (under the lines and points)
  A.el.forEach(e => {
    const [x, y] = e.label, red = e.pole > 0, w = o.small ? 66 : 44, h = o.small ? 28 : 20;
    s += `<g transform="translate(${F.X(x)} ${F.Y(y)})"><rect x="${-w / 2}" y="${-h / 2}" width="${w}" height="${h}" rx="${h / 2}" fill="${red ? '#d7263d' : '#000'}" stroke="#fff" stroke-width="1.4"/>` +
      `<text y="${h * .27}" text-anchor="middle" font-size="${o.small ? 17 : 12}" font-weight="800" fill="#fff">${red ? EQ_HIGH + ' V' : '0 V'}</text></g>`;
  });
  // equipotential lines the group drew
  (data.equi || []).forEach(q => {
    if (q.pts.length < 2) return;
    s += `<path d="${eqPathD(F, q.pts)}" stroke="${q.color || eqColor(q.v)}" stroke-width="${o.small ? 4 : 3}" fill="none" stroke-linecap="round" stroke-linejoin="round"/>`;
    if (!o.small && !q.color) {
      const [x, y] = q.pts[q.pts.length - 1];
      s += `<text x="${F.X(x) + 6}" y="${F.Y(y) - 6}" font-size="13" font-weight="700" fill="${eqColor(q.v)}">${q.v} V</text>`;
    }
  });
  // predicted field lines (dashed) and the field lines after the rule (solid)
  (data.field || []).forEach(pts => {
    if (pts.length < 2) return;
    s += `<g opacity="${o.fieldFaint ? .4 : 1}"><path d="${eqPathD(F, pts)}" stroke="${EQ_PRED}" stroke-width="2.4" stroke-dasharray="7 5" fill="none" stroke-linecap="round"/>${eqArrows(F, pts, EQ_PRED)}</g>`;
  });
  (data.field2 || []).forEach(pts => {
    if (pts.length < 2) return;
    s += `<path d="${eqPathD(F, pts)}" stroke="${EQ_FIELD}" stroke-width="2.4" fill="none" stroke-linecap="round"/>${eqArrows(F, pts, EQ_FIELD)}`;
  });
  // the route of each path: its points in order of voltage, faint
  const pts = data.points || [];
  if (o.paths) for (let p = 1; p <= 5; p++) {
    const route = pts.filter(q => q.p === p).sort((a, b) => a.v - b.v);
    if (route.length > 1) s += `<polyline points="${route.map(q => r2(F.X(q.x)) + ',' + r2(F.Y(q.y))).join(' ')}" fill="none" stroke="#fff" stroke-opacity=".28" stroke-width="1.5" stroke-dasharray="3 4"/>`;
  }
  pts.forEach(q => {
    const sel = o.selected && o.selected.p === q.p && o.selected.v === q.v;
    const r = o.small ? 6 : o.fine ? 2 : 5.5, X = r2(F.X(q.x)), Y = r2(F.Y(q.y));
    if (sel) s += `<circle cx="${X}" cy="${Y}" r="${r + 4}" fill="none" stroke="#fff" stroke-width="2"/>`;
    s += `<circle cx="${X}" cy="${Y}" r="${r}" fill="${q.color || eqColor(q.v)}" stroke="#111" stroke-width="1.2"${o.dim && !sel ? ' opacity=".55"' : ''}/>`;
  });
  svg.innerHTML = s + '<g class="live"></g>';
  return F;
}

// ── Crossings: where field lines cut equipotential lines, and at what angle ──
function eqSegHit(a, b, c, d) {
  const r = [b[0] - a[0], b[1] - a[1]], q = [d[0] - c[0], d[1] - c[1]], den = r[0] * q[1] - r[1] * q[0];
  if (Math.abs(den) < 1e-12) return null;
  const t = ((c[0] - a[0]) * q[1] - (c[1] - a[1]) * q[0]) / den, u = ((c[0] - a[0]) * r[1] - (c[1] - a[1]) * r[0]) / den;
  if (t < 0 || t >= 1 || u < 0 || u >= 1) return null;   // half-open, so a crossing at a shared vertex counts once
  const cos = Math.abs(r[0] * q[0] + r[1] * q[1]) / (Math.hypot(...r) * Math.hypot(...q) || 1);
  return { t, angle: Math.acos(Math.min(1, cos)) * 180 / Math.PI };
}
// Smooths a hand-drawn line a little (a short wobble shouldn't count as its direction)
function eqSmooth(pts) {
  return pts.length < 3 ? pts : pts.map((p, i) => i === 0 || i === pts.length - 1 ? p
    : [(pts[i - 1][0] + 2 * p[0] + pts[i + 1][0]) / 4, (pts[i - 1][1] + 2 * p[1] + pts[i + 1][1]) / 4]);
}
// → { n: crossings, angles: [...], medDev: median of |90° − angle|, lines: field lines that cross 2+ different voltages,
//     down: of those, how many go from higher to lower voltage, up: from lower to higher }
function eqCrossStats(field, equi) {
  const angles = [];
  let lines = 0, down = 0, up = 0;
  (field || []).forEach(raw => {
    const f = eqSmooth(raw), hits = [];
    let s0 = 0;
    for (let i = 1; i < f.length; i++) {
      const segL = Math.hypot(f[i][0] - f[i - 1][0], f[i][1] - f[i - 1][1]);
      (equi || []).forEach(q => {
        const e = eqSmooth(q.pts);
        for (let j = 1; j < e.length; j++) {
          const h = eqSegHit(f[i - 1], f[i], e[j - 1], e[j]);
          if (h) { hits.push({ s: s0 + h.t * segL, v: q.v }); angles.push(h.angle); }
        }
      });
      s0 += segL;
    }
    hits.sort((a, b) => a.s - b.s);
    if (new Set(hits.map(h => h.v)).size >= 2) {
      lines++;
      const dv = hits[hits.length - 1].v - hits[0].v;
      if (dv < 0) down++; else if (dv > 0) up++;
    }
  });
  const dev = angles.map(a => 90 - a).sort((a, b) => a - b), n = dev.length;
  const medDev = n ? (n % 2 ? dev[(n - 1) / 2] : (dev[n / 2 - 1] + dev[n / 2]) / 2) : NaN;
  return { n, angles, medDev, lines, down, up };
}

// ── Model: the potential on the paper, from Laplace's equation ──
// Each arrangement is solved on its own 14 × 10 cm quarter, with the electrodes held at 0 V and 9 V
// and no current through the edges of the quarter. Used by the teacher page (model lines and demo class).
const EQ_MODEL = {};
function eqModel(key) {
  if (EQ_MODEL[key]) return EQ_MODEL[key];
  const A = EQ_ARR[key], h = 0.2, nx = Math.round((A.x1 - A.x0) / h) + 1, ny = Math.round((A.y1 - A.y0) / h) + 1;
  const V = new Float64Array(nx * ny).fill(EQ_HIGH / 2), fixed = new Uint8Array(nx * ny);
  for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) {
    const e = A.el.find(e => eqInside(e, A.x0 + i * h, A.y0 + j * h));
    if (e) { V[j * nx + i] = e.pole; fixed[j * nx + i] = 1; }
  }
  for (let it = 0; it < 4000; it++) {
    let maxd = 0;
    for (let j = 0; j < ny; j++) for (let i = 0; i < nx; i++) {
      const id = j * nx + i;
      if (fixed[id]) continue;
      let sum = 0, n = 0;
      if (i > 0) { sum += V[id - 1]; n++; }
      if (i < nx - 1) { sum += V[id + 1]; n++; }
      if (j > 0) { sum += V[id - nx]; n++; }
      if (j < ny - 1) { sum += V[id + nx]; n++; }
      const d = (sum / n - V[id]) * 1.9;
      V[id] += d;
      if (Math.abs(d) > maxd) maxd = Math.abs(d);
    }
    if (maxd < 2e-5) break;
  }
  return (EQ_MODEL[key] = { A, h, nx, ny, V });
}
// The model's potential at a point (bilinear between grid nodes)
function eqPotential(key, x, y) {
  const M = eqModel(key), A = M.A;
  const gx = Math.max(0, Math.min(M.nx - 1.0001, (x - A.x0) / M.h)), gy = Math.max(0, Math.min(M.ny - 1.0001, (y - A.y0) / M.h));
  const i = Math.floor(gx), j = Math.floor(gy), tx = gx - i, ty = gy - j, at = (a, b) => M.V[b * M.nx + a];
  return (at(i, j) * (1 - tx) + at(i + 1, j) * tx) * (1 - ty) + (at(i, j + 1) * (1 - tx) + at(i + 1, j + 1) * tx) * ty;
}
// The model's equipotential lines at 1 V … 8 V, as segments [x1, y1, x2, y2, v] (marching squares)
const EQ_CONTOURS = {};
function eqContours(key) {
  if (EQ_CONTOURS[key]) return EQ_CONTOURS[key];
  const M = eqModel(key), A = M.A, out = [];
  const P = (i, j) => [A.x0 + i * M.h, A.y0 + j * M.h], at = (i, j) => M.V[j * M.nx + i];
  for (let j = 0; j < M.ny - 1; j++) for (let i = 0; i < M.nx - 1; i++) {
    const c = [[i, j], [i + 1, j], [i + 1, j + 1], [i, j + 1]];
    EQ_VOLTS.forEach(L => {
      const hits = [];
      for (let k = 0; k < 4; k++) {
        const [a, b] = [c[k], c[(k + 1) % 4]], va = at(...a), vb = at(...b);
        if ((va - L) * (vb - L) < 0) {
          const t = (L - va) / (vb - va), pa = P(...a), pb = P(...b);
          hits.push([pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t]);
        }
      }
      if (hits.length >= 2) out.push([...hits[0], ...hits[1], L]);
      if (hits.length === 4) out.push([...hits[2], ...hits[3], L]);
    });
  }
  return (EQ_CONTOURS[key] = out);
}
