using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoilaTile.Snapper.Records
{
    public sealed record QuickGrabBadge(
        WindowId Id,
        string HintText,
        double Xdip,
        double Ydip,
        int Z);
}
