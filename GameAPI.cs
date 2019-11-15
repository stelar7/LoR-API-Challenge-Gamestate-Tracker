﻿using LoRTracker.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

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

                if(IsInGame())
                {
                    LastActiveDeck = (await GetActiveDeckList()).DeckCode;
                }

                if(LastGameID == null)
                {
                    LastGameID = (await GetGameResult()).GameID;
                }

                if(QuitGame())
                {
                    LastGameScore = (await GetGameResult()).LocalPlayerWon;
                    LastGameID = (await GetGameResult()).GameID;
                }


                Debug.WriteLine(NextState);
                Debug.WriteLine(CurrentState);
                Debug.WriteLine(LastGameScore);
                Debug.WriteLine(LastGameID);
                Debug.WriteLine(LastActiveDeck);


            }
#pragma warning disable CS0168 
            catch(HttpRequestException e)
            {
                CurrentState = PlayerState.OFFLINE;
            }
        }

        internal static async Task PushGameStart()
        {
            var data = await GetPositionalRects();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("self", data.PlayerName),
                new KeyValuePair<string, string>("other", data.OpponentName)
            });

            var response = await Client.PostAsync(new Uri(Resources.GameStartUrl), formContent);
            ActiveGameToken = await response.Content.ReadAsStringAsync();
            ActiveGameTime = 0;
        }

        internal static async Task PushUpdates()
        {
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

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", Form1.Token),
                new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                new KeyValuePair<string, string>("time", ActiveGameTime.ToString()),
                new KeyValuePair<string, string>("state", CurrentBoardState.ToString()),
            });

            var response = await Client.PostAsync(new Uri(Resources.GameUpdateUrl), formContent);
        }

        internal static async Task PushGameEnd()
        {
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

            ActiveGameToken = null;
            ActiveGameTime = 0;
        }

        internal static async Task CancelGame()
        {
            if(ActiveGameToken != null)
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", Form1.Token),
                    new KeyValuePair<string, string>("gameToken", ActiveGameToken),
                });

                var response = await Client.PostAsync(new Uri(Resources.GameEndUrl), formContent);

                ActiveGameToken = null;
                ActiveGameTime = 0;
            }
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
