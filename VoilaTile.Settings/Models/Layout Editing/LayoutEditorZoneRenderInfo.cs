namespace VoilaTile.Settings.Models
{
    /// <summary>
    /// Holds layout info for zone label placement in the editor grid.
    /// </summary>
    /// <param name="Row">The row which the zone occupies.</param>
    /// <param name="Column">The column which the zone occupies.</param>
    /// <param name="RowSpan">The row span of the zone.</param>
    /// <param name="ColumnSpan">The column span of the zone.</param>
    /// <param name="ZoneNumber">The number of the zone.</param>
    public record LayoutEditorZoneRenderInfo(int Row, int Column, int RowSpan, int ColumnSpan, int ZoneNumber);
}

