using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Stagehand
{
    sealed class AudioSpectrum
    {
        [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioClient
        {
            void Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
            void GetBufferSize(out uint frames);
            void GetStreamLatency(out long latency);
            void GetCurrentPadding(out uint frames);
            void IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
            void GetMixFormat(out IntPtr format);
            void GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
            void Start();
            void Stop();
            void Reset();
            void SetEventHandle(IntPtr handle);
            void GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioCaptureClient
        {
            void GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePosition, out long counterPosition);
            void ReleaseBuffer(int frames);
            void GetNextPacketSize(out int frames);
        }

        const int Size = 4096;
        const int Loopback = 0x20000, Silent = 2;
        readonly float[] ring = new float[Size];
        readonly double[] real = new double[Size], imaginary = new double[Size];
        const double Range = 40;
        readonly double[] levels, decibels;
        double ceiling = -50;
        float[] packet = new float[0];
        int write, channels, sampleRate;
        IAudioClient client;
        IAudioCaptureClient capture;
        DateTime retryAt = DateTime.MinValue, heardAt = DateTime.MinValue;

        public AudioSpectrum(int bands)
        {
            levels = new double[bands];
            decibels = new double[bands];
        }

        public double[] Levels(double dt)
        {
            Read();
            Analyse(dt);
            return levels;
        }

        public void Stop()
        {
            try
            {
                client?.Stop();
            }
            catch (COMException error)
            {
                Log.Write("visualizer stop: " + error.Message);
            }
            client = null;
            capture = null;
        }

        void Open()
        {
            client = CoreAudio.ActivateOnSpeakers<IAudioClient>();
            client.GetMixFormat(out var format);
            try
            {
                channels = Marshal.ReadInt16(format, 2);
                sampleRate = Marshal.ReadInt32(format, 4);
                client.Initialize(0, Loopback, 2000000, 0, format, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeCoTaskMem(format);
            }
            var iid = typeof(IAudioCaptureClient).GUID;
            client.GetService(ref iid, out var service);
            capture = (IAudioCaptureClient)service;
            client.Start();
        }

        void Read()
        {
            if (DateTime.UtcNow < retryAt) return;
            try
            {
                if (client == null) Open();
                capture.GetNextPacketSize(out var frames);
                while (frames > 0)
                {
                    capture.GetBuffer(out var data, out var count, out var flags, out _, out _);
                    var samples = count * channels;
                    if (packet.Length < samples) packet = new float[samples];
                    if ((flags & Silent) != 0) Array.Clear(packet, 0, samples);
                    else Marshal.Copy(data, packet, 0, samples);
                    capture.ReleaseBuffer(count);
                    for (var i = 0; i < count; i++)
                    {
                        float mono = 0;
                        for (var c = 0; c < channels; c++) mono += packet[i * channels + c];
                        ring[write] = mono / channels;
                        write = (write + 1) % Size;
                    }
                    heardAt = DateTime.UtcNow;
                    capture.GetNextPacketSize(out frames);
                }
            }
            catch (COMException error)
            {
                Log.Write("visualizer: " + error.Message);
                client = null;
                capture = null;
                retryAt = DateTime.UtcNow.AddSeconds(3);
            }
            if ((DateTime.UtcNow - heardAt).TotalSeconds > 0.2) Array.Clear(ring, 0, Size);
        }

        void Analyse(double dt)
        {
            if (sampleRate == 0) return;
            for (var i = 0; i < Size; i++)
            {
                real[i] = ring[(write + i) % Size] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1)));
                imaginary[i] = 0;
            }
            Transform();
            var binWidth = (double)sampleRate / Size;
            var low = (int)(50 / binWidth);
            for (var band = 0; band < levels.Length; band++)
            {
                var high = Math.Min(Size / 2, Math.Max(low + 1, (int)(50 * Math.Pow(12000 / 50.0, (band + 1.0) / levels.Length) / binWidth)));
                double peak = 0;
                for (var bin = low; bin < high; bin++) peak = Math.Max(peak, Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]));
                decibels[band] = 20 * Math.Log10(peak / (Size / 4.0) + 1e-9) + 4 * Math.Log((low + high) / 2.0 * binWidth / 1000, 2);
                low = high;
            }
            ceiling = Math.Max(decibels.Max(), Math.Max(-50, ceiling - dt * 4));
            for (var band = 0; band < levels.Length; band++)
            {
                var target = Math.Max(0, Math.Min(1, (decibels[band] - ceiling + Range) / Range));
                levels[band] += (target - levels[band]) * (1 - Math.Exp(-dt * (target > levels[band] ? 30 : 7)));
            }
        }

        void Transform()
        {
            for (int i = 1, j = 0; i < Size; i++)
            {
                var bit = Size >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i >= j) continue;
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
            for (var length = 2; length <= Size; length <<= 1)
            {
                var angle = -2 * Math.PI / length;
                for (var start = 0; start < Size; start += length)
                    for (var k = 0; k < length / 2; k++)
                    {
                        double wr = Math.Cos(angle * k), wi = Math.Sin(angle * k);
                        int a = start + k, b = a + length / 2;
                        double xr = real[b] * wr - imaginary[b] * wi, xi = real[b] * wi + imaginary[b] * wr;
                        real[b] = real[a] - xr;
                        imaginary[b] = imaginary[a] - xi;
                        real[a] += xr;
                        imaginary[a] += xi;
                    }
            }
        }
    }
}
