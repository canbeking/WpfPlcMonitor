using System;
using SqlSugar;

namespace WpfPlcMonitor
{
    [SugarTable("DeviceDataLog")] // 对应 SQLite 中的表名
    public class DeviceDataLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public float Temperature { get; set; }  // 实时温度
        public float Humidity { get; set; }     // 实时湿度
        public bool RunStatus { get; set; }     // 运行状态
        public DateTime RecordTime { get; set; } // 记录时间
    }
}