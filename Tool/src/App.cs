using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace PackStudio
{
    internal static class App
    {
        internal const string Version = "0.2.2"; // composed previews for fragment pools (guild questions etc.)

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--selftest")
            {
                string packDir = args.Length > 1 ? args[1] : null;
                return SelfTest.Run(packDir);
            }
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Theme.Apply(app);
            return app.Run(new MainWindow());
        }
    }

    /// <summary>
    /// Headless test mode: `PackStudio.exe --selftest <packdir>`.
    /// Writes selftest.log next to the EXE and returns 0 (pass) / 1 (fail).
    /// Covers: JSON round-trip fidelity on the given pack, validation
    /// regression (the shipped example pack must be blocker- and
    /// warning-free), and render-engine asserts against live-verified
    /// behavior (VERIFICATION.md).
    /// </summary>
    internal static class SelfTest
    {
        private static readonly StringBuilder Log = new StringBuilder();
        private static int _failures;

        internal static int Run(string packDir)
        {
            try
            {
                TestJsonBasics();
                TestPersonalize();
                TestCompositions();
                if (!string.IsNullOrEmpty(packDir) && Directory.Exists(packDir))
                {
                    TestRoundTrip(packDir);
                    TestValidation(packDir);
                    TestPageConstruction(packDir);
                }
                else
                {
                    Note("no pack dir given/found - pack tests skipped (pass a Packs/<name> path).");
                }
            }
            catch (Exception ex)
            {
                Fail("unhandled exception: " + ex);
            }
            Log.Insert(0, "PackStudio " + App.Version + " selftest - "
                + (_failures == 0 ? "ALL PASS" : _failures + " FAILURE(S)") + "\n\n");
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            File.WriteAllText(logPath, Log.ToString());
            return _failures == 0 ? 0 : 1;
        }

        private static void Pass(string what) { Log.AppendLine("PASS  " + what); }

        private static void Note(string what) { Log.AppendLine("note  " + what); }

        private static void Fail(string what)
        {
            _failures++;
            Log.AppendLine("FAIL  " + what);
        }

        private static void Check(bool condition, string what)
        {
            if (condition) { Pass(what); } else { Fail(what); }
        }

        private static void TestJsonBasics()
        {
            JObj obj = (JObj)Json.Parse("{\"a\": 1, \"b\": [\"x\", \"y\"], \"c\": {\"d\": true}, \"e\": null, \"f\": 4.5, \"g\": \"q\\\"\\n\\u0041\"}");
            Check(((JNum)obj["a"]).AsInt() == 1, "parse int");
            Check(PackJsonFile.LinesOf(obj["b"] as JArr).Count == 2, "parse string array");
            Check(((JBool)((JObj)obj["c"])["d"]).Value, "parse nested bool");
            Check(obj["e"] is JNull, "parse null");
            Check(((JNum)obj["f"]).Raw == "4.5", "number raw text preserved");
            Check(((JStr)obj["g"]).Value == "q\"\nA", "string escapes");
            Check(JNode.DeepEquals(Json.Parse(Json.Write(obj)), obj), "basic round-trip deep-equal");
            bool strict = false;
            try { Json.Parse("{\"a\": [1,] }"); }
            catch (FormatException) { strict = true; }
            Check(strict, "trailing comma rejected (JsonUtility parity)");
        }

        private static void TestPersonalize()
        {
            PreviewQuirks vex = new PreviewQuirks { RefersToSelfAs = "Vex", SimName = "Vex" };
            Check(Preview.Personalize("I'm busy", vex, null) == "Vex'm busy",
                "RSA: ASCII I'm renders Vex'm (contraction rules dead, 2026-07-20 finding)");
            Check(Preview.Personalize("victory is mine", vex, null) == "victory is Vex's",
                "RSA: mine -> Vex's");
            Check(Preview.Personalize("grats i guess", vex, null) == "grats Vex guess",
                "RSA: lowercase bare i converts (case-insensitive)");
            PreviewQuirks behox = new PreviewQuirks { ThirdPerson = true, SimName = "Behox" };
            Check(Preview.Personalize("I'm busy", behox, null) == "Behox is busy",
                "3P: ASCII I'm converts correctly");
            Check(Preview.Personalize("i'll pay well", behox, null) == "Behox'll pay well",
                "3P: ASCII I'll garbles (only curly converts)");
            PreviewQuirks lower = new PreviewQuirks { AllLowers = true, SimName = "Paul" };
            Check(Preview.Personalize("OK Sure", lower, null) == "ok sure", "AllLowers");
            Check(Preview.Personalize("", lower, null) == "lol", "empty renders lol");
        }

        /// <summary>Composed-preview regression: fragment pools must render
        /// inside their real in-game sentence (decompile-verified shapes).</summary>
        private static void TestCompositions()
        {
            PreviewQuirks plain = new PreviewQuirks { SimName = "Sim" };
            List<KeyValuePair<string, string>> ex;

            ex = Preview.RenderExamples("ItemSearch", "anybody know where", plain);
            Check(ex.Count == 1 && ex[0].Value == "hey hey anybody know where Funeral Shroud drops?",
                "ItemSearch composes with forced greeting + item + drops?");

            ex = Preview.RenderExamples("LevelAdvice", "whats efficient at", plain);
            Check(ex[0].Value.Contains("  level 12?"),
                "LevelAdvice keeps the game's double space before level");

            ex = Preview.RenderExamples("QuestAskEnding", "i can trade for it", plain);
            Check(ex[0].Value.Contains("? i can trade for it. "),
                "QuestAskEnding sits between the ? and the game's own period");

            ex = Preview.RenderExamples("Targeting", "burn down", plain);
            Check(ex[0].Value == "burn down A Brittle Skeleton",
                "Targeting appends the target name");

            ex = Preview.RenderExamples("OutOfZoneAnswers", "pretty sure that's over in", plain);
            Check(ex[0].Value.EndsWith("Faerie's Brake"),
                "OutOfZoneAnswers appends the zone");

            ex = Preview.RenderExamples("InterestMSG", "sign me up", plain);
            Check(ex.Count == 2 && ex[1].Value.EndsWith("I'm right here in your group!"),
                "InterestMSG shows the in-group variant");

            ex = Preview.RenderExamples("BadLastOuting", "we got humbled out there NN", plain);
            Check(ex[0].Value == "hey hey! we got humbled out there Alden.",
                "BadLastOuting renders inside the memory greeting with the game's period");
        }

        private static void TestRoundTrip(string packDir)
        {
            foreach (string file in Directory.GetFiles(packDir, "*.json"))
            {
                string name = Path.GetFileName(file);
                string text = File.ReadAllText(file);
                JNode parsed;
                try
                {
                    parsed = Json.Parse(text);
                }
                catch (Exception ex)
                {
                    Fail("parse " + name + ": " + ex.Message);
                    continue;
                }
                JNode reparsed = Json.Parse(Json.Write(parsed));
                Check(JNode.DeepEquals(parsed, reparsed),
                    "round-trip (keys, order, comments, values) " + name);
            }
        }

        /// <summary>UI smoke test: construct every page headlessly (STA main
        /// thread, no window shown). Catches metadata/binding crashes that
        /// only fire when a page builds its editors.</summary>
        private static void TestPageConstruction(string packDir)
        {
            Pack pack = Pack.Load(packDir);
            Action noop = delegate { };
            try
            {
                new SimsPage(pack, noop);
                Pass("SimsPage constructs (all sim editors)");
            }
            catch (Exception ex) { Fail("SimsPage construction: " + ex.Message); }
            try
            {
                GlobalPage global = new GlobalPage(pack, noop);
                Pass("GlobalPage constructs (section 1)");
            }
            catch (Exception ex) { Fail("GlobalPage construction: " + ex.Message); }
            try
            {
                new ZonesPage(pack, noop);
                Pass("ZonesPage constructs (AllZones view)");
            }
            catch (Exception ex) { Fail("ZonesPage construction: " + ex.Message); }
            try
            {
                new TopicsPage(pack, noop);
                Pass("TopicsPage constructs (topic 1 editor)");
            }
            catch (Exception ex) { Fail("TopicsPage construction: " + ex.Message); }
            try
            {
                new LogPage(null);
                Pass("LogPage constructs (no install)");
            }
            catch (Exception ex) { Fail("LogPage construction: " + ex.Message); }
            try
            {
                new FilesPage(pack, noop);
                Pass("FilesPage constructs (file 1 view)");
            }
            catch (Exception ex) { Fail("FilesPage construction: " + ex.Message); }
        }

        private static void TestValidation(string packDir)
        {
            Pack pack = Pack.Load(packDir);
            List<Finding> findings = Validation.ValidatePack(pack);
            int blockers = 0, warnings = 0, tips = 0;
            foreach (Finding finding in findings)
            {
                if (finding.Severity == Severity.Blocker) { blockers++; }
                else if (finding.Severity == Severity.Warning) { warnings++; }
                else { tips++; }
                Log.AppendLine("      " + finding);
            }
            Note("validation on '" + pack.Name + "': " + blockers + " blocker(s), "
                + warnings + " warning(s), " + tips + " tip(s)");
            Check(blockers == 0, "shipped pack has no blockers");
            Check(warnings == 0, "shipped pack has no warnings (tips allowed)");
        }
    }
}
