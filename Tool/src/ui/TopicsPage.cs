using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PackStudio
{
    /// <summary>
    /// M3: Guild Topics editor. Topic list on the left, full topic editor on
    /// the right (openers / triggers / responses / wrappers, level-coherence
    /// sliders with the asker-gate math spelled out, RelevantScene picker
    /// limited to verified runtime display names). Trigger safety rides the
    /// live Problems panel (shadow checks vs vanilla's 533 phrases run on
    /// every commit).
    /// </summary>
    internal sealed class TopicsPage : DockPanel
    {
        private readonly Pack _pack;
        private readonly Action _revalidate;
        private readonly ListBox _topicList = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Margin = new Thickness(0, 8, 0, 8),
            Width = 220,
        };
        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        internal TopicsPage(Pack pack, Action revalidate)
        {
            _pack = pack;
            _revalidate = revalidate;

            StackPanel side = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            side.Children.Add(Theme.Title("Guild Topics"));
            TextBlock blurb = Theme.Dim("Conversations guild sims start and answer on their own. Sims open"
                + " them on the guild chat timer, and player messages matching the triggers get"
                + " on-topic replies.");
            blurb.MaxWidth = 230;
            blurb.Margin = new Thickness(0, 4, 0, 0);
            side.Children.Add(blurb);
            side.Children.Add(_topicList);
            Button newTopic = new Button { Content = "＋ New Topic", HorizontalAlignment = HorizontalAlignment.Left };
            newTopic.Click += delegate { NewTopic(); };
            side.Children.Add(newTopic);
            SetDock(side, Dock.Left);
            Children.Add(side);
            Children.Add(_scroll);

            _topicList.SelectionChanged += delegate
            {
                if (_suppressSelection) { return; }
                JObj topic = SelectedTopic();
                if (topic != null) { ShowTopic(topic); }
            };
            RefreshList(0);
            List<JObj> initial = TopicObjs();
            if (initial.Count > 0) { ShowTopic(initial[0]); }
        }

        // RefreshList rebuilds the sidebar (counts change on edits), so without
        // suppression that would re-fire selection and rebuild the whole
        // editor mid-edit, yanking the scroll position.
        private bool _suppressSelection;

        // ── storage ─────────────────────────────────────────────────

        private JArr TopicsArr()
        {
            return _pack.Topics != null ? _pack.Topics.Root["Topics"] as JArr : null;
        }

        private List<JObj> TopicObjs()
        {
            List<JObj> topics = new List<JObj>();
            JArr arr = TopicsArr();
            if (arr != null)
            {
                foreach (JNode node in arr.Items)
                {
                    JObj topic = node as JObj;
                    if (topic != null) { topics.Add(topic); }
                }
            }
            return topics;
        }

        private JObj SelectedTopic()
        {
            List<JObj> topics = TopicObjs();
            int index = _topicList.SelectedIndex;
            return index >= 0 && index < topics.Count ? topics[index] : null;
        }

        private void MarkDirty()
        {
            if (_pack.Topics != null) { _pack.Topics.Dirty = true; }
            _revalidate();
        }

        private static string Str(JObj topic, string key, string fallback)
        {
            JStr value = topic[key] as JStr;
            return value != null ? value.Value : fallback;
        }

        private static int Int(JObj topic, string key, int fallback)
        {
            JNum value = topic[key] as JNum;
            return value != null ? value.AsInt() : fallback;
        }

        private static List<string> Lines(JObj topic, string key)
        {
            return PackJsonFile.LinesOf(topic[key] as JArr);
        }

        private static void SetTopicLines(JObj topic, string key, IList<string> lines)
        {
            JArr arr = new JArr();
            foreach (string line in lines) { arr.Items.Add(new JStr(line)); }
            topic[key] = arr;
        }

        // ── UI ──────────────────────────────────────────────────────

        private void RefreshList(int selectIndex)
        {
            _suppressSelection = true;
            try
            {
                _topicList.Items.Clear();
                List<JObj> topics = TopicObjs();
                foreach (JObj topic in topics)
                {
                    int openers = Lines(topic, "SimPlayerActivations").Count;
                    int triggers = Lines(topic, "ActivationWords").Count;
                    _topicList.Items.Add(Str(topic, "Name", "(unnamed)")
                        + "  (" + openers + " opener(s), " + triggers + " trigger(s))");
                }
                if (topics.Count == 0)
                {
                    _scroll.Content = Theme.Dim("No topics yet - click New Topic to create one."
                        + "\n\nGood topics are specific conversations (a boss grudge, zone news, lore"
                        + " arguments), not general chat - general lines belong in Server Chatter.");
                }
                else if (selectIndex >= 0 && selectIndex < topics.Count)
                {
                    _topicList.SelectedIndex = selectIndex;
                }
            }
            finally
            {
                _suppressSelection = false;
            }
        }

        private void NewTopic()
        {
            PackJsonFile file = _pack.EnsureTopics();
            JArr arr = file.Root["Topics"] as JArr;
            if (arr == null)
            {
                arr = new JArr();
                file.Root["Topics"] = arr;
            }
            JObj topic = new JObj();
            string name = "MyTopic";
            int n = 1;
            List<JObj> existing = TopicObjs();
            while (true)
            {
                bool taken = false;
                foreach (JObj other in existing)
                {
                    if (Str(other, "Name", "") == name) { taken = true; }
                }
                if (!taken) { break; }
                name = "MyTopic" + (++n);
            }
            topic["Name"] = new JStr(name);
            topic["SimPlayerActivations"] = new JArr();
            topic["ActivationWords"] = new JArr();
            topic["Responses"] = new JArr();
            file.Root["Topics"] = arr; // ensure key exists before add
            arr.Items.Add(topic);
            file.Dirty = true;
            RefreshList(_topicList.Items.Count); // suppressed - show explicitly
            ShowTopic(topic);
            _revalidate();
        }

        private void ShowTopic(JObj topic)
        {
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 8, 20) };

            // header: name + delete
            DockPanel header = new DockPanel();
            Button delete = new Button { Content = "🗑 Delete topic", Margin = new Thickness(10, 0, 0, 0) };
            delete.Click += delegate
            {
                if (MessageBox.Show("Delete topic '" + Str(topic, "Name", "?") + "'?", "Delete topic",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    JArr arr = TopicsArr();
                    if (arr != null)
                    {
                        arr.Items.Remove(topic);
                        MarkDirty();
                        RefreshList(0);
                        List<JObj> remaining = TopicObjs();
                        if (remaining.Count > 0) { ShowTopic(remaining[0]); }
                        else { _scroll.Content = Theme.Dim("No topics yet - click New Topic to create one."); }
                    }
                }
            };
            DockPanel.SetDock(delete, Dock.Right);
            TextBlock title = Theme.Title(Str(topic, "Name", "(unnamed)"));
            header.Children.Add(delete);
            header.Children.Add(title);
            body.Children.Add(header);

            Grid nameGrid = new Grid { Margin = new Thickness(0, 8, 0, 8) };
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock nameLabel = Theme.Dim("Topic name");
            nameLabel.VerticalAlignment = VerticalAlignment.Center;
            TextBox nameBox = new TextBox { Text = Str(topic, "Name", ""), Width = 240, HorizontalAlignment = HorizontalAlignment.Left };
            nameBox.LostFocus += delegate
            {
                string newName = nameBox.Text.Trim();
                if (newName.Length > 0 && newName != Str(topic, "Name", ""))
                {
                    topic["Name"] = new JStr(newName);
                    title.Text = newName;
                    MarkDirty();
                    RefreshList(_topicList.SelectedIndex);
                }
            };
            Grid.SetColumn(nameLabel, 0);
            Grid.SetColumn(nameBox, 1);
            nameGrid.Children.Add(nameLabel);
            nameGrid.Children.Add(nameBox);
            body.Children.Add(nameGrid);

            body.Children.Add(TopicList(topic, "SimPlayerActivations",
                "Conversation openers",
                "Full sentences a sim posts in guild chat to start this topic. Leave empty for a"
                + " keyword-only topic (fires only when someone's message matches a trigger).", true));
            body.Children.Add(TopicList(topic, "ActivationWords",
                "Trigger phrases",
                "ALL words of a phrase must appear in a guild message (any order, whole words) to"
                + " fire the topic. A matched topic swallows the message BEFORE knowledge answers"
                + " and the greeting, goodnight and ding waves, so keep phrases SPECIFIC and use 2"
                + " or more words. The Problems panel checks every phrase against vanilla's 533"
                + " triggers as you type.", false));
            body.Children.Add(TopicList(topic, "Responses",
                "Responses  (REQUIRED)",
                "The on-topic replies. Between 2 and 4 sims answer with these, so 3 or more lines"
                + " keep it from repeating. NN does not work in topics.", true));
            body.Children.Add(TopicList(topic, "Preceed",
                "Response openers (optional)",
                "Wrappers before a response, sim-started conversations only (\"ok so\" + response).", true));
            body.Children.Add(TopicList(topic, "End",
                "Response closers (optional)",
                "Wrappers after a response, sim-started conversations only (response + \"anyway...\").", true));

            body.Children.Add(Theme.Panel(LevelPanel(topic)));
            body.Children.Add(Theme.Panel(ScenePanel(topic)));

            _scroll.Content = body;
            _scroll.ScrollToTop();
        }

        private LineListEditor TopicList(JObj topic, string key, string title, string guidance, bool preview)
        {
            LineListEditor editor = new LineListEditor(title, guidance, 0);
            editor.BindCustom(
                delegate { return Lines(topic, key); },
                delegate(List<string> lines)
                {
                    SetTopicLines(topic, key, lines);
                    MarkDirty();
                    if (key == "SimPlayerActivations" || key == "ActivationWords")
                    {
                        RefreshList(_topicList.SelectedIndex); // counts in the sidebar
                    }
                },
                key, "A guildmate", null, _revalidate, preview, null);
            return editor;
        }

        private StackPanel LevelPanel(JObj topic)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.SectionTitle("Level coherence"));
            panel.Children.Add(Theme.Dim("Set the level a sim could plausibly KNOW this topic:"
                + " world news = 1, mid-game gear = 13, raids = 30."));

            TextBlock math = Theme.Body("");
            math.Margin = new Thickness(0, 6, 0, 6);
            math.Foreground = Theme.Tip;

            StackPanel reqRow = new StackPanel { Orientation = Orientation.Horizontal };
            reqRow.Children.Add(Theme.Dim("Required level to know:  "));
            Slider req = new Slider { Minimum = 1, Maximum = 35, Width = 200, Value = Int(topic, "RequiredLevelToKnow", 1), VerticalAlignment = VerticalAlignment.Center };
            TextBlock reqValue = Theme.Body(((int)req.Value).ToString());
            reqValue.Margin = new Thickness(8, 0, 0, 0);
            reqRow.Children.Add(req);
            reqRow.Children.Add(reqValue);
            panel.Children.Add(reqRow);

            StackPanel maxRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            maxRow.Children.Add(Theme.Dim("Max level to ask:  "));
            Slider max = new Slider { Minimum = 1, Maximum = 35, Width = 200, Value = Int(topic, "MaxLevelToAsk", 35), VerticalAlignment = VerticalAlignment.Center };
            TextBlock maxValue = Theme.Body(((int)max.Value).ToString());
            maxValue.Margin = new Thickness(8, 0, 0, 0);
            maxRow.Children.Add(max);
            maxRow.Children.Add(maxValue);
            panel.Children.Add(maxRow);
            panel.Children.Add(math);

            Action updateMath = delegate
            {
                int r = (int)req.Value;
                int m = (int)max.Value;
                math.Text = "Sims level " + Math.Max(1, r - 2) + " to " + m + " may START this topic"
                    + " (openers fire from required minus 2). When a PLAYER triggers it by keyword,"
                    + " responders below level " + r + " answer with the guild's \"too low to know\""
                    + " lines. Sim-started conversations skip that responder check.";
            };
            req.ValueChanged += delegate
            {
                req.Value = Math.Round(req.Value);
                reqValue.Text = ((int)req.Value).ToString();
                topic["RequiredLevelToKnow"] = new JNum((int)req.Value);
                updateMath();
                MarkDirty();
            };
            max.ValueChanged += delegate
            {
                max.Value = Math.Round(max.Value);
                maxValue.Text = ((int)max.Value).ToString();
                topic["MaxLevelToAsk"] = new JNum((int)max.Value);
                updateMath();
                MarkDirty();
            };
            updateMath();

            JBool force = topic["forceLotsOfResponses"] as JBool;
            CheckBox flood = new CheckBox
            {
                Content = "forceLotsOfResponses - 5 up to the WHOLE GUILD answers (floods chat, use sparingly)",
                IsChecked = force != null && force.Value,
                Margin = new Thickness(0, 6, 0, 0),
            };
            flood.Checked += delegate { topic["forceLotsOfResponses"] = new JBool(true); MarkDirty(); };
            flood.Unchecked += delegate { topic["forceLotsOfResponses"] = new JBool(false); MarkDirty(); };
            panel.Children.Add(flood);
            return panel;
        }

        private StackPanel ScenePanel(JObj topic)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.SectionTitle("Zone gate (optional)"));
            panel.Children.Add(Theme.Dim("With zones listed, keyword-triggered replies only answer properly"
                + " while the guild chat happens IN one of them. Asked from anywhere else, sims"
                + " answer \"that's over in\" plus the zone. Sim-started conversations ignore the"
                + " gate. Always pick zones from the list, because a typed name that is even"
                + " slightly off silently breaks the gate."));

            StackPanel current = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            Action rebuild = null;
            rebuild = delegate
            {
                current.Children.Clear();
                List<string> scenes = Lines(topic, "RelevantScene");
                if (scenes.Count == 0)
                {
                    current.Children.Add(Theme.Dim("(no zone gate - answered anywhere)"));
                }
                for (int i = 0; i < scenes.Count; i++)
                {
                    int index = i;
                    DockPanel row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                    Button remove = new Button { Content = "✕", Width = 26, Margin = new Thickness(5, 0, 0, 0) };
                    remove.Click += delegate
                    {
                        List<string> updated = Lines(topic, "RelevantScene");
                        updated.RemoveAt(index);
                        SetTopicLines(topic, "RelevantScene", updated);
                        MarkDirty();
                        rebuild();
                    };
                    DockPanel.SetDock(remove, Dock.Right);
                    row.Children.Add(remove);
                    row.Children.Add(Theme.Body(scenes[index]));
                    current.Children.Add(row);
                }
            };
            rebuild();
            panel.Children.Add(current);

            StackPanel addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            ComboBox picker = new ComboBox { Width = 260 };
            List<ZoneEntry> ordered = new List<ZoneEntry>();
            foreach (ZoneEntry zone in RefGen.Atlas) { ordered.Add(zone); }
            ordered.Sort(delegate(ZoneEntry a, ZoneEntry b)
            {
                return string.CompareOrdinal(RefData.RuntimeDisplayName(a.Scene), RefData.RuntimeDisplayName(b.Scene));
            });
            foreach (ZoneEntry zone in ordered)
            {
                picker.Items.Add(RefData.RuntimeDisplayName(zone.Scene)
                    + (RefData.DisplayNameUnverified.Contains(zone.Scene) ? "  (best guess)" : ""));
            }
            picker.SelectedIndex = 0;
            Button add = new Button { Content = "Add zone", Margin = new Thickness(8, 0, 0, 0) };
            add.Click += delegate
            {
                string display = ((string)picker.SelectedItem).Replace("  (best guess)", "");
                List<string> scenes = Lines(topic, "RelevantScene");
                if (!scenes.Contains(display))
                {
                    scenes.Add(display);
                    SetTopicLines(topic, "RelevantScene", scenes);
                    MarkDirty();
                    rebuild();
                }
            };
            addRow.Children.Add(picker);
            addRow.Children.Add(add);
            panel.Children.Add(addRow);
            return panel;
        }
    }
}
