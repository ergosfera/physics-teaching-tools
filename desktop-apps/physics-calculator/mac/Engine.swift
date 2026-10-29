// Physics Calculator engine: a line-by-line port of the calculator in
// tools/teaching/notebook_calculator.html (same as windows/Engine.cs).
// Results must match the web version; ../tests.tsv checks that (run with --selftest).
//
// Every value is a quantity {v, d, ang}: v in SI units, d the exponents of the
// seven SI base units, ang marks a dimensionless angle (v in radians).
import Foundation

struct CalcError: Error { let message: String }

final class Q {
    let v: Double
    let d: [Double]
    let ang: Bool
    init(_ v: Double, _ d: [Double] = Engine.NODIM, _ ang: Bool = false) { self.v = v; self.d = d; self.ang = ang }
}

struct Conversion { let v: Double; let label: String }
struct CalcResult { let q: Q; let conv: Conversion? }
struct UnitDef { let f: Double; let d: [Double]; let p: Bool; let ang: Bool }
struct TempDef { let f: Double; let off: Double }
struct Constant {
    let group: String, name: String, sym: String, alias: [String], value: String, unit: String, desc: String
    // What gets typed: the first other name, else the name
    var typed: String { alias.first ?? name }
}

final class Engine {
    // ── Settings ──
    var angleMode = "deg"
    var lastAns = Q(0)
    var decPlaces: Int? = nil
    var sigFigs: Int? = nil
    var pow10 = false
    private var bootstrapping = false

    static let BASE = ["m", "kg", "s", "A", "K", "mol", "cd"]
    static let NODIM = [Double](repeating: 0, count: 7)
    static func D(_ e: Double...) -> [Double] {
        var d = NODIM
        for (i, x) in e.enumerated() { d[i] = x }
        return d
    }

    static func near(_ a: Double, _ b: Double) -> Bool { abs(a - b) < 1e-9 }
    static func isDimless(_ d: [Double]) -> Bool { d.allSatisfy { near($0, 0) } }
    static func sameDim(_ a: [Double], _ b: [Double]) -> Bool { (0..<7).allSatisfy { near(a[$0], b[$0]) } }
    static func combine(_ a: [Double], _ b: [Double], _ k: Double) -> [Double] { (0..<7).map { a[$0] + k * b[$0] } }
    static func fail(_ msg: String) -> CalcError { CalcError(message: msg) }

    static let LEN = D(1), MASS = D(0, 1), TIME = D(0, 0, 1), CUR = D(0, 0, 0, 1), TEMPD = D(0, 0, 0, 0, 1),
               AMOUNT = D(0, 0, 0, 0, 0, 1), AREA = D(2), VOL = D(3), FREQ = D(0, 0, -1), SPEED = D(1, 0, -1),
               FORCE = D(1, 1, -2), ENERGY = D(2, 1, -2), POWER = D(2, 1, -3), PRESS = D(-1, 1, -2),
               CHARGE = D(0, 0, 1, 1), VOLT = D(2, 1, -3, -1), OHM = D(2, 1, -3, -2), FARAD = D(-2, -1, 4, 2),
               TESLA = D(0, 1, -2, -1), WEBER = D(2, 1, -2, -1), HENRY = D(2, 1, -2, -2), DOSE = D(2, 0, -2)

    // °C / °F: a lone reading such as "25 °C" or "(25 °C)" is a temperature
    // (298.15 K); inside a calculation they are steps, as in J/(kg·°C) × 10 °C.
    static let TEMP: [String: TempDef] = {
        let c = TempDef(f: 1, off: 273.15), f = TempDef(f: 5.0 / 9, off: 459.67 * 5 / 9)
        return ["°C": c, "°F": f, "degC": c, "degF": f]
    }()

