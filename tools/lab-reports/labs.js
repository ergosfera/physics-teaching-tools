// Lab reports and math activities by topic. Both use the same student and teacher pages engine.
//   LAB_TOPICS:  the Lab Reports tab of the site's index (students) and teacher.html (unlisted)
//   MATH_TOPICS: the Math Activities tab of the site's index and math_teacher.html (unlisted)
// Add an experiment or activity here and it appears on both pages.
//   student: the page pairs fill in during the lab
//   teacher: the consolidated class results for the same experiment
const LAB_TOPICS = [
  {
    name: 'Mechanics', icon: '⚙️',
    labs: [
      {
        id: 'hookes-law', icon: '🧪', title: "Hooke's Law",
        desc: 'Hang masses from a spring, measure the stretch, and find the spring constant k. Predict, discuss and test your model.',
        student: 'mechanics/hookes_law_lab.html',
        teacher: 'mechanics/hookes_law_teacher.html',
      },
      {
        id: 'falling-cone', icon: '🔻', title: 'Falling Cone',
        desc: 'Film a paper cone falling inside a 1-meter tube and track it with the Video Tracker. Find its velocity with two methods and decide if the motion is uniform.',
        student: 'mechanics/falling_cone_lab.html',
        teacher: 'mechanics/falling_cone_teacher.html',
      },
      {
        id: 'net-force-rest', icon: '⚖️', title: 'Net Force at Rest',
        desc: 'Hold a ring at rest with two spring scales and a hanging mass. Measure the forces and their angles in a photo, decompose each force, and find the net force on the ring.',
        student: 'mechanics/net_force_rest_lab.html',
        teacher: 'mechanics/net_force_rest_teacher.html',
      },
      {
        id: 'buoyancy', icon: '🚢', title: 'Buoyant Force',
        desc: 'Lower a marked cylinder into your group\'s liquid on a balance and find how the buoyant force grows with the volume under the liquid. Then compare every group\'s liquid to find how it depends on density.',
        student: 'mechanics/buoyancy_lab.html',
        teacher: 'mechanics/buoyancy_teacher.html',
      },
    ],
  },
  {
    name: 'Thermal physics', icon: '🌡️',
    labs: [
      {
        id: 'equilibrium-temperature', icon: '☕', title: 'Equilibrium Temperature',
        desc: 'Mix hot and cold water and predict the final temperature with Q = m·c·ΔT. Test the model in two trials and track where the energy went.',
        student: 'thermal-physics/equilibrium_temperature_lab.html',
        teacher: 'thermal-physics/equilibrium_temperature_teacher.html',
      },
    ],
  },
];

const MATH_TOPICS = [
  {
    name: 'PreCalculus', icon: '📐',
    labs: [
      {
        id: 'rational-functions', icon: '📈', title: 'Rational Functions',
        desc: 'AP Precalculus 1.7–1.12: end behavior, zeros, vertical asymptotes, holes, long division and transformations, with instant feedback and live graphs. Then build your own rational function.',
        student: 'precalculus/rational_functions_lab.html',
        teacher: 'precalculus/rational_functions_teacher.html',
      },
    ],
  },
  {
    // One practice page for every set: ?set= picks the 10 questions from precalculus/practice_sets.js
    name: 'PreCalculus · Unit 1 practice', icon: '✏️',
    labs: [
      ['1.7', '↔️', 'End Behavior', 'Limits at ±∞, horizontal and slant asymptotes, and how the degrees decide the end behavior.'],
      ['1.8', '🎯', 'Zeros', 'Zeros of rational functions, canceled factors, sign of r(x), and touching or crossing the x-axis.'],
      ['1.9', '📏', 'Vertical Asymptotes', 'Where they are, and whether r(x) goes to +∞ or −∞ on each side.'],
      ['1.10', '⭕', 'Holes', 'Common factors, the coordinates of a hole, and a hole versus an asymptote.'],
      ['1.11', '🔁', 'Equivalent Representations', 'Factoring, polynomial long division, slant asymptotes and the binomial theorem.'],
    ].map(([set, icon, title, desc]) => ({
      id: 'pc-' + set.replace('.', '-'), icon, title: `${set} ${title}`,
      desc: `10 questions with instant feedback. ${desc}`,
      student: 'precalculus/practice_lab.html?set=' + set,
      teacher: 'precalculus/practice_teacher.html?set=' + set,
    })),
  },
];
