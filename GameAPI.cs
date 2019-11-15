using LoRTracker.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
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

        internal static GameRectResponse LastBoardState;

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

        static string LastStateString;

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

                if(LastGameID == null)
                {
                    LastGameID = (await GetGameResult()).GameID;
                }

                if(IsOffline())
                {
                    await CancelGame();
                }

                if(EnteredGame())
                {
                    await PushGameStart();
                }

                if(IsInGame())
                {
                    LastActiveDeck = (await GetActiveDeckList()).DeckCode;
                    await PushUpdates();
                }

                if(QuitGame())
                {
                    LastGameScore = (await GetGameResult()).LocalPlayerWon;
                    LastGameID = (await GetGameResult()).GameID;
                    await PushGameEnd();
                }
            }
#pragma warning disable CS0168 
            catch(HttpRequestException e)
            {
                CurrentState = PlayerState.OFFLINE;
            }
        }

        internal static string GetMouseLocation()
        {
            GetCursorPos(out POINT mousePosition);
            return mousePosition.X + ", " + (Screen.PrimaryScreen.Bounds.Height - mousePosition.Y);
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
            ActiveGameTime = 0;

            LastDebugMessage = "Pushed game start event";
            Debug.WriteLine("Pushed game start event");
        }

        internal static async Task PushUpdates()
        {
            if(ActiveGameToken == null || ActiveGameToken.Length < 5)
            {
                return;
            }

            var CurrentBoardState = await GetPositionalRects();
            if(LastBoardState == null)
            {
                LastBoardState = CurrentBoardState;
            }

            if(LastBoardState == CurrentBoardState)
            {
                return;
            }

            LastBoardState = CurrentBoardState;
            CurrentBoardState.Mouse = GetMouseLocation();
            string OutputState = JsonConvert.SerializeObject(CurrentBoardState);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                new KeyValuePair<string, string>("time", ActiveGameTime.ToString()),
                new KeyValuePair<string, string>("state", OutputState),
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
            ActiveGameTime = 0;
        }

        internal static async Task CancelGame()
        {
            if(ActiveGameToken == null || ActiveGameToken.Length < 5)
            {
                return;
            }

            var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", Form1.Token),
                    new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                });

            var response = await Client.PostAsync(new Uri(Resources.GameEndUrl), formContent);
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
            public string Mouse;
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
