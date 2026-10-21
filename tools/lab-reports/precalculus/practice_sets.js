// Unit 1 practice: 10 questions per AP Precalculus topic (1.7–1.11), one activity each.
// Shared by practice_lab.html (renders and checks them) and practice_teacher.html (answer key and charts);
// both pick the set from the link: ?set=1.7
//
// A set: { id (report id), code, title, icon, why: { prompt, min }, questions: [10] }
// A question uses the same format as rational_questions.js ({ type: 'mc' | 'num' | 'list', short, text,
// options?, answer, answerText?, hint(value), ok }), plus an optional graph: { f, win, marks }
// (marks: false hides the dashed asymptote lines, for questions that ask students to read them).
const PRACTICE_SETS = {
  '1.7': {
    id: 'pc-1-7', code: '1.7', title: 'End Behavior', icon: '↔️',
    why: { prompt: 'Explain how to find the end behavior of a rational function without graphing. Use one of the functions from the questions as an example.', min: 15 },
    questions: [
      {
        type: 'num', short: 'lim x→∞ (3x² + 1)/(x² − 4)',
        text: 'For <span class="fx">r(x) = (3x² + 1) / (x² − 4)</span>, what is lim<sub>x→∞</sub> r(x)?',
        answer: 3,
        hint: v => v === 0 ? 'Both degrees are 2, so the leading terms 3x²/x² don\'t go to 0.' : 'The degrees are equal: divide the leading coefficients.',
        ok: 'Equal degrees: r(x) ≈ 3x²/x² = 3.',
      },
      {
        type: 'num', short: 'lim x→∞ (5x − 2)/(x³ + 1)',
        text: 'For <span class="fx">r(x) = (5x − 2) / (x³ + 1)</span>, what is lim<sub>x→∞</sub> r(x)?',
        answer: 0,
        hint: v => v === 5 ? '5 is the leading coefficient of the numerator, but the denominator has a higher degree.' : 'Compare the degrees: 1 on top, 3 on the bottom.',
        ok: 'The denominator has the higher degree: r(x) ≈ 5x/x³ = 5/x² → 0.',
      },
      {
        type: 'mc', short: '(x³ − 2x)/(4x² + 7) as x → ∞',
        text: 'As x → ∞, <span class="fx">r(x) = (x³ − 2x) / (4x² + 7)</span> …',
        options: { zero: '→ 0', quarter: '→ 1/4', pinf: '→ +∞', minf: '→ −∞' },
        answer: 'pinf',
        hint: v => v === 'quarter' ? '1/4 would be the asymptote if the degrees were equal. Here the numerator has degree 3 and the denominator degree 2.'
          : v === 'zero' ? 'The numerator has the higher degree, so the ratio does not shrink.'
          : 'For large x, r(x) ≈ x³/(4x²) = x/4. What does x/4 do when x is a large positive number?',
        ok: 'r(x) ≈ x/4, which grows without bound as x → ∞.',
      },
      {
        type: 'mc', short: '(x³ − 2x)/(4x² + 7) as x → −∞',
        text: 'And as x → −∞, the same <span class="fx">r(x) = (x³ − 2x) / (4x² + 7)</span> …',
        options: { zero: '→ 0', quarter: '→ 1/4', pinf: '→ +∞', minf: '→ −∞' },
        answer: 'minf',
        hint: v => v === 'pinf' ? 'r(x) ≈ x/4. When x is a large negative number, x/4 is negative too.' : 'r(x) ≈ x/4 for large |x|. What does x/4 do when x is a large negative number?',
        ok: 'r(x) ≈ x/4 → −∞: the odd power x³ keeps the sign of x.',
      },
      {
        type: 'num', short: 'Horizontal asymptote of (6 − 2x²)/(4x² + x)', answerText: '−1/2',
        text: 'The horizontal asymptote of <span class="fx">r(x) = (6 − 2x²) / (4x² + x)</span> is y = ? (a fraction is fine)',
        answer: -0.5,
        hint: v => v === 1.5 ? '6/4 uses the constant term. Use the leading terms, the ones with the highest power: −2x² and 4x².'
          : v === 0.5 ? 'Careful with the sign: the leading term of the numerator is −2x².'
          : 'Equal degrees: divide the leading coefficients, −2 and 4.',
        ok: 'y = −2/4 = −1/2. The leading term of the numerator is −2x², even though it is written last.',
      },
      {
        type: 'mc', short: 'Which has horizontal asymptote y = 0?',
        text: 'Which function has the horizontal asymptote y = 0?',
        options: { a: '(2x + 1) / (x² + 3)', b: '(2x² + 1) / (x² + 3)', c: '(2x³ + 1) / (x² + 3)', d: '(x² + 3) / (2x + 1)' },
        answer: 'a',
        hint: v => v === 'b' ? 'Equal degrees give y = 2/1 = 2, not 0.' : 'y = 0 happens when the denominator has the higher degree.',
        ok: 'Degree 1 over degree 2: the denominator wins, so r(x) → 0.',
      },
      {
        type: 'mc', short: 'Which has a slant asymptote?',
        text: 'Which function has a <b>slant</b> (oblique) asymptote?',
        options: { a: '(x² − 1) / (x + 2)', b: '(x² − 1) / (x² + 2)', c: '(x − 1) / (x² + 2)', d: '(x³ − 1) / (x + 2)' },
        answer: 'a',
        hint: v => v === 'd' ? 'The degrees differ by 2 here: the quotient is a quadratic, so the end behavior is a parabola, not a line.'
          : 'A slant asymptote needs the numerator\'s degree to be exactly one more than the denominator\'s.',
        ok: 'Degree 2 over degree 1: the quotient of the long division is a line.',
      },
      {
        type: 'mc', short: 'Slant asymptote of (2x² + 3x + 1)/(x − 1)',
        text: 'The slant asymptote of <span class="fx">r(x) = (2x² + 3x + 1) / (x − 1)</span> is …',
        options: { a: 'y = 2x + 5', b: 'y = 2x + 3', c: 'y = 2x', d: 'y = 2x − 5' },
        answer: 'a',
        hint: v => v === 'b' ? 'That copies the numerator. Do the long division: 2x·(x − 1) = 2x² − 2x, so what is left is 5x + 1.'
          : 'Do the long division by (x − 1): first 2x, then subtract 2x² − 2x and divide again.',
        ok: '2x² + 3x + 1 = (x − 1)(2x + 5) + 6, so the asymptote is the quotient y = 2x + 5.',
      },
      {
        type: 'num', short: 'Read lim x→−∞ from a graph',
        text: 'This is the graph of a rational function r. According to the graph, lim<sub>x→−∞</sub> r(x) = ?',
        graph: { f: '(2x^2 - 8)/(x^2 + 1)', win: { x0: -20, x1: 20, y0: -9, y1: 5 }, marks: false },
        answer: 2,
        hint: () => 'Zoom out and follow the left side of the graph: which y-value does it get closer and closer to?',
        ok: 'The graph levels off at y = 2 on both sides: r(x) = (2x² − 8)/(x² + 1).',
      },
      {
        type: 'mc', short: 'Degree needed for a horizontal asymptote',
        text: 'The numerator of r has degree 4. For r to have a horizontal asymptote, the degree of the denominator must be …',
        options: { a: 'at least 4', b: 'exactly 4', c: 'less than 4', d: 'exactly 5' },
        answer: 'a',
        hint: v => v === 'b' ? 'Degree 4 works (y = ratio of leading coefficients), but a higher degree gives y = 0, which is also a horizontal asymptote.'
          : v === 'c' ? 'If the denominator has a lower degree, the numerator wins and r grows without bound.'
          : 'Degree 5 works (y = 0), but it is not the only option.',
        ok: 'Degree 4 gives y = ratio of leading coefficients; degree 5 or more gives y = 0.',
      },
    ],
  },

  '1.8': {
    id: 'pc-1-8', code: '1.8', title: 'Zeros', icon: '🎯',
    why: { prompt: 'Explain the difference between a zero of the numerator and a zero of the rational function. Use an example from the questions.', min: 15 },
    questions: [
      {
        type: 'list', short: 'Zeros of (x − 4)(x + 1)/(x + 6)',
        text: 'List all the zeros of <span class="fx">r(x) = (x − 4)(x + 1) / (x + 6)</span>.',
        answer: [4, -1],
        hint: v => v.includes(-6) ? 'x = −6 makes the denominator 0: that is a vertical asymptote, not a zero.'
          : v.includes(-4) || v.includes(1) ? 'Check the signs: (x − 4) is 0 when x = 4, not −4.'
          : 'A zero makes the numerator 0 while the denominator is not 0.',
        ok: 'x = 4 and x = −1: the numerator is 0 there and the denominator is not.',
      },
      {
        type: 'list', short: 'Zeros of (x² − 9)/(x − 3)',
        text: 'List all the zeros of <span class="fx">r(x) = (x² − 9) / (x − 3)</span>.',
        answer: [-3],
        hint: v => v.includes(3) ? 'At x = 3 the denominator is 0 too, so r(3) is undefined. Factor x² − 9 and cancel.'
          : 'Factor the numerator: x² − 9 = (x − 3)(x + 3).',
        ok: 'Only x = −3. (x − 3) cancels, so x = 3 is a hole, not a zero.',
      },
      {
        type: 'num', short: 'Zero of (2x − 5)/(x² + 1)', answerText: '5/2',
        text: 'The zero of <span class="fx">r(x) = (2x − 5) / (x² + 1)</span> is x = ?',
        answer: 2.5,
        hint: v => v === 5 ? 'Solve 2x − 5 = 0: divide by 2 at the end.' : 'Set the numerator equal to 0: 2x − 5 = 0.',
        ok: 'x = 5/2. The denominator x² + 1 is never 0, so nothing cancels.',
      },
      {
        type: 'mc', short: 'Which has no real zeros?',
        text: 'Which function has <b>no</b> real zeros?',
        options: { a: '(x² + 4) / (x − 1)', b: '(x² − 4) / (x − 1)', c: 'x / (x² + 4)', d: '(x − 1) / (x² + 4)' },
        answer: 'a',
        hint: v => v === 'b' ? 'x² − 4 = 0 at x = 2 and x = −2.' : 'Look at the numerators: which one can never be 0?',
        ok: 'x² + 4 is always at least 4, so the numerator is never 0.',
      },
      {
        type: 'mc', short: 'Where is (x − 1)/(x + 2) < 0?',
        text: 'For which x is <span class="fx">r(x) = (x − 1) / (x + 2) &lt; 0</span>?',
        options: { a: '−2 < x < 1', b: 'x < −2 or x > 1', c: 'x < 1', d: 'x > −2' },
        answer: 'a',
        hint: v => v === 'b' ? 'That is where r is positive. Test x = 0: r(0) = −1/2.'
          : v === 'c' ? 'Test x = −3: r(−3) = (−4)/(−1) = 4 > 0.'
          : 'Test x = 2: r(2) = 1/4 > 0.',
        ok: 'The sign changes only at x = −2 and x = 1; between them, the factors have opposite signs.',
      },
      {
        type: 'mc', short: 'Where is (x + 3)/(x − 2)² > 0?',
        text: 'For which x is <span class="fx">r(x) = (x + 3) / (x − 2)² &gt; 0</span>?',
        options: { a: 'x > −3, except x = 2', b: '−3 < x < 2', c: 'x > 2', d: 'x < −3' },
        answer: 'a',
        hint: v => v === 'b' || v === 'c' ? '(x − 2)² is positive on both sides of 2, so the sign does not change at x = 2.'
          : 'Test x = 0: r(0) = 3/4 > 0.',
        ok: '(x − 2)² is always positive (except at 2), so r has the sign of x + 3.',
      },
      {
        type: 'list', short: 'Read the zeros from a graph',
        text: 'This is the graph of a rational function r. Using the graph, list its zeros.',
        graph: { f: '(x^2 - x - 6)/(x^2 + 1)', win: { x0: -6, x1: 6, y0: -7, y1: 3 } },
        answer: [3, -2],
        hint: () => 'The zeros are the x-values where the graph crosses or touches the x-axis. Move the mouse over the graph to read values.',
        ok: 'x = −2 and x = 3: r(x) = (x − 3)(x + 2)/(x² + 1).',
      },
      {
        type: 'mc', short: 'Definition of the zeros of p(x)/q(x)',
        text: 'The real zeros of a rational function <span class="fx">r(x) = p(x)/q(x)</span> are …',
        options: { a: 'the x-values where p(x) = 0 and q(x) ≠ 0', b: 'the x-values where q(x) = 0', c: 'the x-values where p(x) = 0, even if q(x) = 0', d: 'the y-value where the graph crosses the y-axis' },
        answer: 'a',
        hint: v => v === 'b' ? 'Where q(x) = 0, r is undefined: those are asymptotes or holes.'
          : v === 'c' ? 'If q(x) = 0 too, r is undefined there: it can\'t be a zero.'
          : 'That is the y-intercept, not a zero.',
        ok: 'A zero must be in the domain: the numerator is 0 and the denominator is not.',
      },
      {
        type: 'mc', short: 'Crosses or touches at a double zero?',
        text: 'For <span class="fx">r(x) = (x − 1)²(x + 2) / (x² + 5)</span>, at x = 1 the graph …',
        options: { touch: 'touches the x-axis and turns back', cross: 'crosses the x-axis', va: 'has a vertical asymptote', hole: 'has a hole' },
        answer: 'touch',
        hint: v => v === 'cross' ? '(x − 1)² is positive on both sides of 1, so r keeps its sign there.' : 'x = 1 makes the numerator 0, and x² + 5 is never 0.',
        ok: 'Even multiplicity (squared): the sign does not change, so the graph touches and turns.',
      },
      {
        type: 'num', short: 'Number of zeros of (x² + 2x − 15)/(x + 5)',
        text: 'How many real zeros does <span class="fx">r(x) = (x² + 2x − 15) / (x + 5)</span> have?',
        answer: 1,
        hint: v => v === 2 ? 'Factor: x² + 2x − 15 = (x + 5)(x − 3). The factor (x + 5) cancels.' : 'Factor the numerator first.',
        ok: 'Only x = 3. At x = −5 the factor cancels: that is a hole.',
      },
    ],
  },

  '1.9': {
    id: 'pc-1-9', code: '1.9', title: 'Vertical Asymptotes', icon: '📏',
    why: { prompt: 'Explain how you decide whether r(x) goes to +∞ or −∞ on each side of a vertical asymptote. Use an example from the questions.', min: 15 },
    questions: [
      {
        type: 'list', short: 'Vertical asymptotes of (x + 2)/((x − 1)(x + 4))',
        text: 'List the vertical asymptotes of <span class="fx">r(x) = (x + 2) / ((x − 1)(x + 4))</span> (the x-values).',
        answer: [1, -4],
        hint: v => v.includes(-2) ? 'x = −2 makes the numerator 0: that is a zero.' : v.includes(-1) || v.includes(4) ? 'Check the signs: (x − 1) = 0 at x = 1.' : 'Find where each factor of the denominator is 0.',
        ok: 'x = 1 and x = −4: the denominator is 0 and nothing cancels.',
      },
      {
        type: 'list', short: 'Vertical asymptotes of (x − 5)/(x² − 25)',
        text: 'List the vertical asymptotes of <span class="fx">r(x) = (x − 5) / (x² − 25)</span>.',
        answer: [-5],
        hint: v => v.includes(5) ? 'Factor x² − 25 = (x − 5)(x + 5). The factor (x − 5) cancels: at x = 5 there is a hole.' : 'Factor the denominator first.',
        ok: 'Only x = −5. x = 5 is a hole.',
      },
      {
        type: 'mc', short: 'Vertical asymptotes of (x + 1)/(x² + 9)?',
        text: 'Does <span class="fx">r(x) = (x + 1) / (x² + 9)</span> have vertical asymptotes?',
        options: { a: 'No: x² + 9 is never 0', b: 'Yes, at x = 3 and x = −3', c: 'Yes, at x = −1', d: 'Yes, at x = 9' },
        answer: 'a',
        hint: v => v === 'b' ? 'x² + 9 is not x² − 9: with a plus sign it is never 0.' : v === 'c' ? 'x = −1 makes the numerator 0: that is a zero.' : 'Can x² + 9 be 0 for a real x?',
        ok: 'x² + 9 ≥ 9 for every real x, so the denominator is never 0.',
      },
      {
        type: 'mc', short: '1/(x − 2) as x → 2⁺',
        text: 'For <span class="fx">r(x) = 1 / (x − 2)</span>, as x → 2<sup>+</sup>, r(x) → ?',
        options: { pinf: '+∞', minf: '−∞' },
        answer: 'pinf',
        hint: () => 'Try x = 2.01: x − 2 = 0.01, a small positive number.',
        ok: '1 divided by a small positive number is a large positive number.',
      },
      {
        type: 'mc', short: '(x + 3)/(x − 2) as x → 2⁻',
        text: 'For <span class="fx">r(x) = (x + 3) / (x − 2)</span>, as x → 2<sup>−</sup>, r(x) → ?',
        options: { pinf: '+∞', minf: '−∞' },
        answer: 'minf',
        hint: () => 'Just left of 2, the numerator is about 5 (positive) and x − 2 is a small negative number.',
        ok: 'Positive ÷ small negative → −∞.',
      },
      {
        type: 'mc', short: '−4/(x + 1)² near x = −1',
        text: 'For <span class="fx">r(x) = −4 / (x + 1)²</span>, near x = −1, r(x) …',
        options: { mm: '→ −∞ on both sides', pp: '→ +∞ on both sides', mp: '→ −∞ on the left, +∞ on the right', pm: '→ +∞ on the left, −∞ on the right' },
        answer: 'mm',
        hint: v => v === 'pp' ? 'The square is positive, but the numerator is −4.' : '(x + 1)² is positive on both sides of −1, so r has the same sign on both sides.',
        ok: '−4 ÷ (small positive) → −∞ on both sides.',
      },
      {
        type: 'mc', short: 'x/((x − 1)(x + 3)) as x → −3⁺',
        text: 'For <span class="fx">r(x) = x / ((x − 1)(x + 3))</span>, as x → −3<sup>+</sup>, r(x) → ?',
        options: { pinf: '+∞', minf: '−∞' },
        answer: 'pinf',
        hint: () => 'Find each sign just right of −3: x ≈ −3 (negative), x − 1 ≈ −4 (negative), x + 3 is small positive.',
        ok: '(−3) ÷ ((−4)·small positive) = negative ÷ negative → +∞.',
      },
      {
        type: 'list', short: 'Read the vertical asymptotes from a graph',
        text: 'This is the graph of a rational function r. Using the graph, list its vertical asymptotes.',
        graph: { f: '2x/(x^2 - 4)', win: { x0: -6, x1: 6, y0: -5, y1: 5 }, marks: false },
        answer: [2, -2],
        hint: () => 'Look for the x-values where the graph shoots up or down without end.',
        ok: 'x = −2 and x = 2: r(x) = 2x/(x² − 4).',
      },
      {
        type: 'mc', short: 'lim x→3⁻ (x − 1)/(x − 3)',
        text: 'For <span class="fx">r(x) = (x − 1) / (x − 3)</span>, lim<sub>x→3<sup>−</sup></sub> r(x) = ?',
        options: { pinf: '+∞', minf: '−∞', zero: '0', two: '2' },
        answer: 'minf',
        hint: v => v === 'two' ? '2 is the value of the numerator at x = 3, but the denominator goes to 0.'
          : v === 'zero' ? 'The denominator goes to 0, not the numerator: the quotient gets huge.'
          : 'Just left of 3, x − 1 ≈ 2 is positive and x − 3 is a small negative number.',
        ok: '2 ÷ (small negative) → −∞.',
      },
      {
        type: 'mc', short: 'Which goes to +∞ on both sides of x = 4?',
        text: 'A rational function has a vertical asymptote at x = 4, and r(x) → +∞ on <b>both</b> sides. Which could it be?',
        options: { a: '1 / (x − 4)²', b: '1 / (x − 4)', c: '−1 / (x − 4)²', d: '(x − 4) / (x + 4)' },
        answer: 'a',
        hint: v => v === 'b' ? '1/(x − 4) changes sign across x = 4: −∞ on the left, +∞ on the right.'
          : v === 'c' ? 'The minus sign sends it to −∞ on both sides.'
          : 'This one has its asymptote at x = −4, and x = 4 is a zero.',
        ok: 'An even power in the denominator keeps the sign, and the numerator is positive.',
      },
    ],
  },

  '1.10': {
    id: 'pc-1-10', code: '1.10', title: 'Holes', icon: '⭕',
    why: { prompt: 'Explain how to tell a hole from a vertical asymptote, and how to find the coordinates of a hole. Use an example from the questions.', min: 15 },
    questions: [
      {
        type: 'num', short: 'x of the hole of (x² − 4)/(x − 2)',
        text: '<span class="fx">r(x) = (x² − 4) / (x − 2)</span> has a hole at x = ?',
        answer: 2,
        hint: v => v === -2 ? 'x = −2 makes only the numerator 0: that is a zero.' : 'Factor x² − 4 and look for the factor that is also in the denominator.',
        ok: 'x = 2: (x − 2) is in both the numerator and the denominator.',
      },
      {
        type: 'num', short: 'y of the hole of (x² − 4)/(x − 2)',
        text: 'The same hole is at y = ?',
        answer: 4,
        hint: v => v === 0 ? 'r(2) = 0/0 is undefined, not 0. Cancel (x − 2) first, then substitute x = 2.' : 'After canceling, r(x) = x + 2. Substitute x = 2.',
        ok: 'r(x) = x + 2 for x ≠ 2, so the hole is at (2, 4).',
      },
      {
        type: 'mc', short: 'Which has a hole at x = 3?',
        text: 'Which function has a <b>hole</b> at x = 3?',
        options: { a: '(x − 3)(x + 1) / ((x − 3)(x − 2))', b: '(x + 3) / (x − 3)', c: '(x − 3) / (x + 3)', d: '(x² + 9) / (x − 3)' },
        answer: 'a',
        hint: v => v === 'b' || v === 'd' ? 'Here (x − 3) is only in the denominator: x = 3 is a vertical asymptote.' : 'Here (x − 3) is only in the numerator: x = 3 is a zero.',
        ok: 'The factor (x − 3) is in both the numerator and the denominator, so it cancels.',
      },
      {
        type: 'num', short: 'x of the hole of (x + 1)/(x² + 3x + 2)',
        text: '<span class="fx">r(x) = (x + 1) / (x² + 3x + 2)</span> has a hole at x = ?',
        answer: -1,
        hint: v => v === -2 ? 'x = −2 is a zero of the denominator only: that is a vertical asymptote.' : 'Factor the denominator: x² + 3x + 2 = (x + 1)(x + 2).',
        ok: 'x = −1: (x + 1) cancels.',
      },
      {
        type: 'num', short: 'y of the hole of (x + 1)/(x² + 3x + 2)',
        text: 'The same hole is at y = ?',
        answer: 1,
        hint: v => v === 0 ? 'r(−1) is 0/0, which is undefined. Cancel first: r(x) = 1/(x + 2).' : 'After canceling, r(x) = 1/(x + 2). Substitute x = −1.',
        ok: '1/(−1 + 2) = 1: the hole is at (−1, 1).',
      },
      {
        type: 'mc', short: '(x − 1)²/(x − 1) at x = 1',
        text: 'At x = 1, <span class="fx">r(x) = (x − 1)² / (x − 1)</span> has …',
        options: { a: 'a hole at (1, 0)', b: 'a vertical asymptote', c: 'a zero', d: 'nothing special' },
        answer: 'a',
        hint: v => v === 'c' ? 'r(1) is 0/0, undefined, so x = 1 is not in the domain: it can\'t be a zero.'
          : v === 'b' ? 'After canceling, r(x) = x − 1, which does not blow up at x = 1.'
          : 'x = 1 makes the denominator 0, so r(1) is undefined.',
        ok: 'r(x) = x − 1 for x ≠ 1, so the graph is a line with a hole at (1, 0).',
      },
      {
        type: 'mc', short: '(x − 1)/(x − 1)² at x = 1',
        text: 'At x = 1, <span class="fx">r(x) = (x − 1) / (x − 1)²</span> has …',
        options: { a: 'a vertical asymptote', b: 'a hole', c: 'a zero', d: 'a hole and a zero' },
        answer: 'a',
        hint: () => 'Cancel one factor: r(x) = 1/(x − 1). Is (x − 1) still in the denominator?',
        ok: 'The denominator has (x − 1) twice and the numerator only once, so one is left over: r(x) = 1/(x − 1).',
      },
      {
        type: 'num', short: 'Read the y of a hole from a graph',
        text: 'This is the graph of <span class="fx">r(x) = (x² + x − 6) / (x − 2)</span>. What is the y-coordinate of its hole?',
        graph: { f: '(x^2 + x - 6)/(x - 2)', win: { x0: -6, x1: 6, y0: -3, y1: 9 }, marks: false },
        answer: 5,
        hint: () => 'Find the open circle on the graph. Or cancel: (x² + x − 6) = (x − 2)(x + 3).',
        ok: 'r(x) = x + 3 for x ≠ 2, so the hole is at (2, 5).',
      },
      {
        type: 'mc', short: 'Why a hole and not an asymptote?',
        text: 'Why does <span class="fx">q(x) = (x² − 4) / (x − 2)</span> have a hole, and not a vertical asymptote, at x = 2?',
        options: {
          a: 'The factor (x − 2) cancels, so q(x) approaches a finite value near x = 2',
          b: 'Because the numerator has a higher degree than the denominator',
          c: 'Because q(2) = 0',
          d: 'Because the graph is a line, and lines have no asymptotes',
        },
        answer: 'a',
        hint: v => v === 'c' ? 'q(2) is 0/0, which is undefined, not 0.' : v === 'b' ? 'The degrees tell you the end behavior, not what happens at x = 2.' : 'Think about what q(x) does as x gets close to 2.',
        ok: 'After canceling, q(x) = x + 2 near x = 2, so q(x) → 4: finite, so there is no asymptote.',
      },
      {
        type: 'num', short: 'y of the hole of (x² − 9)/(x² − 2x − 3)', answerText: '3/2',
        text: 'Find the y-coordinate of the hole of <span class="fx">r(x) = (x² − 9) / (x² − 2x − 3)</span>. (A fraction is fine.)',
        answer: 1.5,
        hint: v => v === 0 ? 'r(3) is 0/0, undefined. Cancel first.' : 'Factor: (x − 3)(x + 3) / ((x − 3)(x + 1)). Cancel, then substitute the x of the hole.',
        ok: 'The hole is at x = 3, and (3 + 3)/(3 + 1) = 6/4 = 3/2.',
      },
    ],
  },

  '1.11': {
    id: 'pc-1-11', code: '1.11', title: 'Equivalent Representations', icon: '🔁',
    why: { prompt: 'Explain when you would use polynomial long division on a rational function, and what the result tells you about its graph.', min: 15 },
    questions: [
      {
        type: 'mc', short: 'Factor x² − x − 12',
        text: 'The factored form of <span class="fx">x² − x − 12</span> is …',
        options: { a: '(x − 4)(x + 3)', b: '(x + 4)(x − 3)', c: '(x − 6)(x + 2)', d: '(x − 12)(x + 1)' },
        answer: 'a',
        hint: v => v === 'b' ? 'Expand it: (x + 4)(x − 3) = x² + x − 12. The middle sign is wrong.' : 'You need two numbers that multiply to −12 and add to −1.',
        ok: '−4 · 3 = −12 and −4 + 3 = −1.',
      },
      {
        type: 'num', short: 'Remainder of (x² + 5x + 7) ÷ (x + 2)',
        text: 'Divide: <span class="fx">(x² + 5x + 7) / (x + 2) = q(x) + R / (x + 2)</span>. The remainder is R = ?',
        answer: 1,
        hint: () => '(x + 2)(x + 3) = x² + 5x + 6. What is left over from x² + 5x + 7?',
        ok: 'R = 1: x² + 5x + 7 = (x + 2)(x + 3) + 1.',
      },
      {
        type: 'mc', short: 'Quotient of (x² + 5x + 7) ÷ (x + 2)',
        text: 'And the quotient is q(x) = …',
        options: { a: 'x + 3', b: 'x + 5', c: 'x + 7', d: 'x − 3' },
        answer: 'a',
        hint: v => v === 'b' ? 'After x·(x + 2) = x² + 2x, what is left is 3x + 7, not 5x + 7.' : 'First term: x² ÷ x = x. Subtract x(x + 2) and divide again.',
        ok: 'q(x) = x + 3, so (x² + 5x + 7)/(x + 2) = x + 3 + 1/(x + 2).',
      },
      {
        type: 'num', short: 'Remainder of (2x² − 3x + 4) ÷ (x − 1)',
        text: 'Divide <span class="fx">(2x² − 3x + 4)</span> by <span class="fx">(x − 1)</span>. The remainder is R = ?',
        answer: 3,
        hint: () => 'First 2x: subtract 2x(x − 1) = 2x² − 2x to get −x + 4. Then −1: subtract −(x − 1) = −x + 1.',
        ok: 'R = 3: 2x² − 3x + 4 = (x − 1)(2x − 1) + 3.',
      },
      {
        type: 'mc', short: 'Slant asymptote of (2x² − 3x + 4)/(x − 1)',
        text: 'So the slant asymptote of <span class="fx">r(x) = (2x² − 3x + 4) / (x − 1)</span> is …',
        options: { a: 'y = 2x − 1', b: 'y = 2x + 3', c: 'y = 2x', d: 'y = 2x − 3' },
        answer: 'a',
        hint: v => v === 'b' ? '3 is the remainder; the asymptote is the quotient.' : 'The slant asymptote is the quotient of the division you just did.',
        ok: 'r(x) = 2x − 1 + 3/(x − 1), and 3/(x − 1) → 0, so r gets close to y = 2x − 1.',
      },
      {
        type: 'mc', short: '(x³ − 8)/(x − 2) = ?',
        text: 'For x ≠ 2, <span class="fx">(x³ − 8) / (x − 2)</span> equals …',
        options: { a: 'x² + 2x + 4', b: 'x² − 2x + 4', c: 'x² + 4', d: 'x² + 2x − 4' },
        answer: 'a',
        hint: v => v === 'b' ? 'Multiply back: (x − 2)(x² − 2x + 4) = x³ − 4x² + 8x − 8, not x³ − 8.' : 'Do the long division, or multiply each option by (x − 2) and check which gives x³ − 8.',
        ok: 'x³ − 8 = (x − 2)(x² + 2x + 4): a difference of cubes.',
      },
      {
        type: 'num', short: 'Coefficient of x² in (x + 2)⁴',
        text: 'In the expansion of <span class="fx">(x + 2)⁴</span>, the coefficient of x² is ?',
        answer: 24,
        hint: v => v === 6 ? '6 comes from Pascal\'s triangle, but each x² term also has 2² = 4 in it.' : v === 4 ? 'Use row 1, 4, 6, 4, 1 of Pascal\'s triangle: the x² term is 6·x²·2².' : 'Binomial theorem: the x² term is C(4, 2)·x²·2².',
        ok: 'C(4, 2)·2² = 6·4 = 24.',
      },
      {
        type: 'mc', short: 'Pascal row for (a + b)⁵',
        text: 'The coefficients of <span class="fx">(a + b)⁵</span> are …',
        options: { a: '1, 5, 10, 10, 5, 1', b: '1, 4, 6, 4, 1', c: '1, 5, 10, 5, 1', d: '1, 6, 15, 20, 15, 6, 1' },
        answer: 'a',
        hint: v => v === 'b' ? 'That is the row for power 4.' : v === 'd' ? 'That is the row for power 6.' : 'Power 5 has 6 terms. Add neighbors in the row 1, 4, 6, 4, 1.',
        ok: 'Each number is the sum of the two above it in Pascal\'s triangle: 1, 5, 10, 10, 5, 1.',
      },
      {
        type: 'mc', short: 'Equivalent to (x² − 1)/(x + 1)',
        text: 'For x ≠ −1, <span class="fx">(x² − 1) / (x + 1)</span> is equivalent to …',
        options: { a: 'x − 1', b: 'x + 1', c: '1 / (x + 1)', d: 'x² − 1' },
        answer: 'a',
        hint: v => v === 'b' ? 'Factor x² − 1 = (x − 1)(x + 1) and cancel (x + 1): what is left?' : 'Factor the numerator: x² − 1 = (x − 1)(x + 1).',
        ok: '(x − 1)(x + 1)/(x + 1) = x − 1, for x ≠ −1.',
      },
      {
        type: 'mc', short: 'Which form shows the zeros directly?',
        text: 'These are three forms of the same function. Which one shows its <b>zeros</b> most directly?',
        options: {
          a: 'r(x) = (x − 2)(x + 5) / ((x − 1)(x + 3))',
          b: 'r(x) = (x² + 3x − 10) / (x² + 2x − 3)',
          c: 'r(x) = 1 + (x − 7) / (x² + 2x − 3)',
        },
        answer: 'a',
        hint: v => v === 'c' ? 'This form (from long division) shows the horizontal asymptote y = 1 best, not the zeros.' : 'In standard form you would still need to factor to see the zeros.',
        ok: 'The factored form shows the zeros (2 and −5) and the asymptotes (1 and −3) at a glance.',
      },
    ],
  },
};
// Question ids: q1 … q10
Object.values(PRACTICE_SETS).forEach(set => set.questions.forEach((q, i) => { q.id = 'q' + (i + 1); }));
