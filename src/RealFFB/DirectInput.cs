// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - interop DirectInput 8 (escrito para este projeto)
using System;
using System.Runtime.InteropServices;

namespace FS25RealFFB
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int EnumDevicesCallback(IntPtr lpddi, IntPtr pvRef);

    [ComImport, Guid("BF798031-483A-4DA2-AA99-5D64ED369700"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInput8W
    {
        [PreserveSig] int CreateDevice(ref Guid rguid, [MarshalAs(UnmanagedType.Interface)] out IDirectInputDevice8W device, IntPtr outer);
        [PreserveSig] int EnumDevices(int devType, [MarshalAs(UnmanagedType.FunctionPtr)] EnumDevicesCallback callback, IntPtr pvRef, int flags);
        [PreserveSig] int GetDeviceStatus(ref Guid rguid);
        [PreserveSig] int RunControlPanel(IntPtr hwnd, int flags);
        [PreserveSig] int Initialize(IntPtr hinst, int version);
        [PreserveSig] int FindDevice(ref Guid rguidClass, IntPtr name, out Guid instance);
        [PreserveSig] int EnumDevicesBySemantics(IntPtr a, IntPtr b, IntPtr c, IntPtr d, int e);
        [PreserveSig] int ConfigureDevices(IntPtr a, IntPtr b, int c, IntPtr d);
    }

    [ComImport, Guid("54D41081-DC15-4833-A41B-748F73A38179"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInputDevice8W
    {
        [PreserveSig] int GetCapabilities(IntPtr caps);
        [PreserveSig] int EnumObjects(IntPtr cb, IntPtr pvRef, int flags);
        [PreserveSig] int GetProperty(IntPtr rguidProp, IntPtr pdiph);
        [PreserveSig] int SetProperty(IntPtr rguidProp, IntPtr pdiph);
        [PreserveSig] int Acquire();
        [PreserveSig] int Unacquire();
        [PreserveSig] int GetDeviceState(int cbData, IntPtr data);
        [PreserveSig] int GetDeviceData(int cbObjectData, IntPtr rgdod, IntPtr pdwInOut, int flags);
        [PreserveSig] int SetDataFormat(IntPtr lpdf);
        [PreserveSig] int SetEventNotification(IntPtr hEvent);
        [PreserveSig] int SetCooperativeLevel(IntPtr hwnd, int flags);
        [PreserveSig] int GetObjectInfo(IntPtr pdidoi, int obj, int how);
        [PreserveSig] int GetDeviceInfo(IntPtr pdidi);
        [PreserveSig] int RunControlPanel(IntPtr hwnd, int flags);
        [PreserveSig] int Initialize(IntPtr hinst, int version, ref Guid rguid);
        [PreserveSig] int CreateEffect(ref Guid rguid, IntPtr lpeff, [MarshalAs(UnmanagedType.Interface)] out IDirectInputEffect effect, IntPtr outer);
        [PreserveSig] int EnumEffects(IntPtr cb, IntPtr pvRef, int effType);
        [PreserveSig] int GetEffectInfo(IntPtr pdei, ref Guid rguid);
        [PreserveSig] int GetForceFeedbackState(out int state);
        [PreserveSig] int SendForceFeedbackCommand(int flags);
        [PreserveSig] int EnumCreatedEffectObjects(IntPtr cb, IntPtr pvRef, int flags);
        [PreserveSig] int Escape(IntPtr pesc);
        [PreserveSig] int Poll();
    }

    [ComImport, Guid("E7E1F7C0-88D2-11D0-9AD0-00A0C9A06E35"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInputEffect
    {
        [PreserveSig] int Initialize(IntPtr hinst, int version, ref Guid rguid);
        [PreserveSig] int GetEffectGuid(out Guid guid);
        [PreserveSig] int GetParameters(IntPtr peff, int flags);
        [PreserveSig] int SetParameters(IntPtr peff, int flags);
        [PreserveSig] int Start(int iterations, int flags);
        [PreserveSig] int Stop();
        [PreserveSig] int GetEffectStatus(out int flags);
        [PreserveSig] int Download();
        [PreserveSig] int Unload();
        [PreserveSig] int Escape(IntPtr pesc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DIDEVICEINSTANCEW
    {
        public int dwSize;
        public Guid guidInstance;
        public Guid guidProduct;
        public int dwDevType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string tszInstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string tszProductName;
        public Guid guidFFDriver;
        public short wUsagePage;
        public short wUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DIEFFECT
    {
        public int dwSize;
        public int dwFlags;
        public uint dwDuration;
        public int dwSamplePeriod;
        public int dwGain;
        public uint dwTriggerButton;
        public int dwTriggerRepeatInterval;
        public int cAxes;
        public IntPtr rgdwAxes;
        public IntPtr rglDirection;
        public IntPtr lpEnvelope;
        public int cbTypeSpecificParams;
        public IntPtr lpvTypeSpecificParams;
        public int dwStartDelay;
    }

    internal static class DI
    {
        public const int DI8DEVCLASS_GAMECTRL = 4;
        public const int DIEDFL_ATTACHEDONLY = 0x1;
        public const int DIEDFL_FORCEFEEDBACK = 0x100;
        public const int DISCL_EXCLUSIVE = 0x1;
        public const int DISCL_FOREGROUND = 0x4;
        public const int DISCL_BACKGROUND = 0x8;
        public const int DIEFF_OBJECTOFFSETS = 0x2;
        public const int DIEFF_CARTESIAN = 0x10;
        public const uint DIEB_NOTRIGGER = 0xFFFFFFFF;
        public const int DIEP_DURATION = 0x1;
        public const int DIEP_TYPESPECIFICPARAMS = 0x100;
        public const int DIEP_START = 0x20000000;
        public const int DISFFC_RESET = 0x1;
        public const int DISFFC_STOPALL = 0x2;
        public const int DISFFC_SETACTUATORSON = 0x10;
        public const int DIPROP_RANGE = 4;
        public const int DIPROP_FFGAIN = 7;
        public const int DIPROP_AUTOCENTER = 9;
        public const int DIPH_DEVICE = 0;
        public const int DIPH_BYOFFSET = 1;
        public const int DIDC_FORCEFEEDBACK = 0x100;
        public const uint INFINITE = 0xFFFFFFFF;

        public static readonly Guid IID_IDirectInput8W = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        public static readonly Guid GUID_XAxis = new Guid("A36D02E0-C9F3-11CF-BFC7-444553540000");
        public static readonly Guid GUID_ConstantForce = new Guid("13541C20-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_RampForce = new Guid("13541C21-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Square = new Guid("13541C22-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Sine = new Guid("13541C23-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Triangle = new Guid("13541C24-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Spring = new Guid("13541C27-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Damper = new Guid("13541C28-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Inertia = new Guid("13541C29-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Friction = new Guid("13541C2A-8E33-11D0-9AD0-00A0C9A06E35");

        [DllImport("dinput8.dll")]
        public static extern int DirectInput8Create(IntPtr hinst, int version, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IDirectInput8W ppv, IntPtr outer);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);

        public static string Hr(int hr) { return "0x" + hr.ToString("X8"); }
    }
}
