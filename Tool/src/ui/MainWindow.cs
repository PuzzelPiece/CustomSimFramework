using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PackStudio
{
    /// <summary>
    /// Shell: top bar (Open / Save / Deploy), left rail, page host, Problems
    /// panel, status bar, all themed via Theme (M1a). Pages manage their own
    /// scrolling (the sim editor keeps its sidebar fixed).
    /// </summary>
    internal sealed class MainWindow : Window
    {
        private readonly ContentControl _pageHost = new ContentControl { Margin = new Thickness(12) };
        private readonly ListBox _problems = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
        };
        private readonly TextBlock _status = new TextBlock
        {
            Margin = new Thickness(8, 3, 8, 3),
            Foreground = Theme.TextDim,
        };
        private readonly ListBox _rail = new ListBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = Theme.FontSize + 1,
            Margin = new Thickness(0, 8, 0, 0),
        };

        private readonly TextBlock _problemsCounts = new TextBlock { Foreground = Theme.TextDim, FontSize = 12 };
        private readonly System.Windows.Threading.DispatcherTimer _revalidateTimer =
            new System.Windows.Threading.DispatcherTimer();
        private bool _tipsExpanded;
        private InstallTarget _target;
        private Pack _pack;

        internal MainWindow()
        {
            Title = "Erenshor Pack Studio " + App.Version;
            Width = 1180;
            Height = 800;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.WindowBg;
            Foreground = Theme.Text;
            FontSize = Theme.FontSize;

            DockPanel root = new DockPanel();

            // top bar
            DockPanel top = new DockPanel { Background = Theme.Surface, LastChildFill = false };
            TextBlock brand = new TextBlock
            {
                Text = "  Erenshor Pack Studio",
                Foreground = Theme.Accent,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Button open = TopButton("Open / New pack");
            open.Click += delegate { ChoosePack(); };
            Button save = TopButton("💾 Save pack");
            save.Click += delegate { SavePack(); };
            Button deploy = TopButton("🚀 Deploy to game");
            deploy.Click += delegate { DeployPack(); };
            DockPanel.SetDock(brand, Dock.Left);
            DockPanel.SetDock(deploy, Dock.Right);
            DockPanel.SetDock(save, Dock.Right);
            DockPanel.SetDock(open, Dock.Right);
            top.Children.Add(brand);
            top.Children.Add(deploy);
            top.Children.Add(save);
            top.Children.Add(open);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            // status bar (target text + "change install" for multi-install users)
            DockPanel statusRow = new DockPanel { LastChildFill = true };
            Button changeTarget = new Button
            {
                Content = "change install",
                FontSize = 11,
                Padding = new Thickness(8, 1, 8, 1),
                Margin = new Thickness(4, 2, 6, 2),
                ToolTip = "Pick which Erenshor install the tool deploys to and reads logs from"
                    + " (remembered between sessions).",
            };
            changeTarget.Click += delegate { ChangeTarget(); };
            DockPanel.SetDock(changeTarget, Dock.Right);
            statusRow.Children.Add(changeTarget);
            statusRow.Children.Add(_status);
            Border statusBar = new Border { Background = Theme.Surface, Child = statusRow };
            DockPanel.SetDock(statusBar, Dock.Bottom);
            root.Children.Add(statusBar);

            // problems panel: wrapping items (horizontal scroll disabled so the
            // ListBox constrains item width. WPF measures at infinite width
            // otherwise and TextWrapping never engages), live count header,
            // tips collapsed behind a toggle (user feedback 2026-07-20).
            StackPanel problemsBody = new StackPanel();
            DockPanel problemsHeader = new DockPanel { Margin = new Thickness(8, 4, 8, 2) };
            TextBlock problemsTitle = Theme.SectionTitle("Problems");
            _problemsCounts.VerticalAlignment = VerticalAlignment.Bottom;
            _problemsCounts.Margin = new Thickness(10, 0, 0, 1);
            DockPanel.SetDock(problemsTitle, Dock.Left);
            problemsHeader.Children.Add(problemsTitle);
            problemsHeader.Children.Add(_problemsCounts);
            problemsBody.Children.Add(problemsHeader);
            _problems.Height = 120;
            ScrollViewer.SetHorizontalScrollBarVisibility(_problems, ScrollBarVisibility.Disabled);
            problemsBody.Children.Add(_problems);
            Border problemsBox = new Border
            {
                Background = Theme.Surface,
                BorderBrush = Theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(12, 0, 12, 8),
                Child = problemsBody,
            };
            DockPanel.SetDock(problemsBox, Dock.Bottom);
            root.Children.Add(problemsBox);

            _revalidateTimer.Interval = TimeSpan.FromMilliseconds(300);
            _revalidateTimer.Tick += delegate
            {
                _revalidateTimer.Stop();
                RevalidateNow();
            };

            // left rail
            _rail.Items.Add("🙂  My Sims");
            _rail.Items.Add("💬  Server Chatter");
            _rail.Items.Add("🗺  Zone Chatter");
            _rail.Items.Add("🏰  Guild Topics");
            _rail.Items.Add("📖  Check my log");
            _rail.Items.Add("🗃  Files (advanced)");
            _rail.SelectionChanged += delegate { ShowSelectedPage(); };
            Border railBorder = new Border
            {
                Width = 195,
                Background = Theme.Surface,
                BorderBrush = Theme.BorderBrush,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = _rail,
            };
            DockPanel.SetDock(railBorder, Dock.Left);
            root.Children.Add(railBorder);

            root.Children.Add(_pageHost);
            Content = root;
            Loaded += delegate { Startup(); };
        }

        private static Button TopButton(string label)
        {
            return new Button { Content = label, Margin = new Thickness(6), Padding = new Thickness(12, 4, 12, 4) };
        }

        // ── startup / pack choice ───────────────────────────────────

        private List<InstallTarget> _targets = new List<InstallTarget>();

        private void Startup()
        {
            Settings.Load();
            // A target remembered before mod v0.9.3 points at the old
            // plugins-based pack home. Translate it to the config home once.
            if (!string.IsNullOrEmpty(Settings.TargetPacksRoot))
            {
                int oldStyle = Settings.TargetPacksRoot.IndexOf(
                    "\\plugins\\CustomSimFramework\\Packs", StringComparison.OrdinalIgnoreCase);
                if (oldStyle >= 0)
                {
                    Settings.TargetPacksRoot = Settings.TargetPacksRoot.Substring(0, oldStyle)
                        + "\\config\\CustomSimFramework\\Packs";
                    Settings.Save();
                }
            }
            _targets = InstallLocator.FindAll();
            // A previously chosen target wins if it still exists (user pref,
            // saved via the status bar's "change install").
            if (!string.IsNullOrEmpty(Settings.TargetPacksRoot))
            {
                foreach (InstallTarget target in _targets)
                {
                    if (string.Equals(target.PacksRoot, Settings.TargetPacksRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        _target = target;
                    }
                }
                if (_target == null && Directory.Exists(Path.GetDirectoryName(Settings.TargetPacksRoot)))
                {
                    _target = new InstallTarget { Label = "Saved choice", PacksRoot = Settings.TargetPacksRoot };
                    _targets.Insert(0, _target);
                }
            }
            if (_target == null && _targets.Count > 0)
            {
                _target = _targets[0];
            }
            UpdateTargetStatus();
            if (_target == null)
            {
                ShowSelectedPage();
                return;
            }
            if (Directory.Exists(_target.PacksRoot))
            {
                foreach (string dir in Directory.GetDirectories(_target.PacksRoot))
                {
                    LoadPack(dir);
                    return;
                }
            }
            ShowSelectedPage();
        }

        private void UpdateTargetStatus()
        {
            _status.Text = _target == null
                ? "No Erenshor install found automatically - use 'Open / New pack' to browse, or 'change install'."
                : "Using " + _target.Label + "  (" + _target.PacksRoot + ")"
                    + (_targets.Count > 1 ? "   -   " + _targets.Count + " installs detected" : "");
        }

        private void ChangeTarget()
        {
            TargetDialog dialog = new TargetDialog(_targets) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Chosen != null)
            {
                _target = dialog.Chosen;
                if (!_targets.Contains(_target)) { _targets.Add(_target); }
                Settings.TargetPacksRoot = _target.PacksRoot;
                Settings.Save();
                UpdateTargetStatus();
            }
        }

        private void ChoosePack()
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Pick a pack folder (a folder inside CustomSimFramework/Packs)."
                + " Pick an EMPTY folder to start a new pack.";
            if (_target != null && Directory.Exists(_target.PacksRoot))
            {
                dialog.SelectedPath = _target.PacksRoot;
            }
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            string picked = dialog.SelectedPath;
            // Opening the Packs ROOT (or any folder whose subfolders are the
            // actual packs) would let the user edit a "pack" the mod never
            // loads. The loader only reads Packs/<pack name>/ subfolders.
            bool looksLikePacksRoot = string.Equals(Path.GetFileName(picked), "Packs", StringComparison.OrdinalIgnoreCase)
                || (_target != null && string.Equals(Path.GetFullPath(picked).TrimEnd('\\'),
                    Path.GetFullPath(_target.PacksRoot).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
            if (looksLikePacksRoot && Directory.GetFiles(picked, "*.json").Length == 0)
            {
                MessageBox.Show("That's the Packs folder itself - packs are the folders INSIDE it.\n\n"
                    + "Open one of those folders, or create a new folder in there for a new pack.",
                    "Pick a pack folder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            LoadPack(picked);
        }

        private void LoadPack(string dir)
        {
            _pack = Pack.Load(dir);
            Title = "Erenshor Pack Studio " + App.Version + "  -  " + _pack.Name;
            if (_rail.SelectedIndex < 0)
            {
                _rail.SelectedIndex = 0; // triggers ShowSelectedPage
            }
            else
            {
                ShowSelectedPage();
            }
            Revalidate();
        }

        private void SavePack()
        {
            if (_pack == null) { return; }
            List<string> refused = _pack.SaveAll();
            List<Finding> findings = RevalidateNow();
            int problems = 0;
            foreach (Finding finding in findings)
            {
                if (finding.Severity != Severity.Tip) { problems++; }
            }
            string status = "Saved. " + (problems == 0 ? "No problems." : problems + " problem(s) - see the Problems panel.");
            if (refused.Count > 0)
            {
                status = "Saved, EXCEPT " + string.Join(", ", refused.ToArray())
                    + " - that file failed to load (broken JSON) and overwriting it would destroy the"
                    + " original. Fix it in a text editor, then reopen the pack. " + status;
            }
            _status.Text = status;
            // Saving scrubs empty rows out of the DOM. Rebuild the page so
            // the visible rows (and Files-page badges) match it again.
            ShowSelectedPage();
        }

        private void DeployPack()
        {
            if (_pack == null) { return; }
            if (_target == null)
            {
                _status.Text = "No game install detected - can't deploy. Use 'Open / New pack' to work"
                    + " directly inside a Packs folder instead.";
                return;
            }
            string dest = Path.Combine(_target.PacksRoot, _pack.Name);
            bool samePlace = string.Equals(Path.GetFullPath(_pack.Dir), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase);
            if (MessageBox.Show(samePlace
                ? "Save this pack? (It already lives in the game's Packs folder.)\n\nChanges load next time you start Erenshor."
                : "Copy pack '" + _pack.Name + "' to:\n" + dest + "\n\nFiles you deleted here are removed"
                    + " there too, so the game matches what you see. Changes load next time you start Erenshor.",
                "Deploy to game", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return;
            }
            List<string> refused = _pack.SaveAll();
            if (!samePlace)
            {
                Directory.CreateDirectory(dest);
                HashSet<string> sourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in Directory.GetFiles(_pack.Dir, "*.json"))
                {
                    sourceNames.Add(Path.GetFileName(file));
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
                }
                // Mirror deletions: a sim/file removed locally must not keep
                // loading from a stale deployed copy.
                foreach (string file in Directory.GetFiles(dest, "*.json"))
                {
                    if (!sourceNames.Contains(Path.GetFileName(file)))
                    {
                        File.Delete(file);
                    }
                }
            }
            _status.Text = "Deployed to " + dest + " - start Erenshor to see it in-game."
                + (refused.Count > 0 ? " (NOT deployed: " + string.Join(", ", refused.ToArray())
                    + " - broken JSON, fix by hand.)" : "");
            ShowSelectedPage(); // resync rows/badges after the save
        }

        // ── pages ───────────────────────────────────────────────────

        private void ShowSelectedPage()
        {
            if (_pack == null)
            {
                _pageHost.Content = Theme.Body("Open or create a pack to begin.");
                return;
            }
            switch (_rail.SelectedIndex)
            {
                case 0:
                    _pageHost.Content = new SimsPage(_pack, delegate { Revalidate(); });
                    break;
                case 1:
                    _pageHost.Content = new GlobalPage(_pack, delegate { Revalidate(); });
                    break;
                case 2:
                    _pageHost.Content = new ZonesPage(_pack, delegate { Revalidate(); });
                    break;
                case 3:
                    _pageHost.Content = new TopicsPage(_pack, delegate { Revalidate(); });
                    break;
                case 4:
                    _pageHost.Content = new LogPage(_target);
                    break;
                default:
                    _pageHost.Content = new FilesPage(_pack, delegate { ReloadPack(); });
                    break;
            }
        }

        private UIElement Placeholder(string title, string text)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.Title(title));
            TextBlock body = Theme.Body(text);
            body.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(body);
            return panel;
        }

        // ── validation plumbing ────────────────────────────────────

        /// <summary>Debounced: rapid edit bursts (slider drags, quick typing
        /// across boxes) coalesce into one validation run ~300ms later.</summary>
        private void Revalidate()
        {
            _revalidateTimer.Stop();
            _revalidateTimer.Start();
        }

        private List<Finding> RevalidateNow()
        {
            _revalidateTimer.Stop();
            if (_pack == null)
            {
                _problems.Items.Clear();
                _problemsCounts.Text = "";
                return new List<Finding>();
            }
            List<Finding> findings = Validation.ValidatePack(_pack);
            RenderProblems(findings);
            return findings;
        }

        private void RenderProblems(List<Finding> findings)
        {
            _problems.Items.Clear();
            List<Finding> blockers = new List<Finding>();
            List<Finding> warnings = new List<Finding>();
            List<Finding> tips = new List<Finding>();
            foreach (Finding finding in findings)
            {
                if (finding.Severity == Severity.Blocker) { blockers.Add(finding); }
                else if (finding.Severity == Severity.Warning) { warnings.Add(finding); }
                else { tips.Add(finding); }
            }
            _problemsCounts.Text = findings.Count == 0 ? "all clear"
                : blockers.Count + " blocker(s) · " + warnings.Count + " warning(s) · " + tips.Count + " tip(s)";
            foreach (Finding finding in blockers) { _problems.Items.Add(ProblemItem(finding, Theme.Blocker)); }
            foreach (Finding finding in warnings) { _problems.Items.Add(ProblemItem(finding, Theme.Warning)); }
            if (tips.Count > 0)
            {
                TextBlock toggle = new TextBlock
                {
                    Text = (_tipsExpanded ? "▾ " : "▸ ") + tips.Count + " tip(s)"
                        + (_tipsExpanded ? "" : "  (click to show)"),
                    Foreground = Theme.Tip,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };
                List<Finding> captured = findings;
                toggle.MouseLeftButtonUp += delegate
                {
                    _tipsExpanded = !_tipsExpanded;
                    RenderProblems(captured);
                };
                _problems.Items.Add(toggle);
                if (_tipsExpanded)
                {
                    foreach (Finding finding in tips) { _problems.Items.Add(ProblemItem(finding, Theme.Tip)); }
                }
            }
            if (findings.Count == 0)
            {
                _problems.Items.Add(new TextBlock { Text = "No problems found.", Foreground = Theme.Ok, FontSize = 12 });
            }
        }

        private TextBlock ProblemItem(Finding finding, Brush brush)
        {
            return new TextBlock
            {
                Text = FriendlyFinding(finding),
                Foreground = brush,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };
        }

        /// <summary>Unsaved edits must never be lost to the window's ✕: force
        /// the focused line box to commit (the close button never moves WPF
        /// focus), then offer save / discard / cancel.</summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Focus(); // moves logical focus off any TextBox -> commits its edit
            bool dirty = false;
            if (_pack != null)
            {
                foreach (PackJsonFile file in _pack.AllFiles())
                {
                    if (file.Dirty) { dirty = true; }
                }
            }
            if (dirty)
            {
                MessageBoxResult choice = MessageBox.Show(
                    "Save your changes to '" + _pack.Name + "' before closing?",
                    "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (choice == MessageBoxResult.Yes)
                {
                    List<string> refused = _pack.SaveAll();
                    if (refused.Count > 0)
                    {
                        MessageBox.Show("NOT saved (broken JSON, fix in a text editor): "
                            + string.Join(", ", refused.ToArray()), "Some files were not saved");
                    }
                }
            }
            base.OnClosing(e);
        }

        /// <summary>Re-reads the whole pack from disk (after hand edits in an
        /// external editor). Unsaved in-tool edits prompt first.</summary>
        private void ReloadPack()
        {
            if (_pack == null) { return; }
            Focus(); // commit any pending line edit
            bool dirty = false;
            foreach (PackJsonFile file in _pack.AllFiles())
            {
                if (file.Dirty) { dirty = true; }
            }
            if (dirty)
            {
                MessageBoxResult choice = MessageBox.Show(
                    "Reload from disk? You have unsaved edits in the tool.\n\nYes = save them first,"
                    + " No = discard them and load what's on disk.",
                    "Reload pack", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Cancel) { return; }
                if (choice == MessageBoxResult.Yes) { _pack.SaveAll(); }
            }
            LoadPack(_pack.Dir);
            _status.Text = "Pack reloaded from disk.";
        }

        /// <summary>
        /// Problems entries name the PAGE the way the rail does ("My Sims ›
        /// Paul › Greetings, line 2"), with the raw filename demoted to the
        /// end (user feedback 2026-07-20). The validation engine itself keeps
        /// emitting file/where. This is display-only.
        /// </summary>
        private string FriendlyFinding(Finding finding)
        {
            string tag = finding.Severity == Severity.Blocker ? "BLOCKER"
                : finding.Severity == Severity.Warning ? "warning" : "tip";
            string page;
            string detail = finding.Where;
            if (finding.File.EndsWith(".sim.json", StringComparison.OrdinalIgnoreCase))
            {
                string simName = finding.File.Substring(0, finding.File.Length - ".sim.json".Length);
                foreach (SimFile sim in _pack.Sims)
                {
                    if (sim.FileName == finding.File) { simName = sim.SimName; }
                }
                page = "My Sims › " + simName;
                detail = MapWhere(finding.Where, null);
            }
            else if (finding.File == "global.dialogue.json")
            {
                page = "Server Chatter";
                detail = MapWhere(finding.Where, GlobalMeta.Sections);
            }
            else if (finding.File == "zones.dialogue.json") { page = "Zone Chatter"; }
            else if (finding.File == "topics.dialogue.json") { page = "Guild Topics"; }
            else { page = finding.File; }
            return "[" + tag + "]  " + page + " › " + detail + ":  " + finding.Message
                + "   (" + finding.File + ")";
        }

        /// <summary>Translates "Section.Pool, line N" / "ListKey, line N" into
        /// the titles the editor pages show. Falls back to the raw text.</summary>
        private static string MapWhere(string where, SectionMeta[] sections)
        {
            string location = where;
            string rest = "";
            int comma = where.IndexOf(", ", StringComparison.Ordinal);
            if (comma >= 0)
            {
                location = where.Substring(0, comma);
                rest = where.Substring(comma);
            }
            if (sections != null)
            {
                int dot = location.IndexOf('.');
                string sectionKey = dot >= 0 ? location.Substring(0, dot) : "";
                string poolKey = dot >= 0 ? location.Substring(dot + 1) : location;
                foreach (SectionMeta section in sections)
                {
                    if (section.Key != sectionKey) { continue; }
                    foreach (PoolMeta pool in section.Pools)
                    {
                        if (pool.Key == poolKey)
                        {
                            return section.Title + " › " + pool.Title + rest;
                        }
                    }
                }
                return where;
            }
            ListMeta meta = SimListMeta.Get(location);
            return meta != null ? meta.Title + rest : where;
        }
    }

    /// <summary>Install picker for multi-install users (status bar "change
    /// install"). Detected installs plus a browse fallback. The choice is
    /// remembered in settings.</summary>
    internal sealed class TargetDialog : Window
    {
        internal InstallTarget Chosen;
        private readonly ListBox _list = new ListBox { MaxHeight = 260, MinWidth = 460 };
        private readonly List<InstallTarget> _targets;

        internal TargetDialog(List<InstallTarget> targets)
        {
            _targets = targets;
            Title = "Which Erenshor install?";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Theme.WindowBg;
            ResizeMode = ResizeMode.NoResize;

            StackPanel body = new StackPanel { Margin = new Thickness(16) };
            body.Children.Add(Theme.Title("Which install should the tool use?"));
            TextBlock hint = Theme.Dim("Deploy and Check-my-log point at this install. Remembered"
                + " between sessions.");
            hint.Margin = new Thickness(0, 4, 0, 8);
            body.Children.Add(hint);
            foreach (InstallTarget target in targets)
            {
                _list.Items.Add(target.Label + "  -  " + target.PacksRoot);
            }
            if (targets.Count > 0) { _list.SelectedIndex = 0; }
            body.Children.Add(_list);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button browse = new Button { Content = "Browse for game folder...", Margin = new Thickness(0, 0, 8, 0) };
            browse.Click += delegate { Browse(); };
            Button ok = new Button { Content = "Use this install", Width = 130, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += delegate
            {
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _targets.Count)
                {
                    Chosen = _targets[_list.SelectedIndex];
                    DialogResult = true;
                }
            };
            Button cancel = new Button { Content = "Cancel", Width = 90 };
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(browse);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            body.Children.Add(buttons);
            Content = body;
        }

        private void Browse()
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Pick the Erenshor game folder (the one containing BepInEx), or the"
                + " BepInEx folder itself.";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) { return; }
            string picked = dialog.SelectedPath;
            string bepinex = null;
            if (Directory.Exists(Path.Combine(picked, "BepInEx")))
            {
                bepinex = Path.Combine(picked, "BepInEx");
            }
            else if (string.Equals(Path.GetFileName(picked), "BepInEx", StringComparison.OrdinalIgnoreCase))
            {
                bepinex = picked;
            }
            if (bepinex == null)
            {
                MessageBox.Show(this, "No BepInEx folder found there - pick the game folder that"
                    + " contains BepInEx (the mod loader must be installed).", "Not a modded install");
                return;
            }
            Chosen = new InstallTarget
            {
                Label = "Custom (browsed)",
                PacksRoot = Path.Combine(bepinex, Path.Combine("config", Path.Combine("CustomSimFramework", "Packs"))),
            };
            DialogResult = true;
        }
    }
}
