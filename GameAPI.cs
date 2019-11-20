using LoRTracker.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LoRTracker
{
    partial class GameAPI
    {
        static readonly string UrlBase = "http://localhost:"+Form1.PortString + "/";
        static readonly string RectsUrl = UrlBase + "positional-rectangles";
        static readonly string ResultUrl = UrlBase + "game-result";
        static readonly string DeckListUrl = UrlBase + "static-decklist";
        static readonly string expeditionsUrl = UrlBase + "expeditions-state";
        static readonly HttpClient Client = new HttpClient();

        internal static PlayerState CurrentState = PlayerState.OFFLINE;
        internal static PlayerState LastState = PlayerState.OFFLINE;

        internal static string ActiveGameToken;
        internal static string LastActiveDeck;
        internal static string LastGameScore;
        internal static string LastGameID;
        internal static int ActiveGameTime;


        internal static string LastBoardState;
        internal static string LastStateString;

        internal static string LogFolderPath = Path.GetTempPath() + "/../Riot Games/Legends of Runeterra/Logs";
        internal static string LatestLogFilePath;

        internal static long LastLogPosition = 0;

        public static async Task<ActiveDeckResponse> GetActiveDeckList()
        {
            var response = await Client.GetStringAsync(new Uri(DeckListUrl));
            var data = JsonConvert.DeserializeObject<ActiveDeckResponse>(response);
            return data;
        }

        public static async Task<GameRectResponse> GetPositionalRects()
        {
            var response = await Client.GetStringAsync(new Uri(RectsUrl));
            var data = JsonConvert.DeserializeObject<GameRectResponse>(response);
            return data;
        }

        public static async Task<GameResultResponse> GetGameResult()
        {
            var response = await Client.GetStringAsync(new Uri(ResultUrl));
            var data = JsonConvert.DeserializeObject<GameResultResponse>(response);
            return data;
        }

        public static async Task<ExpeditionResponse> GetExpedition()
        {
            var response = await Client.GetStringAsync(new Uri(expeditionsUrl));
            var data = JsonConvert.DeserializeObject<ExpeditionResponse>(response);
            return data;
        }


        public static bool EnteredGame()
        {
            return LastState == PlayerState.MENU && CurrentState == PlayerState.GAME;
        }

        public static bool QuitGame()
        {
            return LastState == PlayerState.GAME && CurrentState == PlayerState.MENU;
        }

        public static bool IsInGame()
        {
            return CurrentState == PlayerState.GAME;
        }

        public static bool IsOffline()
        {
            return CurrentState == PlayerState.OFFLINE;
        }

        public static async Task UpdateState()
        {
            try
            {
                LastState = CurrentState;

                string NextState = (await GetPositionalRects()).GameState;
                switch(NextState)
                {
                    case "Menus":
                        CurrentState = PlayerState.MENU;
                        break;

                    case "InProgress":
                        CurrentState = PlayerState.GAME;
                        break;

                    default:
                        CurrentState = PlayerState.MENU;
                        break;
                }

                if(NextState != LastStateString)
                {
                    LastStateString = NextState;
                    Debug.WriteLine(NextState);
                }
            }
#pragma warning disable CS0168 
            catch(HttpRequestException e)
            {
                CurrentState = PlayerState.OFFLINE;
            }

            if(IsOffline())
            {
                if(ActiveGameToken != null || ActiveGameToken?.Length < 5)
                {
                    await CancelGame();
                }

                if(LatestLogFilePath != null || LatestLogFilePath?.Length > 5)
                {
                    LatestLogFilePath = null;
                }

                return;

            }
            else
            {
                if(LatestLogFilePath == null || LatestLogFilePath?.Length < 5)
                {
                    LatestLogFilePath = FindLatestLogFile();
                }
            }

            try
            {
                if(LastGameID == null)
                {
                    LastGameID = (await GetGameResult()).GameID;
                }

                if(EnteredGame())
                {
                    await PushGameStart();
                }

                if(IsInGame())
                {
                    if(LastActiveDeck == null || LastActiveDeck.Length < 10)
                    {
                        LastActiveDeck = (await GetActiveDeckList()).DeckCode;
                    }

                    await PushUpdates();
                }

                if(QuitGame())
                {
                    LastGameScore = (await GetGameResult()).LocalPlayerWon;
                    LastGameID = (await GetGameResult()).GameID;
                    await PushGameEnd();
                }
            }
            catch(HttpRequestException e)
            {
                CurrentState = PlayerState.OFFLINE;
            }
        }

        private static string FindLatestLogFile()
        {
            string[] files = Directory.GetFiles(LogFolderPath);
            Array.Sort(files, StrCmpLogicalW);
            Array.Reverse(files);
            return files[0];
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int StrCmpLogicalW(string psz1, string psz2);

        internal static Point GetMouseLocation()
        {
            GetCursorPos(out POINT mousePosition);
            return new Point(mousePosition.X, (Screen.PrimaryScreen.Bounds.Height - mousePosition.Y));
        }

        static string LastDebugMessage;

        internal static async Task PushGameStart()
        {
            if(ActiveGameToken != null && ActiveGameToken.Length > 5)
            {
                return;
            }

            var data = await GetPositionalRects();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("self", data.PlayerName),
                new KeyValuePair<string, string>("other", data.OpponentName),
                new KeyValuePair<string, string>("interval", Form1._InGameInterval.ToString()),
            });

            var response = await Client.PostAsync(new Uri(Resources.GameStartUrl), formContent);
            ActiveGameToken = await response.Content.ReadAsStringAsync();
            LastActiveDeck = null;
            ActiveGameTime = 0;
            LastLogPosition = 0;

            LastDebugMessage = "Pushed game start event with token " + ActiveGameToken;
            Debug.WriteLine("Pushed game start event with token " + ActiveGameToken);
        }

        internal static async Task PushUpdates()
        {
            if(ActiveGameToken == null || ActiveGameToken.Length < 5)
            {
                return;
            }

            var CurrentBoardState = await GetPositionalRects();

            CurrentBoardState.Log = await GetLogChanges();
            CurrentBoardState.Mouse = GetMouseState();

            string OutputState = JsonConvert.SerializeObject(CurrentBoardState);
            if(LastBoardState == OutputState)
            {
                return;
            }

            LastBoardState = OutputState;
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                new KeyValuePair<string, string>("time", ActiveGameTime.ToString()),
                new KeyValuePair<string, string>("state", LastBoardState),
            });

            var response = await Client.PostAsync(new Uri(Resources.GameUpdateUrl), formContent);
            var content = await response.Content.ReadAsStringAsync();

            if(content.Length > 1)
            {
                Debug.WriteLine(content);
            }

            if(LastDebugMessage != "Pushed update event")
            {
                LastDebugMessage = "Pushed update event";
                Debug.WriteLine("Pushed update event");
            }
        }

        private static MouseState GetMouseState()
        {
            var location = GetMouseLocation();
            ushort state = GetAsyncKeyState(0x01);
            bool pressed = false;

            if(state > 0)
            {
                pressed = true;
            }

            return new MouseState
            {
                Pressed = pressed,
                X = location.X,
                Y = location.Y
            };
        }

        [DllImport("user32.dll")]
        public static extern ushort GetAsyncKeyState(ushort virtualKeyCode);

        private static async Task<List<string>> GetLogChanges()
        {
            FileStream fs = File.Open(LatestLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            fs.Position = LastLogPosition;
            long remaining = fs.Length - fs.Position;
            byte[] data = new byte[remaining];
            await fs.ReadAsync(data, 0, data.Length);
            LastLogPosition = fs.Position;

            string encoded = Encoding.UTF8.GetString(data, 0, data.Length);
            var lines = encoded.Split('\n');
            List<string> results = new List<string>();

            foreach(var line in lines)
            {
                if(line.Contains("GameAction"))
                {
                    results.Add(line);
                }
            }

            return results;

        }

        internal static async Task PushGameEnd()
        {
            if(ActiveGameToken == null || ActiveGameToken.Length < 5)
            {
                return;
            }

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                new KeyValuePair<string, string>("deckCode", LastActiveDeck),
                new KeyValuePair<string, string>("win", LastGameScore),
                new KeyValuePair<string, string>("lastFrame", ActiveGameTime.ToString()),
                new KeyValuePair<string, string>("interval", Form1._InGameInterval.ToString()),
            });

            var response = await Client.PostAsync(new Uri(Resources.GameEndUrl), formContent);
            var content = await response.Content.ReadAsStringAsync();

            Debug.WriteLine("Ending game for token " + ActiveGameToken);

            LastDebugMessage = "Pushed game end event";
            Debug.WriteLine("Pushed game end event");
            Debug.WriteLine(content);

            ActiveGameToken = null;
            LastActiveDeck = null;
            ActiveGameTime = 0;
            LastLogPosition = 0;
        }

        internal static async Task CancelGame()
        {
            var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", Form1.Token),
                    new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                });

            var response = await Client.PostAsync(new Uri(Resources.GameCancelUrl), formContent);
            var content = await response.Content.ReadAsStringAsync();

            LastDebugMessage = "Pushed game cancel event";
            Debug.WriteLine("Pushed game cancel event");
            Debug.WriteLine(content);

            ActiveGameToken = null;
            ActiveGameTime = 0;
        }

        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);

            return lpPoint;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public class ExpeditionResponse
        {
            public bool IsActive;
            public string State;
            public List<string> Record;
            public List<string> Deck;
            public int Games;
            public int Wins;
            public int Losses;
        }

        public class ActiveDeckResponse
        {
            public string DeckCode;
            public Dictionary<String, int> CardsInDeck;
        }

        public class GameResultResponse
        {
            public string GameID;
            public string LocalPlayerWon;
        }

        public class GameRectResponse
        {
            public string PlayerName;
            public string OpponentName;
            public string GameState;
            public GameScreen Screen;
            public List<GameRectangle> Rectangles;


            public MouseState Mouse;
            public List<string> Log;
        }

        public class MouseState
        {
            public int X;
            public int Y;
            public bool Pressed;
        }

        public class GameScreen
        {
            public int ScreenWidth;
            public int ScreenHeight;
        }


        public class GameRectangle
        {
            public string CardID;
            public string CardCode;
            public int TopLeftX;
            public int TopLeftY;
            public int Width;
            public int Height;
            public bool LocalPlayer;
        }
    }
}
