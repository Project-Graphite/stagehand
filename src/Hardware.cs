using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Stagehand
{
    sealed class HardwareReading
    {
        public string GpuName;
        public double? GpuTemperature;
        public double? GpuLoad;
        public double? VramUsedGb;
        public double? VramTotalGb;
        public double? CpuLoad;
    }

    sealed class HardwareMonitor
    {
        [StructLayout(LayoutKind.Sequential)] struct Utilization { public uint Gpu, Memory; }
        [StructLayout(LayoutKind.Sequential)] struct Memory { public ulong Total, Free, Used; }

        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlInit_v2();
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlDeviceGetTemperature(IntPtr device, int sensor, out uint celsius);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out Utilization rates);
        [DllImport("nvml.dll", CallingConvention = CallingConvention.Cdecl)] static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out Memory memory);

        IntPtr gpu;
        string gpuName;
        PerformanceCounter cpu;
        bool cpuUnavailable;

        public HardwareMonitor()
        {
            try
            {
                var name = new StringBuilder(96);
                if (nvmlInit_v2() == 0 && nvmlDeviceGetHandleByIndex_v2(0, out gpu) == 0 && nvmlDeviceGetName(gpu, name, (uint)name.Capacity) == 0)
                    gpuName = name.ToString().Replace("NVIDIA ", "").Replace("GeForce ", "");
                else gpu = IntPtr.Zero;
            }
            catch (DllNotFoundException)
            {
                gpu = IntPtr.Zero;
            }
        }

        public bool HasGpu => gpu != IntPtr.Zero;

        public string GpuName => gpuName;

        public double? GpuTemperature() => HasGpu && nvmlDeviceGetTemperature(gpu, 0, out var celsius) == 0 ? celsius : (double?)null;

        public HardwareReading Read(bool withCpu)
        {
            var reading = new HardwareReading { GpuName = gpuName, GpuTemperature = GpuTemperature() };
            if (HasGpu && nvmlDeviceGetUtilizationRates(gpu, out var rates) == 0) reading.GpuLoad = rates.Gpu;
            if (HasGpu && nvmlDeviceGetMemoryInfo(gpu, out var memory) == 0)
            {
                reading.VramUsedGb = memory.Used / 1073741824.0;
                reading.VramTotalGb = memory.Total / 1073741824.0;
            }
            if (withCpu && !cpuUnavailable)
            {
                try
                {
                    if (cpu == null) cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                    reading.CpuLoad = Math.Min(100, cpu.NextValue());
                }
                catch (InvalidOperationException error)
                {
                    Log.Write("cpu load: " + error.Message);
                    cpuUnavailable = true;
                }
            }
            return reading;
        }
    }
}
