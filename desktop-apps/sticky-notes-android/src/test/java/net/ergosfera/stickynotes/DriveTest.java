package net.ergosfera.stickynotes;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

import java.io.BufferedInputStream;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;
import org.json.JSONArray;
import org.json.JSONObject;
import org.junit.After;
import org.junit.Before;
import org.junit.Test;

// The app's calls against a stand-in for the Apps Script web app: like Google, it answers each POST
// with a redirect, and the result comes from a GET at the new place
public class DriveTest {
    private ServerSocket server;
    private String base;
    private final Map<String, String[]> files = new HashMap<>(); // id -> {name, text, version}
    private final Map<Integer, String> results = new HashMap<>();
    private final AtomicInteger next = new AtomicInteger();

    @Before public void start() throws Exception {
        files.put("f1", new String[] { "20261101-100000-aaaa.txt", "color: pink\n---\n[ ] caf\u00E9 \u2713", "100" });
        files.put("t", new String[] { "tags.conf", "Urgent: red\n", "5" });
        server = new ServerSocket(0, 50, InetAddress.getByName("127.0.0.1"));
        base = "http://127.0.0.1:" + server.getLocalPort();
        Thread t = new Thread(() -> {
            while (!server.isClosed()) {
                try (Socket c = server.accept()) { serve(c); } catch (Exception ignored) { }
            }
        });
        t.setDaemon(true);
        t.start();
    }

    @After public void stop() throws Exception { server.close(); }

