package net.ergosfera.stickynotes;

import android.app.PendingIntent;
import android.content.Context;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import com.google.android.gms.auth.GoogleAuthUtil;
import com.google.android.gms.auth.api.identity.AuthorizationRequest;
import com.google.android.gms.auth.api.identity.AuthorizationResult;
import com.google.android.gms.auth.api.identity.Identity;
import com.google.android.gms.common.api.Scope;
import com.google.android.gms.tasks.Tasks;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

// The notes as last seen on Google Drive, kept on the phone so they open without a connection,
// plus the edits made here that haven't reached Drive yet. Every Drive call runs on one background thread, in order.
final class Store {
    // A note: its file on Drive (fileId is null until it is first uploaded) and its content
    static final class Entry {
        String id;            // the note id, also the file name without ".txt"
        String fileId;        // the Drive file, or null
        long version;         // the Drive version the content was based on
        NoteFile.Note note;   // the note as shown here (with any edits not yet on Drive)
        NoteFile.Note base;   // the note as it was on Drive at that version
        boolean pending;      // edited here, not on Drive yet
    }

    // Sign-in needs the person: show this to them, then sync again
    static final class NeedsSignIn extends Exception {
        final PendingIntent intent;
        NeedsSignIn(PendingIntent intent) { this.intent = intent; }
    }

    interface Done { void done(Exception error); }

    static final String BY = Build.MODEL == null || Build.MODEL.isEmpty() ? "Android" : Build.MODEL;

    private static Store instance;

    static synchronized Store get(Context c) {
        if (instance == null) instance = new Store(c.getApplicationContext());
        return instance;
    }

    private final Context app;
    private final File file;
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private final Handler main = new Handler(Looper.getMainLooper());
    private final Map<String, Entry> notes = new HashMap<>();
    private String folderId, tagsText = "", lastSync = "";
    private long tagsVersion = -1;
    private final List<Runnable> listeners = new ArrayList<>();

    private Store(Context app) {
        this.app = app;
        file = new File(app.getFilesDir(), "notes.json");
        load();
    }

    // MARK: Reading (main thread)

    synchronized List<Entry> all() {
        List<Entry> list = new ArrayList<>(notes.values());
        Collections.sort(list, (a, b) -> modified(b).compareTo(modified(a)));
        return list;
    }

    private static String modified(Entry e) { return e.note.modified == null ? "" : e.note.modified; }

    synchronized Entry find(String id) { return notes.get(id); }

    synchronized List<String[]> tags() { return NoteFile.parseTags(tagsText); }

    synchronized String lastSync() { return lastSync; }

    synchronized int pendingCount() {
        int n = 0;
        for (Entry e : notes.values()) if (e.pending) n++;
        return n;
    }

    void listen(Runnable r) { listeners.add(r); }
    void unlisten(Runnable r) { listeners.remove(r); }

    private void changed() {
        main.post(() -> { for (Runnable r : new ArrayList<>(listeners)) r.run(); });
    }

    // MARK: Changes (main thread; they reach Drive on the next push)

    synchronized Entry create(String color) {
        Entry e = new Entry();
        e.id = NoteFile.newId();
        e.note = new NoteFile.Note();
        e.note.id = e.id;
        e.note.color = color;
        e.note.modified = NoteFile.now();
        e.note.by = BY;
        e.pending = false; // reaches Drive with its first edit; left empty, it is dropped
        notes.put(e.id, e);
        persist();
        changed();
        return e;
    }

    // Keeps an edit here; push() takes it to Drive
    synchronized void edit(String id, NoteFile.Note n) {
        Entry e = notes.get(id);
        if (e == null || e.note.sameContent(n)) return;
        e.note = n.copy();
        e.note.id = id;
        e.note.modified = NoteFile.now();
        e.note.by = BY;
        e.pending = true;
        persist();
        changed();
    }

    void delete(String id, Done done) {
        run(() -> {
            Entry e;
            synchronized (this) { e = notes.get(id); }
            if (e == null) return;
            if (e.fileId != null) {
                withDrive(d -> {
                    String folder = folder(d);
                    String trash = d.findOrCreateFolder(folder, "Trash");
                    try { d.move(e.fileId, folder, trash, NoteFile.trashName(e.id)); }
                    catch (Drive.Failure f) { if (f.code != 404) throw f; } // already gone
                    return null;
                });
            }
            synchronized (this) { notes.remove(id); persist(); }
        }, done);
    }

    // MARK: Sync with Drive

    // Sends the edits made here, then reads every note that changed on Drive
    void sync(Done done) {
        run(() -> {
            withDrive(d -> { pushAll(d); pull(d); return null; });
            synchronized (this) { lastSync = NoteFile.now(); persist(); }
        }, done);
    }

