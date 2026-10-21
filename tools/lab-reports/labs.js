// Lab reports by topic. Shared by the Lab Reports tab of the site's index (students) and
// teacher.html (unlisted): add an experiment here and it appears on both.
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
];
