// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
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

            // --- 0.14: deteccao universal (agrupamento por dispositivo fisico, qualquer fabricante)
            List<HidHide.GamingDevice> fan = new List<HidHide.GamingDevice>();
            fan.Add(Dev(@"HID\VID_0EB7&PID_0020&COL01\7&2A3C1B&0&0000", @"USB\VID_0EB7&PID_0020\SERIAL1", 2));
            fan.Add(Dev(@"HID\VID_0EB7&PID_0020&COL02\7&2A3C1B&0&0001", @"USB\VID_0EB7&PID_0020\SERIAL1", 2));
            fan.Add(Dev(@"HID\VID_0EB7&PID_6204\7&99&0&0000", @"USB\VID_0EB7&PID_6204\P", 1));          // pedais USB
            fan.Add(Dev(@"HID\VID_044F&PID_0405\8&1&0&0000", @"USB\VID_044F&PID_0405\X", 1));           // joystick sem FFB
            List<WheelDevice> oth = new List<WheelDevice>();
            List<WheelDevice> fw = Wheels.Find(fan, null, null, oth);
            Eq("fan: uma base (conhecida, sem DirectInput)", 1, fw.Count);
            Eq("fan: duas coleccoes", 2, fw.Count > 0 ? fw[0].InstanceIds.Count : 0);
            Eq("fan: pedais e joystick ficam de fora", 2, oth.Count);
            List<string> fp = fw.Count > 0 ? Wheels.PathsFor(fw[0], fan, null) : new List<string>();
            Eq("fan: 2 coleccoes + contentor (so do volante)", 3, fp.Count);
            Eq("fan: nunca outros dispositivos", false, string.Join("|", fp.ToArray()).Contains("6204") || string.Join("|", fp.ToArray()).Contains("044F"));
            fan.Add(Dev(@"HID\VID_0EB7&PID_0020&COL01\7&FFFF&0&0000", @"USB\VID_0EB7&PID_0020\SERIAL2", 1));
            Eq("fan: duas bases iguais distinguidas", 2, Wheels.Find(fan, null, null, null).Count);

            // MOZA R5 (o caso do testador): base + stalk + Farming Control System, todos 346E
            List<HidHide.GamingDevice> moza = new List<HidHide.GamingDevice>();
            moza.Add(Dev(@"HID\VID_346E&PID_0004&MI_00\8&2B1&0&0000", @"USB\VID_346E&PID_0004\MOZA1", 1));
            moza.Add(Dev(@"HID\VID_346E&PID_0024\8&3C2&0&0000", @"USB\VID_346E&PID_0024\STALK", 1));
            moza.Add(Dev(@"HID\VID_346E&PID_0030\8&4D3&0&0000", @"USB\VID_346E&PID_0030\FCS", 1));
            moza.Add(Dev(@"HID\VID_1532&PID_022B&MI_02\9&5E&0&0000", @"USB\VID_1532&PID_022B\RZ", 4));  // Razer Tartarus
            List<FfbInfo> di = Wheels.ParseList("346E:0004|1|MOZA R5 Base\r\n346E:0024|0|MOZA Multi-function Stalk\r\n346E:0030|0|MOZA Farming Control System\r\n1532:022B|0|Razer Tartarus V2\r\n");
            Eq("di: 4 controladores lidos", 4, di.Count);
            List<WheelDevice> mo = new List<WheelDevice>();
            List<WheelDevice> mw = Wheels.Find(moza, di, null, mo);
            Eq("moza: so a base (FFB)", 1, mw.Count);
            Eq("moza: nome do DirectInput", "MOZA R5 Base (346E:0004)", mw.Count > 0 ? mw[0].Label : null);
            Eq("moza: stalk, FCS e Razer ficam de fora", 3, mo.Count);
            List<string> mp = mw.Count > 0 ? Wheels.PathsFor(mw[0], moza, null) : new List<string>();
            Eq("moza: HID + contentor da base", 2, mp.Count);
            Eq("moza: nada do stalk/FCS/Razer", false, string.Join("|", mp.ToArray()).Contains("0024") || string.Join("|", mp.ToArray()).Contains("0030") || string.Join("|", mp.ToArray()).Contains("1532"));

            // dispositivo composto: contentor USB partilhado com outras funcoes -> so o HID e escondido
            List<HidHide.GamingDevice> comp = new List<HidHide.GamingDevice>();
            comp.Add(Dev(@"HID\VID_044F&PID_B696&MI_00\8&AA&0&0000", @"USB\VID_044F&PID_B696\TM1", 3));
            List<FfbInfo> tm = Wheels.ParseList("044F:B696|1|Thrustmaster T248\n");
            List<WheelDevice> tw = Wheels.Find(comp, tm, null, null);
            Eq("tm: base Thrustmaster pelo FFB", 1, tw.Count);
            Eq("tm: contentor partilhado nao e escondido", 1, tw.Count > 0 ? Wheels.PathsFor(tw[0], comp, null).Count : 0);

            // XInput: o no xusb tambem e escondido
            HidHide.GamingDevice xd = Dev(@"HID\VID_046D&PID_C262&IG_00\8&BB&0&0000", @"USB\VID_046D&PID_C262\G920", 1);
            xd.XusbPath = @"USB\VID_046D&PID_C262&IG_00\9&CC";
            List<HidHide.GamingDevice> xl = new List<HidHide.GamingDevice>(new HidHide.GamingDevice[] { xd });
            List<WheelDevice> xw = Wheels.Find(xl, null, null, null);
            Eq("g920: conhecido", 1, xw.Count);
            Eq("g920: HID + xusb + contentor", 3, xw.Count > 0 ? Wheels.PathsFor(xw[0], xl, null).Count : 0);

            // pedais ativos com FFB nunca contam como base; base anterior reconhecida para reparar
            List<HidHide.GamingDevice> pd = new List<HidHide.GamingDevice>();
            pd.Add(Dev(@"HID\VID_16D0&PID_1111\8&D&0&0000", @"USB\VID_16D0&PID_1111\P", 1));
            List<FfbInfo> pdi = Wheels.ParseList("16D0:1111|1|Active Pedal\n");
            Eq("pedal FFB: nao e base", 0, Wheels.Find(pd, pdi, null, null).Count);
            Eq("reparacao: base anterior", 1, Wheels.Find(pd, null, "16D0:1111|8&D&0", null).Count);
            Eq("rato Logitech nunca conta", 0, Wheels.Find(new List<HidHide.GamingDevice>(new HidHide.GamingDevice[] { Dev(@"HID\VID_046D&PID_C08B&MI_00\8&11&0&0000", "", 0) }), null, null, null).Count);
            Eq("bases DI: MOZA", 1, Wheels.FfbBases(di).Count);

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
            Eq("json real: contagem do contentor", 3, rg.Count > 0 ? rg[0].ContainerCount : -1);
            List<WheelDevice> rw = Wheels.Find(rg, null, null, null);
            Eq("json real: 1 base", 1, rw.Count);
            List<string> rp = rw.Count == 1 ? Wheels.PathsFor(rw[0], rg, null) : new List<string>();
            Eq("json real: 3 coleccoes + contentor", 4, rp.Count);
            Eq("json real: Thrustmaster fora", false, string.Join("|", rp.ToArray()).Contains("044F"));

            // --- desinstalacao oficial do HidHide
            HidHide.UninstallEntry ue = new HidHide.UninstallEntry();
            ue.UninstallString = "MsiExec.exe /X{8822CC70-E2A5-4CB7-8F14-E27101150A1D}";
            string[] uc = HidHide.UninstallCommand(ue);
            Eq("uninst: msi", "msiexec.exe|/x {8822CC70-E2A5-4CB7-8F14-E27101150A1D} /passive /norestart", uc == null ? null : uc[0] + "|" + uc[1]);
            ue.UninstallString = "\"C:\\ProgramData\\Package Cache\\{abc}\\HidHide_1.5.230_x64.exe\"  /uninstall";
            uc = HidHide.UninstallCommand(ue);
            Eq("uninst: bundle", "C:\\ProgramData\\Package Cache\\{abc}\\HidHide_1.5.230_x64.exe|/uninstall /passive /norestart", uc == null ? null : uc[0] + "|" + uc[1]);
            ue.UninstallString = "";
            Eq("uninst: sem entrada", true, HidHide.UninstallCommand(ue) == null);

            // --- HidHide /releases/latest (formato real do GitHub: "uploader" aninhado, digest)
            string up = "\"uploader\":{\"login\":\"nefarius\",\"id\":1},";
            string rel = "{\"tag_name\":\"v1.7.346.0\",\"prerelease\":false,\"assets\":[" +
                "{\"name\":\"HidHide_1.7.346_arm64.exe\"," + up + "\"size\":100,\"digest\":null,\"browser_download_url\":\"https://github.com/nefarius/HidHide/releases/download/v1.7.346.0/HidHide_1.7.346_arm64.exe\"}," +
                "{\"name\":\"HidHide_1.7.346_x64.exe\"," + up + "\"size\":5000,\"digest\":\"sha256:ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789\",\"browser_download_url\":\"https://github.com/nefarius/HidHide/releases/download/v1.7.346.0/HidHide_1.7.346_x64.exe\"}," +
                "{\"name\":\"HidHide_1.7.346_x64.exe\"," + up + "\"size\":1,\"browser_download_url\":\"https://evil.example.com/HidHide_1.7.346_x64.exe\"}]}";
            HidHide.Asset ha = HidHide.SelectAsset(rel);
            Eq("hidhide: versao", "1.7.346.0", ha.Version);
            Eq("hidhide: escolhe x64 oficial", "https://github.com/nefarius/HidHide/releases/download/v1.7.346.0/HidHide_1.7.346_x64.exe", ha.Url);
            Eq("hidhide: tamanho", 5000L, ha.Size);
            Eq("hidhide: digest", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", ha.Sha256);
            string msiRel = "{\"tag_name\":\"v2.0.0\",\"assets\":[{\"name\":\"Nefarius_HidHide_Drivers_x64_arm64_v2.0.0.msi\"," + up + "\"size\":9,\"browser_download_url\":\"https://github.com/nefarius/HidHide/releases/download/v2.0.0/Nefarius_HidHide_Drivers_x64_arm64_v2.0.0.msi\"}]}";
            Eq("hidhide: aceita msi x64+arm64", true, HidHide.SelectAsset(msiRel).IsMsi);
            Throws2("hidhide: sem instalador", delegate { HidHide.SelectAsset("{\"tag_name\":\"v1\",\"assets\":[]}"); });
            Throws2("hidhide: resposta invalida", delegate { HidHide.SelectAsset("<html>rate limited</html>"); });

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

        private static void Throws2(string name, Action a)
        {
            runs++;
            try { a(); fails++; Console.WriteLine("FAILED " + name + " (should throw)"); }
            catch (HidHide.DownloadException) { Console.WriteLine("ok     " + name); }
        }

        private static HidHide.GamingDevice Dev(string inst, string cont) { return Dev(inst, cont, 0); }

        private static HidHide.GamingDevice Dev(string inst, string cont, int count)
        {
            HidHide.GamingDevice d = new HidHide.GamingDevice();
            d.InstancePath = inst; d.ContainerPath = cont; d.ContainerCount = count;
            return d;
        }
    }
}
