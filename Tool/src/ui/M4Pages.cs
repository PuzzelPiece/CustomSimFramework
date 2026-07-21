using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PackStudio
{
    /// <summary>
    /// M4: "Check my log". After a game session, shows what the mod's loader
    /// actually did with the packs (parse counts, injections, warnings),
    /// filtered from BepInEx's LogOutput.log and colored by level. This closes
    /// the feedback loop novices otherwise never close.
    /// </summary>
    internal sealed class LogPage : DockPanel
    {
        private readonly InstallTarget _target;
        private readonly ListBox _lines = new ListBox
        {
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        };
        private readonly TextBlock _summary = Theme.Dim("");

        internal LogPage(InstallTarget target)
        {
            _target = target;

            StackPanel top = new StackPanel();
            top.Children.Add(Theme.Title("Check my log"));
            top.Children.Add(Theme.Dim("Deploy your pack, start Erenshor once and reach the world, then"
                + " quit and read the log here. It shows exactly what the game loaded, including"
                + " parse counts, injected sims and any loader warnings."));
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) };
            Button read = new Button { Content = "📖 Read my log" };
            read.Click += delegate { ReadLog(); };
            buttons.Children.Add(read);
            _summary.Margin = new Thickness(10, 0, 0, 0);
            _summary.VerticalAlignment = VerticalAlignment.Center;
            buttons.Children.Add(_summary);
            top.Children.Add(buttons);
            SetDock(top, Dock.Top);
            Children.Add(top);

            ScrollViewer.SetHorizontalScrollBarVisibility(_lines, ScrollBarVisibility.Disabled);
            Children.Add(_lines);
        }

        private string LogPath()
        {
            if (_target == null) { return null; }
            // PacksRoot = <game>\BepInEx\plugins\CustomSimFramework\Packs
            string bepinex = Path.GetFullPath(Path.Combine(_target.PacksRoot, "..", "..", ".."));
            string path = Path.Combine(bepinex, "LogOutput.log");
            return File.Exists(path) ? path : null;
        }

        private void ReadLog()
        {
            _lines.Items.Clear();
            string path = LogPath();
            if (path == null)
            {
                _summary.Text = "no LogOutput.log found - start Erenshor once first"
                    + (_target == null ? " (no game install detected)" : "");
                return;
            }
            int warnings = 0;
            int errors = 0;
            int shown = 0;
            DateTime stamp = File.GetLastWriteTime(path);
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    // BepInEx line shape: "[Level  :Custom Sim Framework] message"
                    int tag = raw.IndexOf(":Custom Sim Framework]", StringComparison.Ordinal);
                    if (tag < 0) { continue; }
                    string message = raw.Substring(tag + ":Custom Sim Framework]".Length).Trim();
                    System.Windows.Media.Brush brush = Theme.Ok;
                    if (raw.StartsWith("[Error", StringComparison.Ordinal))
                    {
                        brush = Theme.Blocker;
                        errors++;
                    }
                    else if (raw.StartsWith("[Warning", StringComparison.Ordinal))
                    {
                        brush = Theme.Warning;
                        warnings++;
                    }
                    _lines.Items.Add(new TextBlock
                    {
                        Text = message,
                        Foreground = brush,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap,
                    });
                    shown++;
                }
            }
            catch (Exception ex)
            {
                _summary.Text = "could not read the log: " + ex.Message;
                return;
            }
            _summary.Text = shown == 0
                ? "log found, but no Custom Sim Framework lines - is the mod installed and enabled?"
                : shown + " line(s) from the session of " + stamp.ToString("g") + "  -  "
                    + errors + " error(s), " + warnings + " warning(s)";
        }
    }

    /// <summary>
    /// M4: raw file view for advanced users. Read-only on-disk content,
    /// open-in-Notepad / open-folder, and reload-from-disk after hand edits.
    /// Deliberately not an in-tool text editor: hand edits belong to a real
    /// editor, and the reload path keeps the DOM authoritative.
    /// </summary>
    internal sealed class FilesPage : DockPanel
    {
        private readonly Pack _pack;
        private readonly Action _reloadPack;
        private readonly TextBox _view = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        private readonly TextBlock _note = Theme.Dim("");
        private string _selectedPath;

        internal FilesPage(Pack pack, Action reloadPack)
        {
            _pack = pack;
            _reloadPack = reloadPack;

            StackPanel side = new StackPanel { Margin = new Thickness(0, 0, 12, 0), Width = 240 };
            side.Children.Add(Theme.Title("Files"));
            side.Children.Add(Theme.Dim("The pack's raw JSON, as it is ON DISK right now (advanced)."));
            ListBox files = new ListBox
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 8),
            };
            foreach (PackJsonFile file in pack.AllFiles())
            {
                files.Items.Add(file.FileName
                    + (file.LoadError != null ? "  ⛔ broken" : file.Dirty ? "  ● unsaved edits" : ""));
            }
            List<PackJsonFile> ordered = new List<PackJsonFile>(pack.AllFiles());
            files.SelectionChanged += delegate
            {
                if (files.SelectedIndex >= 0 && files.SelectedIndex < ordered.Count)
                {
                    ShowFile(ordered[files.SelectedIndex]);
                }
            };
            side.Children.Add(files);

            Button openFile = new Button { Content = "📝 Open in Notepad", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            openFile.Click += delegate
            {
                if (_selectedPath != null) { Process.Start("notepad.exe", "\"" + _selectedPath + "\""); }
            };
            Button openFolder = new Button { Content = "📂 Open pack folder", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
            openFolder.Click += delegate { Process.Start("explorer.exe", "\"" + pack.Dir + "\""); };
            Button reload = new Button { Content = "🔄 Reload pack from disk", HorizontalAlignment = HorizontalAlignment.Left };
            reload.ToolTip = "Re-reads every file - use after editing them outside the tool.";
            reload.Click += delegate { _reloadPack(); };
            side.Children.Add(openFile);
            side.Children.Add(openFolder);
            side.Children.Add(reload);

            SetDock(side, Dock.Left);
            Children.Add(side);

            DockPanel body = new DockPanel();
            _note.Margin = new Thickness(0, 0, 0, 4);
            SetDock(_note, Dock.Top);
            body.Children.Add(_note);
            body.Children.Add(_view);
            Children.Add(body);

            if (ordered.Count > 0) { files.SelectedIndex = 0; }
            else { _note.Text = "No files yet - everything you add in the editors creates them on save."; }
        }

        private void ShowFile(PackJsonFile file)
        {
            _selectedPath = file.Path;
            try
            {
                _view.Text = File.Exists(file.Path) ? File.ReadAllText(file.Path) : "(not on disk yet - unsaved)";
            }
            catch (Exception ex)
            {
                _view.Text = "(could not read: " + ex.Message + ")";
            }
            _note.Text = file.LoadError != null
                ? "⛔ This file failed to parse (" + file.LoadError + ") - the tool will NOT overwrite it."
                    + " Fix it in Notepad, then Reload."
                : file.Dirty
                    ? "● You have unsaved edits in the tool that are NOT in this on-disk view yet - Save to write them."
                    : "This is exactly what the game will load.";
        }
    }
}
