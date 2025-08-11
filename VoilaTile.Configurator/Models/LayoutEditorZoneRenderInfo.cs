namespace VoilaTile.Configurator.Models
{
    /// <summary>
    /// Holds layout info for zone label placement in the editor grid.
    /// </summary>
    public record LayoutEditorZoneRenderInfo(int Row, int Column, int RowSpan, int ColumnSpan, int ZoneNumber);
}

