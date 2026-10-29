// Physics Calculator engine: a line-by-line port of the calculator in
// tools/teaching/notebook_calculator.html. Results must match the web version;
// tests.tsv (generated from the web page) checks that. Keep the two in sync.
//
// Every value is a quantity {V, D, Ang}: V in SI units, D the exponents of the
// seven SI base units, Ang marks a dimensionless angle (V in radians).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace PhysicsCalculator
{
    class CalcError : Exception
    {
        public CalcError(string message) : base(message) { }
    }

    class Q
    {
        public readonly double V;
        public readonly double[] D;
        public readonly bool Ang;
        public Q(double v, double[] d = null, bool ang = false) { V = v; D = d ?? Engine.NODIM; Ang = ang; }
    }

    class Conversion
    {
        public double V;
        public string Label;
    }

    class Result
    {
        public Q Q;
        public Conversion Conv;
    }

    class Unit
    {
        public double F;
        public double[] D;
        public bool P, Ang;
    }

    class Temp
    {
        public double F, Off;
    }

    class Constant
    {
        public string Group, Name, Sym, Value, Unit, Desc;
        public string[] Alias;
        // What gets typed: the first other name, else the name
        public string Typed { get { return Alias.Length > 0 ? Alias[0] : Name; } }
    }

    class Engine
    {
        // ── Settings ──
        public string AngleMode = "deg";
        public Q LastAns = new Q(0);
        public int? DecPlaces, SigFigs;
        public bool Pow10;

        public static readonly string[] BASE = { "m", "kg", "s", "A", "K", "mol", "cd" };
        public static readonly double[] NODIM = Dim();

        static double[] Dim(params double[] e)
        {
            var d = new double[7];
            for (int i = 0; i < e.Length; i++) d[i] = e[i];
            return d;
        }

        static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }
        static bool IsDimless(double[] d) { return d.All(x => Near(x, 0)); }
        static bool SameDim(double[] a, double[] b) { for (int i = 0; i < 7; i++) if (!Near(a[i], b[i])) return false; return true; }
        static double[] Combine(double[] a, double[] b, double k) { var r = new double[7]; for (int i = 0; i < 7; i++) r[i] = a[i] + k * b[i]; return r; }
        static Exception Fail(string msg) { return new CalcError(msg); }

        static readonly double[] LEN = Dim(1), MASS = Dim(0, 1), TIME = Dim(0, 0, 1), CUR = Dim(0, 0, 0, 1),
            TEMPD = Dim(0, 0, 0, 0, 1), AMOUNT = Dim(0, 0, 0, 0, 0, 1), AREA = Dim(2), VOL = Dim(3), FREQ = Dim(0, 0, -1),
            SPEED = Dim(1, 0, -1), FORCE = Dim(1, 1, -2), ENERGY = Dim(2, 1, -2), POWER = Dim(2, 1, -3), PRESS = Dim(-1, 1, -2),
            CHARGE = Dim(0, 0, 1, 1), VOLT = Dim(2, 1, -3, -1), OHM = Dim(2, 1, -3, -2), FARAD = Dim(-2, -1, 4, 2),
            TESLA = Dim(0, 1, -2, -1), WEBER = Dim(2, 1, -2, -1), HENRY = Dim(2, 1, -2, -2), DOSE = Dim(2, 0, -2);

        // name → factor to SI, dimension, accepts SI prefixes, is an angle
        static readonly Dictionary<string, Unit> UNITS = new Dictionary<string, Unit>();
        static void DefUnit(string names, double f, double[] d, bool p = false, bool ang = false)
        {
            foreach (var n in names.Split(' ')) UNITS[n] = new Unit { F = f, D = d, P = p, Ang = ang };
        }

        // °C / °F: a lone reading such as "25 °C" or "(25 °C)" is a temperature
        // (298.15 K); inside a calculation they are steps, as in J/(kg·°C) × 10 °C.
        static readonly Dictionary<string, Temp> TEMP = new Dictionary<string, Temp>();

        // Insertion order matters: the first prefix that fits wins.
        static readonly KeyValuePair<string, double>[] PREFIX = {
            P("f", 1e-15), P("p", 1e-12), P("n", 1e-9), P("μ", 1e-6), P("µ", 1e-6), P("u", 1e-6), P("m", 1e-3), P("c", 1e-2),
            P("d", 1e-1), P("h", 1e2), P("k", 1e3), P("M", 1e6), P("G", 1e9), P("T", 1e12), P("P", 1e15) };
        static KeyValuePair<string, double> P(string k, double v) { return new KeyValuePair<string, double>(k, v); }

        // CODATA 2022 (physics.nist.gov/constants); Earth, Moon and Sun: IAU nominal values.
        // Group, name, symbol, other names, value, unit, description. The symbol is drawn with
        // "_" starting a subscript; the first other name (else the name) is what gets typed.
        // h, e and g already mean hour, Euler's number and gram, hence hP, qe and gn.
        public static readonly Constant[] CONSTANTS = {
            K("Universal",           "c",       "c",       "",          "299792458",         "m/s",       "speed of light in vacuum"),
            K("Universal",           "G",       "G",       "",          "6.67430e-11",       "N·m²/kg²",  "gravitational constant"),
            K("Universal",           "hP",      "h",       "",          "6.62607015e-34",    "J·s",       "Planck constant"),
            K("Universal",           "hbar",    "ħ",       "ħ",         "1.054571817e-34",   "J·s",       "reduced Planck constant"),
            K("Universal",           "gn",      "g_n",     "g_n",       "9.80665",           "m/s²",      "standard gravity (free fall)"),
            K("Electromagnetism",    "qe",      "e",       "q_e",       "1.602176634e-19",   "C",         "elementary charge"),
            K("Electromagnetism",    "eps0",    "ε_0",     "ε₀ ε0 ε_0", "8.8541878188e-12",  "F/m",       "vacuum electric permittivity"),
            K("Electromagnetism",    "mu0",     "μ_0",     "μ₀ μ0 μ_0", "1.25663706127e-6",  "H/m",       "vacuum magnetic permeability"),
            K("Electromagnetism",    "ke",      "k_e",     "k_e",       "8.9875517862e9",    "N·m²/C²",   "Coulomb constant, 1/(4πε₀)"),
            K("Electromagnetism",    "muB",     "μ_B",     "μ_B",       "9.2740100657e-24",  "J/T",       "Bohr magneton"),
            K("Electromagnetism",    "muN",     "μ_N",     "μ_N",       "5.0507837393e-27",  "J/T",       "nuclear magneton"),
            K("Atoms and particles", "me",      "m_e",     "m_e",       "9.1093837139e-31",  "kg",        "electron mass"),
            K("Atoms and particles", "mp",      "m_p",     "m_p",       "1.67262192595e-27", "kg",        "proton mass"),
            K("Atoms and particles", "mn",      "m_n",     "m_n",       "1.67492750056e-27", "kg",        "neutron mass"),
            K("Atoms and particles", "m_u",     "m_u",     "",          "1.66053906892e-27", "kg",        "atomic mass constant (1 u)"),
            K("Atoms and particles", "alpha",   "α",       "α",         "7.2973525643e-3",   "",          "fine-structure constant"),
            K("Atoms and particles", "a0",      "a_0",     "a₀ a_0",    "5.29177210544e-11", "m",         "Bohr radius"),
            K("Atoms and particles", "Rinf",    "R_∞",     "R_inf",     "10973731.568157",   "1/m",       "Rydberg constant"),
            K("Atoms and particles", "lambdaC", "λ_C",     "λ_C",       "2.42631023538e-12", "m",         "Compton wavelength of the electron"),
            K("Heat and chemistry",  "kB",      "k_B",     "k_B",       "1.380649e-23",      "J/K",       "Boltzmann constant"),
            K("Heat and chemistry",  "NA",      "N_A",     "N_A",       "6.02214076e23",     "1/mol",     "Avogadro constant"),
            K("Heat and chemistry",  "R",       "R",       "",          "8.314462618",       "J/(mol·K)", "molar gas constant"),
            K("Heat and chemistry",  "Faraday", "F",       "",          "96485.33212",       "C/mol",     "Faraday constant"),
            K("Heat and chemistry",  "Vm",      "V_m",     "V_m",       "22.41396954e-3",    "m³/mol",    "molar volume of an ideal gas at 0 °C, 1 atm"),
            K("Heat and chemistry",  "sigma",   "σ",       "σ",         "5.670374419e-8",    "W/(m²·K⁴)", "Stefan–Boltzmann constant"),
            K("Heat and chemistry",  "wien",    "b",       "",          "2.897771955e-3",    "m·K",       "Wien displacement constant"),
            K("Earth, Moon and Sun", "Mearth",  "M_Earth", "M_Earth",   "5.972e24",          "kg",        "mass of the Earth"),
            K("Earth, Moon and Sun", "Rearth",  "R_Earth", "R_Earth",   "6.371e6",           "m",         "mean radius of the Earth"),
            K("Earth, Moon and Sun", "Mmoon",   "M_Moon",  "M_Moon",    "7.342e22",          "kg",        "mass of the Moon"),
            K("Earth, Moon and Sun", "Rmoon",   "R_Moon",  "R_Moon",    "1.7374e6",          "m",         "mean radius of the Moon"),
            K("Earth, Moon and Sun", "dmoon",   "d_Moon",  "d_Moon",    "3.844e8",           "m",         "mean Earth–Moon distance"),
            K("Earth, Moon and Sun", "Msun",    "M_Sun",   "M_Sun",     "1.9884e30",         "kg",        "mass of the Sun"),
            K("Earth, Moon and Sun", "Rsun",    "R_Sun",   "R_Sun",     "6.957e8",           "m",         "radius of the Sun"),
            K("Earth, Moon and Sun", "Lsun",    "L_Sun",   "L_Sun",     "3.828e26",          "W",         "luminosity of the Sun"),
        };
        static Constant K(string group, string name, string sym, string alias, string value, string unit, string desc)
        {
            return new Constant { Group = group, Name = name, Sym = sym, Value = value, Unit = unit, Desc = desc,
                                  Alias = alias == "" ? new string[0] : alias.Split(' ') };
        }
        static readonly Dictionary<string, Q> CONST_Q = new Dictionary<string, Q>();
        static readonly Dictionary<string, string> CONST_ALIAS = new Dictionary<string, string> {
            { "π", "pi" }, { "PI", "pi" }, { "E", "e" }, { "µ₀", "mu0" }, { "µ0", "mu0" } };

        // LaTeX names typed with a backslash become symbols: \hbar → ħ, \epsilon_0 → ε_0.
        // None is the start of another, so each can be swapped as soon as it is complete.
        static readonly Dictionary<string, string> LATEX = new Dictionary<string, string> {
            { "alpha", "α" }, { "beta", "β" }, { "gamma", "γ" }, { "delta", "δ" }, { "epsilon", "ε" }, { "varepsilon", "ε" },
            { "zeta", "ζ" }, { "eta", "η" }, { "theta", "θ" }, { "iota", "ι" }, { "kappa", "κ" }, { "lambda", "λ" },
            { "mu", "μ" }, { "nu", "ν" }, { "xi", "ξ" }, { "pi", "π" }, { "rho", "ρ" }, { "sigma", "σ" }, { "tau", "τ" },
            { "upsilon", "υ" }, { "phi", "φ" }, { "varphi", "φ" }, { "chi", "χ" }, { "psi", "ψ" }, { "omega", "ω" },
            { "Gamma", "Γ" }, { "Delta", "Δ" }, { "Theta", "Θ" }, { "Lambda", "Λ" }, { "Xi", "Ξ" }, { "Pi", "Π" },
            { "Sigma", "Σ" }, { "Phi", "Φ" }, { "Psi", "Ψ" }, { "Omega", "Ω" }, { "hbar", "ħ" },
            { "times", "×" }, { "cdot", "·" }, { "div", "÷" }, { "circ", "°" }, { "to", "→" } };
        public static string FromLatex(string s)
        {
            return Regex.Replace(s, @"\\([A-Za-z]+)", m => { string r; return LATEX.TryGetValue(m.Groups[1].Value, out r) ? r : m.Value; });
        }

        // Constants search. 0 = the name or symbol itself, 1 = starts it, 2 = starts a word
        // of the description, 3 = inside it, 4 = the group's name
        static int ConstRank(Constant c, string q)
        {
            Func<string, string> norm = t => Regex.Replace(t.ToLowerInvariant(), @"[_\s]", "");
            string qn = norm(q), ql = q.ToLowerInvariant().Trim();
            var keys = new[] { c.Name, c.Sym }.Concat(c.Alias).Select(norm).ToList();
            if (keys.Contains(qn)) return 0;
            if (keys.Any(k => k.StartsWith(qn, StringComparison.Ordinal))) return 1;
            string d = c.Desc.ToLowerInvariant();
            if (Regex.Split(d, @"[^\p{L}\d]+").Any(w => w.StartsWith(ql, StringComparison.Ordinal))) return 2;
            if (d.Contains(ql)) return 3;
            return c.Group.ToLowerInvariant().Contains(ql) ? 4 : -1;
        }
        public static List<Constant> FindConstants(string q)
        {
            if (q.Trim() == "") return CONSTANTS.ToList();
            return CONSTANTS.Select((c, i) => new { c, i, r = ConstRank(c, q) }).Where(x => x.r >= 0)
                .OrderBy(x => x.r).ThenBy(x => x.i).Select(x => x.c).ToList();
        }

        static Engine()
        {
            DefUnit("m", 1, LEN, true);
            DefUnit("g", 1e-3, MASS, true);
            DefUnit("s", 1, TIME, true);
            DefUnit("A", 1, CUR, true);
            DefUnit("K", 1, TEMPD, true);
            DefUnit("mol", 1, AMOUNT, true);
            DefUnit("cd", 1, Dim(0, 0, 0, 0, 0, 0, 1));
            DefUnit("Hz Bq", 1, FREQ, true);
            DefUnit("N", 1, FORCE, true);
            DefUnit("J", 1, ENERGY, true);
            DefUnit("W", 1, POWER, true);
            DefUnit("Pa", 1, PRESS, true);
            DefUnit("C", 1, CHARGE, true);
            DefUnit("V", 1, VOLT, true);
            DefUnit("Ω ohm", 1, OHM, true);
            DefUnit("F", 1, FARAD, true);
            DefUnit("T", 1, TESLA, true);
            DefUnit("Wb", 1, WEBER, true);
            DefUnit("H", 1, HENRY, true);
            DefUnit("Gy Sv", 1, DOSE, true);
            // time
            DefUnit("min", 60, TIME);
            DefUnit("h", 3600, TIME);
            DefUnit("d day", 86400, TIME);
            DefUnit("yr", 3.15576e7, TIME, true);
            // length, area, volume
            DefUnit("Å", 1e-10, LEN);
            DefUnit("in", 0.0254, LEN);
            DefUnit("ft", 0.3048, LEN);
            DefUnit("yd", 0.9144, LEN);
            DefUnit("mi", 1609.344, LEN);
            DefUnit("nmi", 1852, LEN);
            DefUnit("AU", 1.495978707e11, LEN);
            DefUnit("ly", 9.4607304725808e15, LEN);
            DefUnit("pc", 3.0856775814914e16, LEN);
            DefUnit("ha", 1e4, AREA);
            DefUnit("L l", 1e-3, VOL, true);
            DefUnit("gal", 3.785411784e-3, VOL);
            // mass
            DefUnit("t", 1000, MASS);
            DefUnit("u Da", 1.66053906892e-27, MASS, true);
            DefUnit("lb", 0.45359237, MASS);
            DefUnit("oz", 0.028349523125, MASS);
            // speed
            DefUnit("mph", 0.44704, SPEED);
            DefUnit("knot", 1852.0 / 3600, SPEED);
            // force, energy, power, pressure, charge
            DefUnit("kgf", 9.80665, FORCE);
            DefUnit("lbf", 4.4482216152605, FORCE);
            DefUnit("dyn", 1e-5, FORCE);
            DefUnit("eV", 1.602176634e-19, ENERGY, true);
            DefUnit("cal", 4.184, ENERGY, true);
            DefUnit("Wh", 3600, ENERGY, true);
            DefUnit("erg", 1e-7, ENERGY);
            DefUnit("hp", 745.69987158227, POWER);
            DefUnit("bar", 1e5, PRESS, true);
            DefUnit("atm", 101325, PRESS);
            DefUnit("mmHg", 133.322387415, PRESS);
            DefUnit("torr", 101325.0 / 760, PRESS);
            DefUnit("psi", 6894.757293168, PRESS);
            DefUnit("Ah", 3600, CHARGE, true);
            // angles (dimensionless, stored in radians)
            DefUnit("rad", 1, NODIM, true, true);
            DefUnit("° deg", Math.PI / 180, NODIM, false, true);
            DefUnit("rev", 2 * Math.PI, NODIM, false, true);
            DefUnit("arcmin", Math.PI / 10800, NODIM, false, true);
            DefUnit("arcsec", Math.PI / 648000, NODIM, false, true);
            DefUnit("rpm", 2 * Math.PI / 60, FREQ);

            TEMP["°C"] = new Temp { F = 1, Off = 273.15 };
            TEMP["°F"] = new Temp { F = 5.0 / 9, Off = 459.67 * 5 / 9 };
            TEMP["degC"] = TEMP["°C"];
            TEMP["degF"] = TEMP["°F"];
            foreach (var t in TEMP) DefUnit(t.Key, t.Value.F, TEMPD);

            CONST_Q["pi"] = new Q(Math.PI);
            CONST_Q["e"] = new Q(Math.E);
            foreach (var c in CONSTANTS) foreach (var a in c.Alias) CONST_ALIAS[a] = c.Name;
            var boot = new Engine();
            foreach (var c in CONSTANTS) CONST_Q[c.Name] = boot.Evaluate(c.Value + " " + c.Unit).Q;
        }

        double ToRad(Q x) { return x.Ang ? x.V : AngleMode == "deg" ? x.V * Math.PI / 180 : x.V; }

        // ── Unit names for display ──
        const string SUP = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        static double JsRound(double x) { return Math.Floor(x + 0.5); }

        static string Sup(double e)
        {
            if (Near(e, 1)) return "";
            if (Near(e, JsRound(e)))
            {
                var s = JsNum.ToJsString(JsRound(e));
                var sb = new StringBuilder();
                foreach (char ch in s) sb.Append(ch >= '0' && ch <= '9' ? SUP[ch - '0'] : ch);
                return sb.ToString();
            }
            return "^" + JsNum.ParseFloatString(JsNum.ToFixed(e, 3));
        }

        static readonly KeyValuePair<string, double[]>[] NAMED = {
            N("N", FORCE), N("J", ENERGY), N("W", POWER), N("Pa", PRESS), N("Hz", FREQ), N("C", CHARGE),
            N("V", VOLT), N("Ω", OHM), N("F", FARAD), N("T", TESLA), N("Wb", WEBER), N("H", HENRY) };
        static KeyValuePair<string, double[]> N(string k, double[] d) { return new KeyValuePair<string, double[]>(k, d); }

        // The way these are usually written in class, ahead of any automatic choice
        static readonly KeyValuePair<string, double[]>[] PREFERRED = {
            N("kg·m/s", Dim(1, 1, -1)), N("m/s²", Dim(1, 0, -2)), N("m²/s²", Dim(2, 0, -2)),
            N("N/m", Dim(0, 1, -2)), N("N·m²/C²", Dim(3, 1, -4, -2)), N("N·m²/kg²", Dim(3, -1, -2)) };
        static readonly int[] DISPLAY_ORDER = { 1, 0, 2, 3, 5, 4, 6 };  // kg·m·s·A·mol·K, as usually written
        static double Cost(double[] d) { return d.Sum(e => Math.Abs(e)); }
        static int Parts(double[] d) { return d.Count(e => !Near(e, 0)); }

        static string Compose(List<string> num, List<string> den, double[] d)
        {
            foreach (int i in DISPLAY_ORDER)
            {
                if (d[i] > 1e-9) num.Add(BASE[i] + Sup(d[i]));
                else if (d[i] < -1e-9) den.Add(BASE[i] + Sup(-d[i]));
            }
            string s = num.Count > 0 ? string.Join("·", num) : "1";
            if (den.Count > 0) s += "/" + (den.Count > 1 ? "(" + string.Join("·", den) + ")" : den[0]);
            return s;
        }

        public static string UnitLabel(double[] d)
        {
            if (IsDimless(d)) return "";
            foreach (var pref in PREFERRED)
            {
                bool match = true;
                for (int i = 0; i < 7; i++) if (JsRound(d[i] * 1e6) / 1e6 != pref.Value[i]) { match = false; break; }
                if (match) return pref.Key;
            }
            foreach (var n in NAMED) if (SameDim(d, n.Value)) return n.Key;
            double baseCost = Cost(d);
            if (baseCost <= 2) return Compose(new List<string>(), new List<string>(), d);
            // Try one named unit times base units, e.g. J/(kg·K) or W/m²
            bool found = false;
            double bestC = 0; string bestName = null; int bestSign = 0; double[] bestRest = null;
            foreach (var n in NAMED)
            {
                if (n.Key == "Hz") continue;
                foreach (int sign in new[] { 1, -1 })
                {
                    // fewer base units break ties: W/(m²·K⁴) rather than N/(m·s·K⁴)
                    var rest = Combine(d, n.Value, -sign);
                    double c = 1 + Cost(rest) + 0.1 * Parts(rest);
                    if (c < baseCost && (!found || c < bestC)) { found = true; bestC = c; bestName = n.Key; bestSign = sign; bestRest = rest; }
                }
            }
            if (!found) return Compose(new List<string>(), new List<string>(), d);
            return bestSign > 0 ? Compose(new List<string> { bestName }, new List<string>(), bestRest)
                                : Compose(new List<string>(), new List<string> { bestName }, bestRest);
        }
        static string DimName(double[] d) { var s = UnitLabel(d); return s != "" ? s : "a plain number"; }

        // ── Operations ──
        Q Add(Q a, Q b, int sign)
        {
            if (a.Ang != b.Ang && IsDimless(a.D) && IsDimless(b.D))
                return new Q(ToRad(a) + sign * ToRad(b), NODIM, true);   // plain number joins an angle in DEG/RAD
            if (!SameDim(a.D, b.D))
                throw Fail(string.Format("can't {0} {1} {2} {3}", sign > 0 ? "add" : "subtract", DimName(b.D), sign > 0 ? "to" : "from", DimName(a.D)));
            return new Q(a.V + sign * b.V, a.D, a.Ang || b.Ang);
        }
        static Q Mul(Q a, Q b)
        {
            var d = Combine(a.D, b.D, 1);
            return new Q(a.V * b.V, d, IsDimless(d) && a.Ang != b.Ang);
        }
        static Q Div(Q a, Q b)
        {
            var d = Combine(a.D, b.D, -1);
            return new Q(a.V / b.V, d, IsDimless(d) && a.Ang && !b.Ang);
        }
        static Q Pow(Q a, Q b)
        {
            if (!IsDimless(b.D)) throw Fail("an exponent can't have units (got " + UnitLabel(b.D) + ")");
            return new Q(JsNum.Pow(a.V, b.V), a.D.Select(e => e * b.V).ToArray(), a.Ang && b.V == 1);
        }

        static void NeedPlain(string name, Q x)
        {
            if (!IsDimless(x.D)) throw Fail(name + "( ) needs a plain number, got " + UnitLabel(x.D));
        }

        Q CallFn(string name, List<Q> xs)
        {
            Q x = xs.Count > 0 ? xs[0] : null;
            switch (name)
            {
                case "sin": NeedPlain(name, x); return new Q(Trig.Sin(ToRad(x)));
                case "cos": NeedPlain(name, x); return new Q(Trig.Cos(ToRad(x)));
                case "tan": NeedPlain(name, x); return new Q(Trig.Tan(ToRad(x)));
                case "asin": NeedPlain(name, x); return new Q(Math.Asin(x.V), NODIM, true);
                case "acos": NeedPlain(name, x); return new Q(Math.Acos(x.V), NODIM, true);
                case "atan": NeedPlain(name, x); return new Q(Math.Atan(x.V), NODIM, true);
                case "atan2":
                    if (!SameDim(xs[0].D, xs[1].D)) throw Fail("atan2(y, x) needs y and x in the same units");
                    return new Q(Math.Atan2(xs[0].V, xs[1].V), NODIM, true);
                case "sinh": NeedPlain(name, x); return new Q(Math.Sinh(x.V));
                case "cosh": NeedPlain(name, x); return new Q(Math.Cosh(x.V));
                case "tanh": NeedPlain(name, x); return new Q(Math.Tanh(x.V));
                case "log": NeedPlain(name, x); return new Q(Math.Log10(x.V));
                case "ln": NeedPlain(name, x); return new Q(Math.Log(x.V));
                case "log2": NeedPlain(name, x); return new Q(Math.Log(x.V) / Math.Log(2));
                case "exp": NeedPlain(name, x); return new Q(Math.Exp(x.V));
                case "sqrt": return new Q(Math.Sqrt(x.V), x.D.Select(e => e / 2).ToArray());
                case "cbrt": return new Q(Cbrt(x.V), x.D.Select(e => e / 3).ToArray());
                case "abs": return new Q(Math.Abs(x.V), x.D, x.Ang);
                case "floor": return new Q(Math.Floor(x.V), x.D, x.Ang);
                case "ceil": return new Q(Math.Ceiling(x.V), x.D, x.Ang);
                case "round": return new Q(JsRound(x.V), x.D, x.Ang);
                case "max":
                case "min":
                    if (xs.Count == 0) throw Fail("max( ) and min( ) need at least one value");
                    foreach (var y in xs) if (!SameDim(y.D, xs[0].D)) throw Fail("max( ) and min( ) need values in the same units");
                    double v = xs[0].V;
                    foreach (var y in xs)
                    {
                        if (double.IsNaN(y.V) || double.IsNaN(v)) v = double.NaN;
                        else v = name == "max" ? Math.Max(v, y.V) : Math.Min(v, y.V);
                    }
                    return new Q(v, xs[0].D, xs[0].Ang);
            }
            throw Fail("unknown function " + name);
        }

        static double Cbrt(double x)
        {
            if (x == 0 || double.IsNaN(x) || double.IsInfinity(x)) return x;
            double r = Math.Pow(Math.Abs(x), 1.0 / 3);
            r = r - (r * r * r - Math.Abs(x)) / (3 * r * r);   // one Newton step for full precision
            return x < 0 ? -r : r;
        }

        // Arity checked like the web version's fn.length (0 = any number)
        static readonly Dictionary<string, int> FN = new Dictionary<string, int> {
            { "sin", 1 }, { "cos", 1 }, { "tan", 1 }, { "asin", 1 }, { "acos", 1 }, { "atan", 1 }, { "atan2", 2 },
            { "sinh", 1 }, { "cosh", 1 }, { "tanh", 1 }, { "log", 1 }, { "ln", 1 }, { "log2", 1 }, { "exp", 1 },
            { "sqrt", 1 }, { "cbrt", 1 }, { "abs", 1 }, { "floor", 1 }, { "ceil", 1 }, { "round", 1 }, { "max", 0 }, { "min", 0 } };

        Q Lookup(string name)
        {
            if (name == "ANS" || name == "Ans" || name == "ans") return LastAns;
            Unit u;
            if (UNITS.TryGetValue(name, out u)) return new Q(u.F, u.D, u.Ang);
            string alias;
            Q c;
            if (CONST_Q.TryGetValue(CONST_ALIAS.TryGetValue(name, out alias) ? alias : name, out c)) return c;
            foreach (var pre in PREFIX)
            {
                Unit b;
                if (name.Length > pre.Key.Length && name.StartsWith(pre.Key, StringComparison.Ordinal)
                    && UNITS.TryGetValue(name.Substring(pre.Key.Length), out b) && b.P)
                    return new Q(pre.Value * b.F, b.D, b.Ang);
            }
            if (FN.ContainsKey(name)) throw Fail(name + " needs brackets: " + name + "( … )");
            throw Fail("unknown unit or symbol \"" + name + "\"");
        }

        // ── Tokenizer ──
        class Tok { public string T, S, Raw; public double V; public int Pos; }

        static readonly Regex RE_WS = new Regex(@"\G\s+");
        static readonly Regex RE_NUM = new Regex(@"\G(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?");
        static readonly Regex RE_SUP = new Regex(@"\G⁻?[⁰¹²³⁴⁵⁶⁷⁸⁹]+");
        static readonly Regex RE_ID = new Regex(@"\G[\p{L}°_][\p{L}0-9_°₀-₉]*");
        static readonly Regex RE_OP = new Regex(@"\G(?:\*\*|->|[-+−*×·⋅/÷^(),→])");
        static readonly Dictionary<string, string> OP_ALIAS = new Dictionary<string, string> {
            { "−", "-" }, { "×", "*" }, { "·", "*" }, { "⋅", "*" }, { "÷", "/" }, { "**", "^" }, { "->", "to" }, { "→", "to" } };

        static List<Tok> Tokenize(string s)
        {
            var toks = new List<Tok>();
            int i = 0;
            while (i < s.Length)
            {
                Match m;
                if ((m = RE_WS.Match(s, i)).Success) { i += m.Length; continue; }
                if ((m = RE_NUM.Match(s, i)).Success)
                    toks.Add(new Tok { T = "num", V = double.Parse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture), Raw = m.Value, Pos = i });
                else if ((m = RE_SUP.Match(s, i)).Success)
                {
                    var sb = new StringBuilder();
                    foreach (char ch in m.Value) sb.Append(ch == '⁻' ? '-' : (char)('0' + SUP.IndexOf(ch)));
                    toks.Add(new Tok { T = "sup", V = int.Parse(sb.ToString(), CultureInfo.InvariantCulture), Raw = m.Value, Pos = i });
                }
                else if ((m = RE_ID.Match(s, i)).Success)
                    toks.Add(m.Value == "to" ? new Tok { T = "op", S = "to", Raw = m.Value, Pos = i } : new Tok { T = "id", S = m.Value, Raw = m.Value, Pos = i });
                else if ((m = RE_OP.Match(s, i)).Success)
                {
                    string op;
                    toks.Add(new Tok { T = "op", S = OP_ALIAS.TryGetValue(m.Value, out op) ? op : m.Value, Raw = m.Value, Pos = i });
                }
                else throw Fail("unexpected \"" + s[i] + "\"");
                i += m.Length;
            }
            return toks;
        }

        // ── Parser: evaluates while it reads ──
        // Implicit multiplication binds tighter than × and ÷, so a number stays
        // with its unit: 10 N / 0.2 m = 50 N/m, and J/kg K = J/(kg·K).
        List<Tok> toks;
        int p;
        Tok Peek() { return p < toks.Count ? toks[p] : null; }
        Tok At(int i) { return i >= 0 && i < toks.Count ? toks[i] : null; }
        static bool IsOp(Tok tk, params string[] ops) { return tk != null && tk.T == "op" && ops.Contains(tk.S); }
        static bool StartsOperand(Tok tk) { return tk != null && (tk.T == "num" || tk.T == "id" || IsOp(tk, "(")); }

        public Result Evaluate(string input)
        {
            string src = FromLatex(input).Replace("º", "°").Replace("℃", "°C").Replace("℉", "°F");
            toks = Tokenize(src);
            p = 0;
            Q q = ParseExpr();
            Conversion conv = null;
            if (IsOp(Peek(), "to"))
            {
                p++;
                var first = Peek();
                if (first == null) throw Fail("write a unit after \"to\", e.g. to km/h");
                string label = Regex.Replace(src.Substring(first.Pos).Trim(), @"\s+", " ");
                Temp t;
                if (first.T == "id" && TEMP.TryGetValue(first.S, out t) && p == toks.Count - 1)
                {
                    p++;
                    if (!SameDim(q.D, TEMPD)) throw Fail("can't convert " + DimName(q.D) + " to " + label);
                    conv = new Conversion { V = (q.V - t.Off) / t.F, Label = label };
                }
                else
                {
                    var target = ParseExpr();
                    if (!SameDim(q.D, target.D)) throw Fail("can't convert " + DimName(q.D) + " to " + label);
                    if (target.V == 0) throw Fail("can't convert to " + label);
                    double v = target.Ang && !q.Ang && IsDimless(q.D) ? ToRad(q) : q.V;
                    conv = new Conversion { V = v / target.V, Label = label };
                }
            }
            if (p < toks.Count) throw Fail("unexpected \"" + toks[p].Raw + "\"");
            return new Result { Q = q, Conv = conv };
        }

        Q ParseExpr()
        {
            var reading = TempReading();
            if (reading != null) return reading;
            var a = ParseTerm();
            while (IsOp(Peek(), "+", "-"))
            {
                int sign = toks[p++].S == "+" ? 1 : -1;
                a = Add(a, ParseTerm(), sign);
            }
            return a;
        }

        // A lone "25 °C", up to the end, a ")" or "to" → 298.15 K
        Q TempReading()
        {
            int i = p, sign = 1;
            if (IsOp(At(i), "-", "+")) { sign = toks[i].S == "-" ? -1 : 1; i++; }
            Tok n = At(i), u = At(i + 1), after = At(i + 2);
            Temp t;
            if (n == null || n.T != "num" || u == null || u.T != "id" || !TEMP.TryGetValue(u.S, out t)) return null;
            if (after != null && !IsOp(after, ")", "to")) return null;
            p = i + 2;
            return new Q(sign * n.V * t.F + t.Off, TEMPD);
        }

        Q ParseTerm()
        {
            var a = ParseGroup();
            while (IsOp(Peek(), "*", "/"))
            {
                string op = toks[p++].S;
                a = op == "*" ? Mul(a, ParseGroup()) : Div(a, ParseGroup());
            }
            return a;
        }

        Q ParseGroup()
        {
            var a = ParseUnary();
            while (StartsOperand(Peek())) a = Mul(a, ParseUnary());
            return a;
        }

        Q ParseUnary()
        {
            if (IsOp(Peek(), "-", "+"))
            {
                bool neg = toks[p++].S == "-";
                var x = ParseUnary();
                return neg ? new Q(-x.V, x.D, x.Ang) : x;
            }
            var a = ParsePrimary();
            while (Peek() != null && Peek().T == "sup") a = Pow(a, new Q(toks[p++].V));
            if (IsOp(Peek(), "^")) { p++; a = Pow(a, ParseUnary()); }
            return a;
        }

        Q ParsePrimary()
        {
            var tk = Peek();
            p++;
            if (tk == null) throw Fail("the expression ends too early");
            if (tk.T == "num") return new Q(tk.V);
            if (IsOp(tk, "("))
            {
                var v = ParseExpr();
                if (!IsOp(At(p++), ")")) throw Fail("missing \")\"");
                return v;
            }
            if (tk.T == "id")
            {
                int arity;
                if (FN.TryGetValue(tk.S, out arity) && IsOp(Peek(), "("))
                {
                    p++;
                    var args = new List<Q>();
                    if (!IsOp(Peek(), ")")) args.Add(ParseExpr());
                    while (IsOp(Peek(), ",")) { p++; args.Add(ParseExpr()); }
                    if (!IsOp(At(p++), ")")) throw Fail("missing \")\"");
                    if (arity > 0 && args.Count != arity)
                        throw Fail(tk.S + "( ) takes " + arity + " value" + (arity > 1 ? "s" : ""));
                    return CallFn(tk.S, args);
                }
                return Lookup(tk.S);
            }
            throw Fail("unexpected \"" + tk.Raw + "\"");
        }

        // ── Formatting (matches the web calculator's display) ──
        public static string WithCommas(string s)
        {
            bool neg = s.StartsWith("-");
            string t = neg ? s.Substring(1) : s;
            int dot = t.IndexOf('.');
            string intPart = dot >= 0 ? t.Substring(0, dot) : t;
            string dec = dot >= 0 ? t.Substring(dot + 1) : null;
            var sb = new StringBuilder();
            for (int i = 0; i < intPart.Length; i++)
            {
                if (i > 0 && (intPart.Length - i) % 3 == 0) sb.Append(',');
                sb.Append(intPart[i]);
            }
            return (neg ? "−" : "") + sb + (dec != null ? "." + dec : "");
        }

        public string FmtNum(double v)
        {
            if (double.IsNaN(v)) return "undefined";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "−∞";
            if (v == 0) v = 0;   // -0 → 0
            double abs = Math.Abs(v);
            bool sciRange = abs != 0 && (abs >= 1e10 || abs < 1e-4);
            string s;
            if (SigFigs.HasValue) s = sciRange ? JsNum.ToExponential(v, SigFigs.Value - 1) : JsNum.ToPrecision(v, SigFigs.Value);
            else if (DecPlaces.HasValue) s = JsNum.ToFixed(v, DecPlaces.Value);
            else if (sciRange)
            {
                var parts = JsNum.ToExponential(v, 8).Split('e');
                s = JsNum.ParseFloatString(parts[0]) + "e" + parts[1];
            }
            else s = JsNum.ParseFloatString(JsNum.ToPrecision(v, 12));
            var me = s.Split('e');
            if (me.Length == 1) return WithCommas(me[0]);
            int exp = int.Parse(me[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return Pow10 ? WithCommas(me[0]) + " ×10^" + exp : WithCommas(me[0]) + "e" + exp;
        }

        // → number and unit as shown on screen
        public string[] FormatQ(Q q)
        {
            if (q.Ang && IsDimless(q.D))
                return AngleMode == "deg" ? new[] { FmtNum(q.V * 180 / Math.PI), "°" } : new[] { FmtNum(q.V), "rad" };
            return new[] { FmtNum(q.V), UnitLabel(q.D) };
        }
        public string[] FormatResult(Result r)
        {
            return r.Conv != null ? new[] { FmtNum(r.Conv.V), r.Conv.Label } : FormatQ(r.Q);
        }
        public static string JoinOut(string[] o)
        {
            return o[1] != "" ? o[0] + (o[1] == "°" ? "" : " ") + o[1] : o[0];
        }

        // Text that types the result back in, at full precision
        public static string ReinsertText(Result r)
        {
            double v; string unit;
            if (r.Conv != null) { v = r.Conv.V; unit = r.Conv.Label; }
            else if (r.Q.Ang && IsDimless(r.Q.D)) { v = r.Q.V; unit = "rad"; }
            else { v = r.Q.V; unit = UnitLabel(r.Q.D); }
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            string n = JsNum.ToJsString12(v);
            string s = unit != "" ? n + " " + unit : n;
            return unit != "" || v < 0 ? "(" + s + ")" : s;
        }
    }

    // .NET Framework's Math.Sin/Cos lose accuracy away from zero (cos(π/2) gives
    // 6.1230e-17, not 6.1232e-17). Reduce by π/2 in two parts first, as fdlibm does.
    static class Trig
    {
        const double PIO2_HI = 1.5707963267948966, PIO2_LO = 6.123233995736766e-17;

        static double Reduce(double x, out int quadrant)
        {
            double k = Math.Floor(x / PIO2_HI + 0.5);
            quadrant = (int)(((long)k % 4 + 4) % 4);
            return (x - k * PIO2_HI) - k * PIO2_LO;
        }

        static bool Plain(double x) { return Math.Abs(x) > 1e6 || double.IsNaN(x) || double.IsInfinity(x); }

        public static double Sin(double x)
        {
            if (Plain(x)) return Math.Sin(x);
            int q; double r = Reduce(x, out q);
            switch (q) { case 0: return Math.Sin(r); case 1: return Math.Cos(r); case 2: return -Math.Sin(r); default: return -Math.Cos(r); }
        }

        public static double Cos(double x)
        {
            if (Plain(x)) return Math.Cos(x);
            int q; double r = Reduce(x, out q);
            switch (q) { case 0: return Math.Cos(r); case 1: return -Math.Sin(r); case 2: return -Math.Cos(r); default: return Math.Sin(r); }
        }

        public static double Tan(double x)
        {
            if (Plain(x)) return Math.Tan(x);
            int q; double r = Reduce(x, out q);
            return q % 2 == 0 ? Math.Tan(r) : -1 / Math.Tan(r);
        }
    }

    // JavaScript's number-to-text rules (toFixed, toPrecision, toExponential, String(x)),
    // computed from the exact binary value so rounding matches the browser digit for digit.
    static class JsNum
    {
        // x (finite, > 0) = num / den exactly
        static void Rational(double x, out BigInteger num, out BigInteger den)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;
            num = new BigInteger(man);
            den = BigInteger.One;
            if (exp >= 0) num <<= exp; else den <<= -exp;
        }

        // floor(num/den + 1/2): halves round up, as in the ECMAScript spec ("pick the larger n")
        static BigInteger RoundHalfUp(BigInteger num, BigInteger den)
        {
            return BigInteger.Divide(2 * num + den, 2 * den);
        }

        // Decimal exponent e with 10^e <= x < 10^(e+1)
        static int Exponent10(BigInteger num, BigInteger den)
        {
            int e = (int)Math.Floor(BigInteger.Log10(num) - BigInteger.Log10(den));
            while (CompareTen(num, den, e) < 0) e--;
            while (CompareTen(num, den, e + 1) >= 0) e++;
            return e;
        }

        // Compare num/den with 10^k
        static int CompareTen(BigInteger num, BigInteger den, int k)
        {
            if (k >= 0) return BigInteger.Compare(num, den * BigInteger.Pow(10, k));
            return BigInteger.Compare(num * BigInteger.Pow(10, -k), den);
        }

        // n = round(x / 10^(e-p+1)) with exactly p digits; e adjusted if rounding carries
        static void Digits(double x, int p, out BigInteger n, out int e)
        {
            BigInteger num, den;
            Rational(x, out num, out den);
            e = Exponent10(num, den);
            n = RoundScaled(num, den, p - 1 - e);
            if (n >= BigInteger.Pow(10, p)) { e++; n = RoundScaled(num, den, p - 1 - e); }
        }

        // round(num/den × 10^k)
        static BigInteger RoundScaled(BigInteger num, BigInteger den, int k)
        {
            if (k >= 0) return RoundHalfUp(num * BigInteger.Pow(10, k), den);
            return RoundHalfUp(num, den * BigInteger.Pow(10, -k));
        }

        public static string ToFixed(double x, int f)
        {
            if (double.IsNaN(x)) return "NaN";
            if (Math.Abs(x) >= 1e21 || double.IsInfinity(x)) return ToJsString(x);
            string sign = x < 0 ? "-" : "";
            x = Math.Abs(x);
            string m;
            if (x == 0) m = "0";
            else
            {
                BigInteger num, den;
                Rational(x, out num, out den);
                m = RoundScaled(num, den, f).ToString();
            }
            if (f == 0) return sign + m;
            if (m.Length <= f) m = new string('0', f + 1 - m.Length) + m;
            return sign + m.Substring(0, m.Length - f) + "." + m.Substring(m.Length - f);
        }

        public static string ToExponential(double x, int f)
        {
            string sign = x < 0 ? "-" : "";
            x = Math.Abs(x);
            string digits; int e;
            if (x == 0) { digits = new string('0', f + 1); e = 0; }
            else { BigInteger n; Digits(x, f + 1, out n, out e); digits = n.ToString(); }
            string m = f == 0 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
            return sign + m + "e" + (e >= 0 ? "+" : "-") + Math.Abs(e);
        }

        public static string ToPrecision(double x, int p)
        {
            string sign = x < 0 ? "-" : "";
            x = Math.Abs(x);
            string digits; int e;
            if (x == 0) { digits = new string('0', p); e = 0; }
            else { BigInteger n; Digits(x, p, out n, out e); digits = n.ToString(); }
            if (e < -6 || e >= p)
            {
                string m = p == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
                return sign + m + "e" + (e >= 0 ? "+" : "-") + Math.Abs(e);
            }
            if (e >= 0)
                return sign + (p > e + 1 ? digits.Substring(0, e + 1) + "." + digits.Substring(e + 1) : digits);
            return sign + "0." + new string('0', -e - 1) + digits;
        }

        // parseFloat(s) then String(): drops trailing zeros of a decimal string
        public static string ParseFloatString(string s)
        {
            int ePos = s.IndexOfAny(new[] { 'e', 'E' });
            if (ePos >= 0) return ToJsString(double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
            if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
            if (s == "-0") s = "0";
            return s;
        }

        // String(parseFloat(v.toPrecision(12)))
        public static string ToJsString12(double v)
        {
            if (v == 0) return "0";
            string sign = v < 0 ? "-" : "";
            BigInteger n; int e;
            Digits(Math.Abs(v), 12, out n, out e);
            return sign + JsFormat(n.ToString().TrimEnd('0'), e);
        }

        // String(x): the shortest digits that read back as x
        public static string ToJsString(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (double.IsInfinity(x)) return x > 0 ? "Infinity" : "-Infinity";
            if (x == 0) return "0";
            string sign = x < 0 ? "-" : "";
            double a = Math.Abs(x);
            for (int p = 1; p <= 17; p++)
            {
                BigInteger n; int e;
                Digits(a, p, out n, out e);
                string digits = n.ToString();
                double back = double.Parse(digits + "e" + (e - p + 1), NumberStyles.Float, CultureInfo.InvariantCulture);
                if (back == a) return sign + JsFormat(digits.TrimEnd('0'), e);
            }
            return sign + a.ToString("R", CultureInfo.InvariantCulture);
        }

        // Number::toString layout for digits d1d2…dk × 10^(e-k+1)
        static string JsFormat(string digits, int e)
        {
            if (digits == "") digits = "0";
            int k = digits.Length, n = e + 1;
            if (k <= n && n <= 21) return digits + new string('0', n - k);
            if (0 < n && n <= 21) return digits.Substring(0, n) + "." + digits.Substring(n);
            if (-6 < n && n <= 0) return "0." + new string('0', -n) + digits;
            string m = k == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
            return m + "e" + (n - 1 >= 0 ? "+" : "-") + Math.Abs(n - 1);
        }

        // Math.pow with JavaScript's special cases
        public static double Pow(double a, double b)
        {
            if (double.IsNaN(b)) return double.NaN;
            if (b == 0) return 1;
            if ((a == 1 || a == -1) && double.IsInfinity(b)) return double.NaN;
            return Math.Pow(a, b);
        }
    }
}
