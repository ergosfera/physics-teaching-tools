// Closing, on the student pages. A teacher can close a lab for one session or for every session, now or
// at a set time (teacher page → ⚙ Setup & data → Accepting reports; Apps Script version 5). From then on
// the sheet answers a report with { ok: false, closed: true, closesAt } and does not save it.
//
//   labClosing.watch(api, lab, session)   on load: a note at the top of the page says until when the
//                                         activity takes reports, or that it is closed
//   labClosing.refused(j) → html          the message for a report the sheet refused (j: its answer)
//
// "Closed" is drawn in solid red with white text, the same in light and dark themes, so nobody misses it.
//
// An older Apps Script answers the status question with an error, and then the page shows nothing.
const labClosing = {
  timer: null,
  RED: 'background:#c62828;color:#fff;border:none;border-radius:8px;padding:14px 18px;font-size:1rem;line-height:1.45',
  async watch(api, lab, session) {
    if (!api) return;
    try {
      const u = new URL(api);
      u.searchParams.set('status', '1');
      u.searchParams.set('lab', lab);
      u.searchParams.set('session', session || '');
      const j = await (await fetch(u)).json();
      if (j.ok && 'closed' in j) this.show(j.closed, j.closesAt);
    } catch (e) {}
  },
  show(closed, closesAt) {
    let el = document.getElementById('closing-note');
    if (!closed && !closesAt) { if (el) el.remove(); return; }
    if (!el) {
      el = document.createElement('div');
      el.id = 'closing-note';
      el.style.marginBottom = '16px';
      document.querySelector('main').prepend(el);
    }
    clearTimeout(this.timer);
    if (closed) {
      el.className = '';
      el.style.cssText = this.RED + ';margin-bottom:16px';
      el.innerHTML = '🔒 <b>Your teacher has closed this activity.</b> It no longer takes reports, so <b>Send</b> won\'t work. Your work is still saved on this computer, and you can download it as a file.';
    } else {
      el.className = 'note';
      el.style.cssText = 'margin-bottom:16px';
      el.innerHTML = `⏰ Your teacher takes reports until <b>${this.when(closesAt)}</b>. Send yours before then.`;
      const ms = new Date(closesAt).getTime() - Date.now();
      if (ms < 2147483647) this.timer = setTimeout(() => this.show(true, closesAt), Math.max(0, ms));
    }
  },
  refused(j) {
    this.show(true, j.closesAt);
    return '<div style="' + this.RED + '">🔒 <b>Not sent: your teacher has closed this activity.</b> Your work is still saved on this computer. If your teacher accepts late work, use <b>Download backup file</b> and give them the file.</div>';
  },
  when: at => new Date(at).toLocaleString([], { weekday: 'long', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }),
};
