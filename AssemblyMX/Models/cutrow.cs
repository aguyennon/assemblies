namespace AssemblyMX.Models;

public record CutRow(
    string Description,  // human-readable part description (for the operator's eyes)
    string Extrusion,    // For the part number (code of material needed)
    string DimMM,        // dimension/measurement in mm (what they cut to)
    string QtsPiece,     // quantity of pieces
    string Sens          // orientation — "way"/direction the piece goes
);