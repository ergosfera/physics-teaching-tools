# Sticky Notes for Android

Shows the sticky notes from the Windows and Mac apps on an Android phone or tablet, and lets you edit them.
It reads and writes the same `.txt` files in **My Drive/StickyNotes** through the Google Drive API.

- The notes appear as colored cards, with checklists, bold text, text colors and tags, as on the computers.
- Tap a note to read or edit it. Tap a box to check it. The bottom buttons make a checklist, bold text,
  colored text, or insert a tag or a date (the tags come from `tags.conf`, made on a computer).
- `+` makes a new note; a long press on a card changes its color or deletes it (deleted notes go to the
  Trash folder, as on the computers).
- Edits are kept on the phone as you type and sent to Drive a few seconds later. Without a connection
  the app shows the notes from the last sync and sends the edits when you're back online.
- If a note changed on a computer while you were editing it on the phone, the computer's version is
  kept as a new note next to it, the same way the desktop apps handle it.

## One-time Google setup

Google only lets an app read your Drive after you register it in a (free) Google Cloud project.
Use the same Google account as the computers.

1. Open <https://console.cloud.google.com/>, and create a project (for example "Sticky Notes").
2. **APIs & Services → Library**: find **Google Drive API** and click **Enable**.
3. **APIs & Services → OAuth consent screen** (also called **Google Auth Platform**):
   - App name "Sticky Notes", your email as support and developer contact, audience **External**.
   - Under **Audience → Test users**, add your own Gmail address. Leave the app in **Testing**.
4. **Clients** (or **Credentials → Create credentials → OAuth client ID**):
   - Application type **Android**.
   - Package name: `net.ergosfera.stickynotes`
   - SHA-1 certificate fingerprint: `72:2F:87:78:AA:C0:3F:B4:BC:1F:24:2C:5C:3A:14:5D:E0:6A:8E:0B`
   - Create. Nothing has to be copied back into the app: Google recognizes it by the package and the key.

The first time the app opens, Google says the app isn't verified; tap **Continue** and allow access to
Google Drive. Apps in testing mode may ask again now and then.

## The signing key

The SHA-1 above belongs to the key that signs the APK. It is in `local/android/` (not in git):
`sticky-notes-release.p12` and its password in `sticky-notes-keystore-password.txt`. Keep a copy:
an APK signed with any other key can't sign in until its SHA-1 is added to the Google Cloud client.

GitHub Actions signs the APK with it from two repository secrets: `STICKY_KEYSTORE_BASE64`
(the `.p12` file in base64) and `STICKY_KEYSTORE_PASSWORD`.

## Building

`./build.sh` runs the unit tests (the same note-format cases as the desktop apps' `--selftest`) and
builds `build/StickyNotes.apk`. It needs JDK 17, Gradle 8.10 and the Android SDK; GitHub Actions builds it
on every push and publishes it on the `sticky-notes` release with the desktop apps.

To install it, open the APK on the phone (allow installing from the browser or Files app when asked).
