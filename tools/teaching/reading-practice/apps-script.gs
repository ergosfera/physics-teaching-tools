// Reading practice backend: paste into Extensions → Apps Script of a new Google Sheet.
// Setup:
//   1. Change FAMILY_KEY below to a secret word of your own.
//   2. Project Settings (gear icon) → Script properties → add ANTHROPIC_API_KEY with your key.
//   3. Deploy → New deployment → Web app, Execute as: Me, Who has access: Anyone.
//   4. Optional: Triggers → Add trigger → prepareToday, Time-driven, Day timer, 5am to 6am,
//      so the day's texts are ready before anyone opens the pages.

const FAMILY_KEY = 'change-me';
const STUDENT = { name: 'Jose', age: 12, grade: '6th grade (sexto) in Colombia' };
const PARENT_LANG = 'English';           // language of the reports you read
const TZ = 'America/Bogota';
const MODEL = 'claude-opus-5-5';

// Five levels. Up after averaging 8/10 or more over 5 sessions at a level,
// down after averaging below 5 over 3 sessions.
const LEVELS = {
  1: { words: 30, focus: 'main idea and explicit details (who, what, where, when)',
       story: ['main_idea', 'detail', 'detail', 'detail', 'detail'],
       math: ['main_idea', 'detail', 'detail', 'detail', 'math'],
       mathRule: 'one-step problem, every number in it is needed' },
  2: { words: 45, focus: 'adds sequence and telling key details from extra ones',
       story: ['main_idea', 'detail', 'sequence', 'key_detail', 'detail'],
       math: ['main_idea', 'detail', 'key_detail', 'sequence', 'math'],
       mathRule: 'one- or two-step problem with exactly one number that is not needed' },
  3: { words: 60, focus: 'adds simple inference',
       story: ['main_idea', 'detail', 'sequence', 'inference', 'inference'],
       math: ['main_idea', 'key_detail', 'inference', 'math', 'math'],
       mathRule: 'two-step problem with one number that is not needed' },
  4: { words: 80, focus: 'adds vocabulary from context and a one-sentence summary',
       story: ['main_idea', 'inference', 'vocabulary', 'detail', 'summary'],
       math: ['main_idea', 'key_detail', 'vocabulary', 'summary', 'math'],
       mathRule: 'two- or three-step problem with one or two numbers that are not needed' },
  5: { words: 110, focus: "adds the author's purpose and multi-step problems with distractor data",
       story: ['main_idea', 'inference', 'vocabulary', 'summary', 'purpose'],
       math: ['main_idea', 'key_detail', 'inference', 'summary', 'math'],
       mathRule: 'multi-step problem with two numbers that are not needed' },
};
const SKILLS = {
  main_idea: 'what the text is mostly about (for a math problem: what it asks to find)',
  detail: 'a fact stated directly in the text',
  key_detail: 'which information matters and which does not',
  sequence: 'the order in which things happen',
  inference: 'something true that the text does not say directly',
  vocabulary: 'the meaning of a word from its context',
  summary: 'the whole text in one sentence of his own',
  purpose: 'why the author wrote the text',
  math: 'solving the problem, with the steps',
};

function doGet(e) {
  return respond_(() => {
    checkKey_(e.parameter.key);
    const action = e.parameter.action;
    if (action === 'today') return today_(e.parameter.role === 'student');
    if (action === 'dashboard') return dashboard_();
    throw new Error('Unknown action');
  });
}

function doPost(e) {
  return respond_(() => {
    const body = JSON.parse(e.postData.contents);
    checkKey_(body.key);
    if (body.action === 'submit') return submit_(body.answers);
    if (body.action === 'note') return note_(body.text);
    throw new Error('Unknown action');
  });
}

function prepareToday() { today_(false); }

// ---------- Today's texts ----------

function today_(forStudent) {
  const date = dateOf_();
  let day = findDay_(date);
  if (!day) {
    const lock = LockService.getScriptLock();
    lock.waitLock(120000);
    try {
      day = findDay_(date) || createDay_(date);
    } finally {
      lock.releaseLock();
    }
  }
  const session = findSession_(date);
  if (forStudent) {
    return {
      date, level: day.level, lang: day.lang, name: STUDENT.name,
      solo: { title: day.solo.title, text: day.solo.text, kind: day.soloKind,
              questions: day.solo.questions.map((q) => q.question) },
      result: session ? studentResult_(session) : null,
    };
  }
  return { date, level: day.level, lang: day.lang, discussion: day.discussion, discussionKind: day.discussionKind,
           submitted: !!session, note: (findNote_(date) || {}).text || '' };
}

