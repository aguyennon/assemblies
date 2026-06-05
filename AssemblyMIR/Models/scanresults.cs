using Microsoft.AspNetCore.SignalR;
using AssemblyMIR.Models;
using AssemblyMIR.Services;
using System.Runtime.Versioning;

namespace AssemblyMIR.Models;

public record ScanResult(
    bool Success,          // did everything resolve? drives the green/red state on screen
    string Message,        // status text ("OK", "Batch not found", "No PA rows", etc.)
    string ScannedCode,    // echo back what was scanned (helps the operator confirm)
    string BatchNo,        // the resolved BatchNO (e.g. "260513B-A")
    List<CutRow> Rows      // the PA## cut rows; empty list on any failure
)
{
    // Convenience factory for the failure case so service code stays readable:
    // 'return ScanResult.Fail("...")' instead of repeating the full constructor.
    public static ScanResult Fail(string scannedCode, string message) =>
        new(false, message, scannedCode, "", new List<CutRow>());
}