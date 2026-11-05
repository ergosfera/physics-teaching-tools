package net.ergosfera.stickynotes;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import org.junit.Test;

// The same cases as StickyNotes.exe --selftest and the Mac app's --selftest, so all three apps read and write the same files
public class NoteFileTest {
    private static List<NoteFile.Span> spans(NoteFile.Span... s) { return new ArrayList<>(Arrays.asList(s)); }
    private static NoteFile.Span bold(int start, int length) { return new NoteFile.Span(start, length, null); }
    private static NoteFile.Span ink(int start, int length, String name) { return new NoteFile.Span(start, length, name); }

    private static String parse(String markup) { return NoteFile.parseMarkup(markup, spans(), spans()); }

    private static String roundTrip(String markup) {
        List<NoteFile.Span> b = spans(), i = spans();
        return NoteFile.serializeMarkup(NoteFile.parseMarkup(markup, b, i), b, i);
    }

    @Test public void fileFormat() {
        NoteFile.Note n = new NoteFile.Note();
        n.id = "a"; n.color = "pink"; n.modified = "2026-10-18T10:00:00.000Z"; n.by = "MAC"; n.text = "Hello\r\n---\nworld: yes";
        String file = NoteFile.serialize(n);
        assertEquals("color: pink\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes", file);
        NoteFile.Note back = NoteFile.parse("a", file);
        assertEquals("pink", back.color);
        assertEquals(n.modified, back.modified);
        assertEquals("MAC", back.by);
        assertEquals("Hello\n---\nworld: yes", back.text);
        assertEquals("", back.title);

        NoteFile.Note plain = NoteFile.parse("b", "Just text\r\nline 2");
        assertEquals("Just text\nline 2", plain.text);
        assertEquals("yellow", plain.color);
        assertEquals("", plain.modified);

        NoteFile.Note bom = NoteFile.parse("c", "﻿color: blue\nmodified: x\nby: y\n---\n");
        assertEquals("blue", bom.color);
        assertEquals("", bom.text);
        assertEquals("x", bom.modified);
        assertEquals("y", bom.by);

        NoteFile.Note list = NoteFile.parse("d", "Shopping list\n---\nmilk");
        assertEquals("Shopping list\n---\nmilk", list.text);
        assertEquals("yellow", list.color);

        NoteFile.Note bodyOnly = NoteFile.parse("e", "---\nonly body");
        assertEquals("only body", bodyOnly.text);
        assertEquals("yellow", bodyOnly.color);

        n.title = " Groceries\nweek 2 ";
        String titled = NoteFile.serialize(n);
        assertEquals("color: pink\ntitle: Groceries week 2\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes", titled);
        assertEquals("Groceries week 2", NoteFile.parse("a", titled).title);
    }

    @Test public void checkboxesAndBold() {
        List<NoteFile.Span> b0 = spans();
        assertEquals("☐ milk\n☑ eggs\nplain", NoteFile.parseMarkup("[ ] milk\n[x] eggs\nplain", b0, spans()));
        assertEquals(0, b0.size());
        List<NoteFile.Span> b1 = spans();
        assertEquals("a b c", NoteFile.parseMarkup("a **b** c", b1, spans()));
        assertEquals(1, b1.size());
        assertEquals(2, b1.get(0).start);
        assertEquals(1, b1.get(0).length);
        assertEquals("x ** y", parse("x ** y"));
        assertEquals("****", parse("****"));
        assertEquals("[ ] a\n[x] b", NoteFile.serializeMarkup("☐ a\n☑ b", spans(), spans()));
        assertEquals("**abc** d", NoteFile.serializeMarkup("abc d", spans(bold(0, 3)), spans()));
        assertEquals("a**b**\n**c**d", NoteFile.serializeMarkup("ab\ncd", spans(bold(1, 3)), spans()));
        assertEquals("☐x", NoteFile.serializeMarkup("☐x", spans(), spans()));
        String mixed = "[ ] **buy** milk\n[x] done\n**Title**\nend\n";
        assertEquals(mixed, roundTrip(mixed));
    }

    @Test public void textColors() {
        List<NoteFile.Span> cb = spans(), c1 = spans();
        assertEquals("a b c", NoteFile.parseMarkup("a {red}b{/red} c", cb, c1));
        assertEquals(0, cb.size());
        assertEquals(1, c1.size());
        assertEquals(2, c1.get(0).start);
        assertEquals("red", c1.get(0).name);
        assertEquals("{red}x", parse("{red}x"));
        assertEquals("{pink}x{/pink}", parse("{pink}x{/pink}"));
        assertEquals("{red}{/red}", parse("{red}{/red}"));
        List<NoteFile.Span> c2b = spans(), c2 = spans();
        assertEquals("b", NoteFile.parseMarkup("{blue}**b**{/blue}", c2b, c2));
        assertEquals(1, c2b.size());
        assertEquals("blue", c2.get(0).name);
        assertEquals("{green}ab{/green} cd", NoteFile.serializeMarkup("ab cd", spans(), spans(ink(0, 2, "green"))));
        assertEquals("{red}a**b**{/red}**c**", NoteFile.serializeMarkup("abc", spans(bold(1, 2)), spans(ink(0, 2, "red"))));
        assertEquals("a{purple}b{/purple}\n{purple}c{/purple}d", NoteFile.serializeMarkup("ab\ncd", spans(), spans(ink(1, 3, "purple"))));
        assertEquals("a b", NoteFile.serializeMarkup("a b", spans(), spans(ink(1, 1, "red"))));
        String colored = "[ ] {red}**buy** milk{/red} now\n[x] {orange}done{/orange}\n**Title** {blue}x{/blue}\nend\n";
        assertEquals("☐ buy milk now\n☑ done\nTitle x\nend\n", parse(colored));
        assertEquals(colored, roundTrip(colored));
    }

    @Test public void tags() {
        List<NoteFile.Tag> t1 = NoteFile.findTags("[#On time] go [@2026-10-30]");
        assertEquals(2, t1.size());
        assertEquals(0, t1.get(0).start);
        assertEquals(10, t1.get(0).length);
        assertEquals("#On time", t1.get(0).token);
        assertEquals(14, t1.get(1).start);
        assertEquals(13, t1.get(1).length);
        assertEquals("@2026-10-30", t1.get(1).token);
        assertEquals(0, NoteFile.findTags("[# x] [#a:b] [#] [@2026-13-01] [@2026-02-30] [x]").size());
        assertEquals(1, NoteFile.findTags("[@2028-02-29]").size());
        assertEquals("Oct 30", NoteFile.tagLabel("@2026-10-30", 2026));
        assertEquals("Jan 5 2027", NoteFile.tagLabel("@2027-01-05", 2026));
        assertEquals("Delayed", NoteFile.tagLabel("#Delayed", 2026));
        assertEquals("On time ok", NoteFile.cleanTagName("  On [time]: ok* "));
        String tagged = "[ ] [#Delayed] **lab** report [@2026-10-30]\n[x] {red}[#Done]{/red}";
        assertEquals(tagged, roundTrip(tagged));
        List<String[]> defs = NoteFile.parseTags("# comment\nOn time: green\nDelayed:RED\nbad line\nOdd: plaid\non time: blue\n");
        assertEquals(3, defs.size());
        assertTrue(Arrays.equals(new String[] { "On time", "green" }, defs.get(0)));
        assertTrue(Arrays.equals(new String[] { "Delayed", "red" }, defs.get(1)));
        assertTrue(Arrays.equals(new String[] { "Odd", "gray" }, defs.get(2)));
        assertNull(NoteFile.date("@2026-02-30"));
    }
}
