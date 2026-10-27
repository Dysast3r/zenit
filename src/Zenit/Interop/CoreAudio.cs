using System.Runtime.InteropServices;
using Zenit.Services;

namespace Zenit.Interop;

/// <summary>Interfaces COM mínimas de Core Audio (MMDevice API). Solo se declaran los métodos que se usan.</summary>
internal static class CoreAudio
{
    private const int ClsCtxAll = 23;

    /// <summary>Activa una interfaz sobre el dispositivo de salida predeterminado (multimedia).</summary>
    public static T? ActivateOnDefaultOutput<T>() where T : class
    {
        // Se llama cada pocos segundos: el enumerador y el dispositivo se liberan; solo queda viva la interfaz devuelta.
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device) != 0) return null;
            var iid = typeof(T).GUID;
            if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var instance) != 0) return null;
            if (instance is T result) return result;
            Release(instance);
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Release(device);
            Release(enumerator);
        }
    }

    public static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
    }

    // ───────────────────────── Salidas de audio ─────────────────────────

    private static readonly PropertyKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    private static readonly PropertyKey EnumeratorName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 24);
    private static readonly PropertyKey FormFactor = new(new Guid("1da5d803-d492-4edd-8c23-e0c0ffee7f0e"), 0);

    /// <summary>Salidas de audio activas, con la predeterminada marcada.</summary>
    public static List<AudioOutput> ListOutputs()
    {
        var outputs = new List<AudioOutput>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            string? defaultId = null;
            if (enumerator.GetDefaultAudioEndpoint(0, 1, out var current) == 0)
            {
                current.GetId(out defaultId);
                Release(current);
            }
            if (enumerator.EnumAudioEndpoints(0 /* eRender */, 1 /* DEVICE_STATE_ACTIVE */, out collection) != 0) return outputs;
            collection.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) != 0) continue;
                IPropertyStore? store = null;
                try
                {
                    if (device.GetId(out var id) != 0 || device.OpenPropertyStore(0 /* STGM_READ */, out store) != 0) continue;
                    var name = ReadString(store, FriendlyName) ?? id;
                    var kind = (ReadString(store, EnumeratorName) ?? "").StartsWith("BTH", StringComparison.OrdinalIgnoreCase)
                        ? AudioOutputKind.Bluetooth
                        : ReadUInt(store, FormFactor) switch
                        {
                            1 => AudioOutputKind.Speakers,
                            3 or 5 => AudioOutputKind.Headphones,
                            9 => AudioOutputKind.Display,
                            _ => AudioOutputKind.Other,
                        };
                    outputs.Add(new AudioOutput(id, name, kind, id == defaultId));
                }
                finally
                {
                    Release(store);
                    Release(device);
                }
            }
        }
        catch
        {
            // Sin Core Audio: la lista queda vacía y la página no ofrece el selector.
        }
        finally
        {
            Release(collection);
            Release(enumerator);
        }
        return outputs;
    }

    /// <summary>
    /// Hace predeterminada una salida para todos los usos (consola, multimedia y comunicaciones). Usa IPolicyConfig,
    /// que no está documentada pero es la que usa el panel de sonido de Windows desde Windows 7.
    /// </summary>
    public static bool SetDefaultOutput(string id)
    {
        IPolicyConfig? policy = null;
        try
        {
            policy = (IPolicyConfig)new PolicyConfigClient();
            var ok = true;
            for (var role = 0; role < 3; role++) ok &= policy.SetDefaultEndpoint(id, role) == 0;
            return ok;
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(policy);
        }
    }

    private static string? ReadString(IPropertyStore store, PropertyKey key)
    {
        var k = key;
        if (store.GetValue(ref k, out var value) != 0) return null;
        try
        {
            return value.Type == 31 /* VT_LPWSTR */ ? Marshal.PtrToStringUni(value.Data) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static uint? ReadUInt(IPropertyStore store, PropertyKey key)
    {
        var k = key;
        if (store.GetValue(ref k, out var value) != 0) return null;
        try
        {
            return value.Type == 19 /* VT_UI4 */ ? (uint)(value.Data.ToInt64() & 0xFFFFFFFF) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct PropertyKey(Guid FormatId, int PropertyId);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort Type;
        public ushort Reserved1, Reserved2, Reserved3;
        public IntPtr Data;
        public IntPtr Data2;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class PolicyConfigClient;

    /// <summary>Solo importa el orden: los diez primeros métodos no se usan y ocupan su lugar en la tabla.</summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat();
        void GetDeviceFormat();
        void ResetDeviceFormat();
        void SetDeviceFormat();
        void GetProcessingPeriod();
        void SetProcessingPeriod();
        void GetShareMode();
        void SetShareMode();
        void GetPropertyValue();
        void SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
    }
}


// Sesiones de audio por aplicación (mezclador de volumen). En COM, cada interfaz repite los métodos de su base.

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control);
    [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
    [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
}

[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    // IAudioSessionControl
    [PreserveSig] int GetState(out int state);
    [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid eventContext);
    [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
    [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid eventContext);
    [PreserveSig] int GetGroupingParam(out Guid groupingParam);
    [PreserveSig] int SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);
    [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
    [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
    // IAudioSessionControl2
    [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetProcessId(out uint processId);
    /// <summary>S_OK (0) si es la sesión de "Sonidos del sistema"; S_FALSE (1) si no.</summary>
    [PreserveSig] int IsSystemSoundsSession();
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    [PreserveSig] int SetMasterVolume(float level, ref Guid eventContext);
    [PreserveSig] int GetMasterVolume(out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig] int GetPeakValue(out float peak);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
    [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
    [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr notifyData);
}
