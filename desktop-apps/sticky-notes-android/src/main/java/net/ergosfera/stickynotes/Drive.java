package net.ergosfera.stickynotes;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

// The few Google Drive v3 calls the app needs. Every call blocks, so run them off the main thread.
final class Drive {
    static final String SCOPE = "https://www.googleapis.com/auth/drive";
    private static final String API = "https://www.googleapis.com/drive/v3/files";
    private static final String UPLOAD = "https://www.googleapis.com/upload/drive/v3/files";
    private static final String FOLDER = "application/vnd.google-apps.folder";
    private static final String FIELDS = "id,name,version,modifiedTime";

    // A call that failed; code is the HTTP status (401 means the sign-in ran out)
    static final class Failure extends IOException {
        final int code;
        Failure(int code, String message) { super(message); this.code = code; }
    }

    static final class Item {
        String id, name, modifiedTime;
        long version;

        static Item of(JSONObject o) {
            Item f = new Item();
            f.id = o.optString("id");
            f.name = o.optString("name");
            f.version = o.optLong("version");
            f.modifiedTime = o.optString("modifiedTime");
            return f;
        }
    }

    private final String token;

    Drive(String token) { this.token = token; }

    // The "StickyNotes" folder: the one at the top of My Drive (where the desktop apps put it), otherwise any
    String findNotesFolder() throws IOException {
        String base = "name = 'StickyNotes' and mimeType = '" + FOLDER + "' and trashed = false";
        List<Item> top = list(base + " and 'root' in parents");
        if (!top.isEmpty()) return top.get(0).id;
        List<Item> any = list(base);
        return any.isEmpty() ? null : any.get(0).id;
    }

    // The files (not folders) directly in a folder
    List<Item> files(String folderId) throws IOException {
        return list("'" + folderId + "' in parents and trashed = false and mimeType != '" + FOLDER + "'");
    }

    Item metadata(String fileId) throws IOException {
        return Item.of(json(request("GET", API + "/" + fileId + "?fields=" + FIELDS + "&supportsAllDrives=true", null, null)));
    }

    String download(String fileId) throws IOException {
        return new String(request("GET", API + "/" + fileId + "?alt=media&supportsAllDrives=true", null, null), StandardCharsets.UTF_8);
    }

    Item update(String fileId, String text) throws IOException {
        return Item.of(json(request("PATCH", UPLOAD + "/" + fileId + "?uploadType=media&fields=" + FIELDS + "&supportsAllDrives=true",
                "text/plain; charset=UTF-8", text.getBytes(StandardCharsets.UTF_8))));
    }

    Item create(String folderId, String name, String text) throws IOException {
        String boundary = "sticky" + System.nanoTime();
        JSONObject meta = new JSONObject();
        try {
            meta.put("name", name);
            meta.put("mimeType", "text/plain");
            meta.put("parents", new JSONArray().put(folderId));
        } catch (JSONException e) { throw new IOException(e); }
        String body = "--" + boundary + "\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n" + meta + "\r\n"
                + "--" + boundary + "\r\nContent-Type: text/plain; charset=UTF-8\r\n\r\n" + text + "\r\n--" + boundary + "--";
        return Item.of(json(request("POST", UPLOAD + "?uploadType=multipart&fields=" + FIELDS + "&supportsAllDrives=true",
                "multipart/related; boundary=" + boundary, body.getBytes(StandardCharsets.UTF_8))));
    }

    String findOrCreateFolder(String parentId, String name) throws IOException {
        List<Item> found = list("name = '" + name + "' and mimeType = '" + FOLDER + "' and '" + parentId + "' in parents and trashed = false");
        if (!found.isEmpty()) return found.get(0).id;
        JSONObject meta = new JSONObject();
        try {
            meta.put("name", name);
            meta.put("mimeType", FOLDER);
            meta.put("parents", new JSONArray().put(parentId));
        } catch (JSONException e) { throw new IOException(e); }
        return json(request("POST", API + "?fields=id&supportsAllDrives=true", "application/json; charset=UTF-8",
                meta.toString().getBytes(StandardCharsets.UTF_8))).optString("id");
    }

    // Moves a file to another folder under a new name (how a deleted note goes to the Trash folder)
    void move(String fileId, String fromFolder, String toFolder, String newName) throws IOException {
        JSONObject meta = new JSONObject();
        try { meta.put("name", newName); } catch (JSONException e) { throw new IOException(e); }
        request("PATCH", API + "/" + fileId + "?addParents=" + toFolder + "&removeParents=" + fromFolder + "&fields=id&supportsAllDrives=true",
                "application/json; charset=UTF-8", meta.toString().getBytes(StandardCharsets.UTF_8));
    }

    private List<Item> list(String q) throws IOException {
        List<Item> out = new ArrayList<>();
        String page = null;
        do {
            String url = API + "?q=" + URLEncoder.encode(q, "UTF-8") + "&pageSize=1000&spaces=drive"
                    + "&includeItemsFromAllDrives=true&supportsAllDrives=true"
                    + "&fields=" + URLEncoder.encode("nextPageToken,files(" + FIELDS + ")", "UTF-8")
                    + (page == null ? "" : "&pageToken=" + URLEncoder.encode(page, "UTF-8"));
            JSONObject o = json(request("GET", url, null, null));
            JSONArray files = o.optJSONArray("files");
            for (int i = 0; files != null && i < files.length(); i++) out.add(Item.of(files.optJSONObject(i)));
            page = o.optString("nextPageToken", null);
            if (page != null && page.isEmpty()) page = null;
        } while (page != null);
        return out;
    }

    private static JSONObject json(byte[] data) throws IOException {
        try { return new JSONObject(new String(data, StandardCharsets.UTF_8)); }
        catch (JSONException e) { throw new IOException("Unexpected answer from Google Drive", e); }
    }

    private byte[] request(String method, String url, String type, byte[] body) throws IOException {
        HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
        c.setConnectTimeout(15000);
        c.setReadTimeout(30000);
        // HttpURLConnection has no PATCH; Google takes it as a POST with this header
        if (method.equals("PATCH")) {
            c.setRequestMethod("POST");
            c.setRequestProperty("X-HTTP-Method-Override", "PATCH");
        } else c.setRequestMethod(method);
        c.setRequestProperty("Authorization", "Bearer " + token);
        if (body != null) {
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", type);
            c.setFixedLengthStreamingMode(body.length);
            try (OutputStream os = c.getOutputStream()) { os.write(body); }
        }
        int code = c.getResponseCode();
        InputStream in = code < 400 ? c.getInputStream() : c.getErrorStream();
        byte[] data = in == null ? new byte[0] : readAll(in);
        c.disconnect();
        if (code >= 400) {
            String message = "Google Drive answered " + code;
            try {
                JSONObject err = new JSONObject(new String(data, StandardCharsets.UTF_8)).optJSONObject("error");
                if (err != null && err.optString("message").length() > 0) message = err.optString("message");
            } catch (JSONException ignored) { }
            throw new Failure(code, message);
        }
        return data;
    }

    private static byte[] readAll(InputStream in) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[16384];
        int n;
        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        in.close();
        return out.toByteArray();
    }
}
