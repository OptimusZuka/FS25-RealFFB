// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - enumeracao de TODOS os controladores DirectInput, com e sem force feedback,
// para a pagina Hardware. So le a lista: nao adquire nada nem envia efeitos.
// Classificacao: tipo/subtipo DirectInput (DI8DEVTYPE_*) + presenca de FFB + nome do produto.
// Nenhum dispositivo sem FFB pode ser aberto para force feedback (isso e garantido em FfbDevice, que so
// enumera com DIEDFL_FORCEFEEDBACK).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FS25RealFFB
{
    internal static class DirectInputProvider
    {
        public const string Name = "DirectInput 8";
        public const string Version = "1.0";

        // valores de dinput.h
        private const int DI8DEVTYPE_JOYSTICK = 0x14, DI8DEVTYPE_GAMEPAD = 0x15, DI8DEVTYPE_DRIVING = 0x16,
                          DI8DEVTYPE_FLIGHT = 0x17, DI8DEVTYPE_SUPPLEMENTAL = 0x1C;
        private const int DI8DEVTYPEDRIVING_COMBINEDPEDALS = 2, DI8DEVTYPEDRIVING_DUALPEDALS = 3, DI8DEVTYPEDRIVING_THREEPEDALS = 4;

        public static DeviceKind Classify(int devType, int subType, bool hasFfb, string product)
        {
            string n = (product ?? "").ToLowerInvariant();
            if (hasFfb && (devType == DI8DEVTYPE_DRIVING || n.Contains("wheel") || n.Contains("base") || devType == DI8DEVTYPE_SUPPLEMENTAL || devType == DI8DEVTYPE_JOYSTICK))
                return DeviceKind.Wheelbase;
            if (n.Contains("handbrake") || n.Contains("hand brake")) return DeviceKind.Handbrake;
            if (n.Contains("shifter") || n.Contains("gearbox") || n.Contains("sequential")) return DeviceKind.Shifter;
            if (n.Contains("pedal")) return DeviceKind.Pedals;
            if (devType == DI8DEVTYPE_DRIVING && (subType == DI8DEVTYPEDRIVING_COMBINEDPEDALS || subType == DI8DEVTYPEDRIVING_DUALPEDALS || subType == DI8DEVTYPEDRIVING_THREEPEDALS))
                return DeviceKind.Pedals;
            if (hasFfb) return DeviceKind.Wheelbase;    // FFB sem tipo de volante (ex.: base DD que se apresenta como joystick)
            if (devType == DI8DEVTYPE_GAMEPAD) return DeviceKind.Gamepad;
            if (devType == DI8DEVTYPE_JOYSTICK || devType == DI8DEVTYPE_FLIGHT) return DeviceKind.Joystick;
            return DeviceKind.Other;
        }

        private static int Vid(Guid product) { return (int)(BitConverter.ToUInt32(product.ToByteArray(), 0) & 0xFFFF); }
        private static int Pid(Guid product) { return (int)(BitConverter.ToUInt32(product.ToByteArray(), 0) >> 16); }

        public static List<DeviceDescriptor> Enumerate(Action<string> log)
        {
            List<DeviceDescriptor> list = new List<DeviceDescriptor>();
            IDirectInput8W di = null;
            try
            {
                Guid iid = DI.IID_IDirectInput8W;
                int hr = DI.DirectInput8Create(DI.GetModuleHandle(null), 0x0800, ref iid, out di, IntPtr.Zero);
                if (hr < 0 || di == null) { if (log != null) log("Hardware: DirectInput8Create failed " + DI.Hr(hr)); return list; }
                List<DIDEVICEINSTANCEW> all = new List<DIDEVICEINSTANCEW>(), ff = new List<DIDEVICEINSTANCEW>();
                EnumDevicesCallback cbAll = delegate(IntPtr p, IntPtr r) { all.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1; };
                EnumDevicesCallback cbFf = delegate(IntPtr p, IntPtr r) { ff.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1; };
                di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbAll, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY);
                di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbFf, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY | DI.DIEDFL_FORCEFEEDBACK);
                GC.KeepAlive(cbAll); GC.KeepAlive(cbFf);
                foreach (DIDEVICEINSTANCEW d in all)
                {
                    bool hasFf = false;
                    foreach (DIDEVICEINSTANCEW f in ff) if (f.guidInstance == d.guidInstance) hasFf = true;
                    DeviceDescriptor x = new DeviceDescriptor();
                    x.Product = d.tszProductName ?? "";
                    x.Vid = Vid(d.guidProduct); x.Pid = Pid(d.guidProduct);
                    x.InstanceId = d.guidInstance.ToString();
                    x.DevType = d.dwDevType & 0xFF; x.DevSubType = (d.dwDevType >> 8) & 0xFF;
                    x.HasForceFeedback = hasFf;
                    x.Kind = Classify(x.DevType, x.DevSubType, hasFf, x.Product);
                    list.Add(x);
                }
            }
            catch (Exception ex) { if (log != null) log("Hardware: enumeration failed: " + ex.Message); }
            finally { if (di != null) { try { Marshal.ReleaseComObject(di); } catch { } } }
            return list;
        }
    }
}
