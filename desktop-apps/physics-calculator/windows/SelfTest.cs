// Console check: runs ../tests.tsv (results recorded from the web calculator)
// through the C# engine and reports every difference. Used by build.ps1.
using System;
using System.IO;
using System.Text;

namespace PhysicsCalculator
{
    static class SelfTest
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var engine = new Engine();
            int total = 0, failed = 0;
            foreach (var line in File.ReadAllLines(args[0], Encoding.UTF8))
            {
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var c = line.Split('\t');
                engine.AngleMode = c[0];
                engine.DecPlaces = c[1] == "auto" ? (int?)null : int.Parse(c[1]);
                engine.SigFigs = c[2] == "auto" ? (int?)null : int.Parse(c[2]);
                engine.Pow10 = c[3] == "1";
                string shown, insert = "";
                try
                {
                    var r = engine.Evaluate(c[4]);
                    engine.LastAns = r.Q;
                    shown = Engine.JoinOut(engine.FormatResult(r));
                    insert = Engine.ReinsertText(r);
                }
                catch (CalcError ex) { shown = "ERR:" + ex.Message; }
                catch (Exception) { shown = "ERR:syntax error"; }
                total++;
                if (shown != c[5] || insert != c[6])
                {
                    failed++;
                    Console.WriteLine("FAIL  {0} [{1}|{2}|{3}|{4}]\n  expected: {5}   [{6}]\n  got:      {7}   [{8}]",
                        c[4], c[0], c[1], c[2], c[3], c[5], c[6], shown, insert);
                }
            }
            Console.WriteLine("{0} of {1} cases match the web calculator.", total - failed, total);
            return failed == 0 ? 0 : 1;
        }
    }
}
