// Alteracoes minimas ao game.xml do FS25, preservando o resto do ficheiro tal como esta:
//   <joystick enable="true" vibration="false" .../>   (os comandos funcionam no jogo)
//   <forceFeedback enable="false"/>                   (o FFB do jogo nao disputa a base)
using System;
using System.Text.RegularExpressions;

namespace RealFFBSetup
{
    internal static class GameXml
    {
        private static readonly Regex Joystick = new Regex("<joystick\\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex Ffb = new Regex("<forceFeedback\\b[^>]*>", RegexOptions.IgnoreCase);

        public static string GetAttr(string tag, string name)
        {
            Match m = Regex.Match(tag, "\\b" + name + "\\s*=\\s*([\"'])([^\"']*)\\1", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[2].Value : null;
        }

        private static string SetAttr(string tag, string name, string value)
        {
            Regex r = new Regex("(\\b" + name + "\\s*=\\s*)([\"'])([^\"']*)\\2", RegexOptions.IgnoreCase);
            if (r.IsMatch(tag)) return r.Replace(tag, delegate(Match m) { return m.Groups[1].Value + m.Groups[2].Value + value + m.Groups[2].Value; }, 1);
            int end = tag.EndsWith("/>") ? tag.Length - 2 : tag.Length - 1;
            return tag.Substring(0, end).TrimEnd() + " " + name + "=\"" + value + "\"" + tag.Substring(end);
        }

        // Valores atuais: joystick#enable, joystick#vibration, forceFeedback#enable ("" se nao existir).
        public static void Read(string xml, out string joy, out string vib, out string ffb)
        {
            Match j = Joystick.Match(xml), f = Ffb.Match(xml);
            joy = j.Success ? (GetAttr(j.Value, "enable") ?? "") : "";
            vib = j.Success ? (GetAttr(j.Value, "vibration") ?? "") : "";
            ffb = f.Success ? (GetAttr(f.Value, "enable") ?? "") : "";
        }

        public static bool NeedsPatch(string xml)
        {
            string joy, vib, ffb;
            Read(xml, out joy, out vib, out ffb);
            return joy != "true" || vib == "true" || ffb != "false";
        }

        // Aplica os valores pedidos. ffb == null -> remove nada; "" em restauro -> remove o elemento que foi acrescentado.
        public static string Apply(string xml, string joy, string vib, string ffb)
        {
            Match j = Joystick.Match(xml);
            if (!j.Success) throw new InvalidOperationException("game.xml sem <joystick>: formato desconhecido, nada foi alterado");
            string tag = j.Value;
            if (joy != null && joy.Length > 0) tag = SetAttr(tag, "enable", joy);
            if (vib != null && vib.Length > 0) tag = SetAttr(tag, "vibration", vib);
            xml = xml.Substring(0, j.Index) + tag + xml.Substring(j.Index + j.Length);

            if (ffb != null)
            {
                Match f = Ffb.Match(xml);
                if (ffb.Length == 0)
                {
                    if (f.Success)   // remover o elemento acrescentado pelo instalador (e a linha dele)
                    {
                        int start = f.Index, end = f.Index + f.Length;
                        while (start > 0 && (xml[start - 1] == ' ' || xml[start - 1] == '\t')) start--;
                        if (end < xml.Length && xml[end] == '\r') end++;
                        if (end < xml.Length && xml[end] == '\n') end++;
                        xml = xml.Substring(0, start) + xml.Substring(end);
                    }
                }
                else if (f.Success)
                {
                    string ft = SetAttr(f.Value, "enable", ffb);
                    xml = xml.Substring(0, f.Index) + ft + xml.Substring(f.Index + f.Length);
                }
                else
                {
                    Match jj = Joystick.Match(xml);
                    int lineStart = xml.LastIndexOf('\n', jj.Index) + 1;
                    string indent = xml.Substring(lineStart, jj.Index - lineStart);
                    if (indent.Trim().Length > 0) indent = "    ";
                    string nl = xml.Contains("\r\n") ? "\r\n" : "\n";
                    int insertAt = jj.Index + jj.Length;
                    xml = xml.Substring(0, insertAt) + nl + indent + "<forceFeedback enable=\"" + ffb + "\"/>" + xml.Substring(insertAt);
                }
            }
            return xml;
        }
    }
}
