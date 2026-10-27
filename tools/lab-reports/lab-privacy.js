// "Your data": what the student pages keep and send, in plain words for teenagers.
// Every lab, prelab and math activity loads this file and calls
//   labPrivacy.init({ api, live })   api: the teacher's sheet from the link ('' = none);
//                                    live: answers are sent as soon as they are checked
// It adds a short note under the page's intro, a line next to the Send button, and the full
// explanation in a dialog that both open. It uses the page's own .note and dialog.guide styles.
const labPrivacy = {
  init({ api = '', live = false } = {}) {
    const intro = document.querySelector('main .intro');
    const where = !api
      ? 'This link is not connected to your teacher, so your work <b>stays on this computer</b> unless you download it and hand in the file.'
      : live
        ? 'Your work is saved on this computer, and each answer you check is sent to <b>your teacher\'s Google Sheet</b>, which only your teacher can open.'
        : 'Your work is saved <b>only on this computer</b> until you press <b>Send</b>. Then it goes to <b>your teacher\'s Google Sheet</b>, which only your teacher can open.';
    const note = document.createElement('div');
    note.className = 'note';
    note.style.marginBottom = '20px';
    note.innerHTML = `🛡️ <b>Your data.</b> ${where} Nothing goes to anyone else. <a href="#" data-privacy style="color:var(--accent);font-weight:600">What exactly is saved and sent?</a>`;
    if (intro) intro.after(note); else document.querySelector('main').prepend(note);

    const status = document.getElementById('send-status');
    if (status && api) {
      const line = document.createElement('p');
      line.style.cssText = 'font-size:.82rem;color:var(--muted)';
      line.innerHTML = `🛡️ ${live ? 'Sending' : 'Send'} gives your teacher what you wrote in this ${live ? 'activity' : 'report'}, with your names, group and session. <a href="#" data-privacy style="color:var(--accent);font-weight:600">What exactly?</a>`;
      status.before(line);
    }

    const dlg = document.createElement('dialog');
    dlg.className = 'guide';
    dlg.innerHTML = `
      <div class="guide-head"><h2>🛡️ Your data in this activity</h2><button class="pill-btn" data-privacy-close>Close</button></div>
      <div class="guide-body">
        <section>
          <h3>What is saved</h3>
          <p>Only what you type or choose on this page: your names, group and session, your answers and measurements, any pictures you add when a step asks for them, and the time. Nothing else: no email, no account, no password, no location.</p>
        </section>
        <section>
          <h3>Where it goes</h3>
          <ol>
            <li><b>On this computer</b>, in this browser, as you type, so a reload doesn't lose your work. On a shared computer your report stays here after you finish, in <i>Earlier reports on this computer</i>, so <b>the next people who use this computer can see and download it</b>.</li>
            ${api ? `<li><b>To your teacher's Google Sheet</b>, ${live ? 'each time you check an answer and when you press <b>Send</b>' : 'only when you press <b>Send</b>'}. The sheet is in your teacher's Google account, and only your teacher can open it. Your teacher may show answers to the class on the projector, normally without names.</li>`
              : '<li><b>To your teacher</b> only if you download your file and give it to them. This link is not connected to a sheet.</li>'}
            <li>The pages come from <b>GitHub</b>, the website that stores them. Like any website, it can see that your browser opened a page, but <b>never what you type</b>.</li>
          </ol>
        </section>
        <section>
          <h3>Who never gets it</h3>
          <p>No advertising, no tracking, no other companies, and not the people who made these pages.${document.querySelector('a[href*="desmos.com"]') ? ' If you open Desmos from this page, you are on the Desmos website, under its own rules.' : ''}</p>
        </section>
        <div class="note warn"><b>Tips:</b> write only what the activity asks for. Never type a phone number, an address or a password here. Questions about your data? Ask your teacher.</div>
      </div>`;
    document.body.appendChild(dlg);
    document.addEventListener('click', e => {
      if (e.target.closest('[data-privacy]')) { e.preventDefault(); if (!dlg.open) dlg.showModal(); }
      else if (e.target.closest('[data-privacy-close]') || e.target === dlg) dlg.close();
    });
  },
};