function createDay_(date) {
  const level = currentLevel_();
  const n = Math.floor(Date.parse(date) / 86400000);
  const lang = n % 2 ? 'English' : 'Spanish';
  const discussionKind = Math.floor(n / 2) % 2 ? 'math' : 'story';
  const soloKind = discussionKind === 'story' ? 'math' : 'story';
  const recent = sheet_('Days').getDataRange().getValues().slice(1).slice(-14)
    .map((r) => { try { return JSON.parse(r[4]).title + ' / ' + JSON.parse(r[5]).title; } catch (err) { return ''; } })
    .filter(String);
  const L = LEVELS[level];
  const skills = L[soloKind];

  const system = [
    `You write daily reading comprehension practice for ${STUDENT.name}, a ${STUDENT.age}-year-old autistic boy in ${STUDENT.grade}.`,
    'He can read the words but struggles to say what a text is about and to pull out the relevant information.',
    'Write for him: literal, concrete and clear. No idioms, sarcasm or figures of speech unless the question is about vocabulary.',
    'Use everyday Colombian settings, names and money (pesos), and topics a 12-year-old finds interesting: animals, space, sports, video games, food, inventions, nature.',
    'Every question must be answerable from the text alone. Keep questions short and direct, one thing per question.',
  ].join('\n');

  const user = [
    `Write today's practice in ${lang}. Level ${level} of 5: ${L.focus}.`,
    '',
    `1. Discussion text (read together with his parent): a ${discussionKind === 'story' ? 'short story' : 'math word problem'} of at most 30 words, with 3 short prompts the parent can use to guide the conversation, focused on: ${L.focus}.`,
    `2. Solo text (he reads alone): a ${soloKind === 'story' ? 'short story' : 'math word problem'} of about ${L.words} words, on a different topic from the discussion text.` +
      (soloKind === 'math' ? ` Math: ${L.mathRule}, at a 6th-grade level.` : ''),
    `   Then exactly ${skills.length} questions, in this order, one per skill: ${skills.join(', ')}.`,
    '   Skills: ' + Object.entries(SKILLS).map(([k, v]) => `${k} = ${v}`).join('; ') + '.',
    '   For each question give the expected answer and what a partial answer looks like, for grading.',
    '',
    recent.length ? 'Recent titles, do not repeat their topics: ' + recent.join('; ') : '',
  ].join('\n');

  const schema = {
    type: 'object', additionalProperties: false, required: ['discussion', 'solo'],
    properties: {
      discussion: { type: 'object', additionalProperties: false, required: ['title', 'text', 'prompts'],
        properties: { title: { type: 'string' }, text: { type: 'string' }, prompts: { type: 'array', items: { type: 'string' } } } },
      solo: { type: 'object', additionalProperties: false, required: ['title', 'text', 'questions'],
        properties: { title: { type: 'string' }, text: { type: 'string' },
          questions: { type: 'array', items: { type: 'object', additionalProperties: false,
            required: ['skill', 'question', 'expected', 'partial'],
            properties: { skill: { type: 'string', enum: Object.keys(SKILLS) }, question: { type: 'string' },
                          expected: { type: 'string' }, partial: { type: 'string' } } } } } },
    },
  };

  const out = claude_(system, user, schema);
  if (out.solo.questions.length !== skills.length) throw new Error('The generated practice had the wrong number of questions. Reload to try again.');
  sheet_('Days').appendRow([date, level, lang, discussionKind, JSON.stringify(out.discussion), JSON.stringify(out.solo), soloKind]);
  return { date, level, lang, discussionKind, soloKind, discussion: out.discussion, solo: out.solo };
}

// ---------- Grading ----------

