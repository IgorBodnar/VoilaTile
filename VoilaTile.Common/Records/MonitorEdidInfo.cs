namespace VoilaTile.Common.Records
{
    /// <summary>
    /// Represents monitor identification data extracted from its Extended Display Identification Data (EDID) block.
    /// </summary>
    /// <param name="ManufacturerId">The manufacturer identifier code.</param>
    /// <param name="ProductCode">The product code assigned by the manufacturer.</param>
    /// <param name="SerialNumber">The serial number of the monitor.</param>
    public record MonitorEdidInfo(string ManufacturerId, string ProductCode, string SerialNumber);
}

