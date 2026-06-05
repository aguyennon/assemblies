using System.Runtime.Versioning;
using AssemblyMIR.Models;
using System.Data.OleDb;

namespace AssemblyMIR.Services;

public class CutListService
{
    private readonly string _folder;
    private readonly ILogger<CutListService> _logger;
    private readonly string _mdbtoolsPath;

    public CutListService(IConfiguration config, ILogger<CutListService> logger)
    {
        _logger = logger;
        var access = config.GetSection("AccessDb");
        _folder = access["Folder"] ?? @"Q:\Quotes\Batch";

        // Look for mdbtools-win next to the project
        var here = AppContext.BaseDirectory;
        var local = Path.Combine(here, "mdbtools-win");
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "mdbtools-win"),
            Path.Combine(AppContext.BaseDirectory, "..", "mdbtools-win"),
            @"C:\Users\alexis\source\repos\dalmen\AssemblyDalmen\CutListDisplay\bin\Debug\net8.0\win-x64\mdbtools-win",
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

    public List<CutRow> GetCutRows(string batchNo, string scannedCode = "")
    {
        var rows = new List<CutRow>();
        var path = Path.Combine(_folder, $"{batchNo}.mdb");

        if (!File.Exists(path))
        {
            _logger.LogWarning("MDB not found: {Path}", path);
            return rows;
        }

        try
        {
            var csv = RunMdb("mdb-export", $"\"{path}\" ListeCoupe");
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return rows;

            var headers = ParseCsvLine(lines[0]);
            int iDesc = Array.IndexOf(headers, "Description");
            int iExtr = Array.IndexOf(headers, "Extrusion");
            int iDim  = Array.IndexOf(headers, "DimMM");
            int iQty  = Array.IndexOf(headers, "QTSPIECE");
            int iSens = Array.IndexOf(headers, "SENS");
            int iVar  = Array.IndexOf(headers, "Variable");
            int iCode = Array.IndexOf(headers, "CODE");

            _logger.LogInformation("iExtr={a} iSens={b}", iExtr, iSens);
            if (lines.Length > 1)
                _logger.LogInformation("First parsed row: {R}", string.Join("|", ParseCsvLine(lines[1])));

            _logger.LogInformation("Headers: {H}", string.Join("|", headers));
            _logger.LogInformation("iDesc={a} iExtr={b} iDim={c} iQty={d} iSens={e} iVar={f} iCode={g}", iDesc, iExtr, iDim, iQty, iSens, iVar, iCode);
            if (lines.Length > 1)
                _logger.LogInformation("First data row: {R}", lines[1]);

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = ParseCsvLine(lines[i]);
                if (cols.Length <= Math.Max(iDesc, Math.Max(iDim, Math.Max(iQty, iSens)))) continue;

                var variable = iVar >= 0 && iVar < cols.Length ? cols[iVar].Trim('"') : "";
                var code = iCode >= 0 && iCode < cols.Length ? cols[iCode].Trim('"') : "";

                if (!string.IsNullOrEmpty(scannedCode) && code != scannedCode) continue;
                if (!variable.StartsWith("MIR")) continue;

                rows.Add(new CutRow(
                    Description: cols[iDesc].Trim('"'),
                    Extrusion: iExtr >= 0 && iExtr < cols.Length ? cols[iExtr].Trim('"') : "",
                    DimMM: cols[iDim].Trim('"'),
                    QtsPiece: cols[iQty].Trim('"'),
                    Sens: cols[iSens].Trim('"')
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed reading MDB {Path}", path);
        }

        return rows;
    }

    private  static string[] ParseCsvLine(string line)
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
}