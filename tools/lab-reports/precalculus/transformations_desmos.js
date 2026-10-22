// Transformations with Desmos (AP Precalculus 1.12): students find the transformation rules themselves
// with a slider a in Desmos and f(x) = 0.1x⁴ − 0.75x³ + x² + 2x − 1.
// Shared by transformations_desmos_lab.html (renders and checks the questions) and
// transformations_desmos_teacher.html (answer key, live progress and charts).
//
// A step: { n, title, lead (html), box? (html: the Desmos instructions) }     steps 2…8; 1 is "who" and the last is "Send"
// An item: { id, step, type, short (plain text for the teacher), text (html), … }
//   predict: options, truth      a guess before using the slider: saved once, never marked right or wrong
//   mc:      options, answer, hint(key), ok
//   num:     answer, tol, hint(x), ok
//   pt:      answer [x, y], tol, hint([x, y]), ok          a point, typed as x and y
//   text:    min (words), optional?
// The functions all use the same f, so every answer can be checked in Desmos.
const TD_MIN = [-0.581, -1.666];   // local minimum of f, as Desmos shows it

const TD = {
  id: 'pc-transformations-desmos',
  title: 'Transformations with Desmos',
  code: '1.12',
  steps: [
    {
      n: 2, title: 'Get Desmos ready',
      lead: 'Open <a href="https://www.desmos.com/calculator" target="_blank" rel="noopener"><b>desmos.com/calculator</b></a> in a new tab and keep this page next to it. Every answer in this guide can be found or checked in Desmos.',
      box: `<ol>
        <li>On the first line type <code>a = 1</code>. Desmos turns <b>a</b> into a <b>slider</b>. Drag it, and press ▶ to animate it.</li>
        <li>Click the <b>−10</b> or <b>10</b> at the ends of the slider to change its limits and its <b>step</b>. Set them to <b>−5</b>, <b>5</b> and step <b>0.5</b>.</li>
        <li>To give a an exact value, click the number in <code>a = 1</code> and type a new one.</li>
        <li>On a new line type <code>f(x) = 0.1x^4 − 0.75x^3 + x^2 + 2x − 1</code>. This is our function. It has <b>no slider</b>: never change it. (If the rest of the line goes up into the exponent, press <b>→</b> after typing the exponent.)</li>
      </ol>`,
    },
    {
      n: 3, title: 'Add outside: f(x) + a',
      lead: 'Now build a new function by adding a <b>outside</b> f, after f has done its work.',
      box: `<ol>
        <li>On a new line type <code>g(x) = f(x) + a</code>.</li>
        <li>Make f dashed so you can tell them apart: press and hold the colored circle next to f and choose the dashed line.</li>
        <li>To follow a point, type it as a point: <code>(2, f(2))</code> is on f, and <code>(2, g(2))</code> is on g.</li>
      </ol>`,
    },
    {
      n: 4, title: 'Add inside: f(x + a)',
      lead: 'Now add a <b>inside</b> the parentheses, to x, before f does its work.',
      box: `<ol>
        <li>Change the line of g to <code>g(x) = f(x + a)</code>. Keep f dashed.</li>
        <li>Answer the prediction <b>before</b> you move the slider.</li>
      </ol>`,
    },
    {
      n: 5, title: 'Multiply outside: a · f(x)',
      lead: 'Multiply f by a: every <b>output</b> of f is multiplied.',
      box: `<ol>
        <li>Change g to <code>g(x) = a f(x)</code> (Desmos reads <code>af(x)</code> as a times f(x)).</li>
        <li>Try a = 0, values bigger than 1, values between 0 and 1, and negative values.</li>
      </ol>`,
    },
    {
      n: 6, title: 'Multiply inside: f(a · x)',
      lead: 'Multiply x by a <b>inside</b> the parentheses: every <b>input</b> is multiplied before f uses it.',
      box: `<ol>
        <li>Change g to <code>g(x) = f(ax)</code>.</li>
        <li>Try a = 0, values bigger than 1, values between 0 and 1, and a = −1.</li>
      </ol>`,
    },
    {
      n: 7, title: 'Combine transformations',
      lead: 'Use the rules you found to answer first. Then <b>check each answer in Desmos</b>: type the new g and plot the point you found.',
      box: `<p>The points of f you will follow: <b>(0, −1)</b>, <b>(2, 2.6)</b> and <b>(4, 0.6)</b>. Plot them in Desmos as <code>(0, f(0))</code>, <code>(2, f(2))</code> and <code>(4, f(4))</code>.</p>`,
    },
    {
      n: 8, title: 'The rules in your own words',
      lead: 'Look back at the four rules you found.',
    },
  ],
  items: [
    // ── 2 Get Desmos ready ──
    {
      id: 'set_a', step: 2, type: 'num', short: 'Slider: a = 2.5, value of a + 1',
      text: 'Give a the exact value <b>2.5</b>. On a new line type <code>a + 1</code>. What value does Desmos show?',
      answer: 3.5, tol: 0.001,
      hint: () => 'Click the number in a = … and type 2.5. Desmos shows the value of a + 1 at the right of its line.',
      ok: 'Desmos uses the current value of a everywhere a appears.',
    },
    {
      id: 'set_f0', step: 2, type: 'num', short: 'f(0) in Desmos',
      text: 'Type <code>f(0)</code> on a new line. What is f(0)?',
      answer: -1, tol: 0.001,
      hint: v => v === 1 ? 'Check the sign.' : 'Did you type f exactly as given? Desmos shows the value of f(0) at the right of the line.',
      ok: 'f(0) = −1: the graph crosses the y-axis at (0, −1).',
    },
    {
      id: 'set_f2', step: 2, type: 'num', short: 'f(2) in Desmos',
      text: 'And f(2)?',
      answer: 2.6, tol: 0.001,
      hint: () => 'Type f(2) on a new line. If you get another value, check the coefficients of f: 0.1, −0.75, 1, 2 and −1.',
      ok: 'So the point (2, 2.6) is on the graph of f.',
    },
    {
      id: 'set_min', step: 2, type: 'pt', short: 'Local minimum of f',
      text: 'Click the graph of f: Desmos shows gray points where it turns and where it crosses the axes. What are the coordinates of the <b>lowest point on the left</b> (the local minimum)?',
      answer: TD_MIN, tol: 0.012,
      hint: ([x, y]) => y > 0 ? 'That point is above the x-axis. Look for the lowest point, to the left of the y-axis.' : 'Click exactly on the curve near its lowest point on the left. Type the two numbers Desmos shows.',
      ok: 'The local minimum is about (−0.581, −1.666). You will follow this point too.',
    },

    // ── 3 f(x) + a ──
    {
      id: 'v_pred', step: 3, type: 'predict', short: 'PREDICT: f(x) + 3 compared with f',
      text: 'Before you move the slider: if a = 3, what will the graph of g do compared with f?',
      options: { up: 'Move up 3', down: 'Move down 3', right: 'Move right 3', left: 'Move left 3', tall: 'Get 3 times taller' },
      truth: 'up',
    },
    {
      id: 'v_zero', step: 3, type: 'mc', short: 'f(x) + a with a = 0',
      text: 'Set a = 0. How does g compare with f?',
      options: { same: 'g is exactly f', up: 'g is above f', flat: 'g is a flat line', gone: 'g disappears' },
      answer: 'same',
      hint: () => 'With a = 0, g(x) = f(x) + 0. Look at the two graphs: is there any difference?',
      ok: 'Adding 0 changes nothing: g(x) = f(x).',
    },
    {
      id: 'v_pos', step: 3, type: 'mc', short: 'f(x) + a with a > 0',
      text: 'Try a = 1, 2, 3. When <b>a &gt; 0</b>, the graph of g is the graph of f…',
      options: { up: 'moved up a units', down: 'moved down a units', right: 'moved right a units', left: 'moved left a units', tall: 'stretched taller' },
      answer: 'up',
      hint: v => v === 'tall' ? 'Compare the distance between f and g at several x values. Is it the same everywhere?' : 'Watch the y-intercept: with a = 3 it goes from (0, −1) to…?',
      ok: 'Every output grows by a, so every point moves a units up.',
    },
    {
      id: 'v_neg', step: 3, type: 'mc', short: 'f(x) + a with a < 0',
      text: 'And when <b>a &lt; 0</b>?',
      options: { up: 'moved up', down: 'moved down |a| units', right: 'moved right', left: 'moved left', flip: 'flipped upside down' },
      answer: 'down',
      hint: () => 'Set a = −2 and watch the y-intercept.',
      ok: 'Adding a negative number lowers every output: the graph moves down.',
    },
    {
      id: 'v_pt', step: 3, type: 'pt', short: 'f(x) + 3: where does (2, 2.6) go?',
      text: 'Set a = 3. The point (2, 2.6) is on f. Which point of g is directly matched with it? Plot <code>(2, g(2))</code> to check.',
      answer: [2, 5.6], tol: 0.011,
      hint: ([x, y]) => x !== 2 ? 'Adding outside does not move points sideways: the x-coordinate stays 2.' : 'g(2) = f(2) + 3.',
      ok: '(2, 2.6) → (2, 5.6): same x, y + 3.',
    },
    {
      id: 'v_min', step: 3, type: 'pt', short: 'f(x) − 2: where does the local minimum go?',
      text: 'Set a = −2. Where is the local minimum of g? (The local minimum of f is about (−0.581, −1.666).)',
      answer: [TD_MIN[0], TD_MIN[1] - 2], tol: 0.012,
      hint: ([x, y]) => Math.abs(x - TD_MIN[0]) > 0.02 ? 'The minimum moves straight down: its x-coordinate does not change.' : 'Subtract 2 from the y-coordinate.',
      ok: 'The whole graph moves down 2, its lowest point too: (−0.581, −3.666).',
    },
    {
      id: 'v_rule', step: 3, type: 'mc', short: 'Rule for f(x) + a',
      text: 'In general, <b>g(x) = f(x) + a</b> moves each point (x, y) of f to…',
      options: { yplus: '(x, y + a)', xplus: '(x + a, y)', xminus: '(x − a, y)', ytimes: '(x, a·y)' },
      answer: 'yplus',
      hint: () => 'Look at your last two answers: which coordinate changed, and how?',
      ok: 'Outside f, a acts on the output y, exactly as written.',
    },
    {
      id: 'v_why', step: 3, type: 'text', min: 12, short: 'Why does + a outside move the graph up or down?',
      text: 'Why does adding a <b>outside</b> f move the graph up or down and never sideways? Use the word <em>output</em>.',
    },

    // ── 4 f(x + a) ──
    {
      id: 'h_pred', step: 4, type: 'predict', short: 'PREDICT: f(x + 2) compared with f',
      text: 'Before you move the slider: if a = 2, what will the graph of g(x) = f(x + 2) do compared with f?',
      options: { right: 'Move right 2', left: 'Move left 2', up: 'Move up 2', down: 'Move down 2' },
      truth: 'left',
    },
    {
      id: 'h_zero', step: 4, type: 'mc', short: 'f(x + a) with a = 0',
      text: 'Set a = 0. How does g compare with f?',
      options: { same: 'g is exactly f', left: 'g is left of f', flat: 'g is a flat line', up: 'g is above f' },
      answer: 'same',
      hint: () => 'With a = 0, g(x) = f(x + 0).',
      ok: 'f(x + 0) = f(x).',
    },
    {
      id: 'h_pos', step: 4, type: 'mc', short: 'f(x + a) with a > 0',
      text: 'Try a = 1, 2, 3. When <b>a &gt; 0</b>, the graph of g is the graph of f…',
      options: { right: 'moved right a units', left: 'moved left a units', up: 'moved up a units', down: 'moved down a units' },
      answer: 'left',
      hint: v => v === 'right' ? 'That is what most people expect. Look again at Desmos with a = 2: follow the local minimum. Which way did it go?' : 'Follow the local minimum while you change a. Does it move up and down, or sideways?',
      ok: 'A positive a inside moves the graph to the LEFT. The next questions show why.',
    },
    {
      id: 'h_neg', step: 4, type: 'mc', short: 'f(x + a) with a < 0',
      text: 'And when <b>a &lt; 0</b>?',
      options: { right: 'moved right |a| units', left: 'moved left |a| units', up: 'moved up', down: 'moved down' },
      answer: 'right',
      hint: () => 'Set a = −3 and follow the local minimum.',
      ok: 'f(x − 3) moves the graph 3 units right.',
    },
    {
      id: 'h_pt', step: 4, type: 'pt', short: 'f(x + 2): where does (2, 2.6) go?',
      text: 'Set a = 2. The point (2, 2.6) is on f. Which point of g has the <b>same height</b> 2.6 and comes from it? Hint: g(0) = f(0 + 2) = f(2).',
      answer: [0, 2.6], tol: 0.011,
      hint: ([x, y]) => x === 4 ? 'That is 2 units right. Compute g(0) = f(0 + 2) = f(2): which input of g gives the output 2.6?'
        : Math.abs(y - 2.6) > 0.02 ? 'Adding inside does not change the heights: y stays 2.6.' : 'Plot (0, g(0)) in Desmos.',
      ok: 'g(0) = f(2) = 2.6. g reaches the height of f(2) two units EARLIER, at x = 0.',
    },
    {
      id: 'h_c', step: 4, type: 'pt', short: 'f(x − 3): where does (4, 0.6) go?',
      text: 'Set a = −3, so g(x) = f(x − 3). Where does the point (4, 0.6) of f go?',
      answer: [7, 0.6], tol: 0.011,
      hint: ([x]) => x === 1 ? 'Check it: g(1) = f(1 − 3) = f(−2), not f(4). Which x gives x − 3 = 4?' : 'Solve x − 3 = 4.',
      ok: 'g(7) = f(7 − 3) = f(4) = 0.6: the point moves right 3.',
    },
    {
      id: 'h_rule', step: 4, type: 'mc', short: 'Rule for f(x + a)',
      text: 'In general, <b>g(x) = f(x + a)</b> moves each point (x, y) of f to…',
      options: { xminus: '(x − a, y)', xplus: '(x + a, y)', yplus: '(x, y + a)', xdiv: '(x / a, y)' },
      answer: 'xminus',
      hint: () => 'With a = 2, the point (2, 2.6) went to (0, 2.6). Which rule gives that?',
      ok: 'Inside f, a acts on the input, backwards: the graph moves −a units horizontally.',
    },
    {
      id: 'h_why', step: 4, type: 'text', min: 15, short: 'Why does f(x + 2) move the graph left?',
      text: 'Explain why f(x + 2) moves the graph <b>left</b> and not right. Use g(0) = f(2) in your explanation. Did your prediction match?',
    },

    // ── 5 a · f(x) ──
    {
      id: 'm_pred', step: 5, type: 'predict', short: 'PREDICT: 2 · f(x) compared with f',
      text: 'Before you move the slider: if a = 2, what will the graph of g(x) = 2 f(x) do compared with f?',
      options: { tall: 'Stretch vertically: twice as tall', wide: 'Stretch horizontally: twice as wide', up: 'Move up 2', right: 'Move right 2' },
      truth: 'tall',
    },
    {
      id: 'm_zero', step: 5, type: 'mc', short: 'a · f(x) with a = 0',
      text: 'Set <b>a = 0</b>. What is the graph of g?',
      options: { xaxis: 'A flat line on the x-axis (y = 0)', ym1: 'A flat line y = −1', same: 'The same as f', gone: 'Nothing: Desmos shows no graph' },
      answer: 'xaxis',
      hint: () => 'g(x) = 0 · f(x). What is 0 times any number?',
      ok: 'Every output becomes 0 · f(x) = 0: g is the x-axis.',
    },
    {
      id: 'm_big', step: 5, type: 'mc', short: 'a · f(x) with a > 1',
      text: 'Try a = 2 and a = 3. When <b>a &gt; 1</b>, the graph of g is the graph of f…',
      options: { vstretch: 'stretched vertically, away from the x-axis', vsqueeze: 'squeezed vertically, toward the x-axis', hstretch: 'stretched horizontally', up: 'moved up a units' },
      answer: 'vstretch',
      hint: v => v === 'up' ? 'Look at the zeros of f: does g still cross the x-axis at the same places? A translation would move them.' : 'Compare the heights of the turning points of f and g.',
      ok: 'Every output is multiplied by a: high points go higher, low points go lower.',
    },
    {
      id: 'm_small', step: 5, type: 'mc', short: 'a · f(x) with 0 < a < 1',
      text: 'Try a = 0.5. When <b>0 &lt; a &lt; 1</b>, the graph of g is the graph of f…',
      options: { vstretch: 'stretched vertically', vsqueeze: 'squeezed vertically, toward the x-axis', hsqueeze: 'squeezed horizontally', down: 'moved down' },
      answer: 'vsqueeze',
      hint: () => 'Half of every output: compare the heights of the turning points.',
      ok: 'Multiplying by a number between 0 and 1 makes every output smaller: the graph flattens toward the x-axis.',
    },
    {
      id: 'm_neg', step: 5, type: 'mc', short: 'a · f(x) with a = −1',
      text: 'Set <b>a = −1</b>. The graph of g is the graph of f…',
      options: { xflip: 'reflected over the x-axis (upside down)', yflip: 'reflected over the y-axis (left to right)', down: 'moved down 1', same: 'unchanged' },
      answer: 'xflip',
      hint: () => 'Follow the point (2, 2.6). Where is it on g?',
      ok: 'Every output changes sign: what was above the x-axis goes below it.',
    },
    {
      id: 'm_fixed', step: 5, type: 'mc', short: 'a · f(x): which points never move?',
      text: 'Move a through many values. Which points of f <b>never move</b>?',
      options: { zeros: 'The zeros, where f crosses the x-axis', yint: 'The y-intercept', turn: 'The turning points', none: 'No point stays' },
      answer: 'zeros',
      hint: () => 'Which outputs stay the same when you multiply them by any a? Look at where f crosses the x-axis.',
      ok: 'a · 0 = 0: the zeros stay where they are.',
    },
    {
      id: 'm_pt', step: 5, type: 'pt', short: '2 · f(x): where does (2, 2.6) go?',
      text: 'Set a = 2. Where does the point (2, 2.6) of f go?',
      answer: [2, 5.2], tol: 0.011,
      hint: ([x, y]) => x !== 2 ? 'Multiplying outside does not move points sideways.' : Math.abs(y - 4.6) < 0.02 ? 'That would be f(x) + 2. Here the output is multiplied.' : 'g(2) = 2 · f(2).',
      ok: '(2, 2.6) → (2, 5.2): same x, y times 2.',
    },
    {
      id: 'm_pt2', step: 5, type: 'pt', short: '−0.5 · f(x): where does (4, 0.6) go?',
      text: 'Set a = −0.5. Where does the point (4, 0.6) of f go?',
      answer: [4, -0.3], tol: 0.011,
      hint: ([x, y]) => x !== 4 ? 'The x-coordinate does not change.' : y > 0 ? 'A negative a flips the point to the other side of the x-axis.' : 'Multiply 0.6 by −0.5.',
      ok: '(4, 0.6) → (4, −0.3): flipped and squeezed to half.',
    },
    {
      id: 'm_rule', step: 5, type: 'mc', short: 'Rule for a · f(x)',
      text: 'In general, <b>g(x) = a · f(x)</b> moves each point (x, y) of f to…',
      options: { ytimes: '(x, a·y)', xtimes: '(a·x, y)', xdiv: '(x / a, y)', yplus: '(x, y + a)' },
      answer: 'ytimes',
      hint: () => 'With a = 2, (2, 2.6) went to (2, 5.2).',
      ok: 'Outside f, a multiplies the output, as written: a vertical dilation by |a|, flipped if a < 0.',
    },

    // ── 6 f(a · x) ──
    {
      id: 'k_pred', step: 6, type: 'predict', short: 'PREDICT: f(2x) compared with f',
      text: 'Before you move the slider: if a = 2, what will the graph of g(x) = f(2x) do compared with f?',
      options: { hsqueeze: 'Get narrower: squeezed toward the y-axis', hstretch: 'Get wider: stretched away from the y-axis', tall: 'Get twice as tall', right: 'Move right 2' },
      truth: 'hsqueeze',
    },
    {
      id: 'k_zero', step: 6, type: 'mc', short: 'f(a · x) with a = 0',
      text: 'Set <b>a = 0</b>. What is the graph of g?',
      options: { xaxis: 'A flat line on the x-axis (y = 0)', ym1: 'A flat line y = −1', same: 'The same as f', vert: 'A vertical line' },
      answer: 'ym1',
      hint: v => v === 'xaxis' ? 'That happened with a · f(x). Here g(x) = f(0 · x) = f(0) for every x. What is f(0)?' : 'g(x) = f(0 · x) = f(0) for every x. What is f(0)?',
      ok: 'Every input becomes 0, so every output is f(0) = −1.',
    },
    {
      id: 'k_big', step: 6, type: 'mc', short: 'f(a · x) with a > 1',
      text: 'Try a = 2 and a = 3. When <b>a &gt; 1</b>, the graph of g is the graph of f…',
      options: { hsqueeze: 'squeezed horizontally, toward the y-axis', hstretch: 'stretched horizontally, away from the y-axis', vstretch: 'stretched vertically', left: 'moved left' },
      answer: 'hsqueeze',
      hint: v => v === 'hstretch' ? 'Follow the point (2, 2.6): with a = 2, is it closer to the y-axis or farther away?' : 'Compare the heights of the turning points of f and g: do they change? Now compare their x-coordinates.',
      ok: 'g reaches each height of f sooner, at x / a: the graph is squeezed toward the y-axis. The heights do not change.',
    },
    {
      id: 'k_small', step: 6, type: 'mc', short: 'f(a · x) with 0 < a < 1',
      text: 'Try a = 0.5. When <b>0 &lt; a &lt; 1</b>, the graph of g is the graph of f…',
      options: { hsqueeze: 'squeezed horizontally', hstretch: 'stretched horizontally, away from the y-axis', vsqueeze: 'squeezed vertically', right: 'moved right' },
      answer: 'hstretch',
      hint: () => 'With a = 0.5, g(4) = f(2). Where does the point (2, 2.6) go?',
      ok: 'g needs twice the input to reach the same height: the graph is twice as wide.',
    },
    {
      id: 'k_neg', step: 6, type: 'mc', short: 'f(a · x) with a = −1',
      text: 'Set <b>a = −1</b>. The graph of g is the graph of f…',
      options: { yflip: 'reflected over the y-axis (left to right)', xflip: 'reflected over the x-axis (upside down)', left: 'moved left 1', same: 'unchanged' },
      answer: 'yflip',
      hint: v => v === 'xflip' ? 'That happened with −f(x). Here the heights do not change: follow the local minimum.' : 'Follow the local minimum: where is it on g?',
      ok: 'g(x) = f(−x): the input changes sign, so the graph flips left to right.',
    },
    {
      id: 'k_fixed', step: 6, type: 'mc', short: 'f(a · x): which point never moves?',
      text: 'Move a through many values (not 0). Which point of f <b>never moves</b>?',
      options: { yint: 'The y-intercept (0, −1)', zeros: 'The zeros', turn: 'The turning points', none: 'No point stays' },
      answer: 'yint',
      hint: v => v === 'zeros' ? 'That was a · f(x). Here the inputs are multiplied: which input stays the same when you multiply it by any a?' : 'Which input x stays the same when you multiply it by any a?',
      ok: 'a · 0 = 0: g(0) = f(0) for every a.',
    },
    {
      id: 'k_pt', step: 6, type: 'pt', short: 'f(2x): where does (2, 2.6) go?',
      text: 'Set a = 2. Where does the point (2, 2.6) of f go?',
      answer: [1, 2.6], tol: 0.011,
      hint: ([x, y]) => x === 4 ? 'Check it: g(4) = f(2 · 4) = f(8), not f(2). Which x gives 2x = 2?' : Math.abs(y - 2.6) > 0.02 ? 'Multiplying inside does not change the heights.' : 'Solve 2x = 2.',
      ok: 'g(1) = f(2 · 1) = f(2) = 2.6: x is divided by 2.',
    },
    {
      id: 'k_pt2', step: 6, type: 'pt', short: 'f(0.5x): where does (4, 0.6) go?',
      text: 'Set a = 0.5. Where does the point (4, 0.6) of f go?',
      answer: [8, 0.6], tol: 0.011,
      hint: ([x]) => x === 2 ? 'Check it: g(2) = f(0.5 · 2) = f(1). Which x gives 0.5x = 4?' : 'Solve 0.5x = 4.',
      ok: 'g(8) = f(4): x is divided by 0.5, so it doubles.',
    },
    {
      id: 'k_pt3', step: 6, type: 'pt', short: 'f(−x): where does the local minimum go?',
      text: 'Set a = −1. Where does the local minimum (−0.581, −1.666) go?',
      answer: [-TD_MIN[0], TD_MIN[1]], tol: 0.012,
      hint: ([x, y]) => y > 0 ? 'That is flipped over the x-axis. Here the heights do not change.' : 'Flip it left to right: change the sign of x.',
      ok: '(−0.581, −1.666) → (0.581, −1.666).',
    },
    {
      id: 'k_rule', step: 6, type: 'mc', short: 'Rule for f(a · x)',
      text: 'In general, <b>g(x) = f(a · x)</b> moves each point (x, y) of f to…',
      options: { xdiv: '(x / a, y)', xtimes: '(a·x, y)', ytimes: '(x, a·y)', xminus: '(x − a, y)' },
      answer: 'xdiv',
      hint: () => 'With a = 2, (2, 2.6) went to (1, 2.6). With a = 0.5, (4, 0.6) went to (8, 0.6).',
      ok: 'Inside f, a acts on the input, backwards: a horizontal dilation by 1/|a|, flipped if a < 0.',
    },
    {
      id: 'k_why', step: 6, type: 'text', min: 15, short: 'Why does f(2x) squeeze the graph?',
      text: 'Why does f(2x) <b>squeeze</b> the graph instead of stretching it? Use g(1) = f(2) in your explanation.',
    },

    // ── 7 Combinations ──
    {
      id: 'c1', step: 7, type: 'pt', short: 'f(x − 3) + 2: image of (2, 2.6)',
      text: '<span class="fx">g(x) = f(x − 3) + 2</span>. Where does (2, 2.6) go?',
      answer: [5, 4.6], tol: 0.011,
      hint: ([x, y]) => x === -1 ? 'x − 3 inside moves the graph RIGHT 3.' : Math.abs(y - 4.6) > 0.02 ? '+2 outside: add 2 to the y-coordinate.' : 'Solve x − 3 = 2.',
      ok: 'Right 3 and up 2: (2, 2.6) → (5, 4.6).',
    },
    {
      id: 'c2', step: 7, type: 'pt', short: '2f(x) − 1: image of (2, 2.6)',
      text: '<span class="fx">g(x) = 2 f(x) − 1</span>. Where does (2, 2.6) go?',
      answer: [2, 4.2], tol: 0.011,
      hint: ([x, y]) => x !== 2 ? 'Both changes are outside f: x stays 2.' : Math.abs(y - 3.2) < 0.02 ? 'Order: first f(2) = 2.6, then times 2, then − 1.' : 'g(2) = 2 · f(2) − 1.',
      ok: 'g(2) = 2 · 2.6 − 1 = 4.2.',
    },
    {
      id: 'c3', step: 7, type: 'pt', short: '−f(x + 1): image of (4, 0.6)',
      text: '<span class="fx">g(x) = −f(x + 1)</span>. Where does (4, 0.6) go?',
      answer: [3, -0.6], tol: 0.011,
      hint: ([x, y]) => x === 5 ? 'x + 1 inside moves the graph LEFT 1.' : y > 0 ? 'The minus sign outside flips the point over the x-axis.' : 'Solve x + 1 = 4.',
      ok: 'Left 1 and flipped: (4, 0.6) → (3, −0.6).',
    },
    {
      id: 'c4', step: 7, type: 'pt', short: 'f(2x) + 3: image of (2, 2.6)',
      text: '<span class="fx">g(x) = f(2x) + 3</span>. Where does (2, 2.6) go?',
      answer: [1, 5.6], tol: 0.011,
      hint: ([x, y]) => x === 4 ? '2x inside squeezes: x is divided by 2.' : Math.abs(y - 5.6) > 0.02 ? '+3 outside: add 3 to y.' : 'Solve 2x = 2.',
      ok: 'Squeezed by 1/2 and up 3: (2, 2.6) → (1, 5.6).',
    },
    {
      id: 'c5', step: 7, type: 'pt', short: '−2f(x + 1) + 3: image of (0, −1)',
      text: '<span class="fx">g(x) = −2 f(x + 1) + 3</span>. Where does the y-intercept (0, −1) of f go?',
      answer: [-1, 5], tol: 0.011,
      hint: ([x, y]) => x === 1 ? 'x + 1 inside moves the graph LEFT 1.' : Math.abs(y - 1) < 0.02 ? 'Outside: first multiply by −2, then add 3. −2 · (−1) = 2.' : 'g(−1) = −2 · f(0) + 3.',
      ok: 'g(−1) = −2 · f(0) + 3 = −2 · (−1) + 3 = 5.',
    },
    {
      id: 'c6', step: 7, type: 'mc', short: 'Which g is f moved right 4 and down 1?',
      text: 'Which function moves the graph of f <b>4 units right</b> and <b>1 unit down</b>?',
      options: { a: 'g(x) = f(x − 4) − 1', b: 'g(x) = f(x + 4) − 1', c: 'g(x) = f(x − 1) + 4', d: 'g(x) = f(x) − 4 − 1' },
      answer: 'a',
      hint: v => v === 'b' ? 'f(x + 4) moves the graph LEFT 4.' : v === 'c' ? 'Right/left changes go inside, up/down changes go outside.' : 'f(x) − 4 − 1 only moves the graph down. Right needs a change inside.',
      ok: 'Right 4 is x − 4 inside; down 1 is − 1 outside.',
    },
    {
      id: 'c7', step: 7, type: 'pt', short: 'f(2x − 4): image of (2, 2.6)',
      text: '<span class="fx">g(x) = f(2x − 4)</span>. Where does (2, 2.6) go? Check in Desmos.',
      answer: [3, 2.6], tol: 0.011,
      hint: () => 'Which input of g gives the input 2 of f? Solve 2x − 4 = 2.',
      ok: '2x − 4 = 2 → x = 3. So g(3) = f(2) = 2.6.',
    },
    {
      id: 'c8', step: 7, type: 'mc', short: 'f(2x − 4) described in steps',
      text: 'Which description gives <span class="fx">f(2x − 4)</span>? (Hint: 2x − 4 = 2(x − 2).)',
      options: { r4: 'Squeeze by 1/2, then move right 4', r2: 'Squeeze by 1/2, then move right 2', r2first: 'Move right 2, then squeeze by 1/2', stretch: 'Stretch by 2, then move right 2' },
      answer: 'r2',
      hint: v => v === 'r4' ? 'Squeeze first: f(2x). Then right 4 replaces x with x − 4: f(2(x − 4)) = f(2x − 8). Not the same.' : v === 'r2first' ? 'Right 2 first: f(x − 2). Then squeeze replaces x with 2x: f(2x − 2). Not the same.' : 'f(2x) squeezes, it does not stretch.',
      ok: 'Factor out the 2: f(2(x − 2)). The number left inside, −2, is the real translation.',
    },

    // ── 8 Rules in words ──
    {
      id: 'r_words', step: 8, type: 'text', min: 25, short: 'Which changes act on x, which on y, which go backwards?',
      text: 'Which of the four transformations change the <b>x</b>-coordinates and which change the <b>y</b>-coordinates? Which ones work "backwards", and why?',
    },
    {
      id: 'r_link', step: 8, type: 'text', min: 0, optional: true, short: 'Desmos graph link',
      text: 'Paste the link of your Desmos graph (in Desmos: <b>Share</b> → copy the link). (optional)',
    },
    {
      id: 'r_doubt', step: 8, type: 'text', min: 0, optional: true, short: 'Questions',
      text: 'Is there anything you are still unsure about? (optional)',
    },
  ],
};

