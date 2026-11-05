package net.ergosfera.stickynotes;

import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.text.Editable;
import android.text.Spannable;
import android.text.SpannableStringBuilder;
import android.text.Spanned;
import android.text.TextPaint;
import android.text.style.CharacterStyle;
import android.text.style.ForegroundColorSpan;
import android.text.style.ReplacementSpan;
import android.text.style.StyleSpan;
import android.text.style.UpdateAppearance;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;

// How a note's text looks: markup to styled text and back
final class Look {
    private Look() {}

    static final int INK = 0xFF222222;

    static int rgb(int hex) { return 0xFF000000 | hex; }

    // Text in one of the note inks; the name is what goes in the file
    static final class Ink extends ForegroundColorSpan {
        final String name;
        Ink(String name) { super(rgb(NoteFile.inkHex(name))); this.name = name; }
    }

    // A checked item: struck through and faded halfway to white (only for showing, never saved)
    static final class Done extends CharacterStyle implements UpdateAppearance {
        @Override public void updateDrawState(TextPaint tp) {
            tp.setStrikeThruText(true);
            tp.setColor(rgb(NoteFile.doneHex(tp.getColor() & 0xFFFFFF)));
        }
    }

    // A tag in the editor: the "[#name]" text stays, on its color with white letters (only for showing)
    static final class TagMark extends CharacterStyle implements UpdateAppearance {
        final int color;
        TagMark(int color) { this.color = color; }
        @Override public void updateDrawState(TextPaint tp) {
            tp.bgColor = color;
            tp.setColor(0xFFFFFFFF);
            tp.setFakeBoldText(true);
        }
    }

    // A tag in the list of notes: a rounded label with the tag's name, or the date as "Oct 30"
    static final class Chip extends ReplacementSpan {
        final String label;
        final int color;
        Chip(String label, int color) { this.label = label; this.color = color; }

        private Paint small(Paint p) {
            Paint q = new Paint(p);
            q.setTextSize(p.getTextSize() * 0.82f);
            q.setTypeface(Typeface.DEFAULT_BOLD);
            return q;
        }

        @Override public int getSize(Paint p, CharSequence text, int start, int end, Paint.FontMetricsInt fm) {
            if (fm != null) p.getFontMetricsInt(fm);
            return Math.round(small(p).measureText(label) + p.getTextSize() * 0.8f);
        }

        @Override public void draw(Canvas c, CharSequence text, int start, int end, float x, int top, int y, int bottom, Paint p) {
            Paint q = small(p);
            float pad = p.getTextSize() * 0.4f, w = q.measureText(label) + 2 * pad;
            Paint.FontMetrics fm = p.getFontMetrics();
            RectF r = new RectF(x + 1, y + fm.ascent + 2, x + w - 1, y + fm.descent - 1);
            int old = q.getColor();
            q.setColor(color);
            c.drawRoundRect(r, pad * 0.8f, pad * 0.8f, q);
            q.setColor(0xFFFFFFFF);
            Paint.FontMetrics qm = q.getFontMetrics();
            c.drawText(label, x + pad, r.centerY() - (qm.ascent + qm.descent) / 2, q);
            q.setColor(old);
        }
    }

    // The note text as it is shown: boxes as ☐/☑, bold and colored runs, and (for the list) tags as labels
    static SpannableStringBuilder render(String markup, List<String[]> tagDefs, boolean chips) {
        List<NoteFile.Span> bold = new ArrayList<>(), ink = new ArrayList<>();
        SpannableStringBuilder s = new SpannableStringBuilder(NoteFile.parseMarkup(markup, bold, ink));
        for (NoteFile.Span b : bold) s.setSpan(new StyleSpan(Typeface.BOLD), b.start, b.start + b.length, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
        for (NoteFile.Span k : ink) s.setSpan(new Ink(k.name), k.start, k.start + k.length, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
        if (chips) {
            int year = Calendar.getInstance().get(Calendar.YEAR);
            for (NoteFile.Tag t : NoteFile.findTags(s))
                s.setSpan(new Chip(NoteFile.tagLabel(t.token, year), rgb(NoteFile.tagHex(tagDefs, t.token))),
                        t.start, t.start + t.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        }
        decorate(s, tagDefs, !chips);
        return s;
    }

    // Puts back the marks that only show things (checked items, tags in the editor) after the text changed
    static void decorate(Spannable s, List<String[]> tagDefs, boolean tagMarks) {
        for (Done d : s.getSpans(0, s.length(), Done.class)) s.removeSpan(d);
        for (TagMark t : s.getSpans(0, s.length(), TagMark.class)) s.removeSpan(t);
        String t = s.toString();
        int start = 0;
        while (start <= t.length()) {
            int end = t.indexOf('\n', start);
            if (end < 0) end = t.length();
            if (NoteFile.hasBox(t, start) && t.charAt(start) == NoteFile.CHECKED && start + 2 < end)
                s.setSpan(new Done(), start + 2, end, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
            start = end + 1;
        }
        if (tagMarks)
            for (NoteFile.Tag g : NoteFile.findTags(t))
                s.setSpan(new TagMark(rgb(NoteFile.tagHex(tagDefs, g.token))), g.start, g.start + g.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
    }

    // The edited text back as markup, from its bold and ink runs
    static String markup(Editable s) {
        List<NoteFile.Span> bold = new ArrayList<>(), ink = new ArrayList<>();
        for (StyleSpan b : s.getSpans(0, s.length(), StyleSpan.class))
            if (b.getStyle() == Typeface.BOLD && s.getSpanEnd(b) > s.getSpanStart(b))
                bold.add(new NoteFile.Span(s.getSpanStart(b), s.getSpanEnd(b) - s.getSpanStart(b), null));
        for (Ink k : s.getSpans(0, s.length(), Ink.class))
            if (s.getSpanEnd(k) > s.getSpanStart(k))
                ink.add(new NoteFile.Span(s.getSpanStart(k), s.getSpanEnd(k) - s.getSpanStart(k), k.name));
        // Later runs win where runs of different inks overlap, as on the desktop
        java.util.Collections.sort(ink, (a, b) -> Integer.compare(a.start, b.start));
        return NoteFile.serializeMarkup(s.toString(), bold, ink);
    }
}