    // name → factor to SI, dimension, accepts SI prefixes, is an angle
    static let UNITS: [String: UnitDef] = {
        var u: [String: UnitDef] = [:]
        func def(_ names: String, _ f: Double, _ d: [Double], _ p: Bool = false, _ ang: Bool = false) {
            for n in names.split(separator: " ") { u[String(n)] = UnitDef(f: f, d: d, p: p, ang: ang) }
        }
        def("m", 1, LEN, true)
        def("g", 1e-3, MASS, true)
        def("s", 1, TIME, true)
        def("A", 1, CUR, true)
        def("K", 1, TEMPD, true)
        def("mol", 1, AMOUNT, true)
        def("cd", 1, D(0, 0, 0, 0, 0, 0, 1))
        def("Hz Bq", 1, FREQ, true)
        def("N", 1, FORCE, true)
        def("J", 1, ENERGY, true)
        def("W", 1, POWER, true)
        def("Pa", 1, PRESS, true)
        def("C", 1, CHARGE, true)
        def("V", 1, VOLT, true)
        def("Ω ohm", 1, OHM, true)
        def("F", 1, FARAD, true)
        def("T", 1, TESLA, true)
        def("Wb", 1, WEBER, true)
        def("H", 1, HENRY, true)
        def("Gy Sv", 1, DOSE, true)
        // time
        def("min", 60, TIME)
        def("h", 3600, TIME)
        def("d day", 86400, TIME)
        def("yr", 3.15576e7, TIME, true)
        // length, area, volume
        def("Å", 1e-10, LEN)
        def("in", 0.0254, LEN)
        def("ft", 0.3048, LEN)
        def("yd", 0.9144, LEN)
        def("mi", 1609.344, LEN)
        def("nmi", 1852, LEN)
        def("AU", 1.495978707e11, LEN)
        def("ly", 9.4607304725808e15, LEN)
        def("pc", 3.0856775814914e16, LEN)
        def("ha", 1e4, AREA)
        def("L l", 1e-3, VOL, true)
        def("gal", 3.785411784e-3, VOL)
        // mass
        def("t", 1000, MASS)
        def("u Da", 1.66053906892e-27, MASS, true)
        def("lb", 0.45359237, MASS)
        def("oz", 0.028349523125, MASS)
        // speed
        def("mph", 0.44704, SPEED)
        def("knot", 1852.0 / 3600, SPEED)
        // force, energy, power, pressure, charge
        def("kgf", 9.80665, FORCE)
        def("lbf", 4.4482216152605, FORCE)
        def("dyn", 1e-5, FORCE)
        def("eV", 1.602176634e-19, ENERGY, true)
        def("cal", 4.184, ENERGY, true)
        def("Wh", 3600, ENERGY, true)
        def("erg", 1e-7, ENERGY)
        def("hp", 745.69987158227, POWER)
        def("bar", 1e5, PRESS, true)
        def("atm", 101325, PRESS)
        def("mmHg", 133.322387415, PRESS)
        def("torr", 101325.0 / 760, PRESS)
        def("psi", 6894.757293168, PRESS)
        def("Ah", 3600, CHARGE, true)
        // angles (dimensionless, stored in radians)
        def("rad", 1, NODIM, true, true)
        def("° deg", Double.pi / 180, NODIM, false, true)
        def("rev", 2 * Double.pi, NODIM, false, true)
        def("arcmin", Double.pi / 10800, NODIM, false, true)
        def("arcsec", Double.pi / 648000, NODIM, false, true)
        def("rpm", 2 * Double.pi / 60, FREQ)
        for (n, t) in TEMP { def(n, t.f, TEMPD) }
        return u
    }()

    // Insertion order matters: the first prefix that fits wins.
    static let PREFIX: [(String, Double)] = [
        ("f", 1e-15), ("p", 1e-12), ("n", 1e-9), ("μ", 1e-6), ("µ", 1e-6), ("u", 1e-6), ("m", 1e-3), ("c", 1e-2),
        ("d", 1e-1), ("h", 1e2), ("k", 1e3), ("M", 1e6), ("G", 1e9), ("T", 1e12), ("P", 1e15)]

    // CODATA 2022 (physics.nist.gov/constants); Earth, Moon and Sun: IAU nominal values.
    // Group, name, symbol, other names, value, unit, description. The symbol is drawn with
    // "_" starting a subscript; the first other name (else the name) is what gets typed.
    // h, e and g already mean hour, Euler's number and gram, hence hP, qe and gn.
    static let CONSTANTS: [Constant] = [
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
    ]
    static func K(_ group: String, _ name: String, _ sym: String, _ alias: String, _ value: String, _ unit: String,
                  _ desc: String) -> Constant {
        Constant(group: group, name: name, sym: sym, alias: alias.isEmpty ? [] : alias.components(separatedBy: " "),
                 value: value, unit: unit, desc: desc)
    }

    static let CONST_Q: [String: Q] = {
        var m: [String: Q] = ["pi": Q(Double.pi), "e": Q(M_E)]
        let boot = Engine()
        boot.bootstrapping = true
        for c in CONSTANTS { m[c.name] = try! boot.evaluate("\(c.value) \(c.unit)").q }
        return m
    }()
    static let CONST_ALIAS: [String: String] = {
        var m = ["π": "pi", "PI": "pi", "E": "e", "µ₀": "mu0", "µ0": "mu0"]
        for c in CONSTANTS { for a in c.alias { m[a] = c.name } }
        return m
    }()

    // LaTeX names typed with a backslash become symbols: \hbar → ħ, \epsilon_0 → ε_0.
    // None is the start of another, so each can be swapped as soon as it is complete.
    static let LATEX: [String: String] = [
        "alpha": "α", "beta": "β", "gamma": "γ", "delta": "δ", "epsilon": "ε", "varepsilon": "ε", "zeta": "ζ", "eta": "η",
        "theta": "θ", "iota": "ι", "kappa": "κ", "lambda": "λ", "mu": "μ", "nu": "ν", "xi": "ξ", "pi": "π", "rho": "ρ",
        "sigma": "σ", "tau": "τ", "upsilon": "υ", "phi": "φ", "varphi": "φ", "chi": "χ", "psi": "ψ", "omega": "ω",
        "Gamma": "Γ", "Delta": "Δ", "Theta": "Θ", "Lambda": "Λ", "Xi": "Ξ", "Pi": "Π", "Sigma": "Σ", "Phi": "Φ",
        "Psi": "Ψ", "Omega": "Ω", "hbar": "ħ", "times": "×", "cdot": "·", "div": "÷", "circ": "°", "to": "→"]
    static let reLatex = try! NSRegularExpression(pattern: "\\\\([A-Za-z]+)")
    static func fromLatex(_ s: String) -> String {
        let ns = s as NSString
        var out = "", last = 0
        for m in reLatex.matches(in: s, range: NSRange(location: 0, length: ns.length)) {
            out += ns.substring(with: NSRange(location: last, length: m.range.location - last))
            out += LATEX[ns.substring(with: m.range(at: 1))] ?? ns.substring(with: m.range)
            last = m.range.location + m.range.length
        }
        return out + ns.substring(from: last)
    }

