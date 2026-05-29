namespace QuanLyGiuXe.Models
{
    public enum DatabaseStatus
    {
        Unknown,
        Empty,
        Valid,
        Invalid,
        NeedMigration
    }

    public class DatabaseItem
    {
        public string Name { get; set; } = "";
        public DatabaseStatus Status { get; set; } = DatabaseStatus.Unknown;
        
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    DatabaseStatus.Valid => "✅ Compatible",
                    DatabaseStatus.Empty => "⚠ Empty",
                    DatabaseStatus.NeedMigration => "⬆ Need Migration",
                    DatabaseStatus.Invalid => "❌ Invalid",
                    _ => "❓ Unknown"
                };
            }
        }

        public string DisplayText => $"{Name} ({StatusDisplay})";

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
