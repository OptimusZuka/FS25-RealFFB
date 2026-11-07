// Testes automaticos (sem hardware): compilados com as fontes do RealFFB.Setup pelo build.bat / CI.
using System;
using System.Collections.Generic;
using RealFFBSetup;

namespace RealFFBSetupTests
{
    internal static class TestMain
    {
        private static int fails, runs;

        private static void Eq(string name, object expected, object actual)
        {
            runs++;
            if (!object.Equals(expected, actual)) { fails++; Console.WriteLine("FAILED " + name + "\n  expected: [" + expected + "]\n  got:      [" + actual + "]"); }
            else Console.WriteLine("ok     " + name);
        }

        private static void Throws(string name, Action a)
        {
            runs++;
            try { a(); fails++; Console.WriteLine("FAILED " + name + " (should throw)"); }
            catch (InvalidOperationException) { Console.WriteLine("ok     " + name); }
        }

        public static int Main()
        {
            // --- Steam
            string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"E:\\\\SteamLibrary\"\n\t\t\"apps\" { \"2300320\" \"123\" }\n\t}\n}";
            List<string> libs = GameLocator.ParseLibraryFolders(vdf);
            Eq("vdf: 2 bibliotecas", 2, libs.Count);
            Eq("vdf: disco E:", @"E:\SteamLibrary", libs.Count > 1 ? libs[1] : null);
            Eq("acf: installdir", "Farming Simulator 25", GameLocator.ParseInstallDir("\"AppState\"\n{\n\t\"appid\"\t\t\"2300320\"\n\t\"installdir\"\t\t\"Farming Simulator 25\"\n}"));

            // --- game.xml
            string nl = "\r\n";
            string gx = "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\" ?>" + nl + "<game>" + nl +
                        "    <development><controls>true</controls></development>" + nl +
                        "    <joystick enable=\"false\" vibration=\"true\"/>" + nl +
                        "    <graphic><display><width>2560</width></display></graphic>" + nl + "</game>" + nl;
            Eq("gx: precisa de ajuste", true, GameXml.NeedsPatch(gx));
            string p = GameXml.Apply(gx, "true", "false", "false");
            Eq("gx: ajustado", false, GameXml.NeedsPatch(p));
            Eq("gx: forceFeedback inserido com indentacao", true, p.Contains(nl + "    <forceFeedback enable=\"false\"/>" + nl));
            Eq("gx: resto preservado", true, p.Contains("<width>2560</width>") && p.Contains("<development><controls>true</controls></development>"));
            Eq("gx: idempotente", p, GameXml.Apply(p, "true", "false", "false"));
            string back = GameXml.Apply(p, "false", "true", "");
            Eq("gx: restauro devolve o original", gx, back);
            string withFfb = gx.Replace("vibration=\"true\"/>", "vibration=\"true\"/>" + nl + "    <forceFeedback enable=\"true\"/>");
            string p2 = GameXml.Apply(withFfb, "true", "false", "false");
            Eq("gx: forceFeedback existente alterado", true, p2.Contains("<forceFeedback enable=\"false\"/>") && !p2.Contains("enable=\"true\"/>" + nl + "    <forceFeedback"));
            Eq("gx: restauro com forceFeedback original", withFfb, GameXml.Apply(p2, "false", "true", "true"));
            Throws("gx: sem <joystick> nao altera", delegate { GameXml.Apply("<game></game>", "true", "false", "false"); });
            string lf = gx.Replace("\r\n", "\n");
            Eq("gx: ficheiro com LF", true, GameXml.Apply(lf, "true", "false", "false").Contains("\n    <forceFeedback enable=\"false\"/>\n"));

            // --- versoes
            Eq("ver: 1.7.346.0 >= 1.2.0", true, HidHide.CompareVersions("1.7.346.0", "1.2.0") >= 0);
            Eq("ver: 1.1.50 < 1.2.0", true, HidHide.CompareVersions("1.1.50", "1.2.0") < 0);
            Eq("ver: 1.10 > 1.9", true, HidHide.CompareVersions("1.10", "1.9") > 0);

            // --- HidHide CLI
            string list = "--app-reg \"C:\\Users\\x\\AppData\\Local\\Programs\\FS25 RealFFB\\RealFFB.exe\"\r\n--app-reg \"D:\\Tools\\Other.exe\"\r\n";
            Eq("cli: app-list", 2, HidHide.ParseList(list, "app-reg").Count);
            byte[] utf16 = System.Text.Encoding.Unicode.GetBytes(list);
            Eq("cli: saida UTF-16", list, HidHide.Decode(utf16));
            Eq("cli: saida UTF-8", list, HidHide.Decode(System.Text.Encoding.UTF8.GetBytes(list)));

            // --- Fanatec: agrupamento e exclusao de outros controladores
            List<string> ids = new List<string>();
            ids.Add(@"HID\VID_0EB7&PID_0020&COL01\7&2A3C1B&0&0000");
            ids.Add(@"HID\VID_0EB7&PID_0020&COL02\7&2A3C1B&0&0001");
            ids.Add(@"HID\VID_0EB7&PID_6204\7&99&0&0000");          // pedais USB: nao e base
            List<string> other = new List<string>();
            List<FanatecBase> b = Fanatec.Group(ids, other);
            Eq("fan: uma base", 1, b.Count);
            Eq("fan: duas coleccoes", 2, b.Count > 0 ? b[0].InstanceIds.Count : 0);
            Eq("fan: pedais fora", 1, other.Count);
            ids.Add(@"HID\VID_0EB7&PID_0020&COL01\7&FFFF&0&0000");
            Eq("fan: duas bases iguais distinguidas", 2, Fanatec.Group(ids, null).Count);

            // --- HidHide: so caminhos da base escolhida
            List<HidHide.GamingDevice> g = new List<HidHide.GamingDevice>();
            g.Add(Dev(@"HID\VID_0EB7&PID_0020&COL01\7&2A3C1B&0&0000", @"USB\VID_0EB7&PID_0020\SERIAL1"));
            g.Add(Dev(@"HID\VID_0EB7&PID_0020&COL02\7&2A3C1B&0&0001", @"USB\VID_0EB7&PID_0020\SERIAL1"));
            g.Add(Dev(@"HID\VID_044F&PID_0405\8&1&0&0000", @"USB\VID_044F&PID_0405\X"));
            g.Add(Dev(@"HID\VID_0EB7&PID_0020&COL01\7&FFFF&0&0000", @"USB\VID_0EB7&PID_0020\SERIAL2"));
            List<FanatecBase> one = Fanatec.Group(new List<string>(new string[] { @"HID\VID_0EB7&PID_0020&COL01\7&2A3C1B&0&0000" }), null);
            List<string> paths = Config.PathsFor(one[0], g);
            Eq("cfg: 2 coleccoes + 1 contentor", 3, paths.Count);
            bool foreign = false;
            foreach (string x in paths) if (x.Contains("044F") || x.Contains("FFFF") || x.Contains("SERIAL2")) foreign = true;
            Eq("cfg: nunca outros dispositivos", false, foreign);

            // --- HidHide --dev-gaming (JSON)
            string js = "[{\"friendlyName\":\"FANATEC Wheel\",\"devices\":[{\"present\":true,\"deviceInstancePath\":\"HID\\\\VID_0EB7\\u0026PID_0020\\u0026COL01\\\\7\\u00262A3C1B\\u00260\\u00260000\",\"baseContainerDeviceInstancePath\":\"USB\\\\VID_0EB7\\u0026PID_0020\\\\SERIAL1\",\"product\":\"FANATEC Wheel\"}]}]";
            List<HidHide.GamingDevice> pj = HidHide.ParseGaming(js);
            Eq("json: 1 dispositivo", 1, pj.Count);
            Eq("json: caminho com escapes", @"HID\VID_0EB7&PID_0020&COL01\7&2A3C1B&0&0000", pj.Count > 0 ? pj[0].InstancePath : null);
            Eq("json: contentor", @"USB\VID_0EB7&PID_0020\SERIAL1", pj.Count > 0 ? pj[0].ContainerPath : null);
            Eq("json: nome do grupo", "FANATEC Wheel", pj.Count > 0 ? pj[0].FriendlyName : null);


            // formato real do HidHideCLI 1.7 (GUIDs entre chavetas dentro de strings, 3 coleccoes)
            string real = " [ { \"friendlyName\" : \"Fanatec Wheel\" , \"devices\" : [\n" +
                Obj("HID\\\\VID_0EB7&PID_0020&Col01\\\\7&1a020f5&0&0000", "USB\\\\VID_0EB7&PID_0020\\\\6&2349&0&2") + " ,\n" +
                Obj("HID\\\\VID_0EB7&PID_0020&Col02\\\\7&1a020f5&0&0001", "USB\\\\VID_0EB7&PID_0020\\\\6&2349&0&2") + " ,\n" +
                Obj("HID\\\\VID_0EB7&PID_0020&Col03\\\\7&1a020f5&0&0002", "USB\\\\VID_0EB7&PID_0020\\\\6&2349&0&2") + " ] } ,\n" +
                "{ \"friendlyName\" : \"Thrustmaster T.A320 Pilot\" , \"devices\" : [\n" +
                Obj("HID\\\\VID_044F&PID_0405\\\\9&e7c901f&0&0000", "USB\\\\VID_044F&PID_0405\\\\1") + " ] } ] ";
            List<HidHide.GamingDevice> rg = HidHide.ParseGaming(real);
            Eq("json real: 4 dispositivos", 4, rg.Count);
            List<FanatecBase> wmi = Fanatec.Group(new List<string>(new string[] {
                @"HID\VID_0EB7&PID_0020&COL03\7&1A020F5&0&0002", @"HID\VID_0EB7&PID_0020&COL02\7&1A020F5&0&0001", @"HID\VID_0EB7&PID_0020&COL01\7&1A020F5&0&0000" }), null);
            Eq("json real: WMI 1 base", 1, wmi.Count);
            List<string> rp = wmi.Count == 1 ? Config.PathsFor(wmi[0], rg) : new List<string>();
            Eq("json real: 3 coleccoes + contentor", 4, rp.Count);
            Eq("json real: Thrustmaster fora", false, string.Join("|", rp.ToArray()).Contains("044F"));

            // --- Logitech G29 (e um rato Logitech, que nunca pode contar)
            List<string> lg = new List<string>(new string[] { @"HID\VID_046D&PID_C24F\7&3B2A1&0&0000", @"HID\VID_046D&PID_C08B&MI_00\8&11&0&0000" });
            List<string> lgOther = new List<string>();
            List<FanatecBase> lgb = Fanatec.Group(lg, lgOther);
            Eq("g29: um volante", 1, lgb.Count);
            Eq("g29: modelo", "Logitech G29", lgb.Count > 0 ? lgb[0].Model : null);
            Eq("g29: rato ignorado", 0, lgOther.Count);
            List<HidHide.GamingDevice> lgg = new List<HidHide.GamingDevice>();
            lgg.Add(Dev(@"HID\VID_046D&PID_C24F\7&3B2A1&0&0000", @"USB\VID_046D&PID_C24F\5&99&0&4"));
            lgg.Add(Dev(@"HID\VID_044F&PID_0405\9&e7c901f&0&0000", @"USB\VID_044F&PID_0405\1"));
            Eq("g29: volante + contentor", 2, lgb.Count > 0 ? Config.PathsFor(lgb[0], lgg).Count : 0);

            // --- anonimizacao
            string user = Environment.UserName;
            Eq("anon: sem nome de utilizador", false, Diagnostics.Anon(@"C:\Users\" + user + @"\x").Contains(user) && user.Length > 2);

            Console.WriteLine();
            Console.WriteLine(runs + " tests, " + fails + " failures");
            return fails == 0 ? 0 : 1;
        }

        private static string Obj(string inst, string cont)
        {
            return "{ \"present\" : true ,\n\"symbolicLink\" : \"\\\\\\\\?\\\\hid#x#{4d1e55b2-f16f-11cf-88cb-001111000030}\" ,\n\"vendor\" : \"V\" ,\n\"product\" : \"P\" ,\n" +
                "\"deviceInstancePath\" : \"" + inst + "\" ,\n\"baseContainerDeviceInstancePath\" : \"" + cont + "\" ,\n\"baseContainerClassGuid\" : \"{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}\" ,\n\"baseContainerDeviceCount\" : 3 }";
        }

        private static HidHide.GamingDevice Dev(string inst, string cont)
        {
            HidHide.GamingDevice d = new HidHide.GamingDevice();
            d.InstancePath = inst; d.ContainerPath = cont;
            return d;
        }
    }
}