    // Constants search. 0 = the name or symbol itself, 1 = starts it, 2 = starts a word
    // of the description, 3 = inside it, 4 = the group's name
    static func constRank(_ c: Constant, _ q: String) -> Int {
        let norm = { (t: String) in t.lowercased().filter { $0 != "_" && !$0.isWhitespace } }
        let qn = norm(q), ql = q.lowercased().trimmingCharacters(in: .whitespaces)
        let keys = ([c.name, c.sym] + c.alias).map(norm)
        if keys.contains(qn) { return 0 }
        if keys.contains(where: { $0.hasPrefix(qn) }) { return 1 }
        let d = c.desc.lowercased()
        if d.split(whereSeparator: { !$0.isLetter && !$0.isNumber }).contains(where: { $0.hasPrefix(ql) }) { return 2 }
        if d.contains(ql) { return 3 }
        return c.group.lowercased().contains(ql) ? 4 : -1
    }
    static func findConstants(_ q: String) -> [Constant] {
        if q.trimmingCharacters(in: .whitespaces).isEmpty { return CONSTANTS }
        return CONSTANTS.enumerated().map { (c: $0.element, i: $0.offset, r: constRank($0.element, q)) }
            .filter { $0.r >= 0 }.sorted { ($0.r, $0.i) < ($1.r, $1.i) }.map { $0.c }
    }

    func toRad(_ x: Q) -> Double { x.ang ? x.v : angleMode == "deg" ? x.v * Double.pi / 180 : x.v }

    // ── Unit names for display ──
    static let SUP: [Character] = Array("⁰¹²³⁴⁵⁶⁷⁸⁹")
    static func jsRound(_ x: Double) -> Double { (x + 0.5).rounded(.down) }

    static func sup(_ e: Double) -> String {
        if near(e, 1) { return "" }
        if near(e, jsRound(e)) {
            return String(JsNum.toJsString(jsRound(e)).map { ch -> Character in
                if let n = ch.wholeNumberValue, ch.isASCII { return SUP[n] }
                return ch
            })
        }
        return "^" + JsNum.parseFloatString(JsNum.toFixed(e, 3))
    }

    static let NAMED: [(String, [Double])] = [("N", FORCE), ("J", ENERGY), ("W", POWER), ("Pa", PRESS), ("Hz", FREQ),
        ("C", CHARGE), ("V", VOLT), ("Ω", OHM), ("F", FARAD), ("T", TESLA), ("Wb", WEBER), ("H", HENRY)]
    // The way these are usually written in class, ahead of any automatic choice
    static let PREFERRED: [(String, [Double])] = [("kg·m/s", D(1, 1, -1)), ("m/s²", D(1, 0, -2)), ("m²/s²", D(2, 0, -2)),
        ("N/m", D(0, 1, -2)), ("N·m²/C²", D(3, 1, -4, -2)), ("N·m²/kg²", D(3, -1, -2))]
    static let DISPLAY_ORDER = [1, 0, 2, 3, 5, 4, 6]  // kg·m·s·A·mol·K, as usually written
    static func cost(_ d: [Double]) -> Double { d.reduce(0) { $0 + abs($1) } }
    static func parts(_ d: [Double]) -> Int { d.filter { !near($0, 0) }.count }

    static func compose(_ numIn: [String], _ denIn: [String], _ d: [Double]) -> String {
        var num = numIn, den = denIn
        for i in DISPLAY_ORDER {
            if d[i] > 1e-9 { num.append(BASE[i] + sup(d[i])) }
            else if d[i] < -1e-9 { den.append(BASE[i] + sup(-d[i])) }
        }
        var s = num.isEmpty ? "1" : num.joined(separator: "·")
        if !den.isEmpty { s += "/" + (den.count > 1 ? "(" + den.joined(separator: "·") + ")" : den[0]) }
        return s
    }

    static func unitLabel(_ d: [Double]) -> String {
        if isDimless(d) { return "" }
        for (name, pd) in PREFERRED where (0..<7).allSatisfy({ jsRound(d[$0] * 1e6) / 1e6 == pd[$0] }) { return name }
        if let exact = NAMED.first(where: { sameDim(d, $0.1) }) { return exact.0 }
        let baseCost = cost(d)
        if baseCost <= 2 { return compose([], [], d) }
        // Try one named unit times base units, e.g. J/(kg·K) or W/m²
        var best: (c: Double, name: String, sign: Double, rest: [Double])? = nil
        for (name, nd) in NAMED where name != "Hz" {
            for sign in [1.0, -1.0] {
                // fewer base units break ties: W/(m²·K⁴) rather than N/(m·s·K⁴)
                let rest = combine(d, nd, -sign)
                let c = 1 + cost(rest) + 0.1 * Double(parts(rest))
                if c < baseCost && (best == nil || c < best!.c) { best = (c, name, sign, rest) }
            }
        }
        guard let b = best else { return compose([], [], d) }
        return b.sign > 0 ? compose([b.name], [], b.rest) : compose([], [b.name], b.rest)
    }
    static func dimName(_ d: [Double]) -> String { let s = unitLabel(d); return s.isEmpty ? "a plain number" : s }

