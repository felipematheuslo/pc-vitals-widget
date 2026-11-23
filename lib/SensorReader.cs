using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Principal;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace PcTempMonitor
{
    internal class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    internal class Program
    {
        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        private static string FormatFloat(float? val)
        {
            if (!val.HasValue || float.IsNaN(val.Value) || float.IsInfinity(val.Value))
                return "null";
            return val.Value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static void CollectSensors(IHardware hw, List<ISensor> list)
        {
            foreach (ISensor s in hw.Sensors)
            {
                list.Add(s);
            }
            foreach (IHardware sub in hw.SubHardware)
            {
                CollectSensors(sub, list);
            }
        }

        private static float? PickCpuTemp(List<ISensor> sensors)
        {
            float? pkg = null;
            float? tctl = null;
            float? coreAvg = null;
            float? coreMax = null;
            float? fallback = null;
            float coreSum = 0f;
            int coreCount = 0;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Temperature || !s.Value.HasValue)
                    continue;
                float v = s.Value.Value;
                if (v <= 0 || v > 150)
                    continue;

                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("package"))
                    pkg = v;
                else if (name.Contains("tctl") || name.Contains("tdie"))
                    tctl = v;
                else if (name.Contains("core average"))
                    coreAvg = v;
                else if (name.Contains("core max"))
                    coreMax = v;
                else if (name.Contains("core #") || name.Contains("cpu core"))
                {
                    coreSum += v;
                    coreCount++;
                }
                else if (!fallback.HasValue)
                {
                    fallback = v;
                }
            }

            if (pkg.HasValue) return pkg;
            if (tctl.HasValue) return tctl;
            if (coreAvg.HasValue) return coreAvg;
            if (coreCount > 0) return coreSum / coreCount;
            if (coreMax.HasValue) return coreMax;
            return fallback;
        }

        private static float? PickCpuLoad(List<ISensor> sensors)
        {
            float? total = null;
            float coreSum = 0f;
            int coreCount = 0;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Load || !s.Value.HasValue)
                    continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("cpu total"))
                    total = s.Value.Value;
                else if (name.Contains("core"))
                {
                    coreSum += s.Value.Value;
                    coreCount++;
                }
            }

            if (total.HasValue) return total;
            if (coreCount > 0) return coreSum / coreCount;
            return null;
        }

        private static float? PickGpuTemp(List<ISensor> sensors)
        {
            float? core = null;
            float? edge = null;
            float? hotspot = null;
            float? fallback = null;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Temperature || !s.Value.HasValue)
                    continue;
                float v = s.Value.Value;
                if (v <= 0 || v > 150)
                    continue;

                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("gpu core") || name == "core")
                    core = v;
                else if (name.Contains("edge"))
                    edge = v;
                else if (name.Contains("hot spot") || name.Contains("hotspot"))
                    hotspot = v;
                else if (!fallback.HasValue)
                    fallback = v;
            }

            if (core.HasValue) return core;
            if (edge.HasValue) return edge;
            if (fallback.HasValue) return fallback;
            return hotspot;
        }

        private static float? PickGpuLoad(List<ISensor> sensors)
        {
            float? best = null;
            float? fallback = null;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Load || !s.Value.HasValue)
                    continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("memory") || name.Contains("vram"))
                    continue;

                float v = s.Value.Value;
                if (v < 0f) v = 0f;
                if (v > 100f) v = 100f;

                if (name.Contains("gpu core") || name == "core" || name.Contains("d3d"))
                {
                    if (!best.HasValue || v > best.Value)
                        best = v;
                }
                else if (!fallback.HasValue || v > fallback.Value)
                {
                    fallback = v;
                }
            }

            return best ?? fallback;
        }

        private static void PickGpuVram(List<ISensor> sensors, out float? usedMb, out float? totalMb)
        {
            float? gpuUsed = null;
            float? d3dUsed = null;
            float? gpuTotal = null;
            float? d3dTotal = null;

            foreach (ISensor s in sensors)
            {
                if ((s.SensorType != SensorType.SmallData && s.SensorType != SensorType.Data) || !s.Value.HasValue)
                    continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                float v = s.Value.Value;
                // SensorType.Data is in GB, SmallData is in MB
                if (s.SensorType == SensorType.Data)
                    v *= 1024f;

                if (name == "gpu memory used")
                    gpuUsed = v;
                else if (name.Contains("dedicated memory used"))
                    d3dUsed = v;
                else if (name == "gpu memory total")
                    gpuTotal = v;
                else if (name.Contains("dedicated memory total"))
                    d3dTotal = v;
            }

            usedMb = d3dUsed ?? gpuUsed;
            totalMb = gpuTotal ?? d3dTotal;
        }

        private static int Main(string[] args)
        {
            bool once = false;
            int intervalMs = 1000;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--once")
                    once = true;
                else if (args[i] == "--interval" && i + 1 < args.Length)
                {
                    int parsed;
                    if (int.TryParse(args[i + 1], out parsed) && parsed >= 250)
                        intervalMs = parsed;
                }
            }

            bool isAdmin = IsAdministrator();
            Computer computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = false,
                IsMemoryEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };

            UpdateVisitor visitor = new UpdateVisitor();

            try
            {
                computer.Open();

                while (true)
                {
                    computer.Accept(visitor);

                    string cpuName = null;
                    string gpuName = null;
                    float? cpuTemp = null;
                    float? cpuLoad = null;
                    float? gpuTemp = null;
                    float? gpuLoad = null;
                    float? gpuVramUsed = null;
                    float? gpuVramTotal = null;

                    foreach (IHardware hw in computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            cpuName = hw.Name;
                            List<ISensor> sensors = new List<ISensor>();
                            CollectSensors(hw, sensors);
                            cpuTemp = PickCpuTemp(sensors);
                            cpuLoad = PickCpuLoad(sensors);
                        }
                        else if (hw.HardwareType == HardwareType.GpuAmd ||
                                 hw.HardwareType == HardwareType.GpuNvidia ||
                                 hw.HardwareType == HardwareType.GpuIntel)
                        {
                            List<ISensor> sensors = new List<ISensor>();
                            CollectSensors(hw, sensors);
                            float? t = PickGpuTemp(sensors);
                            float? l = PickGpuLoad(sensors);
                            float? vUsed;
                            float? vTotal;
                            PickGpuVram(sensors, out vUsed, out vTotal);
                            // Prefer a discrete/active GPU that actually reports a temperature
                            if (gpuTemp == null || t != null)
                            {
                                gpuName = hw.Name;
                                if (t != null) gpuTemp = t;
                                if (l != null) gpuLoad = l;
                                if (vUsed != null) gpuVramUsed = vUsed;
                                if (vTotal != null) gpuVramTotal = vTotal;
                            }
                        }
                    }

                    string json = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"cpu_temp\":{0},\"cpu_load\":{1},\"cpu_name\":{2},\"gpu_temp\":{3},\"gpu_load\":{4},\"gpu_vram_used_mb\":{5},\"gpu_vram_total_mb\":{6},\"gpu_name\":{7},\"is_admin\":{8}}}",
                        FormatFloat(cpuTemp),
                        FormatFloat(cpuLoad),
                        EscapeJson(cpuName),
                        FormatFloat(gpuTemp),
                        FormatFloat(gpuLoad),
                        FormatFloat(gpuVramUsed),
                        FormatFloat(gpuVramTotal),
                        EscapeJson(gpuName),
                        isAdmin ? "true" : "false"
                    );

                    Console.WriteLine(json);
                    Console.Out.Flush();

                    if (once) break;
                    Thread.Sleep(intervalMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                try { computer.Close(); } catch { }
            }

            return 0;
        }
    }
}
