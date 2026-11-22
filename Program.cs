using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

namespace Updater
{
    internal static class Program
    {
        private const string Base = "https://expressts.co.in/api/files/";

        [STAThread]
        private static void Main()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.Combine(dir, "Express.exe");
            string tmp = Path.Combine(dir, "Express.new");

            try
            {
                for (int i = 0; i < 60; i++)
                {
                    if (Process.GetProcessesByName("Express").Length == 0) break;
                    Thread.Sleep(500);
                }
                foreach (Process p in Process.GetProcessesByName("Express"))
                {
                    try { p.Kill(); } catch { }
                }
                Thread.Sleep(1000);

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "ExpressUpdater");
                    wc.DownloadFile(Base + "Express.exe", tmp);
                }

                if (!File.Exists(tmp) || new FileInfo(tmp).Length < 100000) return;

                if (File.Exists(exe)) File.Copy(exe, exe + ".old", true);
                File.Copy(tmp, exe, true);
                File.Delete(tmp);

                Process.Start(exe);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
            finally { Environment.Exit(0); }
        }
    }
}