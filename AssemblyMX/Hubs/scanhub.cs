using AssemblyMX.Services;
using AssemblyMX.Models;
using Microsoft.AspNetCore.SignalR;
using System.Runtime.Versioning;

namespace AssemblyMX.Hubs;

public class ScanHub : Hub
{
    private readonly BatchLookupService _batchLookup;
    private readonly CutListService _cutList;
    private readonly ILogger<ScanHub> _logger;

    public ScanHub(BatchLookupService batchLookup, CutListService cutList, ILogger<ScanHub> logger)
    {
        _batchLookup = batchLookup;
        _cutList = cutList;
        _logger = logger;
    }

    public async Task SubmitScan(string scannedCode)
    {
        scannedCode = (scannedCode ?? "").Trim();
        _logger.LogInformation("Scan received: {Code}", scannedCode);

        var result = await ProcessScan(scannedCode);

        await Clients.All.SendAsync("ReceiveResult", result);
    }

    private async Task<ScanResult> ProcessScan(string scannedCode)
    {
        if (string.IsNullOrWhiteSpace(scannedCode))
            return ScanResult.Fail(scannedCode, "Empty scan");

            var batchNo = await _batchLookup.GetBatchNoAsync(scannedCode);
            if (string.IsNullOrWhiteSpace(batchNo))
                return ScanResult.Fail(scannedCode, "Batch number not found for this scan...");

            var rows = _cutList.GetAssemblyRows(batchNo, scannedCode);
            if (rows.Count == 0)
                return new ScanResult(false, $"No Moulure A Brique assembly found in {batchNo}", scannedCode, batchNo, "", 0, 0, new List<CutRow>());
            
            var totalWidth = rows.Where(r => r.Sens == "L").Sum(r => double.TryParse(r.DimMM, out var d) ? d : 0);
            var totalHeight = rows.Where(r => r.Sens == "H").Sum(r => double.TryParse(r.DimMM, out var d) ? d : 0);

            return new ScanResult(true, "OK", scannedCode, batchNo,
            rows[0].AssemblyNo, totalWidth, totalHeight,
            rows.Select(r => {
                return new CutRow(
                Description: r.Description,
                Extrusion:   r.Code,
                DimMM:       r.DimMM,
                QtsPiece:    r.PositionNo.ToString(),
                Sens:        r.Sens
                );
            }).ToList());
    }
}