    // Sends the edits made here, without reading the rest
    void push(Done done) {
        run(() -> withDrive(d -> { pushAll(d); return null; }), done);
    }

    private interface Work { void run() throws Exception; }
    private interface DriveWork { Object run(Drive d) throws Exception; }

    private void run(Work w, Done done) {
        worker.execute(() -> {
            Exception error = null;
            try { w.run(); } catch (Exception ex) { error = ex; }
            final Exception err = error;
            changed();
            if (done != null) main.post(() -> done.done(err));
        });
    }

    // Runs work with a signed-in Drive; once more with a fresh token if the old one ran out
    private Object withDrive(DriveWork w) throws Exception {
        String token = token();
        try { return w.run(new Drive(token)); }
        catch (Drive.Failure f) {
            if (f.code != 401) throw f;
            GoogleAuthUtil.clearToken(app, token);
            return w.run(new Drive(token()));
        }
    }

    private String token() throws Exception {
        AuthorizationRequest req = AuthorizationRequest.builder()
                .setRequestedScopes(Collections.singletonList(new Scope(Drive.SCOPE))).build();
        AuthorizationResult r;
        try { r = Tasks.await(Identity.getAuthorizationClient(app).authorize(req)); }
        catch (ExecutionException ex) { throw ex.getCause() instanceof Exception ? (Exception) ex.getCause() : ex; }
        if (r.hasResolution()) throw new NeedsSignIn(r.getPendingIntent());
        if (r.getAccessToken() == null) throw new IOException("Google didn't give access to Drive");
        return r.getAccessToken();
    }

    private String folder(Drive d) throws IOException {
        synchronized (this) { if (folderId != null) return folderId; }
        String id = d.findNotesFolder();
        if (id == null) throw new IOException("There is no StickyNotes folder in this Google Drive. Is it the account the computers use?");
        synchronized (this) { folderId = id; persist(); }
        return id;
    }

    private void pushAll(Drive d) throws Exception {
        List<Entry> todo = new ArrayList<>();
        synchronized (this) { for (Entry e : notes.values()) if (e.pending) todo.add(e); }
        for (Entry e : todo) pushOne(d, e);
    }

    // Saves one note. If it changed on Drive since it was read here, Drive's version is kept as a new
    // note next to it first, the way the desktop apps do when both computers edit the same note.
    private void pushOne(Drive d, Entry e) throws Exception {
        String folder = folder(d);
        NoteFile.Note mine, base;
        String fileId;
        long version;
        synchronized (this) { mine = e.note.copy(); base = e.base; fileId = e.fileId; version = e.version; }
        Drive.Item saved = null;
        if (fileId != null) {
            try {
                Drive.Item now = d.metadata(fileId);
                if (now.version != version) {
                    NoteFile.Note theirs = NoteFile.parse(e.id, d.download(fileId));
                    if (!theirs.sameContent(base) && !theirs.sameContent(mine)) keepCopy(d, folder, theirs);
                }
                saved = d.update(fileId, NoteFile.serialize(mine));
            } catch (Drive.Failure f) {
                if (f.code != 404) throw f; // deleted on another computer while edited here: keep it
            }
        }
        if (saved == null) saved = d.create(folder, e.id + ".txt", NoteFile.serialize(mine));
        synchronized (this) {
            e.fileId = saved.id;
            e.version = saved.version;
            e.base = mine;
            if (e.note.sameContent(mine)) e.pending = false; // otherwise edited again meanwhile: send that next time
            persist();
        }
    }

    private void keepCopy(Drive d, String folder, NoteFile.Note theirs) throws IOException {
        NoteFile.Note copy = theirs.copy();
        copy.id = NoteFile.newId();
        Drive.Item item = d.create(folder, copy.id + ".txt", NoteFile.serialize(copy));
        Entry c = new Entry();
        c.id = copy.id;
        c.fileId = item.id;
        c.version = item.version;
        c.note = copy;
        c.base = copy.copy();
        synchronized (this) { notes.put(c.id, c); }
    }