    // ── Operations ──
    func add(_ a: Q, _ b: Q, _ sign: Double) throws -> Q {
        if a.ang != b.ang && Engine.isDimless(a.d) && Engine.isDimless(b.d) {
            return Q(toRad(a) + sign * toRad(b), Engine.NODIM, true)   // plain number joins an angle in DEG/RAD
        }
        if !Engine.sameDim(a.d, b.d) {
            throw Engine.fail("can't \(sign > 0 ? "add" : "subtract") \(Engine.dimName(b.d)) \(sign > 0 ? "to" : "from") \(Engine.dimName(a.d))")
        }
        return Q(a.v + sign * b.v, a.d, a.ang || b.ang)
    }
    static func mul(_ a: Q, _ b: Q) -> Q {
        let d = combine(a.d, b.d, 1)
        return Q(a.v * b.v, d, isDimless(d) && a.ang != b.ang)
    }
    static func div(_ a: Q, _ b: Q) -> Q {
        let d = combine(a.d, b.d, -1)
        return Q(a.v / b.v, d, isDimless(d) && a.ang && !b.ang)
    }
    static func pow(_ a: Q, _ b: Q) throws -> Q {
        if !isDimless(b.d) { throw fail("an exponent can't have units (got \(unitLabel(b.d)))") }
        return Q(JsNum.pow(a.v, b.v), a.d.map { $0 * b.v }, a.ang && b.v == 1)
    }

    static func needPlain(_ name: String, _ x: Q) throws {
        if !isDimless(x.d) { throw fail("\(name)( ) needs a plain number, got \(unitLabel(x.d))") }
    }

    // Arity checked like the web version's fn.length (0 = any number)
    static let FN: [String: Int] = ["sin": 1, "cos": 1, "tan": 1, "asin": 1, "acos": 1, "atan": 1, "atan2": 2,
        "sinh": 1, "cosh": 1, "tanh": 1, "log": 1, "ln": 1, "log2": 1, "exp": 1, "sqrt": 1, "cbrt": 1,
        "abs": 1, "floor": 1, "ceil": 1, "round": 1, "max": 0, "min": 0]

    func callFn(_ name: String, _ xs: [Q]) throws -> Q {
        let x = xs.first ?? Q(0)
        switch name {
        case "sin": try Engine.needPlain(name, x); return Q(sin(toRad(x)))
        case "cos": try Engine.needPlain(name, x); return Q(cos(toRad(x)))
        case "tan": try Engine.needPlain(name, x); return Q(tan(toRad(x)))
        case "asin": try Engine.needPlain(name, x); return Q(asin(x.v), Engine.NODIM, true)
        case "acos": try Engine.needPlain(name, x); return Q(acos(x.v), Engine.NODIM, true)
        case "atan": try Engine.needPlain(name, x); return Q(atan(x.v), Engine.NODIM, true)
        case "atan2":
            if !Engine.sameDim(xs[0].d, xs[1].d) { throw Engine.fail("atan2(y, x) needs y and x in the same units") }
            return Q(atan2(xs[0].v, xs[1].v), Engine.NODIM, true)
        case "sinh": try Engine.needPlain(name, x); return Q(sinh(x.v))
        case "cosh": try Engine.needPlain(name, x); return Q(cosh(x.v))
        case "tanh": try Engine.needPlain(name, x); return Q(tanh(x.v))
        case "log": try Engine.needPlain(name, x); return Q(log10(x.v))
        case "ln": try Engine.needPlain(name, x); return Q(log(x.v))
        case "log2": try Engine.needPlain(name, x); return Q(log2(x.v))
        case "exp": try Engine.needPlain(name, x); return Q(exp(x.v))
        case "sqrt": return Q(x.v.squareRoot(), x.d.map { $0 / 2 })
        case "cbrt": return Q(cbrt(x.v), x.d.map { $0 / 3 })
        case "abs": return Q(Swift.abs(x.v), x.d, x.ang)
        case "floor": return Q(x.v.rounded(.down), x.d, x.ang)
        case "ceil": return Q(x.v.rounded(.up), x.d, x.ang)
        case "round": return Q(Engine.jsRound(x.v), x.d, x.ang)
        case "max", "min":
            if xs.isEmpty { throw Engine.fail("max( ) and min( ) need at least one value") }
            for y in xs where !Engine.sameDim(y.d, xs[0].d) { throw Engine.fail("max( ) and min( ) need values in the same units") }
            var v = xs[0].v
            for y in xs {
                if y.v.isNaN || v.isNaN { v = .nan }
                else { v = name == "max" ? Swift.max(v, y.v) : Swift.min(v, y.v) }
            }
            return Q(v, xs[0].d, xs[0].ang)
        default:
            throw Engine.fail("unknown function \(name)")
        }
    }

