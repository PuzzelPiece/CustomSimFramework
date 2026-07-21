using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PackStudio
{
    /// <summary>
    /// M1d: the full sim editor. Sims sidebar, identity / typing style /
    /// appearance panels with a live sample sentence, and all 30 dialogue
    /// lists grouped by category, each a LineListEditor with guidance,
    /// ghost placeholders and the PreviewPane.
    /// </summary>
    internal sealed class SimsPage : DockPanel
    {
        private readonly Pack _pack;
        private readonly Action _revalidate;
        private readonly ListBox _simList = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        internal SimsPage(Pack pack, Action revalidate)
        {
            _pack = pack;
            _revalidate = revalidate;

            StackPanel side = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            side.Children.Add(Theme.Title("My Sims"));
            _simList.Margin = new Thickness(0, 8, 0, 8);
            _simList.SelectionChanged += delegate
            {
                if (!_suppressSelection && _simList.SelectedIndex >= 0 && _simList.SelectedIndex < _pack.Sims.Count)
                {
                    ShowSim(_pack.Sims[_simList.SelectedIndex]);
                }
            };
            side.Children.Add(_simList);
            Button newSim = new Button { Content = "＋ New Sim" };
            newSim.Click += delegate { NewSim(); };
            side.Children.Add(newSim);
            SetDock(side, Dock.Left);
            Children.Add(side);
            Children.Add(_scroll);

            RefreshSimList();
            if (_pack.Sims.Count > 0)
            {
                _simList.SelectedIndex = 0;
            }
            else
            {
                _scroll.Content = Theme.Dim("No sims in this pack yet - click New Sim to create one.");
            }
        }

        // Rebuilding the sidebar (renames, rival toggles) must not fire
        // selection changes or drop the highlight, same pattern as TopicsPage.
        private bool _suppressSelection;

        private void RefreshSimList()
        {
            int selected = _simList.SelectedIndex;
            _suppressSelection = true;
            try
            {
                _simList.Items.Clear();
                foreach (SimFile sim in _pack.Sims)
                {
                    _simList.Items.Add(sim.SimName + (sim.GetBool("Rival", false) ? "  (rival)" : ""));
                }
                if (selected >= 0 && selected < _simList.Items.Count)
                {
                    _simList.SelectedIndex = selected;
                }
            }
            finally
            {
                _suppressSelection = false;
            }
        }

        // ── the editor body ────────────────────────────────────────

        private void ShowSim(SimFile sim)
        {
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 8, 20) };
            DockPanel titleRow = new DockPanel();
            Button delete = new Button { Content = "🗑 Delete sim", Margin = new Thickness(10, 0, 0, 0) };
            delete.Click += delegate { DeleteSim(sim); };
            DockPanel.SetDock(delete, Dock.Right);
            titleRow.Children.Add(delete);
            titleRow.Children.Add(Theme.Title(sim.SimName));
            body.Children.Add(titleRow);
            body.Children.Add(new TextBlock { Height = 6 });
            body.Children.Add(Theme.Panel(IdentityPanel(sim)));
            body.Children.Add(Theme.Panel(QuirksPanel(sim)));
            body.Children.Add(Theme.Panel(AppearancePanel(sim)));

            string category = null;
            foreach (ListMeta meta in SimListMeta.All)
            {
                if (meta.Category != category)
                {
                    category = meta.Category;
                    TextBlock header = Theme.Title(category);
                    header.FontSize = Theme.FontSize + 3;
                    header.Margin = new Thickness(0, 8, 0, 6);
                    body.Children.Add(header);
                }
                LineListEditor editor = new LineListEditor(meta.Title, meta.Guidance,
                    SimListMeta.VanillaPoolSize(meta.Key));
                editor.Bind(sim, meta.Key, sim.SimName,
                    delegate { return PreviewQuirks.Of(sim); }, _revalidate);
                body.Children.Add(editor);
            }
            _scroll.Content = body;
            _scroll.ScrollToTop();
        }

        private Grid FormGrid()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            return grid;
        }

        private void AddRow(Grid grid, string label, string tooltip, FrameworkElement control)
        {
            int row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock labelBlock = Theme.Body(label);
            labelBlock.Foreground = Theme.TextDim;
            labelBlock.VerticalAlignment = VerticalAlignment.Center;
            labelBlock.Margin = new Thickness(0, 4, 10, 4);
            if (tooltip != null) { labelBlock.ToolTip = tooltip; }
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            control.Margin = new Thickness(0, 4, 0, 4);
            control.HorizontalAlignment = HorizontalAlignment.Left;
            if (tooltip != null && control.ToolTip == null) { control.ToolTip = tooltip; }
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(control);
        }

        private StackPanel IdentityPanel(SimFile sim)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.SectionTitle("Identity"));
            Grid grid = FormGrid();

            TextBox name = new TextBox { Text = sim.GetString("Name", ""), Width = 220 };
            name.LostFocus += delegate
            {
                if (name.Text.Trim() != sim.GetString("Name", ""))
                {
                    sim.SetString("Name", name.Text.Trim());
                    RefreshSimList();
                    _revalidate();
                }
            };
            AddRow(grid, "Name", "Letters, digits and hyphens only. The name becomes a save filename"
                + " and a whisper target in game.", name);

            ComboBox gender = Combo(new[] { "Male", "Female" }, sim.GetString("Gender", "Male"));
            gender.SelectionChanged += delegate { sim.SetString("Gender", (string)gender.SelectedItem); _revalidate(); };
            AddRow(grid, "Gender", null, gender);

            ComboBox cls = Combo(RefData.ClassDisplayNames,
                RefData.ClassToDisplay(sim.GetString("Class", "Duelist")));
            cls.SelectionChanged += delegate
            {
                sim.SetString("Class", RefData.ClassToInternal((string)cls.SelectedItem));
                _revalidate();
            };
            AddRow(grid, "Class", "Windblade is saved as \"Duelist\" in the file (the game's internal name)."
                + " Changing class converts an existing sim next login.", cls);

            AddRow(grid, "Level", "1-35. Only applies when the sim's save is first created - the save owns progression afterwards.",
                IntSlider(sim, "Level", 1, 35, 5));
            AddRow(grid, "Combat skill", "How well they play their class (vanilla sims use roughly 0-65).",
                IntSlider(sim, "SkillLevel", 0, 65, 40));

            // Progression tie (TiedToSlot). Values per the framework audit:
            // unset acts like slot 0; 0-10 follows that character save slot
            // (and staffs that slot's tutorial for NEW characters); 12 = slow
            // daily levelling; 99 = independent fast catch-up. Rivals are
            // forced to 99 by the game, so the picker disables for them.
            ComboBox slotTie = new ComboBox { Width = 360 };
            slotTie.Items.Add("Auto - default slot 0, reassigned to whoever befriends them");
            for (int i = 0; i <= 10; i++)
            {
                slotTie.Items.Add("Pinned to character slot " + i + (i == 0 ? " (the first / top slot)" : ""));
            }
            slotTie.Items.Add("Slow daily levelling (not tied to a character)");
            slotTie.Items.Add("Independent - fast catch-up (rival pace)");
            int tied = sim.GetInt("TiedToSlot", -1);
            slotTie.SelectedIndex = tied >= 0 && tied <= 10 ? tied + 1
                : tied == 12 ? 12 : tied == 99 ? 13 : 0;
            slotTie.SelectionChanged += delegate
            {
                int index = slotTie.SelectedIndex;
                if (index == 0)
                {
                    if (sim.Root.ContainsKey("TiedToSlot"))
                    {
                        sim.Root.Remove("TiedToSlot");
                        sim.Dirty = true;
                    }
                }
                else
                {
                    sim.SetInt("TiedToSlot", index <= 11 ? index - 1 : index == 12 ? 12 : 99);
                }
                _revalidate();
            };

            AddRow(grid, "Patience", "How long they tolerate being dead/stuck in a group before nagging"
                + " (vanilla sims use roughly 600-1500).", IntSlider(sim, "Patience", 100, 3000, 1000));
            AddRow(grid, "Gear hunger", "How hard they chase gear upgrades (vanilla sims use roughly 1-6).",
                IntSlider(sim, "GearChase", 1, 8, 4));

            CheckBox rival = new CheckBox
            {
                Content = "Rival (joins the antagonist guild, taunts you)",
                IsChecked = sim.GetBool("Rival", false),
                ToolTip = "A rival answers most whispers with raw lines from their BeenAWhile list,"
                    + " so write that list as taunts. Their OTHER lists still work normally"
                    + " (greeting replies come from ReturnGreeting!), so flavor those antagonistic too.",
            };
            rival.Checked += delegate
            {
                sim.SetBool("Rival", true);
                // The game forces rivals to independent progression (99) every
                // login and the loader clears any slot tie with a warning -
                // mirror that instead of leaving a dead setting behind.
                slotTie.SelectedIndex = 0;
                slotTie.IsEnabled = false;
                RefreshSimList();
                _revalidate();
            };
            rival.Unchecked += delegate
            {
                sim.SetBool("Rival", false);
                slotTie.IsEnabled = true;
                RefreshSimList();
                _revalidate();
            };
            slotTie.IsEnabled = !sim.GetBool("Rival", false);
            AddRow(grid, "Rival", null, rival);
            AddRow(grid, "Levels up with",
                "Ties the sim's levelling to one of your character save slots. While that character"
                + " out-levels them, the sim catches up a level or two per day. A slot-tied sim also"
                + " GREETS NEW CHARACTERS made on that slot, staffing their tutorial and joining the"
                + " world once they reach a normal zone (instant for existing characters). Using"
                + " /friend on a sim in game ties them to your current slot, and a pinned value set"
                + " here overrides that every login. Rivals always use independent fast catch-up.",
                slotTie);

            ComboBox personality = Combo(new[]
            {
                "0 - game default (nice)", "1 - nice", "2 - tryhard", "3 - mean", "4 - plain", "5 - plain",
            }, null);
            personality.SelectedIndex = Math.Max(0, Math.Min(5, sim.GetInt("PersonalityType", 0)));
            personality.SelectionChanged += delegate { sim.SetInt("PersonalityType", personality.SelectedIndex); _revalidate(); };
            AddRow(grid, "Personality", "Picks the bio pool when Bio is empty, and flavors behavior.", personality);

            TextBox bio = new TextBox
            {
                Text = sim.GetString("Bio", ""),
                Width = 430,
                Height = 60,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            bio.LostFocus += delegate { sim.SetString("Bio", bio.Text); _revalidate(); };
            AddRow(grid, "Bio (inspect window)", "Shown when players inspect the sim (line breaks allowed)."
                + " Empty = a random personality bio instead.", bio);

            panel.Children.Add(grid);
            return panel;
        }

        private StackPanel IntSlider(SimFile sim, string key, int min, int max, int fallback)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            Slider slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Width = 180,
                Value = sim.GetInt(key, fallback),
                VerticalAlignment = VerticalAlignment.Center,
            };
            TextBlock value = Theme.Body(((int)slider.Value).ToString());
            value.Margin = new Thickness(8, 0, 0, 0);
            value.VerticalAlignment = VerticalAlignment.Center;
            slider.ValueChanged += delegate
            {
                slider.Value = Math.Round(slider.Value);
                value.Text = ((int)slider.Value).ToString();
                sim.SetInt(key, (int)slider.Value);
                _revalidate();
            };
            row.Children.Add(slider);
            row.Children.Add(value);
            return row;
        }

        private StackPanel QuirksPanel(SimFile sim)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.SectionTitle("Typing style"));
            panel.Children.Add(Theme.Dim("Applied to everything they say, AFTER lines are composed."
                + " Write plain lines and let the quirks do the styling."));

            TextBlock sample = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8),
                FontStyle = FontStyles.Italic,
                Foreground = Theme.ChatShout,
            };
            Action refreshSample = delegate
            {
                PreviewQuirks quirks = PreviewQuirks.Of(sim);
                sample.Text = sim.SimName + " shouts: " + Preview.Personalize(
                    "I'm heading out. good luck with my old camp, it was great to me!",
                    quirks, new Random(Environment.TickCount));
            };

            WrapPanel toggles = new WrapPanel();
            toggles.Children.Add(QuirkCheck(sim, "TypesInAllCaps", "ALL CAPS", refreshSample));
            toggles.Children.Add(QuirkCheck(sim, "TypesInAllLowers", "all lowercase", refreshSample));
            toggles.Children.Add(QuirkCheck(sim, "TypesInThirdPerson", "third-person", refreshSample));
            toggles.Children.Add(QuirkCheck(sim, "LovesEmojis", "loves emojis", refreshSample));

            TextBox refers = new TextBox { Text = sim.GetString("RefersToSelfAs", ""), Width = 110, Margin = new Thickness(0, 0, 14, 0) };
            refers.ToolTip = "They replace \"I\"/\"me\" with this name. WARNING: no first-person contraction"
                + " survives this quirk (\"I'm\" renders \"" + (refers.Text.Length > 0 ? refers.Text : "Vex") + "'m\").";
            refers.LostFocus += delegate { sim.SetString("RefersToSelfAs", refers.Text.Trim()); _revalidate(); refreshSample(); };
            StackPanel refersRow = new StackPanel { Orientation = Orientation.Horizontal };
            refersRow.Children.Add(Theme.Dim("says \"I\" as: "));
            refersRow.Children.Add(refers);
            toggles.Children.Add(refersRow);

            StackPanel typoRow = new StackPanel { Orientation = Orientation.Horizontal };
            typoRow.Children.Add(Theme.Dim("typos: "));
            Slider typo = new Slider
            {
                Minimum = 0, Maximum = 10, Width = 120,
                Value = sim.GetDouble("TypoRate", 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Per-word typo chance (vanilla sims use 0-10).",
            };
            TextBlock typoValue = Theme.Dim(((int)typo.Value).ToString());
            typo.ValueChanged += delegate
            {
                typo.Value = Math.Round(typo.Value);
                typoValue.Text = ((int)typo.Value).ToString();
                sim.SetDouble("TypoRate", typo.Value);
                refreshSample();
            };
            typoRow.Children.Add(typo);
            typoRow.Children.Add(typoValue);
            toggles.Children.Add(typoRow);
            panel.Children.Add(toggles);

            StackPanel sampleRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button reroll = new Button { Content = "🎲", ToolTip = "Re-roll typos/emojis in the sample", Margin = new Thickness(0, 0, 8, 0) };
            reroll.Click += delegate { refreshSample(); };
            sampleRow.Children.Add(reroll);
            sampleRow.Children.Add(sample);
            panel.Children.Add(sampleRow);
            refreshSample();

            LineListEditor signoffs = new LineListEditor("Sign-off lines",
                "Roughly 10% of their messages get one of these appended (\"paul out\").", 0);
            signoffs.Bind(sim, "SignOffLines", sim.SimName, delegate { return PreviewQuirks.Of(sim); }, _revalidate);
            signoffs.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(signoffs);
            return panel;
        }

        private CheckBox QuirkCheck(SimFile sim, string key, string label, Action refreshSample)
        {
            CheckBox check = new CheckBox
            {
                Content = label,
                IsChecked = sim.GetBool(key, false),
                Margin = new Thickness(0, 0, 14, 4),
            };
            check.Checked += delegate { sim.SetBool(key, true); _revalidate(); refreshSample(); };
            check.Unchecked += delegate { sim.SetBool(key, false); _revalidate(); refreshSample(); };
            return check;
        }

        private StackPanel AppearancePanel(SimFile sim)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.SectionTitle("Appearance"));
            Grid grid = FormGrid();

            List<string> hairNames = new List<string> { "(random)" };
            for (int i = 1; i <= 38; i++)
            {
                if (i != 3) { hairNames.Add("Chr_Hair_" + i.ToString("00")); }
            }
            string current = sim.GetString("HairName", "");
            ComboBox hair = Combo(hairNames.ToArray(), current.Length == 0 ? "(random)" : current);
            hair.SelectionChanged += delegate
            {
                string picked = (string)hair.SelectedItem;
                sim.SetString("HairName", picked == "(random)" ? "" : picked);
                _revalidate();
            };
            AddRow(grid, "Hair style", "Style 03 does not exist in the game (it renders bald), so it isn't offered.", hair);
            panel.Children.Add(grid);

            panel.Children.Add(Swatches(sim, "HairColorIndex", "Hair color", RefGen.HairColors));
            panel.Children.Add(Swatches(sim, "SkinColorIndex", "Skin color", RefGen.SkinColors));
            return panel;
        }

        private StackPanel Swatches(SimFile sim, string key, string label, string[] hexColors)
        {
            StackPanel row = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(Theme.Dim(label + "  (the game's own sim palette, taken from the data dump."
                + " In-game lighting can shift the shade slightly. ? means rolled once, then kept)"));
            WrapPanel wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            int selected = sim.GetInt(key, -1);
            List<Border> cells = new List<Border>();
            Action<int> select = null;
            select = delegate(int index)
            {
                sim.SetInt(key, index);
                for (int i = 0; i < cells.Count; i++)
                {
                    cells[i].BorderBrush = (i - 1) == sim.GetInt(key, -1) || (i == 0 && sim.GetInt(key, -1) == -1)
                        ? Theme.Accent : Theme.BorderBrush;
                    cells[i].BorderThickness = new Thickness(cells[i].BorderBrush == Theme.Accent ? 2 : 1);
                }
                _revalidate();
            };
            // index -1 = random
            for (int i = -1; i < hexColors.Length; i++)
            {
                int index = i;
                Border cell = new Border
                {
                    Width = 26,
                    Height = 26,
                    Margin = new Thickness(0, 0, 5, 5),
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = index == selected ? Theme.Accent : Theme.BorderBrush,
                    BorderThickness = new Thickness(index == selected ? 2 : 1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = index < 0
                        ? Theme.SurfaceAlt
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColors[index].Substring(0, 6))),
                    ToolTip = index < 0 ? "Random (rolled once, then kept)" : (label + " " + index),
                };
                if (index < 0)
                {
                    cell.Child = new TextBlock
                    {
                        Text = "?",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Theme.TextDim,
                    };
                }
                cell.MouseLeftButtonUp += delegate { select(index); };
                cells.Add(cell);
                wrap.Children.Add(cell);
            }
            row.Children.Add(wrap);
            return row;
        }

        private static ComboBox Combo(string[] items, string selected)
        {
            ComboBox combo = new ComboBox { Width = 220 };
            foreach (string item in items)
            {
                combo.Items.Add(item);
                if (selected != null && item.Equals(selected, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                }
            }
            if (combo.SelectedItem == null && items.Length > 0) { combo.SelectedIndex = 0; }
            return combo;
        }

        // ── New Sim ─────────────────────────────────────────────────

        private void NewSim()
        {
            NewSimDialog dialog = new NewSimDialog(_pack) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true && dialog.Created != null)
            {
                _pack.Sims.Sort(delegate(SimFile a, SimFile b)
                {
                    return string.Compare(a.SimName, b.SimName, StringComparison.OrdinalIgnoreCase);
                });
                RefreshSimList();
                _simList.SelectedIndex = _pack.Sims.IndexOf(dialog.Created); // fires ShowSim
                _revalidate();
            }
        }

        /// <summary>The sim's file is renamed to *.removed (not deleted) so a
        /// mistake is recoverable by hand; Deploy's mirroring then removes the
        /// stale copy from the game's folder.</summary>
        private void DeleteSim(SimFile sim)
        {
            if (MessageBox.Show("Delete sim '" + sim.SimName + "'?\n\n(The file is kept next to the pack"
                + " as \"" + sim.FileName + ".removed\" in case you change your mind.)",
                "Delete sim", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            {
                return;
            }
            try
            {
                if (File.Exists(sim.Path))
                {
                    string removed = sim.Path + ".removed";
                    if (File.Exists(removed)) { File.Delete(removed); }
                    File.Move(sim.Path, removed);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not remove the sim's file: " + ex.Message, "Delete sim");
                return;
            }
            _pack.Sims.Remove(sim);
            _simList.SelectedIndex = -1;
            RefreshSimList();
            if (_pack.Sims.Count > 0)
            {
                _simList.SelectedIndex = 0; // fires ShowSim
            }
            else
            {
                _scroll.Content = Theme.Dim("No sims in this pack yet - click New Sim to create one.");
            }
            _revalidate();
        }
    }

    internal sealed class NewSimDialog : Window
    {
        internal SimFile Created;
        private readonly Pack _pack;
        private readonly TextBox _name = new TextBox { Width = 220 };
        private readonly ComboBox _gender = new ComboBox { Width = 220 };
        private readonly ComboBox _class = new ComboBox { Width = 220 };
        private readonly Slider _level = new Slider { Minimum = 1, Maximum = 35, Value = 5, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        private readonly CheckBox _rival = new CheckBox { Content = "Rival (antagonist - taunts instead of befriending)" };

        internal NewSimDialog(Pack pack)
        {
            _pack = pack;
            Title = "New Sim";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Theme.WindowBg;
            ResizeMode = ResizeMode.NoResize;

            StackPanel body = new StackPanel { Margin = new Thickness(16) };
            body.Children.Add(Theme.Title("New Sim"));
            body.Children.Add(Row("Name", _name));
            foreach (string gender in new[] { "Male", "Female" }) { _gender.Items.Add(gender); }
            _gender.SelectedIndex = 0;
            body.Children.Add(Row("Gender", _gender));
            foreach (string cls in RefData.ClassDisplayNames) { _class.Items.Add(cls); }
            _class.SelectedIndex = 5; // Windblade (saved as internal "Duelist")
            body.Children.Add(Row("Class", _class));
            TextBlock levelValue = Theme.Body("5");
            _level.ValueChanged += delegate { _level.Value = Math.Round(_level.Value); levelValue.Text = ((int)_level.Value).ToString(); };
            StackPanel levelRow = new StackPanel { Orientation = Orientation.Horizontal };
            levelRow.Children.Add(_level);
            levelValue.Margin = new Thickness(8, 0, 0, 0);
            levelRow.Children.Add(levelValue);
            body.Children.Add(Row("Starting level", levelRow));
            _rival.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(_rival);
            TextBlock hint = Theme.Dim("Everything else (looks, personality, dialogue) starts sensible and is"
                + " edited on the sim's page - anything you don't write is auto-filled by the game.");
            hint.Margin = new Thickness(0, 8, 0, 0);
            hint.MaxWidth = 380;
            body.Children.Add(hint);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            Button ok = new Button { Content = "Create", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += delegate { Create(); };
            Button cancel = new Button { Content = "Cancel", Width = 90 };
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            body.Children.Add(buttons);
            Content = body;
        }

        private StackPanel Row(string label, FrameworkElement control)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            TextBlock labelBlock = Theme.Dim(label);
            labelBlock.Width = 100;
            labelBlock.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(labelBlock);
            row.Children.Add(control);
            return row;
        }

        private void Create()
        {
            string name = _name.Text.Trim();
            if (name.Length == 0 || !System.Text.RegularExpressions.Regex.IsMatch(name, RefData.NamePattern))
            {
                MessageBox.Show(this, "Names use letters, digits and hyphens only (no spaces) - they become"
                    + " save filenames and whisper targets.", "Pick a different name");
                return;
            }
            foreach (SimFile existing in _pack.Sims)
            {
                if (existing.SimName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "This pack already has a sim named '" + name + "'.", "Pick a different name");
                    return;
                }
            }
            JObj root = new JObj();
            root["Name"] = new JStr(name);
            root["Gender"] = new JStr((string)_gender.SelectedItem);
            root["Class"] = new JStr(RefData.ClassToInternal((string)_class.SelectedItem));
            root["Level"] = new JNum((int)_level.Value);
            if (_rival.IsChecked == true)
            {
                root["Rival"] = new JBool(true);
                root["PersonalityType"] = new JNum(3);
            }
            SimFile sim = new SimFile(Path.Combine(_pack.Dir, name + ".sim.json"), root);
            sim.Save();
            _pack.Sims.Add(sim);
            Created = sim;
            DialogResult = true;
        }
    }
}
