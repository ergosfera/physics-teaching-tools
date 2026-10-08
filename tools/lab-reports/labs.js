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
];
