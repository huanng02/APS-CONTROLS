namespace QuanLyGiuXe.Models
{
    public enum LaneDirection
    {
        In,
        Out,
        Maintenance
    }

    public enum LaneDirectionBadge
    {
        None,
        AutoIn,
        AutoOut,
        Mixed
    }

    public static class LaneDirectionExtensions
    {
        public static string ToDbString(this LaneDirection direction)
        {
            return direction switch
            {
                LaneDirection.In => "IN",
                LaneDirection.Out => "OUT",
                LaneDirection.Maintenance => "MAINTENANCE",
                _ => "IN"
            };
        }

        public static LaneDirection ToLaneDirection(this string directionStr)
        {
            if (string.IsNullOrWhiteSpace(directionStr))
                return LaneDirection.In;

            return directionStr.ToUpperInvariant() switch
            {
                "IN" => LaneDirection.In,
                "OUT" => LaneDirection.Out,
                "MAINTENANCE" => LaneDirection.Maintenance,
                _ => LaneDirection.In
            };
        }
    }
}
