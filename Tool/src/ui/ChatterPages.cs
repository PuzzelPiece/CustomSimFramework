using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PackStudio
{
    /// <summary>
    /// M2: Server Chatter (global.dialogue.json). Section tab strip on top,
    /// pool editors below (built lazily per section). Shared pools get the
    /// preset-based quirk sandbox; LISTEN pools and names/bios get no spoken
    /// preview. The file is only created once the author actually adds lines.
    /// </summary>
    internal sealed class GlobalPage : DockPanel
    {
        private readonly Pack _pack;
        private readonly Action _revalidate;
        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly ListBox _tabs = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Margin = new Thickness(0, 6, 0, 8),
        };

        internal GlobalPage(Pack pack, Action revalidate)
        {
            _pack = pack;
            _revalidate = revalidate;

            StackPanel top = new StackPanel();
            top.Children.Add(Theme.Title("Server Chatter"));
            top.Children.Add(Theme.Dim("Lines ADDED to the pools all generated sims share. Your lines join"
                + " the rotation and never replace vanilla, so a couple of lines per pool is plenty."
                + " Keep names of bosses, gods and places consistent with the real game."));
            ScrollViewer tabScroll = new ScrollViewer
            {
                Content = _tabs,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            // horizontal tab strip
            FrameworkElementFactory panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            _tabs.ItemsPanel = new ItemsPanelTemplate(panelFactory);
            foreach (SectionMeta section in GlobalMeta.Sections)
            {
                _tabs.Items.Add(section.Title);
            }
            _tabs.SelectionChanged += delegate { ShowSection(); };
            top.Children.Add(tabScroll);
            SetDock(top, Dock.Top);
            Children.Add(top);
            Children.Add(_scroll);
            _tabs.SelectedIndex = 0;
        }

        private void ShowSection()
        {
            if (_tabs.SelectedIndex < 0) { return; }
            SectionMeta section = GlobalMeta.Sections[_tabs.SelectedIndex];
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 8, 20) };
            TextBlock blurb = Theme.Dim(section.Blurb);
            blurb.Margin = new Thickness(0, 0, 0, 10);
            body.Children.Add(blurb);
            foreach (PoolMeta pool in section.Pools)
            {
                LineListEditor editor = new LineListEditor(pool.Title, pool.Guidance,
                    GlobalMeta.PoolSize(section, pool.Key));
                string sectionKey = section.Key;
                string listKey = pool.Key;
                Func<List<string>> get;
                Action<List<string>> set;
                if (sectionKey.Length == 0)
                {
                    get = delegate
                    {
                        return _pack.Global != null ? _pack.Global.GetLines(listKey) : new List<string>();
                    };
                    set = delegate(List<string> lines) { _pack.EnsureGlobal().SetLines(listKey, lines); };
                }
                else
                {
                    get = delegate
                    {
                        return _pack.Global != null
                            ? _pack.Global.GetLinesIn(sectionKey, listKey) : new List<string>();
                    };
                    set = delegate(List<string> lines) { _pack.EnsureGlobal().SetLinesIn(sectionKey, listKey, lines); };
                }
                editor.BindCustom(get, set, listKey, "A sim", null, _revalidate,
                    !pool.Listen, GlobalMeta.ExampleFor(section, pool.Key));
                body.Children.Add(editor);
            }
            _scroll.Content = body;
            _scroll.ScrollToTop();
        }
    }

    /// <summary>
    /// M2: Zone Chatter (zones.dialogue.json). Zone picker with level bands,
    /// vanilla chatter counts and "no vanilla chatter" badges; AllZones on top.
    /// </summary>
    internal sealed class ZonesPage : DockPanel
    {
        private readonly Pack _pack;
        private readonly Action _revalidate;
        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly List<ZoneEntry> _ordered = new List<ZoneEntry>();

        internal ZonesPage(Pack pack, Action revalidate)
        {
            _pack = pack;
            _revalidate = revalidate;

            foreach (ZoneEntry zone in RefGen.Atlas) { _ordered.Add(zone); }
            _ordered.Sort(delegate(ZoneEntry a, ZoneEntry b)
            {
                int byLevel = a.LevelLow.CompareTo(b.LevelLow);
                return byLevel != 0 ? byLevel : string.CompareOrdinal(a.Scene, b.Scene);
            });

            StackPanel side = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
            side.Children.Add(Theme.Title("Zone Chatter"));
            ListBox picker = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(0, 8, 0, 0),
                MaxHeight = 560,
            };
            picker.Items.Add("🌍  Everywhere (all zones)");
            foreach (ZoneEntry zone in _ordered)
            {
                string display = RefData.RuntimeDisplayName(zone.Scene);
                string badge = RefData.EmptyChatterScenes.Contains(zone.Scene) ? "  ❗" : "";
                picker.Items.Add(display + "  (" + zone.LevelLow + "-" + zone.LevelHigh + ")" + badge);
            }
            picker.SelectionChanged += delegate
            {
                if (picker.SelectedIndex == 0) { ShowAllZones(); }
                else if (picker.SelectedIndex > 0) { ShowZone(_ordered[picker.SelectedIndex - 1]); }
            };
            ScrollViewer sideScroll = new ScrollViewer
            {
                Content = picker,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            side.Children.Add(sideScroll);
            TextBlock legend = Theme.Dim("❗ means the zone has no vanilla chatter at all. Your lines become"
                + " that zone's entire ambient voice.");
            legend.Margin = new Thickness(0, 6, 0, 0);
            legend.MaxWidth = 240;
            side.Children.Add(legend);
            SetDock(side, Dock.Left);
            Children.Add(side);
            Children.Add(_scroll);
            picker.SelectedIndex = 0;
        }

        private void ShowAllZones()
        {
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 8, 20) };
            body.Children.Add(Theme.Title("Everywhere"));
            TextBlock note = Theme.Dim("These lines join EVERY zone's ambient pool - keep them zone-agnostic.");
            note.Margin = new Thickness(0, 4, 0, 10);
            body.Children.Add(note);
            LineListEditor editor = new LineListEditor("Everywhere lines",
                "Full-line ambient shouts added to every zone. Lines ending in '?' can get"
                + " yes/no answer-shouts from other sims.", 0);
            editor.BindCustom(
                delegate { return _pack.Zones != null ? _pack.Zones.GetLines("AllZones") : new List<string>(); },
                delegate(List<string> lines) { _pack.EnsureZones().SetLines("AllZones", lines); },
                "AllZones", "A sim", null, _revalidate, true, null);
            body.Children.Add(editor);
            _scroll.Content = body;
            _scroll.ScrollToTop();
        }

        private void ShowZone(ZoneEntry zone)
        {
            string display = RefData.RuntimeDisplayName(zone.Scene);
            StackPanel body = new StackPanel { Margin = new Thickness(0, 0, 8, 20) };
            body.Children.Add(Theme.Title(display));
            int vanillaCount;
            bool counted = RefGen.ZoneChatterCounts.TryGetValue(zone.Scene, out vanillaCount);
            string facts = "Levels " + zone.LevelLow + "-" + zone.LevelHigh
                + (zone.Dungeon ? "  •  dungeon" : "")
                + "  •  scene name \"" + zone.Scene + "\""
                + (counted ? "  •  " + vanillaCount + " vanilla chatter line(s)" : "");
            TextBlock factsBlock = Theme.Dim(facts);
            factsBlock.Margin = new Thickness(0, 4, 0, 2);
            body.Children.Add(factsBlock);
            if (RefData.EmptyChatterScenes.Contains(zone.Scene))
            {
                TextBlock badge = Theme.Body("❗ This zone ships with ZERO vanilla chatter. Your lines"
                    + " here, plus the Everywhere lines, are its entire ambient voice.");
                badge.Foreground = Theme.Warning;
                badge.Margin = new Thickness(0, 2, 0, 8);
                body.Children.Add(badge);
            }
            LineListEditor editor = new LineListEditor("Ambient chatter",
                "Full-line shouts sims make while idling in " + display + ". Lines ending in '?'"
                + " can get yes or no answer shouts from other sims, so sprinkle some in.",
                counted ? vanillaCount : 0);
            editor.BindCustom(
                delegate
                {
                    return _pack.Zones != null
                        ? _pack.Zones.GetZoneLines(zone.Scene, display) : new List<string>();
                },
                delegate(List<string> lines) { _pack.EnsureZones().SetZoneLines(zone.Scene, display, lines); },
                "ZoneComments", "A sim", null, _revalidate, true, null);
            body.Children.Add(editor);
            _scroll.Content = body;
            _scroll.ScrollToTop();
        }
    }
}
