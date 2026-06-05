namespace CutListDisplay.Models;

public record CutRow(
    string Description,  // readable part description (for the worker)
    string Extrusion,    // for the part number (code of material needed)
    string DimMM,        // dimension/measurement in mm (what they cut to)
    string QtsPiece,     // quantity of pieces
    string Sens          // orientation — "way"/direction the piece goes (left or right)
);