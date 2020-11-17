namespace Browser_Alpha
{
    partial class Bookmarks
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
            if (disposing && (components != null))
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Bookmarks));
            this.listBox = new System.Windows.Forms.ListBox();
            this.hozzaAdGomb = new System.Windows.Forms.Button();
            this.eltavolitGomb = new System.Windows.Forms.Button();
            this.eltavolitMindetGomb = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listBox
            // 
            this.listBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBox.FormattingEnabled = true;
            this.listBox.Location = new System.Drawing.Point(13, 13);
            this.listBox.Name = "listBox";
            this.listBox.Size = new System.Drawing.Size(408, 173);
            this.listBox.TabIndex = 0;
            this.listBox.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.ListBox_MouseDoubleClick);
            // 
            // hozzaAdGomb
            // 
            this.hozzaAdGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.hozzaAdGomb.Location = new System.Drawing.Point(13, 195);
            this.hozzaAdGomb.Name = "hozzaAdGomb";
            this.hozzaAdGomb.Size = new System.Drawing.Size(75, 23);
            this.hozzaAdGomb.TabIndex = 1;
            this.hozzaAdGomb.Text = "Hozzáad";
            this.hozzaAdGomb.UseVisualStyleBackColor = true;
            this.hozzaAdGomb.Click += new System.EventHandler(this.HozzaAdGomb_Click);
            // 
            // eltavolitGomb
            // 
            this.eltavolitGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.eltavolitGomb.Location = new System.Drawing.Point(94, 195);
            this.eltavolitGomb.Name = "eltavolitGomb";
            this.eltavolitGomb.Size = new System.Drawing.Size(75, 23);
            this.eltavolitGomb.TabIndex = 2;
            this.eltavolitGomb.Text = "Eltávolít";
            this.eltavolitGomb.UseVisualStyleBackColor = true;
            this.eltavolitGomb.Click += new System.EventHandler(this.EltavolitGomb_Click);
            // 
            // eltavolitMindetGomb
            // 
            this.eltavolitMindetGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.eltavolitMindetGomb.Location = new System.Drawing.Point(293, 195);
            this.eltavolitMindetGomb.Name = "eltavolitMindetGomb";
            this.eltavolitMindetGomb.Size = new System.Drawing.Size(128, 23);
            this.eltavolitMindetGomb.TabIndex = 3;
            this.eltavolitMindetGomb.Text = "Összes eltávolítása";
            this.eltavolitMindetGomb.UseVisualStyleBackColor = true;
            this.eltavolitMindetGomb.Click += new System.EventHandler(this.EltavolitMindetGomb_Click);
            // 
            // Bookmarks
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(433, 225);
            this.Controls.Add(this.eltavolitMindetGomb);
            this.Controls.Add(this.eltavolitGomb);
            this.Controls.Add(this.hozzaAdGomb);
            this.Controls.Add(this.listBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Bookmarks";
            this.Text = "Bookmarks";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Bookmarks_FormClosing);
            this.Load += new System.EventHandler(this.Bookmarks_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBox;
        private System.Windows.Forms.Button hozzaAdGomb;
        private System.Windows.Forms.Button eltavolitGomb;
        private System.Windows.Forms.Button eltavolitMindetGomb;
    }
}