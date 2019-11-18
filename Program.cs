using LoRTracker.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LoRTracker
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string ProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            Process[] RunningProcesses = Process.GetProcessesByName(ProcessName);
            if(RunningProcesses.Length == 1)
            {
                HttpClient client = new HttpClient();
                var versionTask = client.GetStringAsync(new Uri(Resources.UpdateVersionUrl));
                versionTask.Wait();
                var version = int.Parse(versionTask.Result);

                if(version > int.Parse(Resources.CurrentVersion))
                {
                    MessageBox.Show("A newer version is avaliable, please download it to use this application");
                    Environment.Exit(1);
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Form1 form = new Form1();
                Application.Run(form);
            }
            else
            {
                MessageBox.Show("Application already running.");
                ShowWindowAsync(RunningProcesses[0].MainWindowHandle, 2);
                ShowWindowAsync(RunningProcesses[0].MainWindowHandle, 9);
            }
        }

        [DllImport("user32.dll")]
        private static extern int ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    }
}