    func lookup(_ name: String) throws -> Q {
        if name == "ANS" || name == "Ans" || name == "ans" { return lastAns }
        if let u = Engine.UNITS[name] { return Q(u.f, u.d, u.ang) }
        if !bootstrapping, let c = Engine.CONST_Q[Engine.CONST_ALIAS[name] ?? name] { return c }
        let ns = name as NSString
        for (pre, factor) in Engine.PREFIX {
            let pl = (pre as NSString).length
            if ns.length > pl && name.hasPrefix(pre), let b = Engine.UNITS[ns.substring(from: pl)], b.p {
                return Q(factor * b.f, b.d, b.ang)
            }
        }
        if Engine.FN[name] != nil { throw Engine.fail("\(name) needs brackets: \(name)( … )") }
        throw Engine.fail("unknown unit or symbol \"\(name)\"")
    }

    // ── Tokenizer ── (positions are UTF-16 offsets, as in JavaScript)
    struct Tok { let t: String; let s: String; let raw: String; let v: Double; let pos: Int }

    static let reWS = try! NSRegularExpression(pattern: "\\s+")
    static let reNum = try! NSRegularExpression(pattern: "(?:[0-9]+\\.?[0-9]*|\\.[0-9]+)(?:[eE][+-]?[0-9]+)?")
    static let reSup = try! NSRegularExpression(pattern: "⁻?[⁰¹²³⁴⁵⁶⁷⁸⁹]+")
    static let reId = try! NSRegularExpression(pattern: "[\\p{L}°_][\\p{L}0-9_°₀-₉]*")
    static let reOp = try! NSRegularExpression(pattern: "\\*\\*|->|[-+−*×·⋅/÷^(),→]")
    static let OP_ALIAS: [String: String] = ["−": "-", "×": "*", "·": "*", "⋅": "*", "÷": "/", "**": "^", "->": "to", "→": "to"]

    static func match(_ re: NSRegularExpression, _ s: NSString, _ i: Int) -> String? {
        guard let m = re.firstMatch(in: s as String, options: [.anchored], range: NSRange(location: i, length: s.length - i)),
              m.range.length > 0 else { return nil }
        return s.substring(with: m.range)
    }

    static func parseNumber(_ s: String) -> Double { strtod(s, nil) }

    static func tokenize(_ str: String) throws -> [Tok] {
        let s = str as NSString
        var toks: [Tok] = []
        var i = 0
        while i < s.length {
            if let m = match(reWS, s, i) { i += (m as NSString).length; continue }
            let m: String
            if let n = match(reNum, s, i) {
                m = n
                toks.append(Tok(t: "num", s: "", raw: n, v: parseNumber(n), pos: i))
            } else if let n = match(reSup, s, i) {
                m = n
                let digits = String(n.map { $0 == "⁻" ? "-" : Character(String(SUP.firstIndex(of: $0) ?? 0)) })
                toks.append(Tok(t: "sup", s: "", raw: n, v: Double(Int(digits) ?? 0), pos: i))
            } else if let n = match(reId, s, i) {
                m = n
                toks.append(n == "to" ? Tok(t: "op", s: "to", raw: n, v: 0, pos: i) : Tok(t: "id", s: n, raw: n, v: 0, pos: i))
            } else if let n = match(reOp, s, i) {
                m = n
                toks.append(Tok(t: "op", s: OP_ALIAS[n] ?? n, raw: n, v: 0, pos: i))
            } else {
                throw fail("unexpected \"\(s.substring(with: NSRange(location: i, length: 1)))\"")
            }
            i += (m as NSString).length
        }
        return toks
    }

    // ── Parser: evaluates while it reads ──
    // Implicit multiplication binds tighter than × and ÷, so a number stays
    // with its unit: 10 N / 0.2 m = 50 N/m, and J/kg K = J/(kg·K).
    private var toks: [Tok] = []
    private var p = 0
    private func peek() -> Tok? { p < toks.count ? toks[p] : nil }
    private func at(_ i: Int) -> Tok? { i >= 0 && i < toks.count ? toks[i] : nil }
    private static func isOp(_ tk: Tok?, _ ops: String...) -> Bool { tk != nil && tk!.t == "op" && ops.contains(tk!.s) }
    private static func startsOperand(_ tk: Tok?) -> Bool {
        guard let tk = tk else { return false }
        return tk.t == "num" || tk.t == "id" || isOp(tk, "(")
    }

