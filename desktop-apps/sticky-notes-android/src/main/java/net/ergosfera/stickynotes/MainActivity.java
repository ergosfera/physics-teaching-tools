package net.ergosfera.stickynotes;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

// The notes, as cards in columns. Tap one to read or edit it; "+" makes a new one.
public class MainActivity extends Activity {
    static final int BAR = 0xFF3A3A3A;

    private Store store;
    private LinearLayout columns;
    private TextView status;
    private EditText search;
    private View message;
    private TextView messageText;
    private Button messageButton;
    private EditText linkField;
    private boolean syncing;
    private final Runnable refresh = this::show;

    @Override protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        store = Store.get(this);
        getWindow().setStatusBarColor(BAR);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(0xFFF2F0EB);

        // Top bar: name, status, sync and new
        LinearLayout bar = new LinearLayout(this);
        bar.setBackgroundColor(BAR);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        bar.setPadding(dp(16), dp(6), dp(4), dp(6));
        LinearLayout names = new LinearLayout(this);
        names.setOrientation(LinearLayout.VERTICAL);
        TextView title = new TextView(this);
        title.setText("Sticky Notes");
        title.setTextColor(0xFFFFFFFF);
        title.setTextSize(20);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        status = new TextView(this);
        status.setTextColor(0xFFC8C8C8);
        status.setTextSize(12);
        names.addView(title);
        names.addView(status);
        bar.addView(names, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        bar.addView(barButton("↻", "Sync now", v -> sync(true)));
        bar.addView(barButton("+", "New note", v -> newNote()));
        bar.addView(barButton("\u22EE", "Settings", v -> new AlertDialog.Builder(this)
                .setItems(new String[] { "Change the script link" }, (d, w) -> askLink(
                        "Paste the link that the showLink function of your Sticky Notes script prints."))
                .show()));
        root.addView(bar);

        search = new EditText(this);
        search.setHint("Search");
        search.setSingleLine(true);
        search.setTextSize(15);
        search.setPadding(dp(14), dp(8), dp(14), dp(8));
        GradientDrawable sbg = new GradientDrawable();
        sbg.setColor(0xFFFFFFFF);
        sbg.setCornerRadius(dp(20));
        sbg.setStroke(dp(1), 0xFFDDDAD2);
        search.setBackground(sbg);
        search.addTextChangedListener(new TextWatcher() {
            public void beforeTextChanged(CharSequence s, int a, int b, int c) { }
            public void onTextChanged(CharSequence s, int a, int b, int c) { }
            public void afterTextChanged(Editable s) { show(); }
        });
        LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        slp.setMargins(dp(10), dp(10), dp(10), dp(2));
        root.addView(search, slp);

        // A message when something needs the person (signing in, no folder, no connection)
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(14), dp(12), dp(14), dp(8));
        box.setBackgroundColor(0xFFFFFFFF);
        messageText = new TextView(this);
        messageText.setTextColor(Look.INK);
        messageText.setTextSize(14);
        messageText.setTextIsSelectable(true); // so an error can be copied
        linkField = new EditText(this);
        linkField.setHint("https://script.google.com/macros/s/\u2026/exec?key=\u2026");
        linkField.setTextSize(13);
        linkField.setSingleLine(true);
        linkField.setVisibility(View.GONE);
        messageButton = new Button(this);
        messageButton.setAllCaps(false);
        box.addView(messageText);
        box.addView(linkField, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        box.addView(messageButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        message = box;
        message.setVisibility(View.GONE);
        LinearLayout.LayoutParams mlp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        mlp.setMargins(dp(10), dp(8), dp(10), 0);
        root.addView(message, mlp);

        ScrollView scroll = new ScrollView(this);
        columns = new LinearLayout(this);
        columns.setPadding(dp(5), dp(5), dp(5), dp(24));
        scroll.addView(columns);
        root.addView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(root);
    }

    @Override protected void onResume() {
        super.onResume();
        store.listen(refresh);
        show();
        sync(false);
    }

    @Override protected void onPause() {
        super.onPause();
        store.unlisten(refresh);
    }

    private TextView barButton(String text, String description, View.OnClickListener click) {
        TextView b = new TextView(this);
        b.setText(text);
        b.setContentDescription(description);
        b.setTextColor(0xFFFFFFFF);
        b.setTextSize(26);
        b.setGravity(Gravity.CENTER);
        TypedValue tv = new TypedValue();
        getTheme().resolveAttribute(android.R.attr.selectableItemBackgroundBorderless, tv, true);
        b.setBackgroundResource(tv.resourceId);
        b.setOnClickListener(click);
        b.setLayoutParams(new LinearLayout.LayoutParams(dp(48), dp(48)));
        return b;
    }

    int dp(float v) { return Math.round(v * getResources().getDisplayMetrics().density); }

    // MARK: Sync

    private void sync(boolean asked) {
        if (syncing) return;
        if (!store.hasLink()) {
            askLink("Sticky Notes reads your notes through a small script in your Google account. "
                    + "Make it once at script.google.com (the steps are in the app's README), then paste its link here.");
            return;
        }
        syncing = true;
        updateStatus();
        store.sync(error -> {
            syncing = false;
            if (error instanceof Store.NeedsLink) {
                askLink(error.getMessage());
            } else if (error instanceof Drive.Failure && (((Drive.Failure) error).code == 403 || ((Drive.Failure) error).code == 401)) {
                String why = ((Drive.Failure) error).code == 403
                        ? "The script refused this link: its key doesn't match. Run showLink in the script again and paste the new link."
                        : error.getMessage();
                askLink(why);
            } else if (error != null) {
                String why = error.getMessage() == null ? error.toString() : error.getMessage();
                if (error instanceof java.net.UnknownHostException || error instanceof java.net.SocketTimeoutException
                        || error instanceof java.net.ConnectException)
                    why = "No connection. These are the notes from the last sync; edits are sent when you're back online.";
                showMessage(why, "Try again", v -> sync(true));
            } else message.setVisibility(View.GONE);
            updateStatus();
        });
    }

    // Shows the box for pasting the script's link
    private void askLink(String why) {
        showMessage(why, "Connect", v -> {
            String link = Drive.findLink(linkField.getText().toString());
            if (link == null) {
                messageText.setText("That isn't the whole link. It starts with https://script.google.com/ and ends with ?key= and a long code.");
                return;
            }
            store.setLink(link);
            linkField.setVisibility(View.GONE);
            message.setVisibility(View.GONE);
            sync(true);
        });
        linkField.setVisibility(View.VISIBLE);
    }

    private void showMessage(String text, String button, View.OnClickListener click) {
        messageText.setText(text);
        messageButton.setText(button);
        messageButton.setOnClickListener(click);
        linkField.setVisibility(View.GONE);
        message.setVisibility(View.VISIBLE);
    }

    private void updateStatus() {
        int n = store.all().size(), waiting = store.pendingCount();
        String s = n == 1 ? "1 note" : n + " notes";
        if (syncing) s += " · syncing…";
        else if (waiting > 0) s += " · " + waiting + (waiting == 1 ? " edit" : " edits") + " not sent yet";
        else if (store.lastSync().length() > 0) s += " · synced " + ago(store.lastSync());
        status.setText(s);
    }

    static String ago(String iso) {
        try {
            SimpleDateFormat f = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.ROOT);
            f.setTimeZone(TimeZone.getTimeZone("UTC"));
            long min = (System.currentTimeMillis() - f.parse(iso).getTime()) / 60000;
            if (min < 1) return "just now";
            if (min < 60) return min + " min ago";
            if (min < 24 * 60) return (min / 60) + " h ago";
            return new SimpleDateFormat("MMM d", Locale.getDefault()).format(f.parse(iso));
        } catch (Exception e) { return ""; }
    }

