namespace Browser_Alpha
{
    partial class Browser
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Browser));
            this.urlSav = new System.Windows.Forms.TextBox();
            this.googleKeresSav = new System.Windows.Forms.TextBox();
            this.visszaGomb = new System.Windows.Forms.Button();
            this.eloreGomb = new System.Windows.Forms.Button();
            this.ujraGomb = new System.Windows.Forms.Button();
            this.urlKeresGomb = new System.Windows.Forms.Button();
            this.googleKeresGomb = new System.Windows.Forms.Button();
            this.kezdoGomb = new System.Windows.Forms.Button();
            this.infGomb = new System.Windows.Forms.Button();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.ujLapGomb = new System.Windows.Forms.Button();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.bookmarksToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tempToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bookmarkseditToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.homepageToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.chrminfToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.proginfToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.changelangToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.huToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.enToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // urlSav
            // 
            this.urlSav.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.urlSav.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.urlSav.Location = new System.Drawing.Point(153, 15);
            this.urlSav.Name = "urlSav";
            this.urlSav.Size = new System.Drawing.Size(418, 26);
            this.urlSav.TabIndex = 1;
            this.urlSav.Text = "http://";
            this.urlSav.KeyDown += new System.Windows.Forms.KeyEventHandler(this.UrlSav_KeyDown);
            // 
            // googleKeresSav
            // 
            this.googleKeresSav.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.googleKeresSav.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.googleKeresSav.Location = new System.Drawing.Point(673, 15);
            this.googleKeresSav.Name = "googleKeresSav";
            this.googleKeresSav.Size = new System.Drawing.Size(234, 26);
            this.googleKeresSav.TabIndex = 2;
            this.googleKeresSav.Text = "Google keresés...";
            this.googleKeresSav.Click += new System.EventHandler(this.GoogleKeresSav_Clear);
            this.googleKeresSav.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GoogleKeresSav_KeyDown);
            this.googleKeresSav.MouseEnter += new System.EventHandler(this.GoogleKeresSav_Clear);
            this.googleKeresSav.MouseLeave += new System.EventHandler(this.GoogleKeresSav_MouseLeave);
            // 
            // visszaGomb
            // 
            this.visszaGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.back;
            this.visszaGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.visszaGomb.Location = new System.Drawing.Point(12, 10);
            this.visszaGomb.Name = "visszaGomb";
            this.visszaGomb.Size = new System.Drawing.Size(41, 36);
            this.visszaGomb.TabIndex = 3;
            this.visszaGomb.UseVisualStyleBackColor = true;
            this.visszaGomb.Click += new System.EventHandler(this.VisszaGomb_Click);
            this.visszaGomb.MouseHover += new System.EventHandler(this.VisszaGomb_MouseHover);
            // 
            // eloreGomb
            // 
            this.eloreGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.next;
            this.eloreGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.eloreGomb.ForeColor = System.Drawing.SystemColors.ControlText;
            this.eloreGomb.Location = new System.Drawing.Point(59, 10);
            this.eloreGomb.Name = "eloreGomb";
            this.eloreGomb.Size = new System.Drawing.Size(41, 36);
            this.eloreGomb.TabIndex = 4;
            this.eloreGomb.UseVisualStyleBackColor = true;
            this.eloreGomb.Click += new System.EventHandler(this.EloreGomb_Click);
            this.eloreGomb.MouseHover += new System.EventHandler(this.EloreGomb_MouseHover);
            // 
            // ujraGomb
            // 
            this.ujraGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.redo_grey_512;
            this.ujraGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ujraGomb.Location = new System.Drawing.Point(106, 10);
            this.ujraGomb.Name = "ujraGomb";
            this.ujraGomb.Size = new System.Drawing.Size(41, 36);
            this.ujraGomb.TabIndex = 5;
            this.ujraGomb.UseVisualStyleBackColor = true;
            this.ujraGomb.Click += new System.EventHandler(this.UjraGomb_Click);
            this.ujraGomb.MouseHover += new System.EventHandler(this.UjraGomb_MouseHover);
            // 
            // urlKeresGomb
            // 
            this.urlKeresGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.urlKeresGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.search_grey_512;
            this.urlKeresGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.urlKeresGomb.Location = new System.Drawing.Point(579, 10);
            this.urlKeresGomb.Name = "urlKeresGomb";
            this.urlKeresGomb.Size = new System.Drawing.Size(41, 36);
            this.urlKeresGomb.TabIndex = 6;
            this.urlKeresGomb.UseVisualStyleBackColor = true;
            this.urlKeresGomb.Click += new System.EventHandler(this.UrlKeresGomb_Click);
            this.urlKeresGomb.MouseHover += new System.EventHandler(this.UrlKeresGomb_MouseHover);
            // 
            // googleKeresGomb
            // 
            this.googleKeresGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.googleKeresGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.search_grey_512;
            this.googleKeresGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.googleKeresGomb.Location = new System.Drawing.Point(913, 10);
            this.googleKeresGomb.Name = "googleKeresGomb";
            this.googleKeresGomb.Size = new System.Drawing.Size(41, 36);
            this.googleKeresGomb.TabIndex = 7;
            this.googleKeresGomb.UseVisualStyleBackColor = true;
            this.googleKeresGomb.Click += new System.EventHandler(this.GoogleKeresGomb_Click);
            this.googleKeresGomb.MouseHover += new System.EventHandler(this.GoogleKeresGomb_MouseHover);
            // 
            // kezdoGomb
            // 
            this.kezdoGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.kezdoGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.home_grey_512;
            this.kezdoGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.kezdoGomb.Location = new System.Drawing.Point(960, 10);
            this.kezdoGomb.Name = "kezdoGomb";
            this.kezdoGomb.Size = new System.Drawing.Size(41, 36);
            this.kezdoGomb.TabIndex = 8;
            this.kezdoGomb.UseVisualStyleBackColor = true;
            this.kezdoGomb.Click += new System.EventHandler(this.KezdoGomb_Click);
            this.kezdoGomb.MouseHover += new System.EventHandler(this.KezdoGomb_MouseHover);
            // 
            // infGomb
            // 
            this.infGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.infGomb.BackgroundImage = global::Browser_Alpha.Properties.Resources.settings_grey_512;
            this.infGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.infGomb.Location = new System.Drawing.Point(1007, 10);
            this.infGomb.Name = "infGomb";
            this.infGomb.Size = new System.Drawing.Size(41, 36);
            this.infGomb.TabIndex = 9;
            this.infGomb.UseVisualStyleBackColor = true;
            this.infGomb.Click += new System.EventHandler(this.InfGomb_Click);
            this.infGomb.MouseHover += new System.EventHandler(this.InfGomb_MouseHover);
            // 
            // tabControl
            // 
            this.tabControl.AccessibleRole = System.Windows.Forms.AccessibleRole.MenuBar;
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabPage1);
            this.tabControl.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabControl.Location = new System.Drawing.Point(-4, 52);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(1068, 618);
            this.tabControl.TabIndex = 1;
            this.tabControl.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.TabControl_DrawItem);
            this.tabControl.Click += new System.EventHandler(this.TabControl_Click);
            this.tabControl.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TabControl_MouseDown);
            // 
            // tabPage1
            // 
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1060, 592);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // ujLapGomb
            // 
            this.ujLapGomb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ujLapGomb.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("ujLapGomb.BackgroundImage")));
            this.ujLapGomb.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ujLapGomb.Location = new System.Drawing.Point(626, 10);
            this.ujLapGomb.Name = "ujLapGomb";
            this.ujLapGomb.Size = new System.Drawing.Size(41, 36);
            this.ujLapGomb.TabIndex = 11;
            this.ujLapGomb.UseVisualStyleBackColor = true;
            this.ujLapGomb.Click += new System.EventHandler(this.UjLapGomb_Click);
            this.ujLapGomb.MouseHover += new System.EventHandler(this.UjLapGomb_MouseHover);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bookmarksToolStripMenuItem,
            this.bookmarkseditToolStripMenuItem,
            this.homepageToolStripMenuItem,
            this.toolStripMenuItem1,
            this.chrminfToolStripMenuItem,
            this.proginfToolStripMenuItem,
            this.toolStripMenuItem2,
            this.changelangToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip1";
            this.contextMenuStrip.ShowImageMargin = false;
            this.contextMenuStrip.Size = new System.Drawing.Size(134, 148);
            // 
            // bookmarksToolStripMenuItem
            // 
            this.bookmarksToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tempToolStripMenuItem});
            this.bookmarksToolStripMenuItem.Enabled = false;
            this.bookmarksToolStripMenuItem.Name = "bookmarksToolStripMenuItem";
            this.bookmarksToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.bookmarksToolStripMenuItem.Text = "bookmarks";
            // 
            // tempToolStripMenuItem
            // 
            this.tempToolStripMenuItem.Name = "tempToolStripMenuItem";
            this.tempToolStripMenuItem.ShowShortcutKeys = false;
            this.tempToolStripMenuItem.Size = new System.Drawing.Size(95, 22);
            this.tempToolStripMenuItem.Text = "temp";
            // 
            // bookmarkseditToolStripMenuItem
            // 
            this.bookmarkseditToolStripMenuItem.Name = "bookmarkseditToolStripMenuItem";
            this.bookmarkseditToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.bookmarkseditToolStripMenuItem.Text = "bookmarks_edit";
            this.bookmarkseditToolStripMenuItem.Click += new System.EventHandler(this.BookmarkseditToolStripMenuItem_Click);
            // 
            // homepageToolStripMenuItem
            // 
            this.homepageToolStripMenuItem.Name = "homepageToolStripMenuItem";
            this.homepageToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.homepageToolStripMenuItem.Text = "homepage";
            this.homepageToolStripMenuItem.Click += new System.EventHandler(this.HomepageToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(130, 6);
            // 
            // chrminfToolStripMenuItem
            // 
            this.chrminfToolStripMenuItem.Name = "chrminfToolStripMenuItem";
            this.chrminfToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.chrminfToolStripMenuItem.Text = "chrm_inf";
            this.chrminfToolStripMenuItem.Click += new System.EventHandler(this.ChrminfToolStripMenuItem_Click);
            // 
            // proginfToolStripMenuItem
            // 
            this.proginfToolStripMenuItem.Name = "proginfToolStripMenuItem";
            this.proginfToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.proginfToolStripMenuItem.Text = "prog_inf";
            this.proginfToolStripMenuItem.Click += new System.EventHandler(this.ProginfToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(130, 6);
            // 
            // changelangToolStripMenuItem
            // 
            this.changelangToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.huToolStripMenuItem,
            this.enToolStripMenuItem});
            this.changelangToolStripMenuItem.Name = "changelangToolStripMenuItem";
            this.changelangToolStripMenuItem.Size = new System.Drawing.Size(133, 22);
            this.changelangToolStripMenuItem.Text = "change_lang";
            // 
            // huToolStripMenuItem
            // 
            this.huToolStripMenuItem.Enabled = false;
            this.huToolStripMenuItem.Name = "huToolStripMenuItem";
            this.huToolStripMenuItem.Size = new System.Drawing.Size(88, 22);
            this.huToolStripMenuItem.Text = "hu";
            this.huToolStripMenuItem.Click += new System.EventHandler(this.Hu_enToolStripMenuItem_Click);
            // 
            // enToolStripMenuItem
            // 
            this.enToolStripMenuItem.Enabled = false;
            this.enToolStripMenuItem.Name = "enToolStripMenuItem";
            this.enToolStripMenuItem.Size = new System.Drawing.Size(88, 22);
            this.enToolStripMenuItem.Text = "en";
            this.enToolStripMenuItem.Click += new System.EventHandler(this.Hu_enToolStripMenuItem_Click);
            // 
            // Browser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1060, 666);
            this.Controls.Add(this.ujLapGomb);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.infGomb);
            this.Controls.Add(this.kezdoGomb);
            this.Controls.Add(this.googleKeresGomb);
            this.Controls.Add(this.urlKeresGomb);
            this.Controls.Add(this.ujraGomb);
            this.Controls.Add(this.eloreGomb);
            this.Controls.Add(this.visszaGomb);
            this.Controls.Add(this.googleKeresSav);
            this.Controls.Add(this.urlSav);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Browser";
            this.Text = "Egyszerű Webböngésző";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Browser_FormClosing);
            this.Load += new System.EventHandler(this.Browser_Load);
            this.tabControl.ResumeLayout(false);
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox urlSav;
        private System.Windows.Forms.TextBox googleKeresSav;
        private System.Windows.Forms.Button visszaGomb;
        private System.Windows.Forms.Button eloreGomb;
        private System.Windows.Forms.Button ujraGomb;
        private System.Windows.Forms.Button urlKeresGomb;
        private System.Windows.Forms.Button googleKeresGomb;
        private System.Windows.Forms.Button kezdoGomb;
        private System.Windows.Forms.Button infGomb;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.Button ujLapGomb;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem chrminfToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem proginfToolStripMenuItem;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.ToolStripMenuItem changelangToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bookmarksToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem homepageToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem bookmarkseditToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem huToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem enToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tempToolStripMenuItem;
        private System.Windows.Forms.ToolTip toolTip;
    }
}

