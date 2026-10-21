// Rational Functions activity: the questions with their answers and hints (AP Precalculus CED 1.7–1.12).
// Shared by the student page (renders and checks them) and the teacher page (answer key and charts).
//
// Each question: { sec, type: 'mc' | 'num' | 'list', short (plain text for the teacher), text (HTML),
//   options?: { key: text }, answer, unit?, hint(value) → why it's wrong, ok: shown when right }
// For 'list', answer is the set of numbers in any order; for 'num' a number (fractions like 1/2 are accepted).
const RQ_SECTIONS = {
  end: { step: 2, code: '1.7', title: 'End behavior' },
  zero: { step: 3, code: '1.8', title: 'Zeros' },
  va: { step: 4, code: '1.9', title: 'Vertical asymptotes' },
  hole: { step: 5, code: '1.10', title: 'Holes' },
  ld: { step: 6, code: '1.11', title: 'Equivalent representations' },
  tr: { step: 7, code: '1.12', title: 'Transformations' },
};

const RQ = {
  // ── 1.7 End behavior ──
  end_f: {
    sec: 'end', type: 'mc', short: 'f(x) = (2x² − 3)/(x² + 1) as x → ±∞',
    text: 'As x → ±∞, <span class="fx">f(x) = (2x² − 3) / (x² + 1)</span> …',
    options: { zero: 'approaches 0', ha: 'approaches a nonzero number', big: 'grows without bound (→ ±∞)' },
    answer: 'ha',
    hint: v => v === 'zero'
      ? 'Compare the degrees: the numerator and the denominator both have degree 2. The leading terms give 2x²/x² = 2, not 0.'
      : 'The numerator and the denominator grow at the same rate (both have degree 2), so their ratio levels off.',
    ok: 'Same degree: for large |x|, f(x) ≈ 2x²/x² = 2.',
  },
  end_g: {
    sec: 'end', type: 'mc', short: 'g(x) = (x + 1)/(x² − 4) as x → ±∞',
    text: 'As x → ±∞, <span class="fx">g(x) = (x + 1) / (x² − 4)</span> …',
    options: { zero: 'approaches 0', ha: 'approaches a nonzero number', big: 'grows without bound (→ ±∞)' },
    answer: 'zero',
    hint: () => 'The denominator has the higher degree (2 against 1), so it grows much faster than the numerator.',
    ok: 'The denominator wins: for large |x|, g(x) ≈ x/x² = 1/x, which goes to 0.',
  },
  end_h: {
    sec: 'end', type: 'mc', short: 'h(x) = (x² + 1)/(x − 1) as x → ±∞',
    text: 'As x → ±∞, <span class="fx">h(x) = (x² + 1) / (x − 1)</span> …',
    options: { zero: 'approaches 0', ha: 'approaches a nonzero number', big: 'grows without bound (→ ±∞)' },
    answer: 'big',
    hint: () => 'Here the numerator has the higher degree (2 against 1). What is x²/x for large x?',
    ok: 'The numerator wins: h(x) ≈ x²/x = x, so it grows without bound, like the line y = x. Section 1.11 comes back to this.',
  },
  end_ha: {
    sec: 'end', type: 'num', short: 'Horizontal asymptote of f: y = ?',
    text: 'The horizontal asymptote of <span class="fx">f</span> is y = ?',
    answer: 2,
    hint: () => 'Divide the leading terms of the numerator and the denominator: 2x² / x².',
    ok: 'y = 2, the ratio of the leading coefficients.',
  },

  // ── 1.8 Zeros ──
  zer_list: {
    sec: 'zero', type: 'list', short: 'Zeros of r(x) = (x − 2)(x + 3)/((x + 3)(x − 5))',
    text: 'List <b>all</b> the zeros of <span class="fx">r</span>, separated by commas.',
    answer: [2],
    hint: v => v.includes(-3) ? 'At x = −3 the denominator is 0 too, so r(−3) is undefined. A zero must be in the domain.'
      : v.includes(5) ? 'x = 5 makes the denominator 0, not the numerator.'
      : 'A zero makes the numerator 0 while the denominator is not 0.',
    ok: 'Only x = 2. The factor (x + 3) cancels, so x = −3 is not a zero.',
  },
  zer_sign: {
    sec: 'zero', type: 'mc', short: 'Where is r(x) > 0?',
    text: 'For which x-values is <span class="fx">r(x) &gt; 0</span>?',
    options: { a: 'x < 2 or x > 5 (except x = −3)', b: '2 < x < 5', c: 'x > 5 only', d: '−3 < x < 2 or x > 5' },
    answer: 'a',
    hint: v => ({
      b: 'That is where r is negative. Test a value: r(3) = (1)(6) / ((6)(−2)) < 0.',
      c: 'Test x = 0: r(0) = (−2)(3) / ((3)(−5)) = 0.4 > 0.',
      d: 'Test x = −4: r(−4) = (−6)(−1) / ((−1)(−9)) = 6/9 > 0. The sign does not change at x = −3, because (x + 3) cancels.',
    }[v] || 'Make a sign chart with the zero and the vertical asymptote.'),
    ok: 'The sign only changes at the zero x = 2 and at the asymptote x = 5. x = −3 is left out: it is not in the domain.',
  },

  // ── 1.9 Vertical asymptotes ──
  va_list: {
    sec: 'va', type: 'list', short: 'Vertical asymptotes of s(x) = (x + 1)/((x − 3)(x + 2)²)',
    text: 'List the x-values of <b>all</b> the vertical asymptotes of <span class="fx">s</span>.',
    answer: [3, -2],
    hint: v => v.includes(-1) ? 'x = −1 makes the numerator 0: that is a zero, not an asymptote.'
      : 'Vertical asymptotes come from the zeros of the denominator that don\'t cancel. Look at every factor of the denominator.',
    ok: 'x = 3 and x = −2: the denominator is 0 there and nothing cancels.',
  },
  va_3l: {
    sec: 'va', type: 'mc', short: 's(x) as x → 3⁻',
    text: 'As x → 3<sup>−</sup> (from the left), <span class="fx">s(x)</span> → ?',
    options: { pinf: '+∞', minf: '−∞' },
    answer: 'minf',
    hint: () => 'Look at the table: s(2.99) is a large negative number. Just left of 3, (x − 3) is a small negative number, and (x + 1)/(x + 2)² ≈ 4/25 is positive.',
    ok: 'Negative small denominator, positive numerator: s → −∞.',
  },
  va_3r: {
    sec: 'va', type: 'mc', short: 's(x) as x → 3⁺',
    text: 'As x → 3<sup>+</sup> (from the right), <span class="fx">s(x)</span> → ?',
    options: { pinf: '+∞', minf: '−∞' },
    answer: 'pinf',
    hint: () => 'Just right of 3, (x − 3) is a small positive number. Check s(3.01) in the table.',
    ok: 's → +∞. The sign flips across x = 3 because (x − 3) appears once (odd multiplicity).',
  },
  va_m2: {
    sec: 'va', type: 'mc', short: 's(x) near x = −2',
    text: 'Near x = −2, <span class="fx">s(x)</span> …',
    options: { pp: '→ +∞ on both sides', mm: '→ −∞ on both sides', mp: '→ −∞ on the left, +∞ on the right', pm: '→ +∞ on the left, −∞ on the right' },
    answer: 'pp',
    hint: v => (v === 'mp' || v === 'pm')
      ? '(x + 2)² is positive on both sides of −2, so s has the same sign on both sides.'
      : 'Find the sign: near −2, (x + 1) ≈ −1 and (x − 3) ≈ −5, and (x + 2)² is a small positive number.',
    ok: '(x + 2)² is squared (even multiplicity), so s keeps its sign: (−1)/((−5)·small positive) → +∞ on both sides.',
  },

  // ── 1.10 Holes ──
  hole_x: {
    sec: 'hole', type: 'num', short: 'x-coordinate of the hole of q(x) = (x² − 1)/(x² + 2x − 3)',
    text: 'The hole of <span class="fx">q</span> is at x = ?',
    answer: 1,
    hint: v => v === -3 ? 'x = −3 is a zero of the denominator only: that is a vertical asymptote.'
      : v === -1 ? 'x = −1 makes only the numerator 0: that is a zero of q.'
      : 'Factor both: (x − 1)(x + 1) / ((x − 1)(x + 3)). Which factor is in both?',
    ok: 'x = 1: the factor (x − 1) is in both the numerator and the denominator.',
  },
  hole_y: {
    sec: 'hole', type: 'num', short: 'y-coordinate of the hole',
    text: 'The hole is at y = ? (a fraction like 1/2 is fine)',
    answer: 0.5,
    hint: v => v === 0 ? 'q(1) = 0/0, which is undefined, not 0. Cancel the common factor first, then substitute x = 1.'
      : 'Cancel (x − 1), then substitute x = 1 into (x + 1)/(x + 3).',
    ok: 'After canceling, (1 + 1)/(1 + 3) = 1/2. The hole is the point (1, 1/2).',
  },
  hole_va: {
    sec: 'hole', type: 'num', short: 'Vertical asymptote of q',
    text: 'The vertical asymptote of <span class="fx">q</span> is x = ?',
    answer: -3,
    hint: () => 'After canceling (x − 1), which factor is left in the denominator?',
    ok: 'x = −3: (x + 3) stays in the denominator after canceling.',
  },

  // ── 1.11 Equivalent representations ──
  ld_a: {
    sec: 'ld', type: 'num', short: 'Long division: quotient a in q(x) = a·x + b',
    text: 'The quotient is <span class="fx">q(x) = a·x + b</span>. a = ?',
    answer: 1,
    hint: () => 'Divide the leading terms: x² ÷ x.',
    ok: 'a = 1, because x² ÷ x = x.',
  },
  ld_b: {
    sec: 'ld', type: 'num', short: 'Long division: quotient b',
    text: 'b = ?',
    answer: 1,
    hint: () => 'Subtract x·(x − 1) = x² − x from x² + 1. You get x + 1; now divide x ÷ x.',
    ok: 'b = 1, so q(x) = x + 1.',
  },
  ld_r: {
    sec: 'ld', type: 'num', short: 'Long division: remainder R',
    text: 'The remainder is R = ?',
    answer: 2,
    hint: () => '(x − 1)(x + 1) = x² − 1. What is left over from x² + 1?',
    ok: 'R = 2: h(x) = x + 1 + 2/(x − 1).',
  },
  ld_mean: {
    sec: 'ld', type: 'mc', short: 'What q(x) = x + 1 says about the graph of h',
    text: 'What does <span class="fx">q(x) = x + 1</span> tell you about the graph of <span class="fx">h</span>?',
    options: {
      a: 'For large |x|, the graph of h gets closer and closer to the line y = x + 1',
      b: 'h has a horizontal asymptote y = 1',
      c: 'h crosses the line y = x + 1 at x = 1',
      d: 'h has a hole at x = 1',
    },
    answer: 'a',
    hint: v => ({
      b: 'A horizontal asymptote happens when the degrees are equal or the denominator\'s is bigger. Here the numerator\'s degree is bigger by 1.',
      c: 'x = 1 is the vertical asymptote: h is not even defined there.',
      d: 'Nothing cancels: (x − 1) is not a factor of x² + 1.',
    }[v] || ''),
    ok: 'y = x + 1 is a slant (oblique) asymptote: the remainder 2/(x − 1) goes to 0 as |x| grows.',
  },

  // ── 1.12 Transformations ──
  tr_va: {
    sec: 'tr', type: 'num', short: 'Vertical asymptote of g(x) = −2/(x − 3) + 1',
    text: 'The vertical asymptote of <span class="fx">g</span> is x = ?',
    answer: 3,
    hint: v => v === -3 ? '(x − 3) shifts the graph 3 to the right, so the asymptote x = 0 of 1/x moves to the right.' : 'Where is the denominator x − 3 equal to 0?',
    ok: 'x = 3: the asymptote x = 0 of 1/x moved 3 to the right.',
  },
  tr_ha: {
    sec: 'tr', type: 'num', short: 'Horizontal asymptote of g',
    text: 'The horizontal asymptote of <span class="fx">g</span> is y = ?',
    answer: 1,
    hint: () => '−2/(x − 3) goes to 0 as |x| grows. What is left?',
    ok: 'y = 1: the asymptote y = 0 of 1/x moved 1 up.',
  },
  tr_list: {
    sec: 'tr', type: 'mc', short: 'The transformations from 1/x to g',
    text: 'Which list describes how to get <span class="fx">g</span> from <span class="fx">f(x) = 1/x</span>?',
    options: {
      a: 'Vertical stretch by 2, reflection over the x-axis, shift 3 right, shift 1 up',
      b: 'Vertical stretch by 2, shift 3 left, shift 1 up',
      c: 'Vertical stretch by 2, reflection over the y-axis, shift 3 right, shift 1 down',
      d: 'Vertical compression by 1/2, reflection over the x-axis, shift 3 right, shift 1 up',
    },
    answer: 'a',
    hint: v => ({
      b: '(x − 3) moves the graph right, not left. And what does the minus sign in −2 do?',
      c: 'The −2 multiplies the output (y), so it reflects over the x-axis. The +1 outside moves the graph up.',
      d: 'Multiplying the output by 2 makes the graph taller: that is a stretch, not a compression.',
    }[v] || ''),
    ok: 'g(x) = −2·f(x − 3) + 1: stretch by 2, reflect over the x-axis, then 3 right and 1 up.',
  },
  tr_val: {
    sec: 'tr', type: 'num', short: 'g(4) = ?',
    text: '<span class="fx">g(4)</span> = ?',
    answer: -1,
    hint: () => 'Substitute: −2/(4 − 3) + 1 = −2/1 + 1.',
    ok: 'g(4) = −2 + 1 = −1.',
  },
};

