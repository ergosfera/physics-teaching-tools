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

  // Problems in one report: what the student page warns about while students type, and what the
  // teacher page flags. Each: { step, msg (HTML for students), flag (short, for the teacher), red }.
  // a: the result of analyze(s) (the teacher page passes its group, which has the same fields).
  const NEAR_C0 = 3;   // pF: a reading this close to C₀ is the leads, not the plates
  function problems(s, a = analyze(s)) {
    const out = [], st = s.setup || {}, L = num(st.L), unit = st.cunit || 'nF';
    const net = C => { const c = num(C); return isFinite(c) && isFinite(a.C0) ? c * a.cu - a.C0 : NaN; };
    const atC0 = (rows, step, what) => {
      const bad = rows.map((r, i) => ({ i, y: net(r.C) })).filter(r => isFinite(r.y) && r.y <= NEAR_C0);
      if (bad.length) out.push({ step, red: true, flag: `Readings at C₀ (${what})`,
        msg: `Row${bad.length > 1 ? 's' : ''} ${bad.map(r => r.i + 1).join(', ')}: the reading is at C₀ (${fmt(a.C0)} pF) or below, so the meter is reading only the leads, not the plates. Check that each clip bites <b>bare metal</b> on its own plate (scrape off any coating), then measure again.` });
    };
    atC0(s.sep || [], 4, 'separation');
    atC0((s.area || {}).rows || [], 5, 'area');
    atC0((s.mats || []).filter(r => r.mat), 6, 'materials');

    // Step 4: with 1 sheet C should be about twice C with 2 sheets
    const at = n => { const r = (s.sep || []).find(r => num(r.n) === n); return r ? net(r.C) : NaN; };
    const ratio = at(1) / at(2);
    if (at(1) > NEAR_C0 && at(2) > NEAR_C0 && ratio < 1.5) out.push({ step: 4, flag: `C(1 sheet)/C(2 sheets) = ${fmt(ratio, 2)}: gap not set by the paper`,
      msg: `With 1 sheet, C is only ${fmt(ratio, 2)} times C with 2 sheets. Twice the separation should give about half: 2 times. Something besides the paper is setting the gap: a clip between the plates, a bent plate, or the book pressing differently each time. Fix it and measure the sheets again.` });

    // Step 5: overlap longer than the plate
    const longX = ((s.area || {}).rows || []).map((r, i) => ({ i, x: num(r.x) })).filter(r => L > 0 && r.x > L + 0.05);
    if (longX.length) out.push({ step: 5, flag: 'Overlap longer than the plate', red: true,
      msg: `Row${longX.length > 1 ? 's' : ''} ${longX.map(r => r.i + 1).join(', ')}: an overlap longer than the plate (L = ${fmt(L)} cm) is not possible. Check x.` });
    // Step 5: full overlap should read about what step 4 read with the same sheets
    const nA = num((s.area || {}).n), fullA = ((s.area || {}).rows || []).find(r => L > 0 && Math.abs(num(r.x) - L) <= 0.05), sameN = at(nA);
    if (fullA && net(fullA.C) > NEAR_C0 && sameN > NEAR_C0 && Math.abs(net(fullA.C) / sameN - 1) > 0.3) out.push({ step: 5, flag: 'Full overlap ≠ step 4',
      msg: `At full overlap (x = L) with ${fmt(nA)} sheets you read ${fmt(net(fullA.C))} pF, but in step 4 the same ${fmt(nA)} sheets gave ${fmt(sameN)} pF. It is the same capacitor, so they should be close: check the decimal point, or measure again.` });

    // Step 6: paper against step 3 and step 4; κ too large
    const paper = (s.mats || []).find(r => r.mat === 'paper' && isFinite(net(r.C)));
    if (paper) {
      const tp = num(paper.t) * 1e-3;
      if (tp > 0 && a.t > 0 && Math.abs(tp / a.t - 1) > 0.3) out.push({ step: 6, flag: 'Paper thickness ≠ step 3',
        msg: `The paper here is ${fmt(tp * 1e3)} mm thick, but in step 3 one sheet was ${fmt(a.t * 1e3)} mm. Use the same value for 1 sheet.` });
      if (net(paper.C) > NEAR_C0 && at(1) > NEAR_C0 && Math.abs(net(paper.C) / at(1) - 1) > 0.3) out.push({ step: 6, flag: '1 sheet: step 6 ≠ step 4',
        msg: `One sheet of paper gives ${fmt(net(paper.C))} pF here but ${fmt(at(1))} pF in step 4. It is the same capacitor, so they should be close: check the decimal point, or find what changed (clips, pressing) and measure both again.` });
    }
    a.mats.forEach((m, i) => {
      if (m.kappa > 15) out.push({ step: 6, flag: `κ = ${fmt(m.kappa)} (${MATS[m.mat] || m.mat})?`,
        msg: `Row ${i + 1}: κ = ${fmt(m.kappa)} is unusually large (most solids are between 2 and 10). Check the decimal point of the reading (in ${unit}) and the thickness (in <b>mm</b>).` });
    });

    // Step 7: model test
    const t = s.test || {}, tx = num(t.x), meas = num(t.meas);
    if (L > 0 && tx > L + 0.05) out.push({ step: 7, flag: 'Test overlap longer than the plate', red: true,
      msg: `An overlap of ${fmt(tx)} cm is longer than your plate (L = ${fmt(L)} cm), so that capacitor can't be built. Choose an x smaller than L.` });
    if (isFinite(meas) && isFinite(a.C0) && meas * a.cu <= a.C0 + NEAR_C0) out.push({ step: 7, flag: 'Test reading at or below C₀', red: true,
      msg: `A reading of ${fmt(meas)} ${unit} is at C₀ (${fmt(a.C0 / a.cu)} ${unit}) or below: the meter would be reading only the leads. Check the clips and measure again.` });

    // Step 8: uncertainty
    const dk = num((s.conclusion || {}).dkappa);
    if (isFinite(dk) && dk <= 0) out.push({ step: 8, flag: 'Uncertainty ≤ 0', msg: 'An uncertainty is always <b>positive</b>: it says how far the true value may be, either way.' });
    return out;
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

  return { EPS0, UNIT, MATS, num, fmt, linfit, niceStep, analyze, problems, plot, barsKappa };
})();
