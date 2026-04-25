using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class DeviceControlLog
    {
        public int Id { get; set; }
        public DateTime? Timestamp { get; set; }

        public string DeviceIp { get; set; }

        public int? OperationId { get; set; }
        public int? Param1 { get; set; }
        public int? Param2 { get; set; }
        public int? Param3 { get; set; }
        public int? Param4 { get; set; }

        public string Options { get; set; }

        public int? Ret { get; set; }
        public int? SdkError { get; set; }

        public string Notes { get; set; }
    }
}
