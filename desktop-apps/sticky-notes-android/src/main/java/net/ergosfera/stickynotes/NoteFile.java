package net.ergosfera.stickynotes;

import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Random;
import java.util.TimeZone;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

// The note file format, shared with Sticky Notes for Windows (Program.cs) and Mac (main.swift):
//   color: yellow
//   title: Groceries          (only when the note has a title)
//   modified: 2026-11-05T14:03:22.120Z
//   by: GMKTEC
//   ---
//   the note text, with "[ ] ", "[x] ", "**bold**", "{red}…{/red}", "[#tag]" and "[@2026-10-30]" markers
// A file without that header is a plain-text note. Nothing here uses Android, so the unit tests run on the JVM.
final class NoteFile {
    private NoteFile() {}

    static final class Note {
        String id = "";
        String color = "yellow";
        String title = "";
        String modified = "";
        String by = "";
        String text = "";

        Note copy() {
            Note n = new Note();
            n.id = id; n.color = color; n.title = title; n.modified = modified; n.by = by; n.text = text;
            return n;
        }

        boolean sameContent(Note o) {
            return o != null && color.equals(o.color) && title.equals(o.title) && text.equals(o.text);
        }
    }

    private static final Random RNG = new Random();

    static String now() {
        SimpleDateFormat f = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT);
        f.setTimeZone(TimeZone.getTimeZone("UTC"));
        return f.format(new Date());
    }

    static String newId() {
        SimpleDateFormat f = new SimpleDateFormat("yyyyMMdd-HHmmss", Locale.ROOT);
        f.setTimeZone(TimeZone.getTimeZone("UTC"));
        return f.format(new Date()) + "-" + String.format(Locale.ROOT, "%04x", RNG.nextInt(0x10000));
    }

    // What a deleted note is called in the Trash folder (local time, like the desktop apps)
    static String trashName(String id) {
        return id + " deleted " + new SimpleDateFormat("yyyy-MM-dd HHmmss", Locale.ROOT).format(new Date()) + ".txt";
    }

    static String normalize(String s) {
        return s.replace("\r\n", "\n").replace('\r', '\n');
    }

    static String cleanTitle(String s) {
        return trim(normalize(s).replace('\n', ' '), " \t");
    }

    static String serialize(Note n) {
        StringBuilder sb = new StringBuilder();
        sb.append("color: ").append(n.color).append('\n');
        String title = cleanTitle(n.title);
        if (title.length() > 0) sb.append("title: ").append(title).append('\n');
        sb.append("modified: ").append(n.modified).append('\n');
        sb.append("by: ").append(n.by.replace('\n', ' ')).append('\n');
        sb.append("---\n");
        sb.append(normalize(n.text));
        return sb.toString();
    }

    static Note parse(String id, String content) {
        Note n = new Note();
        n.id = id;
        String s = normalize(content);
        if (s.length() > 0 && s.charAt(0) == '﻿') s = s.substring(1);
        String header, body;
        if (s.startsWith("---\n")) {
            header = "";
            body = s.substring(4);
        } else {
            int sep = s.indexOf("\n---\n");
            if (sep < 0) { n.text = s; return n; }
            header = s.substring(0, sep);
            body = s.substring(sep + 5);
        }
        Map<String, String> fields = new HashMap<>();
        for (String line : header.split("\n", -1)) {
            if (line.length() == 0) continue;
            int colon = line.indexOf(':');
            boolean key = colon > 0;
            for (int i = 0; key && i < colon; i++) key = line.charAt(i) >= 'a' && line.charAt(i) <= 'z';
            if (!key) { n.text = s; return n; } // not a header after all (a plain note with a "---" line in it)
            fields.put(line.substring(0, colon), line.substring(colon + 1).trim());
        }
        String v;
        if ((v = fields.get("color")) != null && v.length() > 0) n.color = v;
        if ((v = fields.get("title")) != null) n.title = v;
        if ((v = fields.get("modified")) != null) n.modified = v;
        if ((v = fields.get("by")) != null) n.by = v;
        n.text = body;
        return n;
    }

    static String trim(String s, String chars) {
        int a = 0, b = s.length();
        while (a < b && chars.indexOf(s.charAt(a)) >= 0) a++;
        while (b > a && chars.indexOf(s.charAt(b - 1)) >= 0) b--;
        return s.substring(a, b);
    }

    // MARK: Markup

    static final char UNCHECKED = '☐', CHECKED = '☑';
    // Text colors besides the usual dark ink
    static final String[] INKS = { "red", "orange", "green", "blue", "purple" };

    static boolean isBox(char c) { return c == UNCHECKED || c == CHECKED; }

    // Is there a box (and its space) at the start of the line beginning at index i?
    static boolean hasBox(CharSequence s, int i) {
        return i >= 0 && i + 1 < s.length() && isBox(s.charAt(i)) && s.charAt(i + 1) == ' ';
    }

    // A run of characters: bold when name is null, otherwise text in that ink
    static final class Span {
        final int start, length;
        final String name;
        Span(int start, int length, String name) { this.start = start; this.length = length; this.name = name; }
    }

    private static void add(List<Span> list, int i, String name) {
        int n = list.size();
        if (n > 0) {
            Span last = list.get(n - 1);
            boolean same = name == null ? last.name == null : name.equals(last.name);
            if (same && last.start + last.length == i) { list.set(n - 1, new Span(last.start, last.length + 1, name)); return; }
        }
        list.add(new Span(i, 1, name));
    }

    // The text as shown (boxes as ☐/☑, markers removed), with its bold runs and ink runs
    static String parseMarkup(String markup, List<Span> bold, List<Span> ink) {
        StringBuilder o = new StringBuilder();
        String[] lines = markup.split("\n", -1);
        for (int n = 0; n < lines.length; n++) {
            if (n > 0) o.append('\n');
            String line = lines[n];
            if (line.startsWith("[ ] ")) { o.append(UNCHECKED).append(' '); line = line.substring(4); }
            else if (line.startsWith("[x] ") || line.startsWith("[X] ")) { o.append(CHECKED).append(' '); line = line.substring(4); }
            int len = line.length();
            boolean[] drop = new boolean[len];
            boolean[] isBold = new boolean[len];
            String[] color = new String[len];

            // "{name}" and "{/name}" pair up within the line; a tag without its partner, or an empty pair, stays as typed
            for (int i = 0; i < len; ) {
                int next = i + 1;
                if (line.charAt(i) == '{')
                    for (String name : INKS) {
                        String open = "{" + name + "}", close = "{/" + name + "}";
                        if (!line.startsWith(open, i)) continue;
                        int c = line.indexOf(close, i + open.length());
                        if (c <= i + open.length()) continue;
                        for (int k = i; k < i + open.length(); k++) drop[k] = true;
                        for (int k = c; k < c + close.length(); k++) drop[k] = true;
                        for (int k = i + open.length(); k < c; k++) color[k] = name;
                        next = c + close.length();
                        break;
                    }
                i = next;
            }

            // "**" marks pair up within the line; an odd one out, or an empty pair, stays as typed
            List<Integer> marks = new ArrayList<>();
            for (int j = 0; j + 1 < len; ) {
                if (line.charAt(j) == '*' && line.charAt(j + 1) == '*') { marks.add(j); j += 2; } else j++;
            }
            if (marks.size() % 2 == 1) marks.remove(marks.size() - 1);
            for (int k = 0; k < marks.size(); k += 2) {
                int a = marks.get(k), b = marks.get(k + 1);
                if (b == a + 2) continue;
                drop[a] = drop[a + 1] = drop[b] = drop[b + 1] = true;
                for (int x = a + 2; x < b; x++) isBold[x] = true;
            }

            for (int x = 0; x < len; x++) {
                if (drop[x]) continue;
                int at = o.length();
                o.append(line.charAt(x));
                if (isBold[x]) add(bold, at, null);
                if (color[x] != null) add(ink, at, color[x]);
            }
        }
        return o.toString();
    }

    // The shown text back as markup
    static String serializeMarkup(String t, List<Span> bold, List<Span> ink) {
        boolean[] isBold = new boolean[t.length()];
        for (Span r : bold)
            for (int i = Math.max(0, r.start); i < Math.min(t.length(), r.start + r.length); i++) isBold[i] = true;
        String[] color = new String[t.length()];
        for (Span r : ink) {
            boolean known = false;
            for (String k : INKS) if (k.equals(r.name)) known = true;
            if (!known) continue;
            for (int i = Math.max(0, r.start); i < Math.min(t.length(), r.start + r.length); i++) color[i] = r.name;
        }
        StringBuilder o = new StringBuilder();
        int start = 0;
        while (start <= t.length()) {
            int end = t.indexOf('\n', start);
            if (end < 0) end = t.length();
            int i = start;
            if (hasBox(t, i) && i + 1 < end) {
                o.append(t.charAt(i) == UNCHECKED ? "[ ] " : "[x] ");
                i += 2;
            }
            // Colored runs get "{name}" and "{/name}" around them, outside any "**"; each line closes its own
            while (i < end) {
                String c = color[i];
                int j = i;
                while (j < end && eq(color[j], c)) j++;
                if (c != null && trim(t.substring(i, j), " \t").length() > 0) {
                    o.append('{').append(c).append('}');
                    writeBold(o, t, isBold, i, j);
                    o.append("{/").append(c).append('}');
                } else writeBold(o, t, isBold, i, j);
                i = j;
            }
            if (end < t.length()) o.append('\n');
            start = end + 1;
        }
        return o.toString();
    }

    private static boolean eq(String a, String b) { return a == null ? b == null : a.equals(b); }

    private static void writeBold(StringBuilder o, String t, boolean[] isBold, int from, int to) {
        for (int i = from; i < to; ) {
            boolean b = isBold[i];
            int j = i;
            while (j < to && isBold[j] == b) j++;
            String piece = t.substring(i, j);
            if (b && trim(piece, " \t").length() > 0) o.append("**").append(piece).append("**"); else o.append(piece);
            i = j;
        }
    }

    // MARK: Tags

    // "[#Delayed]" (a tag from the tag list) or "[@2026-10-30]" (a date); token is what is between the brackets
    static final class Tag {
        final int start, length;
        final String token;
        Tag(int start, int length, String token) { this.start = start; this.length = length; this.token = token; }
    }

    private static final Pattern TAG = Pattern.compile("\\[(#[^\\[\\]{}*|:\\n]{1,24}|@[0-9]{4}-[0-9]{2}-[0-9]{2})\\]");
    private static final String[] MONTHS = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    static List<Tag> findTags(CharSequence text) {
        List<Tag> tags = new ArrayList<>();
        Matcher m = TAG.matcher(text);
        while (m.find()) {
            String token = m.group(1);
            if (token.charAt(0) == '#' && !cleanTagName(token.substring(1)).equals(token.substring(1))) continue;
            if (token.charAt(0) == '@' && date(token) == null) continue;
            tags.add(new Tag(m.start(), m.end() - m.start(), token));
        }
        return tags;
    }

    // "@2026-10-30" as {year, month, day}, if it is a real date
    static int[] date(String token) {
        if (token.length() != 11 || token.charAt(0) != '@') return null;
        String[] p = token.substring(1).split("-");
        if (p.length != 3) return null;
        int y, mo, d;
        try { y = Integer.parseInt(p[0]); mo = Integer.parseInt(p[1]); d = Integer.parseInt(p[2]); }
        catch (NumberFormatException e) { return null; }
        if (mo < 1 || mo > 12) return null;
        boolean leap = y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);
        int days = new int[] { 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 }[mo - 1];
        return d >= 1 && d <= days ? new int[] { y, mo, d } : null;
    }

    static String dateToken(int y, int mo, int d) {
        return String.format(Locale.ROOT, "@%04d-%02d-%02d", y, mo, d);
    }

    // What the label shows: the tag's name, or the date as "Oct 30" (and the year when it isn't this one)
    static String tagLabel(String token, int thisYear) {
        int[] d = date(token);
        if (d == null) return token.substring(1);
        return MONTHS[d[1] - 1] + " " + d[2] + (d[0] == thisYear ? "" : " " + d[0]);
    }

    static String cleanTagName(String s) {
        StringBuilder sb = new StringBuilder();
        for (char c : s.toCharArray()) if ("[]{}*|:\n\r\t".indexOf(c) < 0) sb.append(c);
        String t = trim(sb.toString(), " ");
        if (t.length() > 24) t = t.substring(0, 24);
        return trim(t, " ");
    }

    // The tag list, shared by all the apps: "tags.conf" in the notes folder, one "name: color" a line
    static final String TAGS_FILE = "tags.conf";
    static final String[] TAG_COLORS = { "red", "orange", "yellow", "green", "teal", "blue", "purple", "pink", "gray" };
    private static final int[] TAG_HEX = { 0xC62828, 0xD84315, 0xA16800, 0x2E7D32, 0x00796B, 0x1565C0, 0x6A1B9A, 0xAD1457, 0x616161 };
    static final int DATE_HEX = 0xD32F2F, UNKNOWN_TAG_HEX = 0x757575;

    // Each entry is {name, color}
    static List<String[]> parseTags(String text) {
        List<String[]> defs = new ArrayList<>();
        for (String line : normalize(text).split("\n", -1)) {
            String t = trim(line, " \t");
            int colon = t.lastIndexOf(':');
            if (t.startsWith("#") || colon < 0) continue;
            String name = cleanTagName(t.substring(0, colon));
            String color = trim(t.substring(colon + 1), " \t").toLowerCase(Locale.ROOT);
            if (name.length() == 0 || findTag(defs, name) != null) continue;
            defs.add(new String[] { name, indexOf(TAG_COLORS, color) >= 0 ? color : "gray" });
        }
        return defs;
    }

    static String[] findTag(List<String[]> defs, String name) {
        for (String[] d : defs) if (d[0].equalsIgnoreCase(name)) return d;
        return null;
    }

    static int tagHex(List<String[]> defs, String token) {
        if (token.startsWith("@")) return DATE_HEX;
        String[] d = findTag(defs, token.substring(1));
        int i = d == null ? -1 : indexOf(TAG_COLORS, d[1]);
        return i < 0 ? UNKNOWN_TAG_HEX : TAG_HEX[i];
    }

    // MARK: Colors

    static final String[] NOTE_COLORS = { "yellow", "green", "pink", "purple", "blue", "gray" };
    static final String[] NOTE_LABELS = { "Yellow", "Green", "Pink", "Purple", "Blue", "Gray" };
    private static final int[] BACK_HEX = { 0xFFF7AA, 0xD4F2C4, 0xFFD3E2, 0xE3D7FA, 0xCDE6FF, 0xE6E6E6 };
    private static final int[] HEADER_HEX = { 0xF7E479, 0xB8E3A3, 0xF9B6CE, 0xCDBAF2, 0xA9D3FB, 0xD2D2D2 };
    static final String[] INK_LABELS = { "Black", "Red", "Orange", "Green", "Blue", "Purple" };
    private static final int[] INK_HEX = { 0x222222, 0xB71C1C, 0xA03F00, 0x1B6E20, 0x0D47A1, 0x6A1B9A };

    static int indexOf(String[] a, String s) {
        for (int i = 0; i < a.length; i++) if (a[i].equals(s)) return i;
        return -1;
    }

    static int noteIndex(String color) { return Math.max(0, indexOf(NOTE_COLORS, color)); }
    static int backHex(String color) { return BACK_HEX[noteIndex(color)]; }
    static int headerHex(String color) { return HEADER_HEX[noteIndex(color)]; }

    // 0 is the usual ink, then INKS
    static int inkHex(int k) { return INK_HEX[k < 0 || k >= INK_HEX.length ? 0 : k]; }
    static int inkHex(String name) { return inkHex(indexOf(INKS, name) + 1); }

    // A checked item's text: its color, faded halfway to white
    static int doneHex(int hex) {
        int r = (((hex >> 16) & 0xFF) + 255) / 2, g = (((hex >> 8) & 0xFF) + 255) / 2, b = ((hex & 0xFF) + 255) / 2;
        return (r << 16) | (g << 8) | b;
    }
}
