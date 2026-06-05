using Microsoft.AspNetCore.SignalR;
using CutListDisplay.Models;
using CutListDisplay.Services;
using System.Runtime.Versioning;

namespace CutListDisplay.Models;

public record ScanResult(
    bool Success,          // did everything resolve? drives the green/red state on screen
    string Message,        // status text ("OK", "Batch not found", "No PA rows", etc.)
    string ScannedCode,    // echo back what was scanned (helps the operator confirm)
    string BatchNo,        // the resolved BatchNO (e.g. "260513B-A")
    List<CutRow> Rows      // the PA## cut rows; empty list on any failure
)
{
    public static ScanResult Fail(string scannedCode, string message) =>
        new(false, message, scannedCode, "", new List<CutRow>());
}