    // One HTTP request per connection, just enough for HttpURLConnection
    private void serve(Socket c) throws Exception {
        InputStream in = new BufferedInputStream(c.getInputStream());
        String[] start = line(in).split(" ");
        int length = 0;
        for (String h; !(h = line(in)).isEmpty(); )
            if (h.toLowerCase().startsWith("content-length:")) length = Integer.parseInt(h.substring(15).trim());
        byte[] body = new byte[length];
        for (int got = 0; got < length; ) got += in.read(body, got, length - got);
        String method = start[0], path = start[1];
        OutputStream out = c.getOutputStream();
        if (method.equals("POST") && path.startsWith("/macros/s/abc/exec")) {
            String q = path.contains("?") ? path.substring(path.indexOf('?') + 1) : null;
            JSONObject res;
            if (q == null || !q.equals("key=0123456789abcdef0123")) res = new JSONObject().put("error", "bad key").put("code", 403);
            else res = handle(new JSONObject(new String(body, StandardCharsets.UTF_8)));
            int n = next.incrementAndGet();
            results.put(n, res.toString());
            out.write(("HTTP/1.1 302 Found\r\nLocation: " + base + "/echo?n=" + n + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
                    .getBytes(StandardCharsets.UTF_8));
        } else if (method.equals("GET") && path.startsWith("/echo?n=")) {
            byte[] b = results.get(Integer.parseInt(path.substring(8))).getBytes(StandardCharsets.UTF_8);
            out.write(("HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + b.length
                    + "\r\nConnection: close\r\n\r\n").getBytes(StandardCharsets.UTF_8));
            out.write(b);
        } else {
            out.write("HTTP/1.1 405 No\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".getBytes(StandardCharsets.UTF_8));
        }
        out.flush();
    }

    private static String line(InputStream in) throws java.io.IOException {
        ByteArrayOutputStream b = new ByteArrayOutputStream();
        for (int ch; (ch = in.read()) >= 0 && ch != '\n'; ) if (ch != '\r') b.write(ch);
        return b.toString("UTF-8");
    }

    private JSONObject item(String id) throws Exception {
        String[] f = files.get(id);
        return new JSONObject().put("id", id).put("name", f[0]).put("version", Long.parseLong(f[2])).put("modifiedTime", "x");
    }

    // What Code.gs does, on the map above
    private JSONObject handle(JSONObject r) throws Exception {
        switch (r.getString("op")) {
            case "folder": return new JSONObject().put("id", "folder1");
            case "files": {
                JSONArray a = new JSONArray();
                for (String id : files.keySet()) a.put(item(id));
                return new JSONObject().put("files", a);
            }
            case "meta": return item(r.getString("id"));
            case "get": {
                JSONObject t = new JSONObject();
                JSONArray ids = r.getJSONArray("ids");
                for (int i = 0; i < ids.length(); i++) t.put(ids.getString(i), files.get(ids.getString(i))[1]);
                return new JSONObject().put("texts", t);
            }
            case "update": {
                String[] f = files.get(r.getString("id"));
                if (f == null) return new JSONObject().put("error", "No item with the given ID could be found").put("code", 404);
                f[1] = r.getString("text");
                f[2] = String.valueOf(Long.parseLong(f[2]) + 1);
                return item(r.getString("id"));
            }
            case "create": {
                String id = "n" + files.size();
                files.put(id, new String[] { r.getString("name"), r.getString("text"), "1" });
                return item(id);
            }
        }
        return new JSONObject().put("error", "unknown").put("code", 400);
    }

    private Drive drive() { return new Drive(base + "/macros/s/abc/exec?key=0123456789abcdef0123"); }

    @Test public void readsAndWritesThroughTheRedirect() throws Exception {
        Drive d = drive();
        assertEquals("folder1", d.findNotesFolder());
        List<Drive.Item> list = d.files("folder1");
        assertEquals(2, list.size());
        Map<String, String> texts = d.downloadAll(Arrays.asList("f1", "t"));
        assertEquals("color: pink\n---\n[ ] café ✓", texts.get("f1")); // UTF-8 both ways
        assertEquals(100, d.metadata("f1").version);
        Drive.Item saved = d.update("f1", "color: blue\n---\nñandú");
        assertEquals(101, saved.version);
        assertEquals("color: blue\n---\nñandú", files.get("f1")[1]);
        Drive.Item made = d.create("folder1", "new.txt", "hi");
        assertEquals("new.txt", made.name);
        assertEquals("hi", d.download(made.id));
    }

    @Test public void errorsKeepTheirCode() throws Exception {
        try { drive().update("gone", "x"); fail(); }
        catch (Drive.Failure f) { assertEquals(404, f.code); }
        try { new Drive(base + "/macros/s/abc/exec?key=ffffffffffffffffffff").findNotesFolder(); fail(); }
        catch (Drive.Failure f) { assertEquals(403, f.code); }
    }

    @Test public void recognizesTheLink() {
        assertTrue(Drive.looksLikeLink("https://script.google.com/macros/s/AKfycbx-1_aB/exec?key=0123456789abcdef0123456789abcdef"));
        assertTrue(Drive.looksLikeLink("https://script.google.com/a/macros/ergosfera.net/s/AKfy/exec?key=0123456789abcdef"));
        assertFalse(Drive.looksLikeLink("https://script.google.com/macros/s/AKfycbx/exec"));
        assertFalse(Drive.looksLikeLink("script.google.com/macros/s/x/exec?key=0123456789abcdef"));
        String good = "https://script.google.com/macros/s/AKfy_x-1/exec?key=0123456789abcdef0123456789abcdef";
        assertEquals(good, Drive.findLink("Your Sticky Notes link (paste it in the phone app; keep it private):\n" + good));
        assertEquals(good, Drive.findLink("  " + good + " \n"));
        assertEquals(good, Drive.findLink("https://script.google.com/macros/s/AKfy_x-1/exec?key=0123456789abcdef\n0123456789abcdef"));
        assertEquals(null, Drive.findLink("hello"));
        String key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        String dev = "https://script.google.com/a/macros/ergosfera.net/s/SCRIPTID/dev?key=" + key;
        String exec = "https://script.google.com/a/macros/ergosfera.net/s/AKfycbDEPLOY/exec";
        assertEquals(null, Drive.findLink(dev));
        assertTrue(Drive.isTestLink(dev));
        assertEquals(exec + "?key=" + key, Drive.findLink(exec + " " + dev));
        assertEquals(exec + "?key=" + key, Drive.findLink(exec + "\n" + key));
        assertEquals(exec + "?key=" + key, Drive.findLink(exec + "?key=" + key.toUpperCase()));
        assertFalse(Drive.isTestLink(exec + " " + dev));
    }
}
