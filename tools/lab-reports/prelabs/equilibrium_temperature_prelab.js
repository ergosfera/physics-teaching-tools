// Equilibrium Temperature prelab: heat lost by the hot water = heat gained by the cold water.
// Shared by equilibrium_temperature_prelab.html (renders the questions) and
// equilibrium_temperature_prelab_teacher.html (answer key, charts and scores).
//
// Students get no right/wrong feedback here: their answers show what they think before the lab,
// and the teacher reviews them on the teacher page.
//
// A step: { n, title, lead (html), box? (html) }      steps 2…6; step 1 is the team and the last one is "Send"
// An item: { id, step, type: 'mc' | 'num' | 'text', short, text (html), … }
//   mc:   options: { key: html }, answer: key
//   num:  answer, unit, tol (absolute) or rel (fraction of the answer)
//   text: min (words), optional?
const PRELAB = {
  id: 'prelab-equilibrium-temperature',
  title: 'Equilibrium Temperature',
  C: 4186,   // J/(kg·°C)
  steps: [
    {
      n: 2, title: 'Where does the energy go?',
      lead: 'You pour the hot water of beaker <b class="h">H</b> into the cold water of beaker <b class="c">C</b> and stir. Answer with what you think now: there is no penalty for being wrong.',
    },
    {
      n: 3, title: 'Degrees are not energy',
      lead: 'A temperature change is not the same as an energy change. The energy needed to change the temperature of a mass m of water by ΔT is:',
      box: `<div class="eq">Q = m · c · ΔT</div>
        <p>c = 4186 J/(kg·°C) is the specific heat of water: it takes 4186 J to warm 1 kg of water by 1 °C. <b>Write m in kilograms</b> (250 g = 0.250 kg).</p>`,
    },
    {
      n: 4, title: 'The energy balance',
      lead: 'If no energy escapes to the air or the beaker, all the energy the hot water loses goes into the cold water:',
      box: `<div class="eq">heat lost by the <span class="h">hot</span> water = heat gained by the <span class="c">cold</span> water</div>
        <p>Use this mixture for the first three questions:</p>
        <div class="mix"><span class="h">H: 200 g at 70 °C</span> + <span class="c">C: 300 g at 20 °C</span> → both end at <b>40 °C</b></div>`,
    },
    {
      n: 5, title: 'Predict with the balance',
      lead: 'Write the energy balance with the numbers, then solve it. Do the algebra in your notebook and type only the result.',
    },
    {
      n: 6, title: 'In the real lab',
      lead: 'In the lab the beakers are open to the air, and the glass of beaker C starts at room temperature.',
    },
  ],
  items: [
    // ── 2 Where does the energy go? ──
    {
      id: 'flow', step: 2, type: 'mc', short: 'Which way does the energy flow?',
      text: 'While the two waters mix, energy flows…',
      options: { hc: 'from the hot water to the cold water', ch: 'from the cold water to the hot water', both: 'both ways, the same amount', none: 'no energy flows; only the temperatures change' },
      answer: 'hc',
    },
    {
      id: 'stop', step: 2, type: 'mc', short: 'When do the temperatures stop changing?',
      text: 'The temperatures stop changing when…',
      options: { same: 'both waters reach the same temperature', empty: 'the hot water has no heat left', degrees: 'the cold water has gained as many degrees as the hot water lost', avg: 'the mixture reaches the average of the two temperatures' },
      answer: 'same',
    },
    {
      id: 'balance', step: 2, type: 'mc', short: 'Heat lost vs heat gained (no losses)',
      text: 'Suppose no energy escapes to the air or the beaker. Compare the heat <b>lost</b> by the hot water with the heat <b>gained</b> by the cold water.',
      options: { equal: 'They are equal', lost: 'The hot water loses more than the cold water gains', gained: 'The cold water gains more than the hot water loses', size: 'It depends on which beaker is bigger' },
      answer: 'equal',
    },
    {
      id: 'why', step: 2, type: 'text', min: 15, short: 'Why is heat lost = heat gained?',
      text: 'Explain in your own words why, with no losses, the heat lost by the hot water equals the heat gained by the cold water. Use the word <em>energy</em>.',
    },
    // ── 3 Degrees are not energy ──
    {
      id: 'deg-equal', step: 3, type: 'mc', short: '100 g + 100 g: hot cools 30 °C, cold warms…',
      text: 'You mix <em>100 g at 80 °C</em> with <em>100 g at 20 °C</em>. The hot water cools down by 30 °C. The cold water warms up by…',
      options: { d30: '30 °C', d60: '60 °C', d15: '15 °C', c: 'You can\'t tell without knowing c' },
      answer: 'd30',
    },
    {
      id: 'deg-unequal', step: 3, type: 'mc', short: '100 g + 300 g: hot cools 30 °C, cold warms…',
      text: 'Now you mix <em>100 g of hot water</em> with <em>300 g of cold water</em>. The hot water cools down by 30 °C. The cold water warms up by…',
      options: { d30: '30 °C', d90: '90 °C', d10: '10 °C', d15: '15 °C' },
      answer: 'd10',
    },
    {
      id: 'q-warm', step: 3, type: 'num', short: 'Energy to warm 250 g from 20 to 60 °C',
      text: 'How much energy does it take to warm <em>250 g of water from 20 °C to 60 °C</em>?',
      answer: 0.250 * 4186 * 40, unit: 'J', rel: 0.02,
    },
    {
      id: 'claim', step: 3, type: 'text', min: 15, short: '"The cold water gains the same degrees": when is it right?',
      text: 'A student says: <em>"The hot water lost 30 degrees, so the cold water must gain 30 degrees."</em> When is the student right, and when are they wrong? Explain why.',
    },
    // ── 4 The energy balance (200 g at 70 °C + 300 g at 20 °C → 40 °C) ──
    {
      id: 'q-lost', step: 4, type: 'num', short: 'Heat lost by 200 g from 70 to 40 °C',
      text: 'How much heat did the <span class="h">hot water</span> lose?',
      answer: 0.200 * 4186 * 30, unit: 'J', rel: 0.02,
    },
    {
      id: 'q-gained', step: 4, type: 'num', short: 'Heat gained by 300 g from 20 to 40 °C',
      text: 'How much heat did the <span class="c">cold water</span> gain?',
      answer: 0.300 * 4186 * 20, unit: 'J', rel: 0.02,
    },
    {
      id: 'compare', step: 4, type: 'mc', short: 'What do the two energies show?',
      text: 'Compare your last two answers. They show that…',
      options: { all: 'all the energy the hot water lost went into the cold water', mass: 'the cold water gained more energy because it has more mass', deg: 'the hot water lost more energy because its temperature changed more', temp: 'the temperature is what is conserved' },
      answer: 'all',
    },
    {
      id: 'equation', step: 4, type: 'mc', short: 'Which equation is the energy balance?',
      text: 'Which equation says "heat lost by the hot water = heat gained by the cold water"?',
      options: {
        right: 'm<sub>h</sub>·c·(T<sub>h</sub> − T<sub>eq</sub>) = m<sub>c</sub>·c·(T<sub>eq</sub> − T<sub>c</sub>)',
        sign: 'm<sub>h</sub>·c·(T<sub>eq</sub> − T<sub>h</sub>) = m<sub>c</sub>·c·(T<sub>eq</sub> − T<sub>c</sub>)',
        temps: 'm<sub>h</sub>·c·T<sub>h</sub> = m<sub>c</sub>·c·T<sub>c</sub>',
        range: 'm<sub>h</sub>·c·(T<sub>h</sub> − T<sub>c</sub>) = m<sub>c</sub>·c·(T<sub>eq</sub> − T<sub>c</sub>)',
      },
      answer: 'right',
    },
    // ── 5 Predict with the balance ──
    {
      id: 'teq', step: 5, type: 'num', short: 'T_eq of 150 g at 60 °C + 100 g at 25 °C',
      text: 'You mix <em>150 g of water at 60 °C</em> with <em>100 g of water at 25 °C</em>. What is the equilibrium temperature T<sub>eq</sub>?',
      answer: (150 * 60 + 100 * 25) / 250, unit: '°C', tol: 0.5,
    },
    {
      id: 'mass', step: 5, type: 'num', short: 'Hot mass at 80 °C to bring 200 g at 20 °C to 40 °C',
      text: 'How many grams of water at <em>80 °C</em> must you add to <em>200 g of water at 20 °C</em> so the mixture ends at <em>40 °C</em>?',
      answer: 200 * (40 - 20) / (80 - 40), unit: 'g', tol: 2,
    },
    {
      id: 'steps', step: 5, type: 'text', min: 12, short: 'How did you find T_eq?',
      text: 'Explain the steps you followed to find T<sub>eq</sub> in the first question of this step.',
    },
    // ── 6 In the real lab ──
    {
      id: 'real', step: 6, type: 'mc', short: 'Measured T_eq vs the model',
      text: 'Compared with the value the energy balance predicts, the T<sub>eq</sub> you measure in the lab will most likely be…',
      options: { lower: 'a bit lower', higher: 'a bit higher', same: 'exactly the same', unknown: 'impossible to predict' },
      answer: 'lower',
    },
    {
      id: 'real-q', step: 6, type: 'mc', short: 'Real lab: heat gained vs heat lost',
      text: 'Then, in the lab, the heat gained by the cold water will be…',
      options: { less: 'less than the heat lost by the hot water', equal: 'equal to the heat lost by the hot water', more: 'more than the heat lost by the hot water' },
      answer: 'less',
    },
    {
      id: 'where', step: 6, type: 'text', min: 15, short: 'Where else can the energy go? How will you reduce it?',
      text: 'Name two places where part of the energy can go besides the cold water, and one thing you will do in the lab to reduce that loss.',
    },
    {
      id: 'doubt', step: 6, type: 'text', min: 0, optional: true, short: 'Questions before the lab',
      text: 'Is there anything you are still unsure about before the lab? (optional)',
    },
  ],
};

// A typed number: "25116", "25 116", "25,116" (thousands) or "46,5" (decimal comma), "2.5e4", with or without the unit
function prelabNumber(s) {
  let t = String(s ?? '').trim().replace(/\s+/g, '').replace(/(J|g|°C|ºC|C)$/, '');
  if (/^-?\d{1,3}(,\d{3})+(\.\d+)?$/.test(t)) t = t.replace(/,/g, '');
  else t = t.replace(',', '.');
  const v = parseFloat(t);
  return isFinite(v) && /^-?[\d.]+(e-?\d+)?$/i.test(t) ? v : NaN;
}
const prelabGraded = () => PRELAB.items.filter(q => q.type !== 'text');
function prelabCorrect(q, v) {
  if (v == null || v === '') return false;
  if (q.type === 'mc') return v === q.answer;
  const x = prelabNumber(v);
  return isFinite(x) && Math.abs(x - q.answer) <= (q.rel ? q.rel * Math.abs(q.answer) : q.tol);
}
const prelabAnswerText = q => q.type === 'mc' ? q.options[q.answer].replace(/<[^>]+>/g, '') : `${+q.answer.toPrecision(5)} ${q.unit}`;
