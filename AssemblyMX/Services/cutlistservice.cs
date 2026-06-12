using AssemblyMX.Models;

namespace AssemblyMX.Services;

public class CutListService
{
    private readonly string _folder;
    private readonly ILogger<CutListService> _logger;
    private readonly string _mdbtoolsPath;
    private readonly string _variablePrefix; // "MX" 

    public CutListService(IConfiguration config, ILogger<CutListService> logger)
    {
        _logger = logger;
        var access = config.GetSection("AccessDb");
        _folder = access["Folder"] ?? @"Q:\Quotes\Batch";
        _variablePrefix = access["VariablePrefix"] ?? "MX";

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "mdbtools-win"),
            Path.Combine(AppContext.BaseDirectory, "..", "mdbtools-win"),
            @"C:\Users\alexis\source\repos\dalmen\AssemblyMX\bin\Debug\net8.0\win-x64\mdbtools-win",
        };
        _mdbtoolsPath = candidates.FirstOrDefault(p => File.Exists(Path.Combine(p, "mdb-export.exe"))) ?? "";
    }

    private string MdbCmd(string name)
    {
        if (!string.IsNullOrEmpty(_mdbtoolsPath))
        {
            var exe = Path.Combine(_mdbtoolsPath, name + ".exe");
            if (File.Exists(exe)) return exe;
        }
        return name;
    }

    private string RunMdb(string tool, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = MdbCmd(tool),
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            { result.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private Dictionary<string, string[]> ParseCsvTable(string csv)
    {
        // Returns dict of header -> column index, plus raw lines
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length < 2 ? new() : null!;
    }

    public List<AssemblyPosition> GetAssemblyRows(string batchNo, string scannedCode)
    {
        var result = new List<AssemblyPosition>();
        var path = Path.Combine(_folder, $"{batchNo}.mdb");

        if (!File.Exists(path))
        {
            _logger.LogWarning("MDB not found: {Path}", path);
            return result;
        }

        try
        {
            // ── STEP 1: Load ETIQUETTE46 ──────────────────────────────
            var etiqCsv   = RunMdb("mdb-export", $"\"{path}\" ETIQUETTE46");
            var etiqLines = etiqCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (etiqLines.Length < 2) return result;

            var etiqHeaders = ParseCsvLine(etiqLines[0]);
            int eCode    = Array.IndexOf(etiqHeaders, "CODE");
            int eOption  = Array.IndexOf(etiqHeaders, "OPTION");
            int eBatchNo = Array.IndexOf(etiqHeaders, "BATCHNO");

            if (eCode < 0 || eOption < 0)
            {
                _logger.LogWarning("ETIQUETTE46 missing CODE or OPTION column");
                return result;
            }

            // ── STEP 2: Find scanned code row → get assembly number ───
            string assemblyNo = "";
            foreach (var line in etiqLines.Skip(1))
            {
                var cols = ParseCsvLine(line);
                if (cols.Length <= Math.Max(eCode, eOption)) continue;
                var code = cols[eCode].Trim('"');
                var option = cols[eOption].Trim('"');
                if (code != scannedCode) continue;

                var asmMatch = System.Text.RegularExpressions.Regex.Match(option, @"ASSEMBLY NO\.\s*(\d+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (asmMatch.Success)
                {
                    assemblyNo = asmMatch.Groups[1].Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(assemblyNo)) return result;

            // STEP 3: Find ALL unique CODEs that share the same assembly number.
            var seenCodes = new HashSet<string>();
            var assemblyCodes = new List<(string Code, int PositionNo)>();

            foreach (var line in etiqLines.Skip(1))
            {
                var cols = ParseCsvLine(line);
                if (cols.Length <= Math.Max(eCode, eOption)) continue;
                var code   = cols[eCode].Trim('"');
                var option = cols[eOption].Trim('"');

                // Only look at rows that have a full ASSEMBLY NO. declaration
                var asmMatch = System.Text.RegularExpressions.Regex.Match(
                    option, @"ASSEMBLY NO\.\s*(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!asmMatch.Success || asmMatch.Groups[1].Value != assemblyNo) continue;

                var posMatch = System.Text.RegularExpressions.Regex.Match(
                    option, @"POSITION NO\.\s*(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                int posNo = posMatch.Success ? int.Parse(posMatch.Groups[1].Value) : 0;

                // Skip if we've already registered this position number
                if (seenCodes.Contains(code)) continue;
                seenCodes.Add(code);
                assemblyCodes.Add((code, posNo));
            }

            assemblyCodes = assemblyCodes.OrderBy(x => x.PositionNo).ToList();

            // ── STEP 4: Load ListeCoupe ───────────────────────────────
            var lcCsv   = RunMdb("mdb-export", $"\"{path}\" ListeCoupe");
            var lcLines = lcCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lcLines.Length < 2) return result;

            var lcHeaders = ParseCsvLine(lcLines[0]);
            int lCode = Array.IndexOf(lcHeaders, "CODE");
            int lDesc = Array.IndexOf(lcHeaders, "Description");
            int lDim  = Array.IndexOf(lcHeaders, "DimMM");
            int lVar  = Array.IndexOf(lcHeaders, "Variable");
            int lSens = Array.IndexOf(lcHeaders, "SENS");

            // Build lookup: CODE -> (Description, DimMM) filtered by variable prefix
            var lcLookup = new Dictionary<string, List<(string Desc, string Dim, string Sens)>>();
            foreach (var line in lcLines.Skip(1))
            {
                var cols = ParseCsvLine(line);
                if (cols.Length <= Math.Max(lCode, Math.Max(lDesc, lDim))) continue;
                var code = cols[lCode].Trim('"');
                var variable = lVar >= 0 && lVar < cols.Length ? cols[lVar].Trim('"') : "";
                if (!variable.StartsWith(_variablePrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var sens = lSens >= 0 && lSens < cols.Length ? cols[lSens].Trim('"') : "";
                var desc = cols[lDesc].Trim('"');
                var dim = cols[lDim].Trim('"');

                if (!lcLookup.ContainsKey(code)) lcLookup[code] = new List<(string, string, string)>();

                if (!lcLookup[code].Any(e => e.Sens == sens))
                    lcLookup[code].Add((desc, dim, sens)); 
            }

            // ── STEP 5: Build result ──────────────────────────────────
            foreach (var (code, posNo) in assemblyCodes)
            {
                if (lcLookup.TryGetValue(code, out var entries))
                {
                    foreach (var entry in entries)
                    {
                        result.Add(new AssemblyPosition(
                            Code: code,
                            PositionNo: posNo,
                            Description: entry.Desc,
                            DimMM: entry.Dim,
                            AssemblyNo: assemblyNo,
                            Sens: entry.Sens
                        ));
                    }
                }
                else
                {
                    result.Add(new AssemblyPosition(
                        Code:        code,
                        PositionNo:  posNo,
                        Description: "",
                        DimMM:       "",
                        AssemblyNo:  assemblyNo,
                        Sens:        ""
                    ));
                }

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reading MDB {Path}", path);
        }

        return result;
    }
}

