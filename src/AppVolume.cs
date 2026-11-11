using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Stagehand
{
    static class AppVolume
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            void Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            void GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
            void GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
            void GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            void GetCount(out int count);
            void GetSession(int index, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            void GetState(out int state);
            void GetDisplayName(out IntPtr name);
            void SetDisplayName(IntPtr name, ref Guid context);
            void GetIconPath(out IntPtr path);
            void SetIconPath(IntPtr path, ref Guid context);
            void GetGroupingParam(out Guid grouping);
            void SetGroupingParam(ref Guid grouping, ref Guid context);
            void RegisterAudioSessionNotification(IntPtr client);
            void UnregisterAudioSessionNotification(IntPtr client);
            void GetSessionIdentifier(out IntPtr identifier);
            void GetSessionInstanceIdentifier(out IntPtr identifier);
            void GetProcessId(out uint processId);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume
        {
            void SetMasterVolume(float level, ref Guid context);
            void GetMasterVolume(out float level);
        }

        static List<ISimpleAudioVolume> Sessions(string appId)
        {
            var name = Path.GetFileNameWithoutExtension((appId ?? "").Split('!').Last().Split('\\').Last());
            var found = new List<ISimpleAudioVolume>();
            if (name.Length == 0) return found;
            IMMDevice device;
            try
            {
                ((IMMDeviceEnumerator)new MMDeviceEnumerator()).GetDefaultAudioEndpoint(0, 1, out device);
            }
            catch (COMException)
            {
                return found;
            }
            var iid = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref iid, 23, IntPtr.Zero, out var manager);
            ((IAudioSessionManager2)manager).GetSessionEnumerator(out var sessions);
            sessions.GetCount(out var count);
            for (var i = 0; i < count; i++)
            {
                sessions.GetSession(i, out var session);
                session.GetProcessId(out var processId);
                string processName;
                try { processName = Process.GetProcessById((int)processId).ProcessName; }
                catch (ArgumentException) { continue; }
                if (string.Equals(processName, name, StringComparison.OrdinalIgnoreCase)) found.Add((ISimpleAudioVolume)session);
            }
            return found;
        }

        public static double? Get(string appId)
        {
            var session = Sessions(appId).FirstOrDefault();
            if (session == null) return null;
            session.GetMasterVolume(out var level);
            return level;
        }

        public static void Set(string appId, double level)
        {
            var context = Guid.Empty;
            foreach (var session in Sessions(appId)) session.SetMasterVolume((float)Math.Max(0, Math.Min(1, level)), ref context);
        }
    }
}