    // MARK: Cards

    private void show() {
        updateStatus();
        columns.removeAllViews();
        float widthDp = getResources().getDisplayMetrics().widthPixels / getResources().getDisplayMetrics().density;
        int n = Math.max(2, (int) (widthDp / 210));
        LinearLayout[] cols = new LinearLayout[n];
        int[] heights = new int[n];
        for (int i = 0; i < n; i++) {
            cols[i] = new LinearLayout(this);
            cols[i].setOrientation(LinearLayout.VERTICAL);
            columns.addView(cols[i], new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        }
        String q = search.getText().toString().trim().toLowerCase(Locale.ROOT);
        List<String[]> tags = store.tags();
        int shown = 0;
        for (Store.Entry e : store.all()) {
            NoteFile.Note note = e.note;
            if (q.length() > 0 && !(note.title + "\n" + note.text).toLowerCase(Locale.ROOT).contains(q)) continue;
            // Each card goes in the shortest column, by a guess of its height
            int best = 0;
            for (int i = 1; i < n; i++) if (heights[i] < heights[best]) best = i;
            int lines = Math.min(12, note.text.split("\n", -1).length) + (note.title.isEmpty() ? 0 : 2) + 2;
            heights[best] += lines;
            cols[best].addView(card(e, tags));
            shown++;
        }
        if (shown == 0) {
            TextView empty = new TextView(this);
            empty.setText(q.length() > 0 ? "No note has “" + q + "”." : syncing ? "Reading your notes…" : "No notes yet.");
            empty.setTextColor(0xFF777777);
            empty.setPadding(dp(16), dp(24), dp(16), dp(16));
            cols[0].addView(empty);
        }
    }

    private View card(Store.Entry e, List<String[]> tags) {
        NoteFile.Note note = e.note;
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        GradientDrawable bg = new GradientDrawable();
        bg.setColor(Look.rgb(NoteFile.backHex(note.color)));
        bg.setCornerRadius(dp(6));
        c.setBackground(bg);
        c.setElevation(dp(2));

        View strip = new View(this);
        GradientDrawable sbg = new GradientDrawable();
        sbg.setColor(Look.rgb(NoteFile.headerHex(note.color)));
        float r = dp(6);
        sbg.setCornerRadii(new float[] { r, r, r, r, 0, 0, 0, 0 });
        strip.setBackground(sbg);
        c.addView(strip, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(e.pending ? 8 : 6)));

        LinearLayout inner = new LinearLayout(this);
        inner.setOrientation(LinearLayout.VERTICAL);
        inner.setPadding(dp(10), dp(6), dp(10), dp(10));
        if (!note.title.isEmpty()) {
            TextView t = new TextView(this);
            t.setText(note.title);
            t.setTypeface(Typeface.DEFAULT_BOLD);
            t.setTextColor(Look.INK);
            t.setTextSize(15);
            t.setPadding(0, 0, 0, dp(3));
            inner.addView(t);
        }
        TextView body = new TextView(this);
        body.setText(Look.render(note.text, tags, true));
        body.setTextColor(Look.INK);
        body.setTextSize(14);
        body.setMaxLines(12);
        body.setEllipsize(TextUtils.TruncateAt.END);
        if (note.text.trim().isEmpty()) { body.setText("(empty)"); body.setTextColor(0xFF999999); }
        inner.addView(body);
        if (e.pending) {
            TextView w = new TextView(this);
            w.setText("not sent yet");
            w.setTextSize(11);
            w.setTextColor(0xFF8A6D00);
            w.setPadding(0, dp(4), 0, 0);
            inner.addView(w);
        }
        c.addView(inner);

        c.setOnClickListener(v -> open(e.id));
        c.setOnLongClickListener(v -> { cardMenu(e); return true; });
        FrameLayout wrap = new FrameLayout(this);
        wrap.setPadding(dp(5), dp(5), dp(5), dp(5));
        wrap.addView(c);
        return wrap;
    }

