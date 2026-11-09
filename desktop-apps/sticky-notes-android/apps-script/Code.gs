// Sticky Notes for Android: the link between the phone and your Google Drive.
//
// Runs in your own Google account (script.google.com), so no Google Cloud project is needed.
// It reads and writes the note files in My Drive/StickyNotes for the phone app, and only answers
// requests that carry the secret key it makes for you (see showLink below).
//
// Setup (once):
//   1. Paste this whole file into a new project at script.google.com (replace what is there) and save.
//   2. Deploy > New deployment > type "Web app". Execute as: Me. Who has access: Anyone. Deploy,
//      and allow access to your Google Drive when asked (Advanced > Go to … if Google warns about it).
//   3. Choose "showLink" in the function menu at the top and click Run. The log shows your link:
//      send it to your phone (email it to yourself, for example) and paste it in the Sticky Notes app.

var FOLDER = 'StickyNotes';
var FOLDER_MIME = 'application/vnd.google-apps.folder';

// Prints the link to paste in the phone app (makes the secret key the first time)
function showLink() {
  var url = ScriptApp.getService().getUrl();
  if (!url) throw new Error('Deploy the script as a web app first (Deploy > New deployment > Web app), then run showLink again.');
  if (/\/exec$/.test(url)) {
    Logger.log('Your Sticky Notes link (paste it in the phone app; keep it private):\n' + url + '?key=' + key_());
  } else {
    // Run from the editor, Google sometimes gives the test address (…/dev), which the phone can't use
    Logger.log('Open Deploy > Manage deployments and copy the Web app URL (it ends with /exec). '
        + 'In the phone app, paste that URL, a space, and then this key (keep it private):\n' + key_());
  }
}

// Makes a new key: the old link stops working, and showLink prints the new one
function newKey() {
  PropertiesService.getScriptProperties().deleteProperty('key');
  showLink();
}

function key_() {
  var props = PropertiesService.getScriptProperties();
  var k = props.getProperty('key');
  if (!k) {
    k = Utilities.getUuid().replace(/-/g, '') + Utilities.getUuid().replace(/-/g, '');
    props.setProperty('key', k);
  }
  return k;
}

function doGet(e) {
  var ok = e && e.parameter && e.parameter.key === key_();
  return ContentService.createTextOutput(ok
      ? 'Sticky Notes: this link works. Paste it in the Sticky Notes app on your phone.'
      : 'Sticky Notes: this link is missing its key, or the key is wrong.');
}

// Every request from the app: {"op": ..., ...} with the key in the link
function doPost(e) {
  var out;
  try {
    if (!e.parameter || e.parameter.key !== key_()) out = {error: 'bad key', code: 403};
    else out = handle_(JSON.parse(e.postData.contents));
  } catch (err) {
    var m = String(err && err.message || err);
    out = {error: m, code: /not found|no item with the given id|does not exist/i.test(m) ? 404 : 500};
  }
  return ContentService.createTextOutput(JSON.stringify(out)).setMimeType(ContentService.MimeType.JSON);
}

function item_(f) {
  return {id: f.getId(), name: f.getName(), version: f.getLastUpdated().getTime(), modifiedTime: f.getLastUpdated().toISOString()};
}

function text_(f) {
  return f.getBlob().getDataAsString('UTF-8');
}

// The notes folder the computers use. There can be several folders called StickyNotes (an old one in
// My Drive, the one in a computer's backup under "Other computers"...): the one with the most recently
// changed note wins. Remembered for 10 minutes so each request doesn't look again.
function notesFolder_() {
  var cache = CacheService.getScriptCache(), id = cache.get('folder');
  if (id) {
    try { var known = DriveApp.getFolderById(id); if (!known.isTrashed()) return known; } catch (e) { }
  }
  var best = null, bestTime = -1, it = DriveApp.getFoldersByName(FOLDER);
  while (it.hasNext()) {
    var f = it.next();
    if (f.isTrashed()) continue;
    var t = newestNote_(f);
    if (t > bestTime) { best = f; bestTime = t; }
  }
  if (best) cache.put('folder', best.getId(), 600);
  return best;
}

function newestNote_(folder) {
  var newest = 0, files = folder.getFiles();
  while (files.hasNext()) {
    var f = files.next();
    if (/\.txt$/.test(f.getName())) newest = Math.max(newest, f.getLastUpdated().getTime());
  }
  return newest;
}

// Prints which StickyNotes folder the phone uses: run it from the editor to check
function showFolder() {
  CacheService.getScriptCache().remove('folder');
  var f = notesFolder_();
  if (!f) { Logger.log('There is no folder called StickyNotes in this Google Drive.'); return; }
  var path = f.getName(), p = f.getParents();
  for (var i = 0; i < 8 && p.hasNext(); i++) { var up = p.next(); path = up.getName() + ' / ' + path; p = up.getParents(); }
  Logger.log('The phone uses: ' + path + '\nNewest note: ' + new Date(newestNote_(f)));
}

function handle_(r) {
  switch (r.op) {
    case 'folder': {
      var folder = notesFolder_();
      return {id: folder ? folder.getId() : null};
    }
    case 'files': {
      var files = [], it = notesFolder_().getFiles(); // always the current notes folder, whatever the app remembers
      while (it.hasNext()) {
        var f = it.next();
        if (f.getMimeType() !== FOLDER_MIME && !f.isTrashed()) files.push(item_(f));
      }
      return {files: files};
    }
    case 'meta':
      return item_(DriveApp.getFileById(r.id));
    case 'get': {
      // Several files in one request: {"ids": [...]} gives {"texts": {id: text}}
      var texts = {};
      (r.ids || []).forEach(function (id) { texts[id] = text_(DriveApp.getFileById(id)); });
      return {texts: texts};
    }
    case 'update': {
      var file = DriveApp.getFileById(r.id);
      if (file.isTrashed()) return {error: 'deleted', code: 404};
      file.setContent(r.text);
      return item_(DriveApp.getFileById(r.id));
    }
    case 'create':
      return item_(notesFolder_().createFile(r.name, r.text, MimeType.PLAIN_TEXT));
    case 'trash': {
      // A deleted note goes to the Trash folder inside the notes folder, as on the computers
      var parent = notesFolder_(), found = parent.getFoldersByName('Trash');
      var trash = found.hasNext() ? found.next() : parent.createFolder('Trash');
      var gone = DriveApp.getFileById(r.id);
      gone.moveTo(trash);
      gone.setName(r.name);
      return {id: r.id};
    }
  }
  return {error: 'unknown op ' + r.op, code: 400};
}
