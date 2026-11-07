// Deteccao das bases Fanatec ligadas (VID 0EB7) pelo WMI, agrupadas por dispositivo fisico.
using System;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace RealFFBSetup
{
    internal sealed class FanatecBase
    {
        public int Pid;
        public string Model;
        public string Key;        // identifica o dispositivo fisico: PID + instancia (sem a coleccao HID)
        public List<string> InstanceIds = new List<string>();
        public string Label { get { return Model + " (PID " + Pid.ToString("X4") + ")"; } }
    }

    internal static class Fanatec
    {
        public const int Vid = 0x0EB7;

        // Bases em modo PC conhecidas. Outros PIDs Fanatec (pedais/volantes USB) nunca sao escondidos.
        public static string ModelName(int pid)
        {
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
                default: return null;
            }
        }

        // Chave do dispositivo fisico a partir de um instance id HID, ex.:
        // HID\VID_0EB7&PID_0020&MI_00&COL01\7&2A3C1B&0&0000  ->  0020|7&2A3C1B&0
        public static string GroupKey(string instanceId, out int pid)
        {
            pid = -1;
            Match m = Regex.Match(instanceId ?? "", "VID_0EB7&PID_([0-9A-F]{4})[^\\\\]*\\\\(.+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            pid = Convert.ToInt32(m.Groups[1].Value, 16);
            string inst = m.Groups[2].Value;
            int amp = inst.LastIndexOf('&');
            if (amp > 0) inst = inst.Substring(0, amp);
            return pid.ToString("X4") + "|" + inst.ToUpperInvariant();
        }

        // Le instance ids HID presentes. other recebe dispositivos Fanatec que nao sao bases.
        public static List<FanatecBase> Detect(List<string> other)
        {
            List<string> ids = new List<string>();
            using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE 'HID\\\\VID_0EB7%'"))
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
                int pid;
                string key = GroupKey(id, out pid);
                if (key == null) continue;
                string model = ModelName(pid);
                if (model == null) { if (other != null && !other.Contains("PID " + pid.ToString("X4"))) other.Add("PID " + pid.ToString("X4")); continue; }
                FanatecBase b;
                if (!map.TryGetValue(key, out b))
                {
                    b = new FanatecBase(); b.Pid = pid; b.Model = model; b.Key = key;
                    map[key] = b; list.Add(b);
                }
                b.InstanceIds.Add(id);
            }
            return list;
        }
    }
}
