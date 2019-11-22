namespace LoRTracker
{
    partial class DeckTrackerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeckTrackerForm));
            this.DeckListView = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // DeckListView
            // 
            this.DeckListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DeckListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.DeckListView.HideSelection = false;
            this.DeckListView.Location = new System.Drawing.Point(0, 0);
            this.DeckListView.Margin = new System.Windows.Forms.Padding(0);
            this.DeckListView.MultiSelect = false;
            this.DeckListView.Name = "DeckListView";
            this.DeckListView.Size = new System.Drawing.Size(384, 761);
            this.DeckListView.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.DeckListView.TabIndex = 0;
            this.DeckListView.UseCompatibleStateImageBehavior = false;
            this.DeckListView.View = System.Windows.Forms.View.List;
            // 
            // DeckTrackerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 761);
            this.Controls.Add(this.DeckListView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(400, 800);
            this.Name = "DeckTrackerForm";
            this.Text = "Deck Tracker";
            this.TopMost = true;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView DeckListView;
    }
}