    func evaluate(_ input: String) throws -> CalcResult {
        let src = Engine.fromLatex(input).replacingOccurrences(of: "º", with: "°").replacingOccurrences(of: "℃", with: "°C")
                       .replacingOccurrences(of: "℉", with: "°F")
        toks = try Engine.tokenize(src)
        p = 0
        let q = try parseExpr()
        var conv: Conversion? = nil
        if Engine.isOp(peek(), "to") {
            p += 1
            guard let first = peek() else { throw Engine.fail("write a unit after \"to\", e.g. to km/h") }
            let tail = (src as NSString).substring(from: first.pos).trimmingCharacters(in: .whitespacesAndNewlines)
            let label = tail.replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)
            if first.t == "id", let t = Engine.TEMP[first.s], p == toks.count - 1 {
                p += 1
                if !Engine.sameDim(q.d, Engine.TEMPD) { throw Engine.fail("can't convert \(Engine.dimName(q.d)) to \(label)") }
                conv = Conversion(v: (q.v - t.off) / t.f, label: label)
            } else {
                let target = try parseExpr()
                if !Engine.sameDim(q.d, target.d) { throw Engine.fail("can't convert \(Engine.dimName(q.d)) to \(label)") }
                if target.v == 0 { throw Engine.fail("can't convert to \(label)") }
                let v = target.ang && !q.ang && Engine.isDimless(q.d) ? toRad(q) : q.v
                conv = Conversion(v: v / target.v, label: label)
            }
        }
        if p < toks.count { throw Engine.fail("unexpected \"\(toks[p].raw)\"") }
        return CalcResult(q: q, conv: conv)
    }

    private func parseExpr() throws -> Q {
        if let reading = tempReading() { return reading }
        var a = try parseTerm()
        while Engine.isOp(peek(), "+", "-") {
            let sign: Double = toks[p].s == "+" ? 1 : -1
            p += 1
            a = try add(a, try parseTerm(), sign)
        }
        return a
    }

    // A lone "25 °C", up to the end, a ")" or "to" → 298.15 K
    private func tempReading() -> Q? {
        var i = p
        var sign = 1.0
        if Engine.isOp(at(i), "-", "+") { sign = toks[i].s == "-" ? -1 : 1; i += 1 }
        guard let n = at(i), n.t == "num", let u = at(i + 1), u.t == "id", let t = Engine.TEMP[u.s] else { return nil }
        if let after = at(i + 2), !Engine.isOp(after, ")", "to") { return nil }
        p = i + 2
        return Q(sign * n.v * t.f + t.off, Engine.TEMPD)
    }

    private func parseTerm() throws -> Q {
        var a = try parseGroup()
        while Engine.isOp(peek(), "*", "/") {
            let op = toks[p].s
            p += 1
            a = op == "*" ? Engine.mul(a, try parseGroup()) : Engine.div(a, try parseGroup())
        }
        return a
    }

    private func parseGroup() throws -> Q {
        var a = try parseUnary()
        while Engine.startsOperand(peek()) { a = Engine.mul(a, try parseUnary()) }
        return a
    }

    private func parseUnary() throws -> Q {
        if Engine.isOp(peek(), "-", "+") {
            let neg = toks[p].s == "-"
            p += 1
            let x = try parseUnary()
            return neg ? Q(-x.v, x.d, x.ang) : x
        }
        var a = try parsePrimary()
        while let tk = peek(), tk.t == "sup" { p += 1; a = try Engine.pow(a, Q(tk.v)) }
        if Engine.isOp(peek(), "^") { p += 1; a = try Engine.pow(a, try parseUnary()) }
        return a
    }

    private func parsePrimary() throws -> Q {
        let tk = peek()
        p += 1
        guard let t = tk else { throw Engine.fail("the expression ends too early") }
        if t.t == "num" { return Q(t.v) }
        if Engine.isOp(t, "(") {
            let v = try parseExpr()
            if !Engine.isOp(at(p), ")") { p += 1; throw Engine.fail("missing \")\"") }
            p += 1
            return v
        }
        if t.t == "id" {
            if let arity = Engine.FN[t.s], Engine.isOp(peek(), "(") {
                p += 1
                var args: [Q] = []
                if !Engine.isOp(peek(), ")") { args.append(try parseExpr()) }
                while Engine.isOp(peek(), ",") { p += 1; args.append(try parseExpr()) }
                if !Engine.isOp(at(p), ")") { p += 1; throw Engine.fail("missing \")\"") }
                p += 1
                if arity > 0 && args.count != arity { throw Engine.fail("\(t.s)( ) takes \(arity) value\(arity > 1 ? "s" : "")") }
                return try callFn(t.s, args)
            }
            return try lookup(t.s)
        }
        throw Engine.fail("unexpected \"\(t.raw)\"")
    }

    // ── Formatting (matches the web calculator's display) ──
    static func withCommas(_ s: String) -> String {
        let neg = s.hasPrefix("-")
        let t = neg ? String(s.dropFirst()) : s
        let parts = t.split(separator: ".", maxSplits: 1, omittingEmptySubsequences: false)
        let intPart = Array(parts[0])
        var out = ""
        for (i, ch) in intPart.enumerated() {
            if i > 0 && (intPart.count - i) % 3 == 0 { out.append(",") }
            out.append(ch)
        }
        return (neg ? "−" : "") + out + (parts.count > 1 ? "." + parts[1] : "")
    }

    func fmtNum(_ vIn: Double) -> String {
        if vIn.isNaN { return "undefined" }
        if vIn.isInfinite { return vIn > 0 ? "∞" : "−∞" }
        let v = vIn == 0 ? 0 : vIn   // -0 → 0
        let a = Swift.abs(v)
        let sciRange = a != 0 && (a >= 1e10 || a < 1e-4)
        var s: String
        if let sig = sigFigs { s = sciRange ? JsNum.toExponential(v, sig - 1) : JsNum.toPrecision(v, sig) }
        else if let dec = decPlaces { s = JsNum.toFixed(v, dec) }
        else if sciRange {
            let parts = JsNum.toExponential(v, 8).components(separatedBy: "e")
            s = JsNum.parseFloatString(parts[0]) + "e" + parts[1]
        }
        else { s = JsNum.parseFloatString(JsNum.toPrecision(v, 12)) }
        let me = s.components(separatedBy: "e")
        if me.count == 1 { return Engine.withCommas(me[0]) }
        let exp = Int(me[1]) ?? 0
        return pow10 ? Engine.withCommas(me[0]) + " ×10^" + String(exp) : Engine.withCommas(me[0]) + "e" + String(exp)
    }

    // → (number, unit) as shown on screen
    func formatQ(_ q: Q) -> (String, String) {
        if q.ang && Engine.isDimless(q.d) {
            return angleMode == "deg" ? (fmtNum(q.v * 180 / Double.pi), "°") : (fmtNum(q.v), "rad")
        }
        return (fmtNum(q.v), Engine.unitLabel(q.d))
    }
    func formatResult(_ r: CalcResult) -> (String, String) {
        if let c = r.conv { return (fmtNum(c.v), c.label) }
        return formatQ(r.q)
    }
    static func joinOut(_ o: (String, String)) -> String {
        o.1.isEmpty ? o.0 : o.0 + (o.1 == "°" ? "" : " ") + o.1
    }

    // Text that types the result back in, at full precision
    static func reinsertText(_ r: CalcResult) -> String {
        var v: Double, unit: String
        if let c = r.conv { v = c.v; unit = c.label }
        else if r.q.ang && isDimless(r.q.d) { v = r.q.v; unit = "rad" }
        else { v = r.q.v; unit = unitLabel(r.q.d) }
        if !v.isFinite { return "" }
        let n = JsNum.toJsString12(v)
        let s = unit.isEmpty ? n : n + " " + unit
        return !unit.isEmpty || v < 0 ? "(" + s + ")" : s
    }
}

