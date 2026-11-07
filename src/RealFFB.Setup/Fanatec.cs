// Deteccao dos volantes com force feedback suportados (Fanatec 0EB7, Logitech 046D) pelo WMI,
// agrupados por dispositivo fisico.
using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace RealFFBSetup
{
    internal sealed class FanatecBase
    {
        public int Vid = 0x0EB7;
        public int Pid;
        public string Model;
        public string Key;        // identifica o dispositivo fisico: PID + instancia (sem a coleccao HID)
        public List<string> InstanceIds = new List<string>();
        public string Label { get { return Model + " (" + Vid.ToString("X4") + ":" + Pid.ToString("X4") + ")"; } }
    }

    internal static class Fanatec
    {
        public const int Vid = 0x0EB7;

        public const int Logitech = 0x046D;

        // Volantes conhecidos. Outros dispositivos destes fabricantes (pedais USB, shifters) nunca sao escondidos.
        public static string ModelName(int vid, int pid)
        {
            if (vid == Vid)
                switch (pid)
                {
                    case 0x0020: return "Fanatec CSL DD / GT DD Pro / ClubSport DD";
                    case 0x0006: return "Fanatec Podium DD1";
                    case 0x0007: return "Fanatec Podium DD2";
                    case 0x0001: return "Fanatec ClubSport Wheel Base V2";
                    case 0x0004: return "Fanatec ClubSport Wheel Base V2.5";
                    case 0x0E03: return "Fanatec CSL Elite Wheel Base";
                    case 0x0005: return "Fanatec CSL Elite Wheel Base (PS4)";
                    case 0x0011: return "Fanatec CSR Elite";
                }
            if (vid == Logitech)
                switch (pid)
                {
                    case 0xC24F: return "Logitech G29";
                    case 0xC262: return "Logitech G920";
                    case 0xC266: case 0xC267: return "Logitech G923 (PlayStation/PC)";
                    case 0xC26D: case 0xC26E: return "Logitech G923 (Xbox/PC)";
                    case 0xC268: case 0xC272: return "Logitech G PRO Racing Wheel";
                    case 0xC29B: return "Logitech G27";
                    case 0xC299: return "Logitech G25";
                    case 0xC29A: return "Logitech Driving Force GT";
                }
            return null;
        }

        public static string ModelName(int pid) { return ModelName(Vid, pid); }

        public static bool IsWheelVendor(string path)
        {
            string u = (path ?? "").ToUpperInvariant();
            return u.Contains("VID_0EB7") || u.Contains("VID_046D");
        }

        // Chave do dispositivo fisico a partir de um instance id HID, ex.:
        // HID\VID_0EB7&PID_0020&MI_00&COL01\7&2A3C1B&0&0000  ->  0EB7:0020|7&2A3C1B&0
        public static string GroupKey(string instanceId, out int pid)
        {
            int vid;
            return GroupKey(instanceId, out vid, out pid);
        }

        public static string GroupKey(string instanceId, out int vid, out int pid)
        {
            pid = -1; vid = -1;
            Match m = Regex.Match(instanceId ?? "", "^HID\\\\VID_(0EB7|046D)&PID_([0-9A-F]{4})[^\\\\]*\\\\(.+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            vid = Convert.ToInt32(m.Groups[1].Value, 16);
            pid = Convert.ToInt32(m.Groups[2].Value, 16);
            string inst = m.Groups[3].Value;
            int amp = inst.LastIndexOf('&');
            if (amp > 0) inst = inst.Substring(0, amp);
            return vid.ToString("X4") + ":" + pid.ToString("X4") + "|" + inst.ToUpperInvariant();
        }

        // Le instance ids HID presentes. other recebe dispositivos Fanatec que nao sao bases.
        public static List<FanatecBase> Detect(List<string> other)
        {
            List<string> ids = new List<string>();
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'HID\\\\VID_0EB7%' OR DeviceID LIKE 'HID\\\\VID_046D%'"))
                foreach (ManagementObject o in s.Get())
                {
                    string id = o["DeviceID"] as string;
                    if (id != null) ids.Add(id);
                }
            return Group(ids, other);
        }

        public static List<FanatecBase> Group(List<string> ids, List<string> other)
        {
            Dictionary<string, FanatecBase> map = new Dictionary<string, FanatecBase>();
            List<FanatecBase> list = new List<FanatecBase>();
            foreach (string id in ids)
            {
                int vid, pid;
                string key = GroupKey(id, out vid, out pid);
                if (key == null) continue;
                string model = ModelName(vid, pid);
                string tag = vid.ToString("X4") + ":" + pid.ToString("X4");
                if (model == null) { if (vid == Vid && other != null && !other.Contains(tag)) other.Add(tag); continue; }
                FanatecBase b;
                if (!map.TryGetValue(key, out b))
                {
                    b = new FanatecBase(); b.Vid = vid; b.Pid = pid; b.Model = model; b.Key = key;
                    map[key] = b; list.Add(b);
                }
                b.InstanceIds.Add(id);
            }
            return list;
        }
    }
}
