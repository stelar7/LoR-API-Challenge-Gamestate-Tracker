using LoRTracker.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LoRTracker
{
    public partial class Form1:Form
    {
        internal static string Token;

        // login once per hour to keep the token alive
        internal static System.Threading.Timer _LoginTimer;
        internal static int _LoginInterval = 1000 * 60 * 60;

        internal static System.Threading.Timer _GameTimer;
        internal static int _InGameInterval = 250;
        internal static int _NotIngameInterval = 1000;

        internal static string PortString = "21337";

        public Form1()
        {
            InitializeComponent();
            InitializeContext();

            LoginWithDefaultIfPresent();
            HideWindow();
            Resize += Form_Resize_Event;
            FormClosing += Form_Closing_Event;
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        static bool CheckLibrary(string fileName)
        {
            return LoadLibrary(fileName) == IntPtr.Zero;
        }

        private void Form_Closing_Event(object sender, FormClosingEventArgs e)
        {
            if(e.CloseReason == CloseReason.UserClosing)
            {
                HideWindow();
                e.Cancel = true;
            }

        }

        private void Form_Resize_Event(object sender, EventArgs e)
        {
            if(WindowState == FormWindowState.Minimized)
            {
                HideWindow();
            }
            else
            {
                ShowWindow();
            }
        }

        private void InitializeContext()
        {
            // run on startup
            var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            key.SetValue("Replayterra", Application.ExecutablePath);
            key.Close();

            Application.ApplicationExit += (s, e) => GameAPI.CancelGame().Wait();

            Icon = Icon.FromHandle(Resources.AppIcon.GetHicon());

            components = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(components)
            {
                Visible = true,
                Icon = Icon.FromHandle(Resources.AppIcon.GetHicon()),
                ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Open", Open),
                    new MenuItem("Exit", Exit)
                })
            };

            var selfKey = Registry.CurrentUser.CreateSubKey("LoRTracker");
            PortString = selfKey.GetValue("port", "21337") as string;
            PortTextBox.Text = PortString;
            selfKey.Close();
        }


        private void HideWindow()
        {
            Opacity = 0;
            Visible = false;
            ShowInTaskbar = false;
        }

        private void ShowWindow()
        {
            Opacity = 100;
            Visible = true;
            ShowInTaskbar = true;
        }

        private void Open(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void OpenMouse(object sender, MouseEventArgs e)
        {
            ShowWindow();
        }

        private void Exit(object sender, EventArgs e)
        {
            GameAPI.CancelGame().Wait();
            Application.Exit();
        }

        private void LoginWithDefaultIfPresent()
        {
            var key = Registry.CurrentUser.OpenSubKey("LoRTracker");
            if(key.GetValue("username") != null)
            {
                TryLoginUI((string) key.GetValue("username", null), (string) key.GetValue("password", null));
            }
        }

        private async void TryLoginUI(string username, string password)
        {
            bool status = await TryLogin(username, password);
            if(!status)
            {
                ConnectionLabel.Text = "Invalid login";
            }
            else
            {
                var key = Registry.CurrentUser.CreateSubKey("LoRTracker");
                key.SetValue("username", username);
                key.SetValue("password", password);
                key.Close();

                UsernameBox.Text = username;
                PasswordBox.Text = password;

                ConnectionLabel.Text = "Connected to replay server!";
                HideWindow();
            }
        }

        private async Task<bool> TryLogin(string username, string password)
        {
            HttpClient client = new HttpClient();
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("fromApp", "true")
            });

            var result = await client.PostAsync(new Uri(Resources.LoginUrl), formContent);
            if(result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                Token = await result.Content.ReadAsStringAsync();
                Debug.WriteLine("Did login! Got token = " + Token);
                StartLoginRefreshTask();
                UpdatePollingRates();
                StartGamePollTask();
                return true;
            }

            return false;
        }

        private void Button_Connect_Click(object sender, EventArgs e)
        {
            TryLoginUI(UsernameBox.Text, PasswordBox.Text);
        }

        private void StartLoginRefreshTask()
        {
            if(_LoginTimer == null)
            {
                _LoginTimer = new System.Threading.Timer(LoginTick, null, 0, _LoginInterval);
            }
        }

        private void LoginTick(object state)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("LoRTracker");
                if(key.GetValue("username") != null)
                {
                    TryLogin((string) key.GetValue("username", null), (string) key.GetValue("password", null)).Wait();
                    UpdatePollingRates();
                }
            }
            finally
            {
                _LoginTimer?.Change(_LoginInterval, Timeout.Infinite);
            }
        }

        private async void UpdatePollingRates()
        {
            HttpClient client = new HttpClient();
            var ShortP = await client.GetStringAsync(new Uri(Resources.ShortPollUrl));
            var LongP = await client.GetStringAsync(new Uri(Resources.LongPollUrl));

            _InGameInterval = int.Parse(ShortP);
            _NotIngameInterval = int.Parse(LongP);

            Debug.WriteLine("Got long poll = " + _NotIngameInterval);
            Debug.WriteLine("Got short poll = " + _InGameInterval);
        }

        private void StartGamePollTask()
        {
            if(_GameTimer == null)
            {
                _GameTimer = new System.Threading.Timer(GameTick, null, 0, _NotIngameInterval);
            }
        }


        private async void GameTick(object TimerState)
        {
            await GameAPI.UpdateState();

            if(GameAPI.EnteredGame())
            {
                _GameTimer?.Change(_InGameInterval, _InGameInterval);
            }

            if(GameAPI.IsInGame())
            {
                GameAPI.ActiveGameTime += _InGameInterval;
            }

            if(GameAPI.QuitGame())
            {
                _GameTimer?.Change(_NotIngameInterval, _NotIngameInterval);
            }

            if(GameAPI.IsOffline())
            {
                GameConnectionLabel.Invoke((MethodInvoker) delegate
                {
                    GameConnectionLabel.Text = "Unable to connect. Is the game running?";
                });
            }
            else
            {
                if(GameConnectionLabel.Text != "Connected!")
                {
                    GameConnectionLabel.Invoke((MethodInvoker) delegate
                    {
                        GameConnectionLabel.Text = "Connected!";
                    });
                }
            }
        }

        private void PortValueSubmit(object sender, EventArgs e)
        {
            PortString = PortTextBox.Text;
            GameConnectionLabel.Text = "Port saved.";

            var key = Registry.CurrentUser.CreateSubKey("LoRTracker");
            key.SetValue("port", PortString);
            key.Close();
        }

    }
}