// JavaScript's number-to-text rules (toFixed, toPrecision, toExponential, String(x)).
// Exact digits come from printf, which prints the binary value exactly on macOS;
// halves round up, as in the ECMAScript spec ("pick the larger n").
enum JsNum {
    // x > 0, finite: 41 significant digits and the decimal exponent of the first one
    static func exact(_ x: Double) -> ([Int], Int) {
        let s = String(format: "%.40e", x)
        let parts = s.components(separatedBy: "e")
        let digits = parts[0].compactMap { $0.wholeNumberValue }
        return (digits, Int(parts[1]) ?? 0)
    }

    // First `keep` digits, rounded half up; the result may gain one digit on carry
    static func roundDigits(_ digits: [Int], _ keep: Int) -> [Int] {
        if keep < 0 { return [0] }
        if keep == 0 { return [digits[0] >= 5 ? 1 : 0] }
        var n = Array(digits[0..<min(keep, digits.count)])
        while n.count < keep { n.append(0) }
        if keep < digits.count && digits[keep] >= 5 {
            var i = keep - 1
            while i >= 0 {
                if n[i] == 9 { n[i] = 0; i -= 1 } else { n[i] += 1; break }
            }
            if i < 0 { n.insert(1, at: 0) }
        }
        return n
    }

    // p significant digits and the exponent, adjusted when rounding carries
    static func sigDigits(_ x: Double, _ p: Int) -> ([Int], Int) {
        let (d, e) = exact(x)
        var n = roundDigits(d, p)
        var exp = e
        if n.count > p { n.removeLast(); exp += 1 }
        return (n, exp)
    }

    static func str(_ d: [Int]) -> String { d.map(String.init).joined() }

    static func toFixed(_ xIn: Double, _ f: Int) -> String {
        if xIn.isNaN { return "NaN" }
        if Swift.abs(xIn) >= 1e21 || xIn.isInfinite { return toJsString(xIn) }
        let sign = xIn < 0 ? "-" : ""
        let x = Swift.abs(xIn)
        var m: String
        if x == 0 { m = "0" }
        else {
            let (d, e) = exact(x)
            m = str(roundDigits(d, e + 1 + f))
            while m.count > 1 && m.hasPrefix("0") { m.removeFirst() }
        }
        if f == 0 { return sign + m }
        if m.count <= f { m = String(repeating: "0", count: f + 1 - m.count) + m }
        let cut = m.index(m.endIndex, offsetBy: -f)
        return sign + m[..<cut] + "." + m[cut...]
    }

    static func toExponential(_ xIn: Double, _ f: Int) -> String {
        let sign = xIn < 0 ? "-" : ""
        let x = Swift.abs(xIn)
        var digits: String, e: Int
        if x == 0 { digits = String(repeating: "0", count: f + 1); e = 0 }
        else { let r = sigDigits(x, f + 1); digits = str(r.0); e = r.1 }
        let m = f == 0 ? digits : String(digits.prefix(1)) + "." + String(digits.dropFirst())
        return sign + m + "e" + (e >= 0 ? "+" : "-") + String(Swift.abs(e))
    }

