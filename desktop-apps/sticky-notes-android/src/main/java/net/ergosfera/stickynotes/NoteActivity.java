package net.ergosfera.stickynotes;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.DatePickerDialog;
import android.graphics.Typeface;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.InputType;
import android.text.Spanned;
import android.text.TextWatcher;
import android.text.style.StyleSpan;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;

// One note, to read and edit. Edits are kept on the phone as you type and sent to Drive a moment later.
public class NoteActivity extends Activity {
    private Store store;
    private String id;
    private String color = "yellow";
    private LinearLayout root, header, tools;
    private EditText title, body;
    private TextView state;
    private boolean applying, dirty, isNew;
    private int newline = -1;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable commit = this::commit;
    private final Runnable send = () -> store.push(e -> showState());
    private final Runnable refresh = this::remoteChanged;

    @Override protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        store = Store.get(this);
        id = getIntent().getStringExtra("id");
        isNew = getIntent().getBooleanExtra("new", false);
        Store.Entry e = store.find(id);
        if (e == null) { finish(); return; }
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE
                | (isNew ? WindowManager.LayoutParams.SOFT_INPUT_STATE_VISIBLE : WindowManager.LayoutParams.SOFT_INPUT_STATE_HIDDEN));

        root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);

        header = new LinearLayout(this);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setPadding(dp(2), dp(4), dp(2), dp(4));
        header.addView(button("‹", "Back", v -> finish(), 30));
        title = new EditText(this);
        title.setHint("Title");
        title.setSingleLine(true);
        title.setTextSize(18);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setTextColor(Look.INK);
        title.setBackground(null);
        title.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        header.addView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        state = new TextView(this);
        state.setTextSize(11);
        state.setTextColor(0x99000000);
        header.addView(state);
        header.addView(button("⋯", "Color or delete", v -> menu(), 24));
        root.addView(header);

        body = new EditText(this);
        body.setGravity(Gravity.TOP | Gravity.START);
        body.setTextSize(16);
        body.setTextColor(Look.INK);
        body.setLineSpacing(0, 1.12f);
        body.setBackground(null);
        body.setPadding(dp(16), dp(10), dp(16), dp(16));
        body.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_MULTI_LINE | InputType.TYPE_TEXT_FLAG_CAP_SENTENCES);
        body.setHorizontallyScrolling(false);
        body.setMaxLines(Integer.MAX_VALUE);
        root.addView(body, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));

        // The buttons the desktop note has: checkbox, bold, text color, tag
        tools = new LinearLayout(this);
        tools.setGravity(Gravity.CENTER_VERTICAL);
        tools.setPadding(dp(6), 0, dp(6), 0);
        tools.addView(tool("☐", "Checklist", v -> toggleBoxes()));
        tools.addView(tool("B", "Bold", v -> toggleBold()));
        tools.addView(tool("A", "Text color", v -> pickInk()));
        tools.addView(tool("#", "Tag or date", v -> pickTag()));
        root.addView(tools, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(48)));
        setContentView(root);

        load(e.note);

        TextWatcher watcher = new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int start, int count, int after) { }
            public void onTextChanged(CharSequence s, int start, int before, int count) {
                if (!applying && before == 0 && count == 1 && s.charAt(start) == '\n') newline = start;
            }
            public void afterTextChanged(Editable s) {
                if (applying) return;
                if (s == body.getText()) {
                    if (newline >= 0) continueList(newline);
                    newline = -1;
                    Look.decorate(body.getText(), store.tags(), true);
                }
                edited();
            }
        };
        body.addTextChangedListener(watcher);
        title.addTextChangedListener(watcher);
        body.setOnTouchListener((v, ev) -> ev.getAction() == MotionEvent.ACTION_UP && tapBox(ev));
        if (isNew) body.requestFocus();
    }

    @Override protected void onResume() {
        super.onResume();
        store.listen(refresh);
        showState();
    }

    @Override protected void onPause() {
        super.onPause();
        store.unlisten(refresh);
        handler.removeCallbacks(commit);
        handler.removeCallbacks(send);
        Store.Entry e = store.find(id);
        if (e == null) return;
        boolean empty = body.getText().toString().trim().isEmpty() && title.getText().toString().trim().isEmpty();
        if (isFinishing() && empty && e.fileId == null) { store.delete(id, null); return; } // a new note left empty
        commit();
        store.push(null);
    }

    int dp(float v) { return Math.round(v * getResources().getDisplayMetrics().density); }

    private TextView button(String text, String description, View.OnClickListener click, int size) {
        TextView b = new TextView(this);
        b.setText(text);
        b.setContentDescription(description);
        b.setTextSize(size);
        b.setTextColor(0xCC000000);
        b.setGravity(Gravity.CENTER);
        TypedValue tv = new TypedValue();
        getTheme().resolveAttribute(android.R.attr.selectableItemBackgroundBorderless, tv, true);
        b.setBackgroundResource(tv.resourceId);
        b.setOnClickListener(click);
        b.setLayoutParams(new LinearLayout.LayoutParams(dp(46), dp(46)));
        return b;
    }

    private TextView tool(String text, String description, View.OnClickListener click) {
        TextView b = button(text, description, click, 20);
        if (text.equals("B")) b.setTypeface(Typeface.DEFAULT_BOLD);
        b.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1));
        // Keep the keyboard and the selection where they are
        b.setFocusable(false);
        return b;
    }

    // MARK: Showing

    private void load(NoteFile.Note n) {
        applying = true;
        color = n.color;
        title.setText(n.title);
        int sel = body.getSelectionStart();
        body.setText(Look.render(n.text, store.tags(), false), TextView.BufferType.EDITABLE);
        if (sel >= 0 && sel <= body.length()) body.setSelection(sel);
        applying = false;
        dirty = false;
        paint();
    }

    private void paint() {
        int back = Look.rgb(NoteFile.backHex(color)), top = Look.rgb(NoteFile.headerHex(color));
        root.setBackgroundColor(back);
        header.setBackgroundColor(top);
        tools.setBackgroundColor(top);
        getWindow().setStatusBarColor(top);
        getWindow().setNavigationBarColor(top);
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR | View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
    }

    private void showState() {
        Store.Entry e = store.find(id);
        state.setText(e == null ? "" : dirty || e.pending ? "not sent yet" : e.fileId != null ? "saved" : "");
    }

    // The note changed on Drive (read by a sync): show it, unless it is being edited here
    private void remoteChanged() {
        Store.Entry e = store.find(id);
        if (e == null) {
            if (!dirty) { Toast.makeText(this, "This note was deleted on another device.", Toast.LENGTH_LONG).show(); finish(); }
            return;
        }
        if (!dirty && !e.pending && !e.note.sameContent(current())) load(e.note);
        showState();
    }

    private NoteFile.Note current() {
        NoteFile.Note n = new NoteFile.Note();
        n.id = id;
        n.color = color;
        n.title = NoteFile.cleanTitle(title.getText().toString());
        n.text = Look.markup(body.getText());
        return n;
    }

    // MARK: Saving

    private void edited() {
        dirty = true;
        showState();
        handler.removeCallbacks(commit);
        handler.postDelayed(commit, 600);
    }

    private void commit() {
        if (!dirty) return;
        store.edit(id, current());
        dirty = false;
        handler.removeCallbacks(send);
        handler.postDelayed(send, 2500);
        showState();
    }

    private void menu() {
        String[] items = new String[NoteFile.NOTE_LABELS.length + 1];
        System.arraycopy(NoteFile.NOTE_LABELS, 0, items, 0, NoteFile.NOTE_LABELS.length);
        items[items.length - 1] = "Delete note";
        new AlertDialog.Builder(this).setItems(items, (d, which) -> {
            if (which < NoteFile.NOTE_COLORS.length) {
                color = NoteFile.NOTE_COLORS[which];
                paint();
                edited();
            } else {
                commit();
                Store.Entry e = store.find(id);
                if (e != null) MainActivity.confirmDelete(this, store, e, this::finish);
            }
        }).show();
    }

    // MARK: Checklists

    private int lineStart(CharSequence s, int i) {
        while (i > 0 && s.charAt(i - 1) != '\n') i--;
        return i;
    }

    // Enter on a checklist item starts the next item; Enter on an empty item ends the list
    private void continueList(int nl) {
        Editable s = body.getText();
        int ls = lineStart(s, nl);
        if (!NoteFile.hasBox(s, ls)) return;
        applying = true;
        if (ls + 2 == nl) {
            s.delete(ls, nl + 1);
        } else {
            s.insert(nl + 1, NoteFile.UNCHECKED + " ");
            // a new item isn't bold or colored just because the last one was
            clear(s, StyleSpan.class, nl + 1, nl + 3);
            clear(s, Look.Ink.class, nl + 1, nl + 3);
        }
        applying = false;
    }

    // A tap on a box checks or unchecks it
    private boolean tapBox(MotionEvent ev) {
        if (body.getLayout() == null) return false;
        int off = body.getOffsetForPosition(ev.getX(), ev.getY());
        Editable s = body.getText();
        if (off < 0 || off > s.length()) return false;
        int ls = lineStart(s, Math.min(off, s.length()));
        if (!NoteFile.hasBox(s, ls) || off > ls + 1) return false;
        // Only when the tap is on the box itself, not just near the start of the line
        float boxRight = body.getLayout().getPrimaryHorizontal(ls + 1) + body.getTotalPaddingLeft() - body.getScrollX();
        if (ev.getX() > boxRight + dp(6)) return false;
        char c = s.charAt(ls) == NoteFile.UNCHECKED ? NoteFile.CHECKED : NoteFile.UNCHECKED;
        int selA = body.getSelectionStart(), selB = body.getSelectionEnd();
        s.replace(ls, ls + 1, String.valueOf(c));
        if (selA >= 0) body.setSelection(selA, selB);
        return true;
    }

    // Makes the selected lines checklist items, or plain lines again when they all already are
    private void toggleBoxes() {
        Editable s = body.getText();
        int a = Math.max(0, Math.min(body.getSelectionStart(), body.getSelectionEnd()));
        int b = Math.max(0, Math.max(body.getSelectionStart(), body.getSelectionEnd()));
        List<Integer> starts = new ArrayList<>();
        for (int ls = lineStart(s, a); ; ) {
            starts.add(ls);
            int nl = s.toString().indexOf('\n', ls);
            if (nl < 0 || nl >= b) break;
            ls = nl + 1;
        }
        boolean all = true;
        for (int ls : starts) all &= NoteFile.hasBox(s, ls);
        for (int i = starts.size() - 1; i >= 0; i--) {
            int ls = starts.get(i);
            if (all) s.delete(ls, ls + 2);
            else if (!NoteFile.hasBox(s, ls)) s.insert(ls, NoteFile.UNCHECKED + " ");
        }
    }

    // MARK: Bold and colors

    private boolean hasSelection() {
        if (body.getSelectionStart() != body.getSelectionEnd()) return true;
        Toast.makeText(this, "Select some text first.", Toast.LENGTH_SHORT).show();
        return false;
    }

    private void toggleBold() {
        if (!hasSelection()) return;
        Editable s = body.getText();
        int a = Math.min(body.getSelectionStart(), body.getSelectionEnd()), b = Math.max(body.getSelectionStart(), body.getSelectionEnd());
        boolean[] bold = new boolean[b - a];
        for (StyleSpan sp : s.getSpans(a, b, StyleSpan.class))
            if (sp.getStyle() == Typeface.BOLD)
                for (int i = Math.max(a, s.getSpanStart(sp)); i < Math.min(b, s.getSpanEnd(sp)); i++) bold[i - a] = true;
        boolean all = true;
        for (boolean x : bold) all &= x;
        clear(s, StyleSpan.class, a, b);
        if (!all) s.setSpan(new StyleSpan(Typeface.BOLD), a, b, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
        edited();
    }

    private void pickInk() {
        if (!hasSelection()) return;
        new AlertDialog.Builder(this).setTitle("Text color").setItems(NoteFile.INK_LABELS, (d, k) -> {
            Editable s = body.getText();
            int a = Math.min(body.getSelectionStart(), body.getSelectionEnd()), b = Math.max(body.getSelectionStart(), body.getSelectionEnd());
            clear(s, Look.Ink.class, a, b);
            if (k > 0) s.setSpan(new Look.Ink(NoteFile.INKS[k - 1]), a, b, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
            edited();
        }).show();
    }

    // Takes spans of a kind off [a, b), keeping their parts outside it
    private static <T> void clear(Editable s, Class<T> kind, int a, int b) {
        for (T sp : s.getSpans(a, b, kind)) {
            if (sp instanceof StyleSpan && ((StyleSpan) sp).getStyle() != Typeface.BOLD) continue;
            int st = s.getSpanStart(sp), en = s.getSpanEnd(sp);
            if (en <= a || st >= b) continue;
            s.removeSpan(sp);
            if (st < a) s.setSpan(copy(sp), st, a, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
            if (en > b) s.setSpan(copy(sp), b, en, Spanned.SPAN_EXCLUSIVE_INCLUSIVE);
        }
    }

    private static Object copy(Object sp) {
        if (sp instanceof Look.Ink) return new Look.Ink(((Look.Ink) sp).name);
        return new StyleSpan(Typeface.BOLD);
    }

    // MARK: Tags

    private void pickTag() {
        List<String[]> defs = store.tags();
        String[] items = new String[defs.size() + 2];
        for (int i = 0; i < defs.size(); i++) items[i] = defs.get(i)[0];
        items[defs.size()] = "Today’s date";
        items[defs.size() + 1] = "Pick a date…";
        new AlertDialog.Builder(this).setTitle("Insert a tag").setItems(items, (d, which) -> {
            Calendar c = Calendar.getInstance();
            if (which < defs.size()) insert("[#" + defs.get(which)[0] + "]");
            else if (which == defs.size()) insert("[" + NoteFile.dateToken(c.get(Calendar.YEAR), c.get(Calendar.MONTH) + 1, c.get(Calendar.DAY_OF_MONTH)) + "]");
            else new DatePickerDialog(this, (p, y, m, day) -> insert("[" + NoteFile.dateToken(y, m + 1, day) + "]"),
                    c.get(Calendar.YEAR), c.get(Calendar.MONTH), c.get(Calendar.DAY_OF_MONTH)).show();
        }).show();
        if (defs.isEmpty()) Toast.makeText(this, "Tags are made in Sticky Notes on a computer.", Toast.LENGTH_SHORT).show();
    }

    private void insert(String tag) {
        Editable s = body.getText();
        int at = Math.max(0, Math.max(body.getSelectionStart(), body.getSelectionEnd()));
        String text = (at > 0 && s.charAt(at - 1) != ' ' && s.charAt(at - 1) != '\n' ? " " : "") + tag + " ";
        s.insert(at, text);
        clear(s, StyleSpan.class, at, at + text.length());
        clear(s, Look.Ink.class, at, at + text.length());
        body.setSelection(at + text.length());
        body.requestFocus();
        ((InputMethodManager) getSystemService(INPUT_METHOD_SERVICE)).showSoftInput(body, 0);
    }
}
