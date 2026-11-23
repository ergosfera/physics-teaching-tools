// Measuring Force: shared by the class page and its teacher page.
//   FORCE.stations / minutes   the seven stations and how long each one should take (70 min in all)
//   FORCE.checkpoints          what the class page counts as done (computed from the progress object p)
//   FORCE.firstTry             the choice questions, graded on the pair's first try
//   FORCE.texts                the written answers
//   FORCE.exit                 the exit check: in each question the first option is the right one
//                              (the class page shuffles them for each pair)
const FORCE = (() => {
  const words = s => String(s || '').trim().split(/\s+/).filter(Boolean).length;
  const ok = (p, id) => !!(p.q && p.q[id] && p.q[id].done);
  const exit = [
    { id: 'parallax', ic: '👁️', q: 'You read a dynamometer with your eye below the level of the pointer. Your reading…',
      o: ['is off: that is a parallax error', 'is right, as long as you are close to it', 'is right if the spring is zeroed', 'is always half a division too low, so you add it back'],
      e: 'The pointer sits a little in front of the scale. Seen from above or below, it lines up with the wrong mark. Put your eye level with the pointer.' },
    { id: 'newton', ic: '🛷', q: 'One newton (1 N) is the force that…',
      o: ['gives a 1 kg mass an acceleration of 1 m/s²', 'holds up a 1 kg mass', 'moves a 1 kg mass by 1 meter', 'a 1 kg mass puts on the floor'],
      e: '1 N = 1 kg · 1 m/s². Holding up 1 kg on Earth takes 9.8 N, not 1 N.' },
    { id: 'bag', ic: '🛍️', q: 'A 2 kg bag on Earth weighs about…',
      o: ['19.6 N, which is 2 kgf', '2 N', '0.2 N', '19.6 kgf'],
      e: 'W = m·g = 2 kg × 9.8 m/s² = 19.6 N. And 1 kgf is the weight of 1 kg, so it is also 2 kgf.' },
    { id: 'kgf', ic: '🏋️', q: '15 kgf is about…',
      o: ['147 N', '1.5 N', '15 N', '150 kg'],
      e: '1 kgf = 9.8 N, so 15 kgf = 15 × 9.8 ≈ 147 N. It is a force, so it can\'t be kilograms.' },
    { id: 'moon', ic: '🌕', q: 'On the Moon, your mass… and your weight…',
      o: ['stays the same · becomes about 6 times smaller', 'becomes 6 times smaller · stays the same', 'both become about 6 times smaller', 'both stay the same'],
      e: 'Mass is how much matter you have: it goes with you. Weight is the Moon\'s pull on you, and the Moon pulls about 6 times less.' },
    { id: 'write', ic: '✏️', q: 'A dynamometer goes from 0 to 5 N in divisions of 0.1 N. A good way to write a reading is…',
      o: ['(3.40 ± 0.05) N', '3.4 N, exactly', '(3.4 ± 0.5) N', '(3.4 ± 1) N'],
      e: 'The uncertainty of an analog scale is half the smallest division: 0.1 / 2 = 0.05 N. The value gets the same decimals as the uncertainty.' },
    { id: 'choose', ic: '🔧', q: 'You need to weigh an object of about 0.3 N. Which dynamometer is best?',
      o: ['0 – 1 N, divisions of 0.02 N', '0 – 10 N, divisions of 0.2 N', '0 – 50 N, divisions of 1 N', '0 – 0.2 N, divisions of 0.005 N'],
      e: 'The smallest range that still holds the load gives the finest divisions. 0 – 0.2 N would be overloaded and damaged.' },
    { id: 'zero', ic: '0️⃣', q: 'Your dynamometer reads 0.2 N with nothing on it. With a stone it reads 2.6 N. The stone weighs…',
      o: ['2.4 N', '2.6 N', '2.8 N', '0.2 N'],
      e: 'Every reading is 0.2 N too high: a systematic (zero) error. Subtract it: 2.6 − 0.2 = 2.4 N. Better still, adjust the zero before measuring.' },
  ];
  return {
    classId: 'force-class',
    words,
    stations: ['Warm-up', 'What is a force?', 'The newton', 'The kilogram-force', 'Inside the dynamometer', 'Reading it right', 'Exit check'],
    minutes: [5, 12, 12, 10, 10, 14, 7],
    checkpoints: [
      { id: 'planets', st: 1, short: 'Scale on 3 worlds', long: 'Took the bathroom scale to at least three worlds', done: p => Object.keys(p.planets || {}).length >= 3 },
      { id: 'w1', st: 1, short: 'What changed?', long: 'On the Moon, the pull changed, not the mass', done: p => ok(p, 'w1') },
      { id: 'predict', st: 1, short: 'Prediction', long: 'Wrote whether a bathroom scale measures mass or force', done: p => words(p.predict) >= 6 },
      { id: 'scenes', st: 2, short: '4 interactions', long: 'Answered the four interactions (cart, spring, magnets, apple)', done: p => [0, 1, 2, 3].every(i => ok(p, 'sc' + i)) },
      { id: 'vec', st: 2, short: '30 N + 30 N', long: 'Two 30 N pushes: the total depends on the directions', done: p => ok(p, 'vec') },
      { id: 'define', st: 2, short: 'Force, own words', long: 'Wrote what a force is, with an example', done: p => words(p.define) >= 12 },
      { id: 'oneN', st: 3, short: 'Made 1 N', long: 'Ran the 1 kg cart with 1 N on the air track', done: p => (p.runs || []).some(r => Math.abs(r.m - 1) < 1e-6 && Math.abs(r.F - 1) < 1e-6) },
      { id: 'n1', st: 3, short: 'Definition of 1 N', long: '1 N gives 1 kg an acceleration of 1 m/s²', done: p => ok(p, 'n1') && ok(p, 'n2') },
      { id: 'est', st: 3, short: 'Estimates in N', long: 'Estimated three everyday forces in newtons', done: p => [0, 1, 2].every(i => ok(p, 'est' + i)) },
      { id: 'kgf', st: 4, short: 'kgf is fixed', long: '1 kgf is 9.8 N everywhere, also on the Moon', done: p => ok(p, 'k1') && Object.keys(p.kgfPlanets || {}).length >= 3 },
      { id: 'conv', st: 4, short: '3 conversions', long: 'Three N ↔ kgf conversions right', done: p => [0, 1, 2].every(i => p.conv && p.conv[i] && p.conv[i].ok) },
      { id: 'scaleAns', st: 4, short: 'Mass or force?', long: 'Went back to the warm-up: what a bathroom scale really measures', done: p => words(p.scaleAns) >= 10 },
      { id: 'hooke', st: 5, short: 'Stretch ∝ force', long: 'Recorded 4+ loads on a zeroed dynamometer', done: p => !!(p.dyn && (p.dyn.pts || []).filter(x => x.z).length >= 4) },
      { id: 'rangediv', st: 5, short: 'Range & division', long: 'Read the range and the smallest division of the dynamometer', done: p => !!(p.dyn && p.dyn.range && p.dyn.div) },
      { id: 'over', st: 5, short: 'Why a maximum', long: 'Linear stretch and why a dynamometer has a maximum load', done: p => ok(p, 'lin') && ok(p, 'over') },
      { id: 'reads', st: 6, short: '3 readings ± u', long: 'Three scales read right, with the uncertainty', done: p => (p.reads || []).filter(r => r.ok).length >= 3 },
      { id: 'parallax', st: 6, short: 'Parallax', long: 'Moved the eye and said where it has to be', done: p => !!p.eyeMoved && ok(p, 'par') },
      { id: 'rel', st: 6, short: 'Relative error', long: 'Chose the best dynamometer and found both relative errors', done: p => ok(p, 'choose') && !!(p.rel && p.rel.a && p.rel.b) },
      { id: 'target', st: 6, short: 'Precise / accurate', long: 'Sorted the four dynamometers: precise, accurate, both, neither', done: p => !!p.target && ok(p, 'sys') },
      { id: 'report', st: 6, short: 'Zero error report', long: 'Wrote what to report with an unzeroed dynamometer', done: p => words(p.report) >= 10 },
      { id: 'exit', st: 7, short: 'Exit check', long: 'Handed in the exit check', done: p => !!(p.exit && p.exit.at) },
    ],
    firstTry: [
      { id: 'w1', short: 'Moon: the pull changed, not the mass' },
      { id: 'sc0', short: 'Cart: the push changes its motion' },
      { id: 'sc1', short: 'Spring: the pull changes its shape' },
      { id: 'sc2', short: 'Magnets push without touching' },
      { id: 'sc3', short: 'The apple pulls the Earth up, 1 N too' },
      { id: 'vec', short: '30 N + 30 N depends on the angle' },
      { id: 'n1', short: '1 N: 1 kg speeds up 1 m/s each second' },
      { id: 'n2', short: '2 N on 2 kg → 1 m/s²' },
      { id: 'est0', short: 'A phone weighs about 2 N' },
      { id: 'est1', short: 'A 5 kg bag weighs about 50 N' },
      { id: 'est2', short: 'A car pushes the road with ~10 000 N' },
      { id: 'k1', short: '1 kgf on the Moon is still 9.8 N' },
      { id: 'lin', short: 'Double force → double stretch' },
      { id: 'over', short: 'Overload stretches the spring for good' },
      { id: 'par', short: 'Eye level with the pointer' },
      { id: 'choose', short: '0.8 N: pick the 0 – 1 N dynamometer' },
      { id: 'sys', short: 'Zero error is systematic' },
    ],
    texts: [
      { id: 'predict', title: 'Warm-up: does a bathroom scale measure mass or force?' },
      { id: 'define', title: 'What is a force? (with an example)' },
      { id: 'scaleAns', title: 'Back to the warm-up: what does the bathroom scale really measure?' },
      { id: 'report', title: 'Reads 0.2 N empty and (3.2 ± 0.1) N with a stone: what do you report, and what kind of error is it?' },
    ],
    exit,
  };
})();
