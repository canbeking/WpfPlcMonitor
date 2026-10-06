using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using S7.Net;
using SqlSugar;

namespace WpfPlcMonitor
{
    public partial class MainWindow : Window
    {
        private Plc plc;
        private SqlSugarClient db;
        private bool isRunning = true;
        private long savedCount = 0;
        private List<DeviceDataLog> historyData = new List<DeviceDataLog>();
        private bool isHistoricalView = false; // 默认为 false，表示处于实时监控模式

        public MainWindow()
        {
            InitializeComponent();

            // 1. 初始化 SQLite 数据库 (SqlSugar)
            InitDatabase();

            // 2. 初始化 S7-1200 PLC 连接
            plc = new Plc(CpuType.S71200, "192.168.1.4", 0, 1);

            // 3. 启动后台采集线程
            Task.Run(() => StartPlcLoop());
        }

        private void InitDatabase()
        {
            db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = "Data Source=WpfPlcMonitor.db",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            // 自动创建表
            db.CodeFirst.InitTables<DeviceDataLog, AlarmLog>();
        }

        private void StartPlcLoop()
        {
            isRunning = true;
            int reconnectDelay = 2000;

            // 定义报警状态标志位（防止每秒重复弹窗刷屏）
            bool isTempAlarmActive = false;
            bool isHumidityAlarmActive = false;

            // 设定报警阈值（你可以根据需要改成可配置的变量）
            float tempHighLimit = 50.0f; // 温度上限示例
            float humidityHighLimit = 80.0f; // 湿度上限示例

            while (isRunning)
            {
                try
                {
                    if (!plc.IsConnected)
                    {
                        Dispatcher.Invoke(() => {
                            txtLogStatus.Text = "状态: 正在连接 PLC...";
                        });
                        plc.Open();
                    }

                    if (plc.IsConnected)
                    {
                        Dispatcher.Invoke(() => {
                            txtLogStatus.Text = "状态: PLC 运行正常 | 数据采集中";
                        });

                        float temperature = (float)plc.Read(DataType.DataBlock, 1, 0, VarType.Real, 1);
                        float humidity = (float)plc.Read(DataType.DataBlock, 1, 4, VarType.Real, 1);
                        bool runStatus = (bool)plc.Read(DataType.DataBlock, 1, 8, VarType.Bit, 1);

                        DateTime now = DateTime.Now;

                        // 2. 存入 SQLite 数据库（实时数据表）
                        var log = new DeviceDataLog
                        {
                            Temperature = temperature,
                            Humidity = humidity,
                            RunStatus = runStatus,
                            RecordTime = now
                        };
                        db.Insertable(log).ExecuteCommand();
                        savedCount++;

                        // ==================== 【新增】报警检测与防刷屏逻辑 ====================

                        // 1. 检测温度是否超标
                        if (temperature > tempHighLimit)
                        {
                            if (!isTempAlarmActive) // 之前是正常的，刚刚触发报警（上升沿）
                            {
                                isTempAlarmActive = true;

                                // 写入报警日志表
                                var alarmLog = new AlarmLog
                                {
                                    AlarmTitle = "温度过高报警",
                                    AlarmMessage = $"当前温度为 {temperature:F1} °C，已超过设定上限 {tempHighLimit} °C",
                                    AlarmLevel = "警告",
                                    OccurTime = now,
                                    IsRecovered = false
                                };
                                db.Insertable(alarmLog).ExecuteCommand();

                                // 弹窗提示（必须在 UI 线程）
                                Dispatcher.Invoke(() =>
                                {
                                    System.Windows.MessageBox.Show(
                                        $"【报警触发】\n{alarmLog.AlarmMessage}\n发生时间：{now:yyyy-MM-dd HH:mm:ss}",
                                        "系统报警",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Warning);
                                });
                            }
                        }
                        else
                        {
                            if (isTempAlarmActive) // 之前处于报警状态，现在恢复了（下降沿）
                            {
                                isTempAlarmActive = false;
                            }
                        }

                        // 2. 检测湿度是否超标（逻辑同上）
                        if (humidity > humidityHighLimit)
                        {
                            if (!isHumidityAlarmActive)
                            {
                                isHumidityAlarmActive = true;
                                var alarmLog = new AlarmLog
                                {
                                    AlarmTitle = "湿度过高报警",
                                    AlarmMessage = $"当前湿度为 {humidity:F1} %，已超过设定上限 {humidityHighLimit} %",
                                    AlarmLevel = "警告",
                                    OccurTime = now,
                                    IsRecovered = false
                                };
                                db.Insertable(alarmLog).ExecuteCommand();

                                Dispatcher.Invoke(() =>
                                {
                                    System.Windows.MessageBox.Show(
                                        $"【报警触发】\n{alarmLog.AlarmMessage}\n发生时间：{now:yyyy-MM-dd HH:mm:ss}",
                                        "系统报警",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Warning);
                                });
                            }
                        }
                        else
                        {
                            if (isHumidityAlarmActive)
                            {
                                isHumidityAlarmActive = false;
                            }
                        }
                        // ====================================================================

                        // 3. 实时刷新 WPF 界面与图表
                        Dispatcher.Invoke(() =>
                        {
                            txtTemp.Text = $"{temperature:F1} °C";
                            txtHumidity.Text = $"{humidity:F1} %";

                            if (runStatus)
                            {
                                txtStatus.Text = "状态: 运行中";
                                txtStatus.Foreground = System.Windows.Media.Brushes.Green;
                            }
                            else
                            {
                                txtStatus.Text = "状态: 停止";
                                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                            }

                            txtLogStatus.Text = $"状态: 正常运行中 | 已成功存入 SQLite: {savedCount} 条数据";

                            if (!isHistoricalView)
                            {
                                UpdateChart();
                            }
                        });

                        reconnectDelay = 2000;
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        txtLogStatus.Text = $"状态: 断线异常，{reconnectDelay / 1000}秒后重试... ({ex.Message})";

                        // 实时温湿度清空或显示占位符
                        txtTemp.Text = "-- °C";
                        txtHumidity.Text = "-- %";

                        // 设备运行状态置为“离线”，颜色变灰
                        txtStatus.Text = "状态: 离线";
                        txtStatus.Foreground = System.Windows.Media.Brushes.Gray;
                    });

                    try
                    {
                        if (plc.IsConnected)
                        {
                            plc.Close();
                        }
                    }
                    catch { }

                    Thread.Sleep(reconnectDelay);

                    if (reconnectDelay < 10000)
                    {
                        reconnectDelay += 2000;
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private void UpdateChart()
        {
            historyData = db.Queryable<DeviceDataLog>()
                                .OrderBy(x => x.Id, SqlSugar.OrderByType.Desc)
                                .Take(50)
                                .ToList();

            if (historyData == null || historyData.Count == 0) return;
            historyData.Reverse();

            double[] temps = historyData.Select(x => (double)x.Temperature).ToArray();
            double[] humids = historyData.Select(x => (double)x.Humidity).ToArray();

            // 清空画布上的线条
            pltTemperature.Plot.Clear();

            // 1. 添加温度曲线（默认绑定左侧 Y 轴）
            var tempSignal = pltTemperature.Plot.Add.Signal(temps);
            tempSignal.Color = ScottPlot.Color.FromHex("#007ACC");
            tempSignal.LegendText = "温度 (°C)";

            // 2. 添加湿度曲线（直接绑定到自带的右侧 Y 轴）
            var humidSignal = pltTemperature.Plot.Add.Signal(humids);
            humidSignal.Color = ScottPlot.Color.FromHex("#28A745");
            humidSignal.LegendText = "湿度 (%)";

            // 【关键修复】：直接使用自带的右侧轴，并将其显示出来
            var rightAxis = pltTemperature.Plot.Axes.Right;
            rightAxis.IsVisible = true;
            humidSignal.Axes.YAxis = rightAxis;

            // 图表基础美化与中文适配
            pltTemperature.Plot.Title("温湿度历史趋势对照");
            pltTemperature.Plot.XLabel("采样点序号");
            pltTemperature.Plot.YLabel("温度 (°C)");
            rightAxis.Label.Text = "湿度 (%)";

            pltTemperature.Plot.Font.Automatic();
            pltTemperature.Plot.ShowLegend();

            pltTemperature.Plot.Axes.AutoScale();
            pltTemperature.Refresh();
        }

        private void pltTemperature_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = e.GetPosition(pltTemperature);
            ScottPlot.Pixel px = new((float)position.X, (float)position.Y);
            ScottPlot.Coordinates coords = pltTemperature.Plot.GetCoordinates(px);

            int sampleIndex = (int)Math.Round(coords.X);

            if (historyData != null && sampleIndex >= 0 && sampleIndex < historyData.Count)
            {
                var target = historyData[sampleIndex];
                // 实时更新底部右侧的光标追踪提示
                txtCursorInfo.Text = $"[光标追踪] 采样序号: {sampleIndex} | 温度: {target.Temperature:F1} °C | 湿度: {target.Humidity:F1} %";
            }
        }

        private void pltTemperature_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            txtCursorInfo.Text = "光标已移出图表区";
        }

        private void BtnQuery_Click(object sender, RoutedEventArgs e)
        {
            // 1. 获取前台选中的起止时间
            DateTime? startTime = dpStart.SelectedDate;
            DateTime? endTime = dpEnd.SelectedDate;

            if (startTime == null || endTime == null)
            {
                MessageBox.Show("请先选择完整的开始时间和结束时间！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 保证结束时间包含当天的最后一秒（可选优化）
            DateTime start = startTime.Value.Date;
            DateTime end = endTime.Value.Date.AddDays(1).AddSeconds(-1);

            if (start > end)
            {
                MessageBox.Show("开始时间不能大于结束时间！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 切换到历史视图模式，锁住实时刷新
            isHistoricalView = true;

            // 2. 暂停实时的后台自动刷新（或者设置一个标志位，避免实时数据覆盖历史查询结果）
            // isRunning = false; // 如果你想在查看历史时暂停实时写入，可以控制标志位

            // 3. 使用 SqlSugar 根据时间段过滤并查询历史数据
            historyData = db.Queryable<DeviceDataLog>()
                            .Where(x => x.RecordTime >= start && x.RecordTime <= end)
                            .OrderBy(x => x.RecordTime, SqlSugar.OrderByType.Asc) // 历史回放一般按时间正序排列
                            .ToList();

            // 4. 用查出来的数据刷新图表
            UpdateChartWithHistoryData();

            txtLogStatus.Text = $"状态: 历史查询完成 | 命中数据: {historyData.Count} 条 (区间: {start:yyyy-MM-dd} 至 {end:yyyy-MM-dd})";
        }

        // 重置回实时监控状态的按钮
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            // 解除历史视图锁，切回实时监控模式
            isHistoricalView = false;

            // 恢复默认加载最近 50 条实时数据
            LoadLatestData();
            txtLogStatus.Text = "状态: 已切换回实时监控模式";
        }

        private void UpdateChartWithHistoryData()
        {
            if (historyData == null || historyData.Count == 0)
            {
                MessageBox.Show("该时间段内没有找到对应的历史数据记录。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 1. 清空图表中的所有图形、曲线与图例
            pltTemperature.Plot.Clear();

            // 2. 准备双 Y 轴的数据源
            double[] xs = Enumerable.Range(0, historyData.Count).Select(i => (double)i).ToArray();
            double[] temps = historyData.Select(x => (double)x.Temperature).ToArray();
            double[] humids = historyData.Select(x => (double)x.Humidity).ToArray();

            // 3. 绘制温度曲线（默认使用左侧主 Y 轴）
            var tempPlot = pltTemperature.Plot.Add.Scatter(xs, temps);
            tempPlot.LegendText = "温度 (°C)";
            tempPlot.Color = ScottPlot.Color.FromHex("#1f77b4");

            // 4. 绘制湿度曲线
            var humidPlot = pltTemperature.Plot.Add.Scatter(xs, humids);
            humidPlot.LegendText = "湿度 (%)";
            humidPlot.Color = ScottPlot.Color.FromHex("#2ca02c");

            // 5. 将湿度曲线绑定到右侧副 Y 轴
            // ScottPlot 5 中可以直接通过 AddRightAxis() 添加，为了防止每次点击按钮重复添加，
            // 我们可以先清空所有右侧面板，或者直接复用。最简单的做法是利用 Plot 自带的 YAxes 集合管理：

            // 移除之前可能多出来的右侧 Y 轴面板
            var rightAxes = pltTemperature.Plot.Axes.GetAxes(ScottPlot.Edge.Right);
            foreach (var axis in rightAxes)
            {
                pltTemperature.Plot.Axes.Remove(axis);
            }

            // 重新添加一个干净的右侧副 Y 轴给湿度曲线
            humidPlot.Axes.YAxis = pltTemperature.Plot.Axes.AddRightAxis();

            // 6. 显示图例并刷新
            pltTemperature.Plot.ShowLegend();
            pltTemperature.Refresh();
        }

        private void LoadLatestData()
        {
            // 从数据库中读取最近的 50 条实时数据用于展示
            historyData = db.Queryable<DeviceDataLog>()
                            .OrderBy(x => x.RecordTime, SqlSugar.OrderByType.Desc)
                            .Take(50)
                            .ToList();

            // 因为取出来是倒序的，需要反转成正序再画到图表上
            historyData.Reverse();
            UpdateChartWithHistoryData();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            isRunning = false;
            try { plc?.Close(); } catch { }
        }
    }
}