    static func toPrecision(_ xIn: Double, _ p: Int) -> String {
        let sign = xIn < 0 ? "-" : ""
        let x = Swift.abs(xIn)
        var digits: String, e: Int
        if x == 0 { digits = String(repeating: "0", count: p); e = 0 }
        else { let r = sigDigits(x, p); digits = str(r.0); e = r.1 }
        if e < -6 || e >= p {
            let m = p == 1 ? digits : String(digits.prefix(1)) + "." + String(digits.dropFirst())
            return sign + m + "e" + (e >= 0 ? "+" : "-") + String(Swift.abs(e))
        }
        if e >= 0 {
            return sign + (p > e + 1 ? String(digits.prefix(e + 1)) + "." + String(digits.dropFirst(e + 1)) : digits)
        }
        return sign + "0." + String(repeating: "0", count: -e - 1) + digits
    }

    // parseFloat(s) then String(): drops trailing zeros of a decimal string
    static func parseFloatString(_ sIn: String) -> String {
        if sIn.contains("e") || sIn.contains("E") { return toJsString(strtod(sIn, nil)) }
        var s = sIn
        if s.contains(".") {
            while s.hasSuffix("0") { s.removeLast() }
            if s.hasSuffix(".") { s.removeLast() }
        }
        if s == "-0" { s = "0" }
        return s
    }

    static func trimZeros(_ d: [Int]) -> String {
        var s = str(d)
        while s.count > 1 && s.hasSuffix("0") { s.removeLast() }
        return s
    }

    // String(parseFloat(v.toPrecision(12)))
    static func toJsString12(_ v: Double) -> String {
        if v == 0 { return "0" }
        let (n, e) = sigDigits(Swift.abs(v), 12)
        return (v < 0 ? "-" : "") + jsFormat(trimZeros(n), e)
    }

    // String(x): the shortest digits that read back as x
    static func toJsString(_ x: Double) -> String {
        if x.isNaN { return "NaN" }
        if x.isInfinite { return x > 0 ? "Infinity" : "-Infinity" }
        if x == 0 { return "0" }
        let a = Swift.abs(x)
        for p in 1...17 {
            let (n, e) = sigDigits(a, p)
            if strtod(str(n) + "e" + String(e - p + 1), nil) == a {
                return (x < 0 ? "-" : "") + jsFormat(trimZeros(n), e)
            }
        }
        return (x < 0 ? "-" : "") + "\(a)"
    }

    // Number::toString layout for digits d1d2…dk × 10^(e-k+1)
    static func jsFormat(_ digits: String, _ e: Int) -> String {
        let k = digits.count, n = e + 1
        if k <= n && n <= 21 { return digits + String(repeating: "0", count: n - k) }
        if 0 < n && n <= 21 { return String(digits.prefix(n)) + "." + String(digits.dropFirst(n)) }
        if -6 < n && n <= 0 { return "0." + String(repeating: "0", count: -n) + digits }
        let m = k == 1 ? digits : String(digits.prefix(1)) + "." + String(digits.dropFirst())
        return m + "e" + (n - 1 >= 0 ? "+" : "-") + String(Swift.abs(n - 1))
    }

    // Math.pow with JavaScript's special cases
    static func pow(_ a: Double, _ b: Double) -> Double {
        if b.isNaN { return .nan }
        if b == 0 { return 1 }
        if (a == 1 || a == -1) && b.isInfinite { return .nan }
        return cPow(a, b)
    }
}

// Runs ../tests.tsv (results recorded from the web calculator); exit code 1 on any difference
func runSelfTest(_ path: String) -> Int32 {
    guard let text = try? String(contentsOfFile: path, encoding: .utf8) else { print("Cannot read \(path)"); return 1 }
    let engine = Engine()
    var total = 0, failed = 0
    for line in text.components(separatedBy: "\n") where !line.isEmpty && !line.hasPrefix("#") {
        let c = line.components(separatedBy: "\t")
        guard c.count >= 7 else { continue }
        engine.angleMode = c[0]
        engine.decPlaces = c[1] == "auto" ? nil : Int(c[1])
        engine.sigFigs = c[2] == "auto" ? nil : Int(c[2])
        engine.pow10 = c[3] == "1"
        var shown: String, insert = ""
        do {
            let r = try engine.evaluate(c[4])
            engine.lastAns = r.q
            shown = Engine.joinOut(engine.formatResult(r))
            insert = Engine.reinsertText(r)
        } catch let e as CalcError {
            shown = "ERR:" + e.message
        } catch {
            shown = "ERR:syntax error"
        }
        total += 1
        if shown != c[5] || insert != c[6] {
            failed += 1
            print("FAIL  \(c[4]) [\(c[0])|\(c[1])|\(c[2])|\(c[3])]\n  expected: \(c[5])   [\(c[6])]\n  got:      \(shown)   [\(insert)]")
            if ProcessInfo.processInfo.environment["GITHUB_ACTIONS"] != nil {
                print("::error::\(c[4]): expected \(c[5]) [\(c[6])], got \(shown) [\(insert)]")
            }
        }
    }
    print("\(total - failed) of \(total) cases match the web calculator.")
    return failed == 0 ? 0 : 1
}

// The C library's pow, reachable from types that define their own pow
func cPow(_ a: Double, _ b: Double) -> Double { pow(a, b) }