function submit_(answers) {
  const date = dateOf_();
  const day = findDay_(date);
  if (!day) throw new Error('There is no practice for today yet.');
  const existing = findSession_(date);
  if (existing) return studentResult_(existing);
  const qs = day.solo.questions;
  if (!Array.isArray(answers) || answers.length !== qs.length) throw new Error('Answer every question.');

  const system = [
    `You grade reading comprehension answers from ${STUDENT.name}, a ${STUDENT.age}-year-old autistic boy in ${STUDENT.grade}.`,
    'Score each answer 0, 1 or 2: 2 = complete and correct, 1 = partly correct or incomplete, 0 = missing, wrong or off-topic.',
    'Judge understanding, not spelling or grammar. Accept his own words when the meaning is right.',
    `Write feedback for him in ${day.lang}: one or two short, literal, specific sentences per answer. Be warm and calm, never sarcastic.`,
    'For an answer below 2, show him where in the text the answer is or what was missing.',
    `Write the notes for his parent in ${PARENT_LANG}.`,
  ].join('\n');

  const user = [
    `Text (${day.soloKind}, level ${day.level}): "${day.solo.title}"`,
    day.solo.text,
    '',
    ...qs.map((q, i) => [
      `Question ${i + 1} (${q.skill}): ${q.question}`,
      `Expected: ${q.expected}`,
      `Partial looks like: ${q.partial}`,
      `His answer: ${String(answers[i] || '').trim() || '(no answer)'}`,
    ].join('\n')),
  ].join('\n');

  const schema = {
    type: 'object', additionalProperties: false,
    required: ['results', 'encouragement', 'parent_summary', 'discussion_tip'],
    properties: {
      results: { type: 'array', items: { type: 'object', additionalProperties: false,
        required: ['score', 'feedback', 'parent_note'],
        properties: { score: { type: 'integer', enum: [0, 1, 2] }, feedback: { type: 'string' }, parent_note: { type: 'string' } } } },
      encouragement: { type: 'string', description: `One or two calm, specific sentences for him in ${day.lang}.` },
      parent_summary: { type: 'string', description: 'What he did well and where he struggled, in 2-3 sentences.' },
      discussion_tip: { type: 'string', description: 'One concrete idea for the next discussion with him.' },
    },
  };

  const g = claude_(system, user, schema);
  if (g.results.length !== qs.length) throw new Error('Grading failed. Please submit again.');
  const score = g.results.reduce((s, r) => s + r.score, 0) * 10 / (2 * qs.length);
  const grading = { results: g.results, encouragement: g.encouragement, parent_summary: g.parent_summary, discussion_tip: g.discussion_tip };
  const before = currentLevel_();
  sheet_('Sessions').appendRow([date, day.level, day.lang, day.soloKind, score, JSON.stringify(answers), JSON.stringify(grading), new Date()]);
  const after = updateLevel_();
  const session = findSession_(date);
  session.levelChange = after - before;
  return studentResult_(session);
}

function studentResult_(s) {
  return { score: s.score, encouragement: s.grading.encouragement, levelChange: s.levelChange || 0,
           results: s.grading.results.map((r, i) => ({ answer: s.answers[i], score: r.score, feedback: r.feedback })) };
}

// ---------- Levels ----------

function currentLevel_() {
  return Number(PropertiesService.getScriptProperties().getProperty('LEVEL') || 1);
}

function updateLevel_() {
  const props = PropertiesService.getScriptProperties();
  const level = currentLevel_();
  const since = props.getProperty('LEVEL_SINCE') || '';
  const scores = sessions_().filter((s) => s.level === level && s.date >= since).map((s) => s.score);
  const avg = (list) => list.reduce((a, b) => a + b, 0) / list.length;
  let next = level;
  if (scores.length >= 5 && avg(scores.slice(-5)) >= 8) next = Math.min(5, level + 1);
  else if (scores.length >= 3 && avg(scores.slice(-3)) < 5) next = Math.max(1, level - 1);
  if (next !== level) {
    // Sessions from tomorrow on count toward the new level.
    const tomorrow = Utilities.formatDate(new Date(Date.now() + 86400000), TZ, 'yyyy-MM-dd');
    props.setProperties({ LEVEL: String(next), LEVEL_SINCE: tomorrow });
  }
  return next;
}

// ---------- Dashboard and notes ----------

function dashboard_() {
  const props = PropertiesService.getScriptProperties();
  const days = {};
  sheet_('Days').getDataRange().getValues().slice(1).forEach((r) => { days[fmt_(r[0])] = r; });
  const sessions = sessions_().map((s) => {
    const d = days[s.date];
    const solo = d ? JSON.parse(d[5]) : { title: '', text: '', questions: [] };
    return { date: s.date, level: s.level, lang: s.lang, kind: s.kind, score: s.score, title: solo.title, text: solo.text,
             summary: s.grading.parent_summary, tip: s.grading.discussion_tip,
             questions: solo.questions.map((q, i) => ({ skill: q.skill, question: q.question, expected: q.expected,
               answer: s.answers[i], score: (s.grading.results[i] || {}).score, note: (s.grading.results[i] || {}).parent_note })) };
  });
  const notes = sheet_('Notes').getDataRange().getValues().slice(1).map((r) => ({ date: fmt_(r[0]), text: r[1] }));
  return { level: currentLevel_(), levelSince: props.getProperty('LEVEL_SINCE') || '', sessions, notes };
}

