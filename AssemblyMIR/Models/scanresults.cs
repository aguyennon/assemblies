using AssemblyMIR.Models;

namespace AssemblyMIR.Models;

public class ScanResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string ScannedCode { get; init; } = "";
    public string BatchNo { get; init; } = "";
    public string AssemblyNo { get; init; } = "";
    public double TotalWidth { get; init; }
    public double TotalHeight { get; init; }
    public List<CutRow> Rows { get; init; } = new();

    public ScanResult(bool success, string message, string scannedCode, string batchNo, string assemblyNo, double totalWidth, double totalHeight, List<CutRow> rows)
    {
        Success = success; Message = message;
        ScannedCode = scannedCode; BatchNo = batchNo;
        AssemblyNo = assemblyNo; TotalWidth = totalWidth; TotalHeight = totalHeight; Rows = rows;
    }

    public static ScanResult Fail(string code, string msg) =>
        new(false, msg, code, "", "", 0, 0, new List<CutRow>());
}