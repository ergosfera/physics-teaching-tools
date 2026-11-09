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
  Logger.log('Your Sticky Notes link (paste it in the phone app; keep it private):\n' + url + '?key=' + key_());
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

function notesFolder_() {
  var top = DriveApp.getRootFolder().getFoldersByName(FOLDER);
  if (top.hasNext()) return top.next();
  var any = DriveApp.getFoldersByName(FOLDER);
  return any.hasNext() ? any.next() : null;
}

function handle_(r) {
  switch (r.op) {
    case 'folder': {
      var folder = notesFolder_();
      return {id: folder ? folder.getId() : null};
    }
    case 'files': {
      var files = [], it = DriveApp.getFolderById(r.folder).getFiles();
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
      return item_(DriveApp.getFolderById(r.folder).createFile(r.name, r.text, MimeType.PLAIN_TEXT));
    case 'trash': {
      // A deleted note goes to the Trash folder inside the notes folder, as on the computers
      var parent = DriveApp.getFolderById(r.folder), found = parent.getFoldersByName('Trash');
      var trash = found.hasNext() ? found.next() : parent.createFolder('Trash');
      var gone = DriveApp.getFileById(r.id);
      gone.moveTo(trash);
      gone.setName(r.name);
      return {id: r.id};
    }
  }
  return {error: 'unknown op ' + r.op, code: 400};
}
