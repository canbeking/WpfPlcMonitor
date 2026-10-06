using SqlSugar;
using System;

namespace WpfPlcMonitor // 必须和你的 MainWindow 处于同一个命名空间（或者被正确 using）
{
    [SugarTable("AlarmLog")]
    public class AlarmLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "AlarmTitle", Length = 100)]
        public string AlarmTitle { get; set; } // 报警标题，如：温度过高报警

        [SugarColumn(ColumnName = "AlarmMessage", Length = 255)]
        public string AlarmMessage { get; set; } // 详细信息，如：当前温度 85.5 °C，超过上限 80.0 °C

        [SugarColumn(ColumnName = "AlarmLevel", Length = 20)]
        public string AlarmLevel { get; set; } // 级别：提示、警告、错误

        [SugarColumn(ColumnName = "OccurTime")]
        public DateTime OccurTime { get; set; } // 报警发生时间

        [SugarColumn(ColumnName = "IsRecovered")]
        public bool IsRecovered { get; set; } // 是否已恢复
    }
}