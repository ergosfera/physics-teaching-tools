// Parallel-Plate Capacitor: what one report's raw data means, and the plots of the student page.
// Shared by capacitance_lab.html (students) and capacitance_teacher.html, so both compute the same values.
// Everything lives in CAP, so it never clashes with the helpers of ../lab-teacher.js.
//
// Units inside: lengths in m, areas in m², capacitance in pF, ε in pF/m (ε₀ = 8.854 pF/m).
// What students type: L, W, x in cm; thicknesses in mm; readings in the meter's unit (setup.cunit).

const CAP = (() => {
  const EPS0 = 8.854;
  const UNIT = { pF: 1, nF: 1000, 'µF': 1e6 };   // pF per unit the meter shows
  const MATS = { paper: 'Paper', glass: 'Glass', plastic: 'Plastic', fabric: 'Fabric', cardboard: 'Cardboard', other: 'Other' };

  const num = s => { const v = parseFloat(String(s ?? '').trim().replace(',', '.')); return isFinite(v) ? v : NaN; };
  function fmt(v, sig = 3) {
    if (!isFinite(v)) return '—';
    if (v === 0) return '0';
    const a = Math.abs(v);
    if (a >= 1e5 || a < 1e-3) return v.toExponential(sig - 1);
    return String(parseFloat(v.toPrecision(sig)));
  }
  function niceStep(range, n) {
    const raw = range / n, p = Math.pow(10, Math.floor(Math.log10(raw))), f = raw / p;
    return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * p;
  }
  // Least squares y = k x + b with standard errors
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

  // Everything derived from one report
  function analyze(s) {
    const st = s.setup || {}, cu = UNIT[st.cunit] || 1000;
    const L = num(st.L), W = num(st.W), A = L > 0 && W > 0 ? L * W * 1e-4 : NaN;
    const N = num(st.N), stack = num(st.stack), t = N > 0 && stack > 0 ? stack / N * 1e-3 : NaN;
    const C0 = isFinite(num(st.C0)) ? num(st.C0) * cu : NaN;
    const net = C => { const c = num(C); return isFinite(c) && isFinite(C0) ? c * cu - C0 : NaN; };

    // Separation: C against 1/d, full overlap. Slope = ε·A
    const sepPts = t > 0 ? (s.sep || []).map(r => ({ n: num(r.n), y: net(r.C) }))
      .filter(r => r.n > 0 && isFinite(r.y)).map(r => ({ n: r.n, d: r.n * t, x: 1 / (r.n * t), y: r.y })) : [];
    const sepFit = sepPts.length >= 2 ? linfit(sepPts) : null;
    const epsSep = sepFit && A > 0 ? sepFit.k / A : NaN;
    const dkSep = sepFit && A > 0 ? sepFit.sk / A / EPS0 : NaN;

    // Area: C against the overlap area W·x, fixed sheets. Slope = ε/d
    const ar = s.area || {}, dArea = num(ar.n) > 0 && t > 0 ? num(ar.n) * t : NaN;
    const areaPts = W > 0 ? (ar.rows || []).map(r => ({ xc: num(r.x), y: net(r.C) }))
      .filter(r => r.xc > 0 && isFinite(r.y)).map(r => ({ xc: r.xc, x: W * r.xc * 1e-4, y: r.y })) : [];
    const areaFit = areaPts.length >= 2 ? linfit(areaPts) : null;
    const epsArea = areaFit && dArea > 0 ? areaFit.k * dArea : NaN;

    // Materials: κ = C·d / (ε₀·A), one layer, full overlap (index kept: one entry per row)
    const mats = (s.mats || []).map(r => {
      const tm = num(r.t) * 1e-3, y = net(r.C);
      const kappa = r.mat && tm > 0 && y > 0 && A > 0 ? y * tm / (EPS0 * A) : NaN;
      return { mat: r.mat || '', name: String(r.name || '').trim(), t: tm, y, kappa };
    });

    return { cu, L, W, A, t, C0, sepPts, sepFit, epsSep, kSep: epsSep / EPS0, dkSep, dArea, areaPts, areaFit, epsArea, kArea: epsArea / EPS0, mats };
  }

  // ── Student page plots (use the .plot classes of the lab page) ──
  // pts in SI (x shown as x·o.xs); fit in SI. Both axes include the origin.
  function plot(svg, pts, fit, o) {
    const W = 640, H = 380, M = { l: 66, r: 20, t: 18, b: 50 }, xs = o.xs || 1;
    const xv = pts.map(p => p.x * xs), yv = pts.map(p => p.y);
    const xMax = Math.max(...xv, 0) || 1, yMax = Math.max(...yv, fit ? fit.b : 0, 0) || 100;
    const xStep = niceStep(xMax - Math.min(0, ...xv), 5), yStep = niceStep(yMax - Math.min(0, ...yv, fit ? fit.b : 0), 5);
    const x0 = Math.floor(Math.min(0, ...xv) / xStep) * xStep, x1 = Math.ceil(xMax * 1.04 / xStep) * xStep;
    const y0 = Math.floor(Math.min(0, ...yv, fit ? fit.b : 0) / yStep) * yStep, y1 = Math.ceil(yMax * 1.04 / yStep) * yStep;
    const X = v => M.l + (v - x0) / (x1 - x0) * (W - M.l - M.r);
    const Y = v => H - M.b - (v - y0) / (y1 - y0) * (H - M.t - M.b);
    const cid = 'clip-' + svg.id;
    let s = `<defs><clipPath id="${cid}"><rect x="${M.l}" y="${M.t}" width="${W - M.l - M.r}" height="${H - M.t - M.b}"/></clipPath></defs>`;
    for (let v = x0; v <= x1 + 1e-9 * xStep; v += xStep) s += `<line class="grid" x1="${X(v)}" x2="${X(v)}" y1="${M.t}" y2="${H - M.b}"/><text x="${X(v)}" y="${H - M.b + 18}" text-anchor="middle">${fmt(v)}</text>`;
    for (let v = y0; v <= y1 + 1e-9 * yStep; v += yStep) s += `<line class="grid" x1="${M.l}" x2="${W - M.r}" y1="${Y(v)}" y2="${Y(v)}"/><text x="${M.l - 8}" y="${Y(v) + 4}" text-anchor="end">${fmt(v)}</text>`;
    s += `<line class="axis" x1="${X(0)}" x2="${X(0)}" y1="${M.t}" y2="${H - M.b}"/><line class="axis" x1="${M.l}" x2="${W - M.r}" y1="${Y(0)}" y2="${Y(0)}"/>`;
    s += `<text class="lbl" x="${(M.l + W - M.r) / 2}" y="${H - 8}" text-anchor="middle">${o.xl}</text>`;
    s += `<text class="lbl" transform="translate(16 ${(M.t + H - M.b) / 2}) rotate(-90)" text-anchor="middle">${o.yl}</text>`;
    if (fit) {
      const f = xd => fit.k * xd / xs + fit.b;
      s += `<line class="fit" x1="${X(x0)}" y1="${Y(f(x0))}" x2="${X(x1)}" y2="${Y(f(x1))}" clip-path="url(#${cid})"/>`;
    }
    pts.forEach(p => { s += `<circle class="pt" cx="${X(p.x * xs)}" cy="${Y(p.y)}" r="6"/>`; });
    if (!pts.length) s += `<text x="${W / 2}" y="${H / 2}" text-anchor="middle">${o.empty || 'No points yet'}</text>`;
    svg.innerHTML = s;
  }

  // Horizontal bars of κ, one per material, with a dashed line at κ = 1 (vacuum)
  function barsKappa(svg, items) {
    const W = 640, rowH = 34, M = { l: 200, r: 50, t: 30, b: 36 }, H = M.t + M.b + Math.max(1, items.length) * rowH;
    svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
    if (!items.length) { svg.innerHTML = `<text x="${W / 2}" y="${H / 2 + 4}" text-anchor="middle">The κ of each material will appear here</text>`; return; }
    const max = Math.max(2, ...items.map(i => i.v)), step = niceStep(max, 5), x1 = Math.ceil(max * 1.05 / step) * step;
    const X = v => M.l + v / x1 * (W - M.l - M.r);
    let s = '';
    for (let v = 0; v <= x1 + 1e-9; v += step) s += `<line class="grid" x1="${X(v)}" x2="${X(v)}" y1="${M.t}" y2="${H - M.b}"/><text x="${X(v)}" y="${H - M.b + 18}" text-anchor="middle">${fmt(v)}</text>`;
    items.forEach((it, i) => {
      const y = M.t + i * rowH + 6;
      s += `<rect x="${X(0)}" y="${y}" width="${Math.max(0, X(it.v) - X(0))}" height="${rowH - 12}" rx="4" fill="var(--accent)" fill-opacity=".75"/>`;
      s += `<text x="${M.l - 8}" y="${y + rowH / 2}" text-anchor="end" class="lbl">${String(it.name).replace(/[<&]/g, c => c === '<' ? '&lt;' : '&amp;').slice(0, 30)}</text>`;
      s += `<text x="${X(it.v) + 6}" y="${y + rowH / 2}">${fmt(it.v)}</text>`;
    });
    s += `<line x1="${X(1)}" x2="${X(1)}" y1="${M.t}" y2="${H - M.b}" stroke="var(--text)" stroke-dasharray="5 4" opacity=".6"/><text x="${X(1)}" y="${M.t - 10}" text-anchor="middle" font-size="11">vacuum κ = 1</text>`;
    s += `<text class="lbl" x="${(M.l + W - M.r) / 2}" y="${H - 4}" text-anchor="middle">Dielectric constant κ</text>`;
    svg.innerHTML = s;
  }

  return { EPS0, UNIT, MATS, num, fmt, linfit, niceStep, analyze, plot, barsKappa };
})();