    private void pull(Drive d) throws Exception {
        String folder;
        List<Drive.Item> files;
        try {
            folder = folder(d);
            files = d.files(folder);
        } catch (Drive.Failure f) {
            if (f.code != 404) throw f;
            synchronized (this) { folderId = null; } // the folder moved: look for it again
            folder = folder(d);
            files = d.files(folder);
        }

        Set<String> present = new HashSet<>();
        List<Drive.Item> changed = new ArrayList<>();
        Drive.Item tagsFile = null;
        synchronized (this) {
            for (Drive.Item f : files) {
                if (f.name.equals(NoteFile.TAGS_FILE)) { tagsFile = f; continue; }
                if (!f.name.endsWith(".txt") || f.name.startsWith(".")) continue;
                String id = f.name.substring(0, f.name.length() - 4);
                present.add(id);
                Entry e = notes.get(id);
                if (e == null || e.version != f.version || !f.id.equals(e.fileId)) changed.add(f);
            }
        }

        // Downloads the changed notes a few at a time
        ExecutorService pool = Executors.newFixedThreadPool(6);
        List<Future<String>> texts = new ArrayList<>();
        for (Drive.Item f : changed) texts.add(pool.submit(() -> d.download(f.id)));
        Map<Drive.Item, String> got = new HashMap<>();
        try {
            for (int i = 0; i < changed.size(); i++) got.put(changed.get(i), texts.get(i).get());
        } catch (ExecutionException ex) {
            throw ex.getCause() instanceof Exception ? (Exception) ex.getCause() : ex;
        } finally { pool.shutdown(); }
        String tags = null;
        if (tagsFile != null && tagsFile.version != tagsVersion) tags = d.download(tagsFile.id);

        synchronized (this) {
            for (Map.Entry<Drive.Item, String> g : got.entrySet()) {
                Drive.Item f = g.getKey();
                String id = f.name.substring(0, f.name.length() - 4);
                NoteFile.Note n = NoteFile.parse(id, g.getValue());
                Entry e = notes.get(id);
                if (e == null) { e = new Entry(); e.id = id; notes.put(id, e); }
                else if (e.pending) {
                    // changed on Drive and here since the last push: Drive's version becomes its own note
                    if (!n.sameContent(e.base) && !n.sameContent(e.note)) {
                        try { keepCopy(d, folder, n); } catch (IOException ignored) { }
                    }
                    e.fileId = f.id;
                    e.version = f.version;
                    e.base = n;
                    continue;
                }
                e.fileId = f.id;
                e.version = f.version;
                e.note = n;
                e.base = n.copy();
            }
            // Notes deleted on another computer go away here too, unless they have edits that haven't been sent
            for (String id : new ArrayList<>(notes.keySet())) {
                Entry e = notes.get(id);
                if (!present.contains(id) && e.fileId != null && !e.pending) notes.remove(id);
                else if (!present.contains(id) && e.fileId != null) { e.fileId = null; e.version = 0; }
            }
            if (tags != null) { tagsText = tags; tagsVersion = tagsFile.version; }
            persist();
        }
        // An edited note whose file was deleted elsewhere is created again
        pushAll(d);
    }

    // Forget everything: the next sync reads the notes again (after switching accounts)
    synchronized void reset() {
        notes.clear();
        folderId = null;
        tagsText = "";
        tagsVersion = -1;
        lastSync = "";
        persist();
        changed();
    }

    // MARK: Saved on the phone

    private synchronized void persist() {
        try {
            JSONObject o = new JSONObject();
            o.put("folderId", folderId == null ? JSONObject.NULL : folderId);
            o.put("tags", tagsText);
            o.put("tagsVersion", tagsVersion);
            o.put("lastSync", lastSync);
            JSONArray arr = new JSONArray();
            for (Entry e : notes.values()) {
                JSONObject j = new JSONObject();
                j.put("id", e.id);
                j.put("fileId", e.fileId == null ? JSONObject.NULL : e.fileId);
                j.put("version", e.version);
                j.put("note", NoteFile.serialize(e.note));
                if (e.base != null) j.put("base", NoteFile.serialize(e.base));
                j.put("pending", e.pending);
                arr.put(j);
            }
            o.put("notes", arr);
            File tmp = new File(file.getPath() + ".tmp");
            try (FileOutputStream out = new FileOutputStream(tmp)) { out.write(o.toString().getBytes(StandardCharsets.UTF_8)); }
            if (!tmp.renameTo(file)) throw new IOException("rename");
        } catch (JSONException | IOException ignored) { }
    }

    private void load() {
        if (!file.exists()) return;
        try {
            JSONObject o = new JSONObject(new String(Files.readAllBytes(file.toPath()), StandardCharsets.UTF_8));
            folderId = o.isNull("folderId") ? null : o.optString("folderId");
            tagsText = o.optString("tags");
            tagsVersion = o.optLong("tagsVersion", -1);
            lastSync = o.optString("lastSync");
            JSONArray arr = o.optJSONArray("notes");
            for (int i = 0; arr != null && i < arr.length(); i++) {
                JSONObject j = arr.getJSONObject(i);
                Entry e = new Entry();
                e.id = j.getString("id");
                e.fileId = j.isNull("fileId") ? null : j.optString("fileId");
                e.version = j.optLong("version");
                e.note = NoteFile.parse(e.id, j.getString("note"));
                e.base = j.has("base") ? NoteFile.parse(e.id, j.getString("base")) : null;
                e.pending = j.optBoolean("pending");
                notes.put(e.id, e);
            }
        } catch (JSONException | IOException ignored) { }
    }
}
