using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace LoRTracker
{
    public partial class DeckTrackerForm:Form
    {
        public DeckTrackerForm()
        {
            InitializeComponent();
            DeckListView.
        }

        public LoR.LoRDeckCode.LoRDeck deck;

        public static bool hasSetDeck = false;

        public void SetDeck(string code)
        {
            if(hasSetDeck)
            {
                return;
            }

            deck = LoR.LoRDeckCode.Decode(code);
            DeckListView.View = View.List;
            DeckListView.Items.Clear();
            foreach(KeyValuePair<LoR.LoRDeckCode.LoRCard, int> entry in deck.Cards)
            {
                for(var i = 0; i < entry.Value; i++)
                {
                    DeckListView.Items.Add(new ListViewItem(entry.Key.CardCode));
                }
            }

            Debug.WriteLine("added items");

            hasSetDeck = true;
        }

        public void UpdateState(string state)
        {

        }

        internal void Reset()
        {
            hasSetDeck = false;
        }
    }
}
