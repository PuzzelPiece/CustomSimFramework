using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PackStudio
{
    /// <summary>
    /// "Preview all lines" pane with the QUIRK SANDBOX (a must feature,
    /// 2026-07-20): renders every line of a list through its real
    /// composition templates, styled like the in-game chat log, with
    /// toggleable quirks so authors see renders, including the garble
    /// cases, without launching the game. Defaults to the sim's actual
    /// quirks. Shared pools get presets instead.
    /// </summary>
    internal sealed class PreviewPane : StackPanel
    {
        private readonly string _listKey;
        private readonly Func<List<string>> _lines;
        private readonly Func<PreviewQuirks> _simQuirks; // null for shared pools
        private readonly string _speakerName;

        private readonly StackPanel _output = new StackPanel { Margin = new Thickness(2, 6, 2, 2) };
        private readonly CheckBox _caps = SandboxCheck("ALL CAPS");
        private readonly CheckBox _lowers = SandboxCheck("all lowercase");
        private readonly CheckBox _third = SandboxCheck("third-person");
        private readonly CheckBox _emoji = SandboxCheck("emojis");
        private readonly TextBox _refersAs = new TextBox { Width = 90, ToolTip = "RefersToSelfAs (empty = off)" };
        private readonly Slider _typo = new Slider
        {
            Minimum = 0, Maximum = 10, Width = 90, Value = 0,
            ToolTip = "Typo rate (vanilla sims use 0-10)",
            VerticalAlignment = VerticalAlignment.Center,
        };
        private Random _dice = new Random(12345);
        private bool _open;
        private readonly StackPanel _body = new StackPanel { Visibility = Visibility.Collapsed };
        private readonly Button _toggle = new Button { HorizontalAlignment = HorizontalAlignment.Left };

        internal PreviewPane(string listKey, string speakerName, Func<List<string>> lines, Func<PreviewQuirks> simQuirks)
        {
            _listKey = listKey;
            _speakerName = speakerName;
            _lines = lines;
            _simQuirks = simQuirks;
            Margin = new Thickness(0, 6, 0, 0);

            _toggle.Content = "▸ Preview all lines";
            _toggle.Click += delegate { Toggle(); };
            Children.Add(_toggle);

            // sandbox bar
            WrapPanel bar = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            bar.Children.Add(Theme.Dim("Try quirks:  "));
            bar.Children.Add(Pad(_caps));
            bar.Children.Add(Pad(_lowers));
            bar.Children.Add(Pad(_third));
            bar.Children.Add(Pad(_emoji));
            bar.Children.Add(Pad(Label("says \"I\" as:")));
            bar.Children.Add(Pad(_refersAs));
            bar.Children.Add(Pad(Label("typos:")));
            bar.Children.Add(Pad(_typo));
            Button reroll = new Button { Content = "🎲 re-roll", ToolTip = "Re-roll the typo / sign-off / emoji dice" };
            reroll.Click += delegate { _dice = new Random(Environment.TickCount); Render(); };
            bar.Children.Add(Pad(reroll));
            // (No explicit "reset to sim's quirks" button: opening the pane
            // always reloads the sim's real quirks, so collapse/expand resets.)
            if (_simQuirks == null)
            {
                // shared pools: any sim may speak the line, so offer presets
                ComboBox presets = new ComboBox { Width = 170 };
                presets.Items.Add("plain sim");
                presets.Items.Add("lowercase + typos");
                presets.Items.Add("third-person");
                presets.Items.Add("refers-to-self (\"Vex\")");
                presets.SelectedIndex = 0;
                presets.SelectionChanged += delegate
                {
                    _caps.IsChecked = false;
                    _lowers.IsChecked = presets.SelectedIndex == 1;
                    _third.IsChecked = presets.SelectedIndex == 2;
                    _refersAs.Text = presets.SelectedIndex == 3 ? "Vex" : "";
                    _typo.Value = presets.SelectedIndex == 1 ? 4 : 0;
                    Render();
                };
                bar.Children.Add(Pad(Label("preset:")));
                bar.Children.Add(Pad(presets));
            }
            _body.Children.Add(bar);
            _body.Children.Add(_output);
            Children.Add(_body);

            RoutedEventHandler rerender = delegate { Render(); };
            _caps.Checked += rerender; _caps.Unchecked += rerender;
            _lowers.Checked += rerender; _lowers.Unchecked += rerender;
            _third.Checked += rerender; _third.Unchecked += rerender;
            _emoji.Checked += rerender; _emoji.Unchecked += rerender;
            _refersAs.TextChanged += delegate { Render(); };
            _typo.ValueChanged += delegate { Render(); };
        }

        private static CheckBox SandboxCheck(string label)
        {
            return new CheckBox { Content = label };
        }

        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.TextDim };
        }

        private static FrameworkElement Pad(FrameworkElement element)
        {
            element.Margin = new Thickness(0, 0, 10, 0);
            return element;
        }

        private void Toggle()
        {
            _open = !_open;
            _toggle.Content = (_open ? "▾" : "▸") + " Preview all lines";
            _body.Visibility = _open ? Visibility.Visible : Visibility.Collapsed;
            if (_open)
            {
                if (_simQuirks != null) { LoadSimQuirks(); }
                Render();
            }
        }

        private void LoadSimQuirks()
        {
            PreviewQuirks quirks = _simQuirks();
            _caps.IsChecked = quirks.AllCaps;
            _lowers.IsChecked = quirks.AllLowers;
            _third.IsChecked = quirks.ThirdPerson;
            _emoji.IsChecked = quirks.LovesEmojis;
            _refersAs.Text = quirks.RefersToSelfAs;
            _typo.Value = quirks.TypoRate;
        }

        private PreviewQuirks SandboxQuirks()
        {
            PreviewQuirks quirks = new PreviewQuirks
            {
                AllCaps = _caps.IsChecked == true,
                AllLowers = _lowers.IsChecked == true,
                ThirdPerson = _third.IsChecked == true,
                RefersToSelfAs = _refersAs.Text.Trim(),
                LovesEmojis = _emoji.IsChecked == true,
                TypoRate = _typo.Value,
                SimName = _speakerName,
            };
            // The sign-off dice ride along for other lists, but previewing the
            // SignOffLines editor itself with random EXTRA sign-offs appended
            // would be confusing. Its composition already demonstrates one.
            if (_simQuirks != null && _listKey != "SignOffLines")
            {
                quirks.SignOffLines = _simQuirks().SignOffLines;
            }
            return quirks;
        }

        /// <summary>Re-render (called by the editor after line edits too).</summary>
        internal void Render()
        {
            if (!_open) { return; }
            _output.Children.Clear();
            List<string> lines = _lines();
            if (lines.Count == 0)
            {
                _output.Children.Add(Theme.Dim("(no lines to preview, this list auto-fills from the game's pools)"));
                return;
            }
            PreviewQuirks quirks = SandboxQuirks();
            Random dice = new Random(_dice.Next()); // stable until re-rolled
            ListMeta meta = SimListMeta.Get(_listKey);
            ChatChannel channel = meta != null ? meta.Channel : ChatChannel.Say;
            // Compose PLAIN first, personalize exactly once (quirks + dice) -
            // running quirks inside RenderExamples AND here would double-apply.
            PreviewQuirks plain = new PreviewQuirks { SimName = _speakerName };
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }
                List<KeyValuePair<string, string>> examples = Preview.RenderExamples(_listKey, line, plain);
                for (int i = 0; i < examples.Count; i++)
                {
                    string rolled = Preview.Personalize(examples[i].Value, quirks, dice);
                    _output.Children.Add(ChatLine(channel, rolled, examples[i].Key));
                }
            }
        }

        private UIElement ChatLine(ChatChannel channel, string text, string label)
        {
            TextBlock block = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) };
            string prefix;
            System.Windows.Media.Brush brush;
            switch (channel)
            {
                case ChatChannel.Shout: prefix = _speakerName + " shouts: "; brush = Theme.ChatShout; break;
                case ChatChannel.Guild: prefix = _speakerName + " tells the guild: "; brush = Theme.ChatGuild; break;
                case ChatChannel.Whisper: prefix = "[WHISPER FROM] " + _speakerName + ": "; brush = Theme.ChatWhisper; break;
                default: prefix = _speakerName + " says: "; brush = Theme.Text; break;
            }
            block.Inlines.Add(new Run(prefix + text) { Foreground = brush });
            block.Inlines.Add(new Run("   (" + label + ")") { Foreground = Theme.TextDim, FontSize = Theme.FontSize - 2 });
            return block;
        }
    }
}