    private void cardMenu(Store.Entry e) {
        String[] items = new String[NoteFile.NOTE_LABELS.length + 1];
        for (int i = 0; i < NoteFile.NOTE_LABELS.length; i++) items[i] = NoteFile.NOTE_LABELS[i];
        items[items.length - 1] = "Delete";
        new AlertDialog.Builder(this).setTitle(e.note.title.isEmpty() ? "Note" : e.note.title).setItems(items, (d, which) -> {
            if (which < NoteFile.NOTE_COLORS.length) {
                NoteFile.Note n = e.note.copy();
                n.color = NoteFile.NOTE_COLORS[which];
                store.edit(e.id, n);
                store.push(null);
            } else confirmDelete(this, store, e, null);
        }).show();
    }

    static void confirmDelete(Activity a, Store store, Store.Entry e, Runnable after) {
        new AlertDialog.Builder(a).setMessage("Delete this note?\n\nIt moves to the Trash folder inside your notes folder.")
                .setPositiveButton("Delete", (d, w) -> store.delete(e.id, error -> {
                    if (error != null) new AlertDialog.Builder(a).setMessage("Couldn't delete the note.\n\n" + error.getMessage())
                            .setPositiveButton("OK", null).show();
                    else if (after != null) after.run();
                }))
                .setNegativeButton("Cancel", null).show();
    }

    private void open(String id) {
        startActivity(new Intent(this, NoteActivity.class).putExtra("id", id));
    }

    private void newNote() {
        Store.Entry e = store.create("yellow");
        startActivity(new Intent(this, NoteActivity.class).putExtra("id", e.id).putExtra("new", true));
    }
}
