# Sticky Notes for Android

Shows the sticky notes from the Windows and Mac apps on an Android phone or tablet, and lets you edit them.
It reads and writes the same `.txt` files in **My Drive/StickyNotes**, through a small script in your Google account.

- The notes appear as colored cards, with checklists, bold text, text colors and tags, as on the computers.
- Tap a note to read or edit it. Tap a box to check it. The bottom buttons make a checklist, bold text,
  colored text, or insert a tag or a date (the tags come from `tags.conf`, made on a computer).
- `+` makes a new note; a long press on a card changes its color or deletes it (deleted notes go to the
  Trash folder, as on the computers).
- Edits are kept on the phone as you type and sent to Drive a few seconds later. Without a connection
  the app shows the notes from the last sync and sends the edits when you're back online.
- If a note changed on a computer while you were editing it on the phone, the computer's version is
  kept as a new note next to it, the same way the desktop apps handle it.

## One-time setup: your Sticky Notes script (no Google Cloud, no credit card)

The app reaches your notes through a small script that runs in your own Google account. It reads and
writes only the files in **StickyNotes**, and only answers requests that carry its secret key.
Use the Google account where your notes are (the one Google Drive for desktop uses).

1. On the computer, open <https://script.google.com/> and click **New project**.
   Name it "Sticky Notes" (click "Untitled project" at the top).
2. Delete what is in the editor, paste the whole of [`apps-script/Code.gs`](apps-script/Code.gs), and save (the disk icon).
3. **Deploy → New deployment**. Click the gear next to "Select type" and choose **Web app**.
   - Execute as: **Me**
   - Who has access: **Anyone** (the key in the link keeps it private).
   - Click **Deploy**, then **Authorize access** and choose your account. Google says it hasn't verified
     the app (it is your own script): click **Advanced → Go to Sticky Notes (unsafe)**, then **Allow**.
4. In the editor, choose **showLink** in the menu next to **Run** at the top, and click **Run**.
   The log at the bottom shows your link: `https://script.google.com/macros/s/…/exec?key=…`
   If Google gives the script its test address instead (with `/dev`), the log shows only the key:
   then copy the **Web app URL** from **Deploy → Manage deployments** (it ends with `/exec`), and in
   the app paste that URL, a space, and the key.
5. Send that link to your phone (email it to yourself, for example), open Sticky Notes on the phone,
   paste it in the box and tap **Connect**.

Keep the link private: anyone with it can read and change your notes. If it gets out, run **newKey**
in the script (the same way as showLink) and paste the new link in the app (**⋮ → Change the script link**).

If you change the script later, deploy it with **Deploy → Manage deployments → Edit (pencil) →
Version: New version**, so the link stays the same.

If "Anyone" isn't offered (a Google Workspace setting), ask the domain's admin to allow it:
"Anyone within …" makes Google ask for a sign-in the app can't do.

## The signing key

Android installs a new version over the old one only if both are signed with the same key.
It is in `local/android/` (not in git): `sticky-notes-release.p12` and its password in
`sticky-notes-keystore-password.txt`. GitHub Actions signs the APK with it from two repository secrets:
`STICKY_KEYSTORE_BASE64` (the `.p12` file in base64) and `STICKY_KEYSTORE_PASSWORD`.

## Building

`./build.sh` runs the unit tests (the same note-format cases as the desktop apps' `--selftest`, and the
calls to the script against a stand-in for it) and
builds `build/StickyNotes.apk`. It needs JDK 17, Gradle 8.10 and the Android SDK; GitHub Actions builds it
on every push and publishes it on the `sticky-notes` release with the desktop apps.

To install it, open the APK on the phone (allow installing from the browser or Files app when asked).
