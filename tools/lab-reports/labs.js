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
