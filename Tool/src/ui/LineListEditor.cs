using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PackStudio
{
    /// <summary>
    /// The workhorse control shared by all editors: title + dilution note,
    /// plain-language guidance, editable lines with GHOST PLACEHOLDERS
    /// (greyed example text in empty boxes, display-only), add/remove, a
    /// per-line render flyout, and the collapsible PreviewPane with the
    /// quirk sandbox.
    /// </summary>
    internal sealed class LineListEditor : Border
    {
        private readonly StackPanel _rows = new StackPanel();
        private readonly TextBlock _previewBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 6, 4, 2),
            Foreground = Theme.Tip,
            FontStyle = FontStyles.Italic,
        };

        private Func<List<string>> _get;
        private Action<List<string>> _set;
        private string _key;
        private Func<PreviewQuirks> _quirks;
        private Action _onChanged;
        private string _placeholder = "type a line...";
        private PreviewPane _previewPane;
        private bool _showPreview = true;

        internal LineListEditor(string title, string guidance, int vanillaPoolSize)
        {
            BorderBrush = Theme.BorderBrush;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(4);
            Margin = new Thickness(0, 0, 0, 10);
            Padding = new Thickness(10);
            Background = Theme.Surface;

            StackPanel body = new StackPanel();
            DockPanel header = new DockPanel();
            TextBlock titleBlock = Theme.SectionTitle(title);
            TextBlock dilution = Theme.Dim(vanillaPoolSize > 0
                ? "vanilla has " + vanillaPoolSize + " line(s) here"
                : "");
            dilution.VerticalAlignment = VerticalAlignment.Bottom;
            dilution.Margin = new Thickness(10, 0, 0, 1);
            DockPanel.SetDock(titleBlock, Dock.Left);
            header.Children.Add(titleBlock);
            header.Children.Add(dilution);

            TextBlock guidanceBlock = Theme.Dim(guidance);
            guidanceBlock.Margin = new Thickness(0, 3, 0, 7);

            Button add = new Button
            {
                Content = "+ Add line",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0),
            };
            add.Click += delegate
            {
                List<string> lines = Lines();
                lines.Add("");
                Commit(lines);
                BuildRows(lines);
                FocusLastRow();
            };

            body.Children.Add(header);
            body.Children.Add(guidanceBlock);
            body.Children.Add(_rows);
            body.Children.Add(add);
            body.Children.Add(_previewBlock);
            Child = body;
        }

        /// <summary>Root-level list on a file (per-sim lists, AllZones...).</summary>
        internal void Bind(PackJsonFile file, string key, string speakerName,
            Func<PreviewQuirks> simQuirks, Action onChanged)
        {
            BindCustom(delegate { return file.GetLines(key); },
                delegate(List<string> lines) { file.SetLines(key, lines); },
                key, speakerName, simQuirks, onChanged, true, null);
        }

        /// <summary>
        /// Arbitrary storage (nested sections, zone entries). simQuirks null =
        /// shared pool (PreviewPane shows quirk presets). showPreview false
        /// hides preview UI entirely (LISTEN pools, names, bios, lines the
        /// game never SPEAKS). placeholder null = resolve from listKey.
        /// </summary>
        internal void BindCustom(Func<List<string>> get, Action<List<string>> set, string listKey,
            string speakerName, Func<PreviewQuirks> simQuirks, Action onChanged,
            bool showPreview, string placeholder)
        {
            _get = get;
            _set = set;
            _key = listKey;
            _quirks = simQuirks;
            _onChanged = onChanged;
            _showPreview = showPreview;
            _placeholder = "e.g.  " + (placeholder ?? SimListMeta.ExampleFor(listKey));
            _previewBlock.Text = "";
            if (_previewPane != null)
            {
                ((StackPanel)Child).Children.Remove(_previewPane);
                _previewPane = null;
            }
            if (showPreview)
            {
                _previewPane = new PreviewPane(listKey, speakerName, Lines, simQuirks);
                ((StackPanel)Child).Children.Add(_previewPane);
            }
            BuildRows(Lines());
        }

        private List<string> Lines()
        {
            return _get != null ? _get() : new List<string>();
        }

        private void Commit(List<string> lines)
        {
            if (_set == null) { return; }
            _set(lines);
            if (_onChanged != null) { _onChanged(); }
            if (_previewPane != null) { _previewPane.Render(); }
        }

        private void BuildRows(List<string> lines)
        {
            _rows.Children.Clear();
            for (int i = 0; i < lines.Count; i++)
            {
                _rows.Children.Add(BuildRow(i, lines[i]));
            }
            if (lines.Count == 0)
            {
                _rows.Children.Add(Theme.Dim("(no lines yet, so the game auto-fills this list from its normal pools)"));
            }
        }

        private UIElement BuildRow(int index, string text)
        {
            DockPanel row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            Button remove = new Button
            {
                Content = "✕",
                Width = 26,
                Padding = new Thickness(0, 2, 0, 2),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Remove this line",
            };
            DockPanel.SetDock(remove, Dock.Right);

            // ghost placeholder: greyed example under a transparent-background
            // TextBox; visible only while the box is empty. Display-only.
            Grid cell = new Grid();
            TextBlock ghost = new TextBlock
            {
                Text = _placeholder,
                Foreground = Theme.TextDim,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(7, 3, 0, 0),
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed,
            };
            TextBox box = new TextBox
            {
                Text = text,
                Tag = index,
                Background = System.Windows.Media.Brushes.Transparent,
            };
            Border inputBg = new Border
            {
                Background = Theme.InputBg,
                CornerRadius = new CornerRadius(3),
            };
            cell.Children.Add(inputBg);
            cell.Children.Add(ghost);
            cell.Children.Add(box);

            remove.Click += delegate
            {
                List<string> lines = Lines();
                int i = (int)box.Tag;
                if (i >= 0 && i < lines.Count)
                {
                    lines.RemoveAt(i);
                    Commit(lines);
                    BuildRows(lines);
                }
                else
                {
                    // Stale row (its empty line was scrubbed by a save) -
                    // just resync the rows with the real list.
                    BuildRows(lines);
                }
            };
            box.LostFocus += delegate
            {
                List<string> lines = Lines();
                int i = (int)box.Tag;
                // Pasted line breaks would recreate the vanilla "paste blob"
                // bug (one shout containing a whole wall of text), so flatten.
                string flattened = box.Text.Replace("\r", " ").Replace("\n", " ");
                if (i >= 0 && i < lines.Count && lines[i] != flattened)
                {
                    lines[i] = flattened;
                    box.Text = flattened;
                    Commit(lines);
                }
                else if (i >= lines.Count && !string.IsNullOrWhiteSpace(flattened))
                {
                    // The row's empty line was scrubbed out of the list by a
                    // save. The row still exists on screen, so the intent is
                    // unambiguous: APPEND instead of silently dropping the text
                    // (verified edit-loss bug, review pass 3).
                    lines.Add(flattened);
                    box.Tag = lines.Count - 1;
                    box.Text = flattened;
                    Commit(lines);
                }
            };
            box.GotKeyboardFocus += delegate { ShowFlyout(box.Text); };
            box.TextChanged += delegate
            {
                ghost.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;
                ShowFlyout(box.Text);
            };

            row.Children.Add(remove);
            row.Children.Add(cell);
            return row;
        }

        private void FocusLastRow()
        {
            if (_rows.Children.Count == 0) { return; }
            DockPanel row = _rows.Children[_rows.Children.Count - 1] as DockPanel;
            if (row == null) { return; }
            foreach (object child in row.Children)
            {
                Grid cell = child as Grid;
                if (cell == null) { continue; }
                foreach (object inner in cell.Children)
                {
                    TextBox box = inner as TextBox;
                    if (box != null) { box.Focus(); }
                }
            }
        }

        private void ShowFlyout(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !_showPreview)
            {
                _previewBlock.Text = "";
                return;
            }
            PreviewQuirks quirks = _quirks != null ? _quirks() : new PreviewQuirks();
            List<KeyValuePair<string, string>> examples = Preview.RenderExamples(_key, line, quirks);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("In-game this can look like:");
            foreach (KeyValuePair<string, string> example in examples)
            {
                sb.Append("\n  • ").Append(example.Value).Append("   (").Append(example.Key).Append(')');
            }
            _previewBlock.Text = sb.ToString();
        }
    }
}