// ── Checking answers ────────────────────────────────────────
// Numbers as students type them: −2, -0.581, 2,6 (decimal comma), 1/2
function tdNumber(s) {
  const t = String(s ?? '').trim().replace(/[−–]/g, '-').replace(/\s+/g, '').replace(',', '.');
  const fr = t.match(/^(-?\d*\.?\d+)\/(-?\d*\.?\d+)$/);
  if (fr) return +fr[1] / +fr[2];
  return /^-?(\d+\.?\d*|\.\d+)(e-?\d+)?$/i.test(t) ? parseFloat(t) : NaN;
}
// The answer as numbers: one for 'num', [x, y] for 'pt' (stored as "x; y"), null if not typed
function tdValue(q, v) {
  if (q.type === 'num') { const x = tdNumber(v); return isFinite(x) ? x : null; }
  if (q.type === 'pt') {
    const p = String(v ?? '').split(';').map(tdNumber);
    return p.length === 2 && p.every(isFinite) ? p : null;
  }
  return v || null;
}
const tdClose = (a, b, tol) => Math.abs(a - b) <= tol + 1e-9;
function tdCorrect(q, v) {
  const x = tdValue(q, v);
  if (x == null) return false;
  if (q.type === 'mc') return x === q.answer;
  if (q.type === 'num') return tdClose(x, q.answer, q.tol);
  if (q.type === 'pt') return tdClose(x[0], q.answer[0], q.tol) && tdClose(x[1], q.answer[1], q.tol);
  return false;
}
const tdRound = x => String(+(+x).toFixed(3)).replace('-', '−');
function tdAnswerText(q) {
  if (q.type === 'mc') return q.options[q.answer];
  if (q.type === 'predict') return q.options[q.truth];
  if (q.type === 'num') return tdRound(q.answer);
  if (q.type === 'pt') return `(${tdRound(q.answer[0])}, ${tdRound(q.answer[1])})`;
  return '';
}
const TD_GRADED = TD.items.filter(q => ['mc', 'num', 'pt'].includes(q.type));
