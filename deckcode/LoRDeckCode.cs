using LoR.utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LoR
{
    public class LoRDeckCode
    {

        class ReversibleDictionary<T1, T2>:IEnumerable
        {
            private Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
            private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();

            public ReversibleDictionary()
            {
                Forward = new Indexer<T1, T2>(_forward);
                Reverse = new Indexer<T2, T1>(_reverse);
            }

            public class Indexer<T3, T4>
            {
                private readonly Dictionary<T3, T4> _dictionary;
                public Indexer(Dictionary<T3, T4> dictionary)
                {
                    _dictionary = dictionary;
                }
                public T4 this[T3 index]
                {
                    get => _dictionary[index];
                    set => _dictionary[index] = value;
                }
            }

            public void Add(T1 t1, T2 t2)
            {
                _forward.Add(t1, t2);
                _reverse.Add(t2, t1);
            }

            public IEnumerator GetEnumerator()
            {
                return new Dictionary<T1, T2>.Enumerator();
            }

            public Indexer<T1, T2> Forward { get; private set; }
            public Indexer<T2, T1> Reverse { get; private set; }
        }

        static readonly ReversibleDictionary<string, int> Factions = new ReversibleDictionary<string, int>()
        {
            {"DE", 0},
            {"FR", 1},
            {"IO", 2},
            {"NX", 3},
            {"PZ", 4},
            {"SI", 5},
        };

        public class LoRCard
        {
            public string Set;
            public string Faction;
            public string Id;

            public string CardCode => Set.PadLeft(2, '0') + Faction + Id.PadLeft(3, '0');

            public LoRCard(string set, string faction, string id)
            {
                Set = set;
                Faction = faction;
                Id = id;
            }

            public override string ToString()
            {
                return CardCode;
            }
        }

        public class LoRDeck
        {
            public readonly Dictionary<LoRCard, int> Cards = new Dictionary<LoRCard, int>();

            public void Add(LoRCard card, int count)
            {
                Cards.Add(card, count);
            }

            public void Remove(LoRCard card)
            {
                Cards.Remove(card);
            }

            public int Count(LoRCard card)
            {
                Cards.TryGetValue(card, out int count);
                return count;
            }

            public int Count()
            {
                return Cards.Values.Sum();
            }

            public void Clear()
            {
                Cards.Clear();
            }

            public override string ToString()
            {
                var output = "";
                foreach(var item in Cards)
                {
                    output += item.Key + ":" + item.Value;
                }
                return output;
            }
        }

        public static LoRDeck Decode(string code)
        {
            LoRDeck deck = new LoRDeck();

            List<byte> data = Base32.Decode(code).ToList();

            byte first = data[0];
            data.RemoveAt(0);

            int format = first >> 4;
            int version = first & 0xF;
            if(version > 1)
            {
                throw new Exception("Invalid version");
            }

            for(int i = 3; i > 0; i--)
            {
                int numGroupOfs = VarInt.Pop(data);

                for(int j = 0; j < numGroupOfs; j++)
                {
                    int numOfsInThisGroup = VarInt.Pop(data);
                    int set = VarInt.Pop(data);
                    int faction = VarInt.Pop(data);

                    for(int k = 0; k < numOfsInThisGroup; k++)
                    {
                        int cardId = VarInt.Pop(data);

                        string setString = set.ToString().PadLeft(2, '0');
                        string factionString = Factions.Reverse[faction];
                        string cardString = cardId.ToString().PadLeft(3, '0');

                        LoRCard card = new LoRCard(setString, factionString, cardString);
                        deck.Add(card, i);
                    }
                }
            }

            while(data.Count > 0)
            {
                int fourPlusCount = VarInt.Pop(data);
                int fourPlusSet = VarInt.Pop(data);
                int fourPlusFaction =VarInt.Pop(data);
                int fourPlusNumber = VarInt.Pop(data);

                string fourPlusSetString = fourPlusSet.ToString().PadLeft(2, '0');
                string fourPlusFactionString = Factions.Reverse[fourPlusFaction];
                string fourPlusNumberString = fourPlusNumber.ToString().PadLeft(3, '0');

                LoRCard card = new LoRCard(fourPlusSetString, fourPlusFactionString, fourPlusNumberString);
                deck.Add(card, fourPlusCount);
            }

            return deck;
        }

        public static string Encode(LoRDeck deck)
        {
            return "";

        }
    }
}
