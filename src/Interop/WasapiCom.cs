using System;
using System.Runtime.InteropServices;

namespace OnmyojiTools.Interop
{
    // ==================== WASAPI 按进程音量控制（COM 接口声明） ====================

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        int GetDevice(string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(object pClient);
        int UnregisterEndpointNotificationCallback(object pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        int GetCount(out uint pcCount);
        int Item(uint nDevice, out IMMDevice ppDevice);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, out IntPtr ppInterface);
        int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        int GetId(out string ppstrId);
        int GetState(out int pdwState);
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        int GetAudioSessionControl(IntPtr AudioSessionGuid, int StreamFlags, out object ppSessionControl);
        int GetSimpleAudioVolume(IntPtr AudioSessionGuid, int StreamFlags, out ISimpleAudioVolume AudioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator ppSessionList);
        int RegisterSessionNotification(object pClient);
        int UnregisterSessionNotification(object pClient);
        int RegisterDuckNotification(string sessionId, object pClient);
        int UnregisterDuckNotification(string sessionId, object pClient);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        int GetCount(out int pcCount);
        int GetSession(int index, out IAudioSessionControl ppSession);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
    {
        int GetState(out int pRetVal);
        int GetDisplayName(out IntPtr ppRetVal);
        int SetDisplayName(string pValue, ref Guid EventContext);
        int GetIconPath(out IntPtr ppRetVal);
        int SetIconPath(string pValue, ref Guid EventContext);
        int GetGroupingParam(out Guid pGroupingParam);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(object pNewNotifications);
        int UnregisterAudioSessionNotification(object pNewNotifications);
    }

    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        int GetState(out int pRetVal);
        int GetDisplayName(out IntPtr ppRetVal);
        int SetDisplayName(string pValue, ref Guid EventContext);
        int GetIconPath(out IntPtr ppRetVal);
        int SetIconPath(string pValue, ref Guid EventContext);
        int GetGroupingParam(out Guid pGroupingParam);
        int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        int RegisterAudioSessionNotification(object pNewNotifications);
        int UnregisterAudioSessionNotification(object pNewNotifications);
        int GetSessionIdentifier(out IntPtr ppRetVal);
        int GetSessionInstanceIdentifier(out IntPtr ppRetVal);
        int GetProcessId(out uint pdwProcessId);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool bDuckingPreference);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        int SetMasterVolume(float fLevel, ref Guid EventContext);
        int GetMasterVolume(out float pfLevel);
        int SetMute(bool bMute, ref Guid EventContext);
        int GetMute(out bool pbMute);
    }

    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        int SetMute(bool bMute, ref Guid pguidEventContext);
        int GetMute(out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(ref Guid pguidEventContext);
        int VolumeStepDown(ref Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflMinVolumeDB, out float pflMaxVolumeDB, out float pflIncrementDB);
    }
}
