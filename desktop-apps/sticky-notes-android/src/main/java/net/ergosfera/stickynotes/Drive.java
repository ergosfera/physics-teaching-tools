package net.ergosfera.stickynotes;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

// The calls to your Sticky Notes script (apps-script/Code.gs), which reads and writes the note files
// in your Google Drive. The link includes the script's secret key. Every call blocks: run them off the main thread.
final class Drive {
    // A call that failed; code is like an HTTP status (403 wrong key, 404 file gone)
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

    private final String link;

    Drive(String link) { this.link = link; }

    private static final java.util.regex.Pattern LINK =
            java.util.regex.Pattern.compile("https://script\\.google(usercontent)?\\.com/\\S+?/exec\\?key=[0-9a-f]{16,}");

    // The script's link inside whatever was pasted (the log line around it, spaces, line breaks), or null
    static String findLink(String pasted) {
        if (pasted == null) return null;
        // As pasted, and with every space and line break taken out (a link broken over lines);
        // the longer find keeps the whole key
        String best = null;
        for (String text : new String[] { pasted, pasted.replaceAll("\\s+", "") }) {
            java.util.regex.Matcher m = LINK.matcher(text);
            while (m.find()) if (best == null || m.group().length() > best.length()) best = m.group();
        }
        if (best != null) return best;
        // The web app address (…/exec) and the key pasted separately, for example the Web app URL from
        // "Manage deployments" and then the test link (…/dev?key=…) that showLink sometimes prints
        java.util.regex.Matcher exec = EXEC.matcher(pasted);
        java.util.regex.Matcher key = KEY.matcher(pasted);
        if (exec.find() && key.find()) return exec.group(1) + "?key=" + key.group(1).toLowerCase(java.util.Locale.ROOT);
        return null;
    }

    private static final java.util.regex.Pattern EXEC =
            java.util.regex.Pattern.compile("(https://script\\.google(?:usercontent)?\\.com/[^\\s?]+?/exec)(?![\\w/])");
    private static final java.util.regex.Pattern KEY =
            java.util.regex.Pattern.compile("(?<![0-9a-zA-Z])(?:key=)?([0-9a-fA-F]{32,})(?![0-9a-zA-Z])");

    // A test link (…/dev?key=…) only works signed in on the computer, so the app can't use it
    static boolean isTestLink(String pasted) {
        return pasted != null && pasted.contains("script.google.com") && pasted.contains("/dev") && !EXEC.matcher(pasted).find();
    }

    static boolean looksLikeLink(String s) { return s != null && s.equals(findLink(s)); }

    // The "StickyNotes" folder: the one at the top of My Drive (where the desktop apps put it), otherwise any
    String findNotesFolder() throws IOException {
        JSONObject o = call(op("folder"));
        return o.isNull("id") ? null : o.optString("id");
    }

    // The files (not folders) directly in a folder
    List<Item> files(String folderId) throws IOException {
        JSONArray a = call(op("files").put("folder", folderId)).optJSONArray("files");
        List<Item> out = new ArrayList<>();
        for (int i = 0; a != null && i < a.length(); i++) out.add(Item.of(a.optJSONObject(i)));
        return out;
    }

    Item metadata(String fileId) throws IOException {
        return Item.of(call(op("meta").put("id", fileId)));
    }

    String download(String fileId) throws IOException {
        List<String> one = new ArrayList<>();
        one.add(fileId);
        return downloadAll(one).get(fileId);
    }

    // Several files in one request, so a first sync of many notes is quick
    Map<String, String> downloadAll(List<String> ids) throws IOException {
        Map<String, String> out = new HashMap<>();
        for (int from = 0; from < ids.size(); from += 40) {
            JSONArray batch = new JSONArray();
            for (int i = from; i < Math.min(ids.size(), from + 40); i++) batch.put(ids.get(i));
            JSONObject texts = call(op("get").put("ids", batch)).optJSONObject("texts");
            for (int i = 0; i < batch.length(); i++) {
                String id = batch.optString(i);
                if (texts == null || !texts.has(id)) throw new Failure(404, "The script didn't send " + id);
                out.put(id, texts.optString(id));
            }
        }
        return out;
    }

    Item update(String fileId, String text) throws IOException {
        return Item.of(call(op("update").put("id", fileId).put("text", text)));
    }

    Item create(String folderId, String name, String text) throws IOException {
        return Item.of(call(op("create").put("folder", folderId).put("name", name).put("text", text)));
    }

    // Moves a note to the Trash folder inside the notes folder, under a new name
    void trash(String fileId, String folderId, String newName) throws IOException {
        call(op("trash").put("id", fileId).put("folder", folderId).put("name", newName));
    }

    // MARK: Requests

    // A request body; put() can't fail on these plain values, so it skips the checked exception
    private static final class Body {
        final JSONObject o = new JSONObject();
        Body put(String k, Object v) {
            try { o.put(k, v); } catch (JSONException e) { throw new IllegalArgumentException(e); }
            return this;
        }
    }

    private static Body op(String name) { return new Body().put("op", name); }

    private JSONObject call(Body body) throws IOException {
        byte[] answer = post(link, body.o.toString().getBytes(StandardCharsets.UTF_8));
        String text = new String(answer, StandardCharsets.UTF_8);
        JSONObject o;
        try { o = new JSONObject(text); }
        catch (JSONException e) {
            // Not JSON: usually a Google page (sign-in, or "Sorry, unable to open the file")
            java.util.regex.Matcher t = java.util.regex.Pattern.compile("(?is)<title>(.*?)</title>").matcher(text);
            String page = t.find() ? " Google answered with the page “" + t.group(1).trim() + "”." : "";
            if (text.contains("<html") || text.contains("<HTML") || text.contains("<!DOCTYPE"))
                throw new Failure(401, "The link didn't reach the script." + page + " Check that the script is deployed as a "
                        + "web app with “Who has access: Anyone” (not “Anyone within…” or “Only myself”).");
            throw new IOException("Unexpected answer from the script: " + (text.length() > 120 ? text.substring(0, 120) + "…" : text));
        }
        if (o.has("error")) throw new Failure(o.optInt("code", 500), o.optString("error"));
        return o;
    }

    // Apps Script answers a POST with a redirect to the result, which is then fetched with a GET
    static byte[] post(String url, byte[] body) throws IOException {
        HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
        c.setConnectTimeout(15000);
        c.setReadTimeout(60000);
        c.setInstanceFollowRedirects(false);
        c.setRequestMethod("POST");
        c.setDoOutput(true);
        c.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
        c.setFixedLengthStreamingMode(body.length);
        try (OutputStream os = c.getOutputStream()) { os.write(body); }
        for (int hops = 0; ; hops++) {
            int code = c.getResponseCode();
            if (code >= 300 && code < 400 && hops < 5) {
                String next = c.getHeaderField("Location");
                c.disconnect();
                if (next == null) throw new IOException("The script sent a redirect without a place to go");
                c = (HttpURLConnection) new URL(new URL(url), next).openConnection();
                c.setConnectTimeout(15000);
                c.setReadTimeout(60000);
                c.setInstanceFollowRedirects(false);
                continue;
            }
            InputStream in = code < 400 ? c.getInputStream() : c.getErrorStream();
            byte[] data = in == null ? new byte[0] : readAll(in);
            c.disconnect();
            if (code == 404) throw new Failure(401, "Google doesn't know this link. Paste it again from the script's showLink.");
            if (code >= 400) throw new Failure(code, "The script answered " + code);
            return data;
        }
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