function note_(text) {
  const date = dateOf_();
  const sh = sheet_('Notes');
  const rows = sh.getDataRange().getValues();
  for (let i = 1; i < rows.length; i++) {
    if (fmt_(rows[i][0]) === date) { sh.getRange(i + 1, 2).setValue(text); return { saved: true }; }
  }
  sh.appendRow([date, text]);
  return { saved: true };
}

// ---------- Claude ----------

function claude_(system, user, schema) {
  const apiKey = PropertiesService.getScriptProperties().getProperty('ANTHROPIC_API_KEY');
  if (!apiKey) throw new Error('Add ANTHROPIC_API_KEY in Project Settings → Script properties.');
  const res = UrlFetchApp.fetch('https://api.anthropic.com/v1/messages', {
    method: 'post',
    contentType: 'application/json',
    muteHttpExceptions: true,
    headers: { 'x-api-key': apiKey, 'anthropic-version': '2023-06-01', 'anthropic-beta': 'server-side-fallback-2026-07-01' },
    payload: JSON.stringify({
      model: MODEL,
      max_tokens: 8000,
      fallbacks: 'default',
      system,
      messages: [{ role: 'user', content: user }],
      output_config: { effort: 'low', format: { type: 'json_schema', schema } },
    }),
  });
  const body = JSON.parse(res.getContentText());
  if (res.getResponseCode() !== 200) throw new Error('Claude API error ' + res.getResponseCode() + ': ' + ((body.error || {}).message || ''));
  if (body.stop_reason === 'refusal') throw new Error('Claude declined this request. Reload to try again.');
  if (body.stop_reason === 'max_tokens') throw new Error('The answer from Claude was cut off. Reload to try again.');
  const text = body.content.filter((b) => b.type === 'text').map((b) => b.text).join('');
  return JSON.parse(text);
}

// ---------- Sheet helpers ----------

const HEADERS = {
  Days: ['date', 'level', 'language', 'discussion_kind', 'discussion', 'solo', 'solo_kind'],
  Sessions: ['date', 'level', 'language', 'kind', 'score', 'answers', 'grading', 'submitted_at'],
  Notes: ['date', 'note'],
};

function sheet_(name) {
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let sh = ss.getSheetByName(name);
  if (!sh) {
    sh = ss.insertSheet(name);
    sh.appendRow(HEADERS[name]);
    sh.getRange('A:A').setNumberFormat('@');
  }
  return sh;
}

function findDay_(date) {
  const r = sheet_('Days').getDataRange().getValues().slice(1).find((row) => fmt_(row[0]) === date);
  if (!r) return null;
  return { date, level: Number(r[1]), lang: r[2], discussionKind: r[3], discussion: JSON.parse(r[4]), solo: JSON.parse(r[5]), soloKind: r[6] };
}

function sessions_() {
  return sheet_('Sessions').getDataRange().getValues().slice(1).map((r) => ({
    date: fmt_(r[0]), level: Number(r[1]), lang: r[2], kind: r[3], score: Number(r[4]),
    answers: JSON.parse(r[5]), grading: JSON.parse(r[6]),
  }));
}

function findSession_(date) { return sessions_().find((s) => s.date === date) || null; }
function findNote_(date) {
  return sheet_('Notes').getDataRange().getValues().slice(1).map((r) => ({ date: fmt_(r[0]), text: r[1] })).find((n) => n.date === date);
}

function dateOf_() { return Utilities.formatDate(new Date(), TZ, 'yyyy-MM-dd'); }
function fmt_(v) { return v instanceof Date ? Utilities.formatDate(v, TZ, 'yyyy-MM-dd') : String(v); }

function checkKey_(key) { if (key !== FAMILY_KEY) throw new Error('Wrong family key.'); }

function respond_(fn) {
  let out;
  try { out = { ok: true, data: fn() }; } catch (err) { out = { ok: false, error: err.message }; }
  return ContentService.createTextOutput(JSON.stringify(out)).setMimeType(ContentService.MimeType.JSON);
}