// Section 7 (build your own): the target features
const RQ_BUILD = { zeros: [-4, 1], vas: [2], holes: [-1], ha: 3 };

// Numbers in a typed answer: "3, -2", "x = 3 and x = −2", "1/2"
function rqNumbers(text) {
  const s = String(text || '').replace(/[−–]/g, '-');
  return (s.match(/-?\d+(?:[.,]\d+)?(?:\/\d+(?:[.,]\d+)?)?/g) || []).map(t => {
    const [a, b] = t.replace(/,/g, '.').split('/');
    return b === undefined ? parseFloat(a) : parseFloat(a) / parseFloat(b);
  }).filter(isFinite);
}
const rqSame = (a, b) => Math.abs(a - b) < 1e-6 * (1 + Math.abs(b));
// Is this answer right? For 'list' the values must match as sets.
function rqCorrect(q, value) {
  if (q.type === 'mc') return value === q.answer;
  const xs = rqNumbers(value);
  if (q.type === 'num') return xs.length === 1 && rqSame(xs[0], q.answer);
  const uniq = xs.filter((x, i) => xs.findIndex(y => rqSame(x, y)) === i);
  return uniq.length === q.answer.length && q.answer.every(a => uniq.some(x => rqSame(x, a)));
}
// The value a hint function receives: the option key, one number, or the list of numbers
function rqValue(q, value) {
  if (q.type === 'mc') return value;
  const xs = rqNumbers(value);
  return q.type === 'num' ? xs[0] : xs;
}
// How the right answer is written (answerText overrides, e.g. '−2/3')
function rqAnswerText(q) {
  if (q.answerText) return q.answerText;
  if (q.type === 'mc') return q.options[q.answer];
  if (q.type === 'list') return q.answer.join(', ');
  return q.answer === 0.5 ? '1/2' : String(q.answer);
}
