using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using Microsoft.VisualBasic;
using CefSharp;
using CefSharp.WinForms;
using CefSharp.WinForms.Internals;
using CefSharp.Example;
using CefSharp.Example.Handlers;

namespace Browser_Alpha
{
    public partial class Browser : Form
    {
        public string cim;
        public string kezdo_oldal;
        public string nincs_url;
        public string nincs_szoveg;
        public string nincs_lap;
        public string hiba;
        public string kereso_szoveg;
        public string google_http;
        public string nevjegy;
        public string informacio_szoveg;
        public string change_lang;
        public string change_lang_text;
        public string change_lang_inf;
        public string chrm_inf;
        public string prog_inf;
        public string vissza;
        public string elore;
        public string ujra;
        public string keres;
        public string uj_lap;
        public string kezdolap;
        public string informacio;
        public string konyvjelzok;
        public string konyvjelzo_kezelo;
        public string kezdolap_valtas;
        public string uj_kezdolap_cime;
        public string magyar;
        public string angol;
        public string beallitasok;
        public string nyelv;

        public Browser(string Nyelv)
        {
            if (Nyelv == "hu")
            {
                cim = "Egyszerű Webböngésző";
                kezdo_oldal = "https://www.google.hu/";
                nincs_url = "Nem adtál meg URL címet!";
                nincs_szoveg = "Nem adtál meg semmit!";
                nincs_lap = "Nincs egyetlen lap sem!\nÚj lap létrehozva, kérlek próbáld újra.";
                hiba = "Hiba";
                kereso_szoveg = "Google keresés...";
                google_http = "https://www.google.hu/search?q=";
                nevjegy = "A program névjegye";
                informacio_szoveg = $"A programot készítette: Szabó Levente\n\n.Net cél verzió: 4.7.2\nProgram verzió: {Assembly.GetExecutingAssembly().GetName().Version}\nBöngésző motor: Chromium (Cefsharp)\n\n{((AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyCopyrightAttribute))).Copyright}";
                change_lang = "Nyelv váltás";
                change_lang_text = "Szeretnéd átváltani a nyelvet Angolra?\nA program újra fog indulni!";
                chrm_inf = "Chromium verzió";
                prog_inf = "Program információ";
                vissza = "Vissza";
                elore = "Előre";
                ujra = "Újratöltés";
                keres = "Keresés";
                uj_lap = "Új lap";
                kezdolap = "Kezdőlap";
                informacio = "Információ";
                konyvjelzok = "Könyvjelzők";
                konyvjelzo_kezelo = "Könyvjelzők kezelése";
                kezdolap_valtas = "Kezdőlap módosítása";
                uj_kezdolap_cime = "Az új kezdőlap címe";
                magyar = "Magyar";
                angol = "Angol";
                beallitasok = "Beállítások";
            }
            else if (Nyelv == "en")
            {
                cim = "Simple Webbrowser";
                kezdo_oldal = "https://www.google.com/";
                nincs_url = "Please set an URL!";
                nincs_szoveg = "Please set something to search!";
                nincs_lap = "There is no tab available!\nNew tab created, please try again now.";
                hiba = "Error";
                kereso_szoveg = "Google Search...";
                google_http = "https://www.google.com/search?q=";
                nevjegy = "About the program";
                informacio_szoveg = $"Program created by: Szabó Levente\n\n.Net target version: 4.7.2\nProgram version: {Assembly.GetExecutingAssembly().GetName().Version}\nBrowser engine: Chromium (Cefsharp)\n\n{((AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyCopyrightAttribute))).Copyright}";
                change_lang = "Change language";
                change_lang_text = "Do you want to change the language to Hungarian?\nThe program will restart!";
                chrm_inf = "Chromium version";
                prog_inf = "Program information";
                vissza = "Back";
                elore = "Forward";
                ujra = "Refresh";
                keres = "Search";
                uj_lap = "New tab";
                kezdolap = "Homepage";
                informacio = "Information";
                konyvjelzok = "Bookmarks";
                konyvjelzo_kezelo = "Bookmarks manager";
                kezdolap_valtas = "Modify homepage";
                uj_kezdolap_cime = "The new homepage address";
                magyar = "Hungarian";
                angol = "English";
                beallitasok = "Settings";
            }
            else
            {
                MessageBox.Show("NOT_VALID_LANG", "nyelv.ini", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
            this.nyelv = Nyelv;
            InitializeComponent();
        }


        private void Browser_Load(object sender, EventArgs e)
        {
            if (nyelv == "hu")
            {
                huToolStripMenuItem.Checked = true;
                enToolStripMenuItem.Enabled = true;
            }
            else if (nyelv == "en")
            {
                enToolStripMenuItem.Checked = true;
                huToolStripMenuItem.Enabled = true;
            }

            Text = cim + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
            googleKeresSav.Text = kereso_szoveg;

            tabControl.Padding = new Point(18, 4);

            bookmarksToolStripMenuItem.DropDownDirection = ToolStripDropDownDirection.Left;
            changelangToolStripMenuItem.DropDownDirection = ToolStripDropDownDirection.BelowLeft;

            ((ToolStripDropDownMenu)bookmarksToolStripMenuItem.DropDown).ShowImageMargin = false;

            enToolStripMenuItem.Text = angol;
            huToolStripMenuItem.Text = magyar;
            chrminfToolStripMenuItem.Text = chrm_inf;
            proginfToolStripMenuItem.Text = prog_inf;
            changelangToolStripMenuItem.Text = change_lang;
            bookmarksToolStripMenuItem.Text = konyvjelzok;
            bookmarkseditToolStripMenuItem.Text = konyvjelzo_kezelo;
            homepageToolStripMenuItem.Text = kezdolap_valtas;

            CefSettings settings = new CefSettings();
            if (nyelv == "en")
            {
                settings.Locale = "en";
            }
            else
            {
                settings.Locale = "hu";
            }
            Cef.Initialize(settings);

            ChromiumWebBrowser chrm;
            if (File.Exists("kezdolap.ini"))
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    chrm = new ChromiumWebBrowser(reader.ReadLine());
                }
            }
            else
            {
                chrm = new ChromiumWebBrowser(kezdo_oldal);
            }
            chrm.Parent = tabControl.SelectedTab;
            chrm.Dock = DockStyle.Fill;
            chrm.AddressChanged += Chrm_AddressChanged;
            chrm.TitleChanged += Chrm_TitleChanged;
            chrm.DownloadHandler = new DownloadHandler();
        }

        private ChromiumWebBrowser GetCurrentChrm()
        {
            try
            {
                return (ChromiumWebBrowser)tabControl.SelectedTab.Controls[0];
            }
            catch (Exception)
            {
                MessageBox.Show(nincs_lap, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ujLapGomb.PerformClick(); 
                return (ChromiumWebBrowser)tabControl.SelectedTab.Controls[0];
            }

        }

        private void GoogleKeresSav_Clear(object sender, EventArgs e)
        {
            if (googleKeresSav.Text == kereso_szoveg)
            {
                googleKeresSav.Clear();
            }
        }

        private void GoogleKeresSav_MouseLeave(object sender, EventArgs e)
        {
            if (googleKeresSav.Text == "")
            {
                googleKeresSav.Text = kereso_szoveg;
            }
        }

        private void Mehet()
        {
            if (urlSav.Text == "")
            {
                MessageBox.Show(nincs_url, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                ChromiumWebBrowser chrm = GetCurrentChrm();
                chrm.DownloadHandler = new DownloadHandler();
                if (chrm != null)
                {
                    if (urlSav.Text.Contains(".") || urlSav.Text == "localhost")
                    {
                        chrm.Load(urlSav.Text);
                    }
                    else
                    {
                        chrm.Load(google_http + urlSav.Text);
                    }

                }
            }
        }
        private void MehetGoogle()
        {
            if (googleKeresSav.Text == "" || googleKeresSav.Text == kereso_szoveg)
            {
                MessageBox.Show(nincs_szoveg, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                ChromiumWebBrowser chrm = GetCurrentChrm();
                chrm.DownloadHandler = new DownloadHandler();
                if (chrm != null)
                {
                    chrm.Load(google_http + googleKeresSav.Text);
                    googleKeresSav.Clear();
                    googleKeresSav.Text = kereso_szoveg;
                }

            }
        }

        private void Browser_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }

        private void Chrm_TitleChanged(object sender, TitleChangedEventArgs e)
        {
            string tmp = "";
            if (e.Title.Length > 20)
                tmp = e.Title.Substring(0, 17) + "...";
            else
            {
                tmp = e.Title;
            }
            this.InvokeOnUiThreadIfRequired(() => tabControl.SelectedTab.Text = tmp);
        }
        private void Chrm_AddressChanged(object sender, AddressChangedEventArgs e)
        {
            this.InvokeOnUiThreadIfRequired(() =>
            {
                if (e.Address.Contains("//version/"))
                {
                    urlSav.Text = chrm_inf;
                }
                else
                {
                    urlSav.Text = e.Address;
                }
            });
        }

        private void ChrminfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            chrm.Load("chrome://version/");
        }

        private void ProginfToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(informacio_szoveg, nevjegy, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.Graphics.DrawString(this.tabControl.TabPages[e.Index].Text, e.Font, Brushes.Black, e.Bounds.Left + 12, e.Bounds.Top + 4);
            e.DrawFocusRectangle();
            e.Graphics.DrawString("X ", e.Font, Brushes.Black, e.Bounds.Right - 17, e.Bounds.Top + 4);
        }

        private void TabControl_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < this.tabControl.TabPages.Count; i++)
            {
                Rectangle r = tabControl.GetTabRect(i);
                Rectangle xGomb = new Rectangle(r.Right - 15, r.Top + 4, 9, 7);
                if (xGomb.Contains(e.Location))
                {
                    ChromiumWebBrowser chrm = GetCurrentChrm();
                    chrm.Dispose();
                    this.tabControl.TabPages.RemoveAt(i);
                }
            }
        }

        private void TabControl_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            urlSav.Text = chrm.Address;
        }

        private void VisszaGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                if (chrm.CanGoBack)
                {
                    chrm.Back();
                }
            }
        }

        private void EloreGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                if (chrm.CanGoForward)
                {
                    chrm.Forward();
                }
            }
        }

        private void UjraGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                chrm.Refresh();
                chrm.Load(urlSav.Text);
            }
        }

        private void UrlKeresGomb_Click(object sender, EventArgs e)
        {
            Mehet();
        }

        private void GoogleKeresGomb_Click(object sender, EventArgs e)
        {
            MehetGoogle();
        }

        private void KezdoGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (File.Exists("kezdolap.ini"))
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    chrm.Load(reader.ReadLine());
                }
            }
            else
            {
                chrm.Load(kezdo_oldal);
            }
        }

        private void InfGomb_Click(object sender, EventArgs e)
        {
            contextMenuStrip.Show(infGomb, new Point(infGomb.Width - contextMenuStrip.Width, infGomb.Height));
            bookmarksToolStripMenuItem.Enabled = false;
            if (File.Exists("konyvjelzok.ini"))
            {
                bookmarksToolStripMenuItem.DropDown.Items.Clear();
                using (StreamReader reader = new StreamReader("konyvjelzok.ini"))
                {
                    while (!reader.EndOfStream)
                    {
                        string sor = reader.ReadLine();
                        if (sor == null)
                            break;
                        bookmarksToolStripMenuItem.DropDown.Items.Add(sor, null, Bm_Kat);

                    }
                }
                bookmarksToolStripMenuItem.Enabled = true;
            }
        }

        private void Bm_Kat(object sender, EventArgs e)
        {
            if (sender is ToolStripItem link)
            {
                ChromiumWebBrowser chrm = GetCurrentChrm();
                chrm.DownloadHandler = new DownloadHandler();
                chrm.Load(link.Text);
            }
        }

        private void UjLapGomb_Click(object sender, EventArgs e)
        {
            TabPage tab = new TabPage();
            tabControl.Controls.Add(tab);
            tabControl.SelectTab(tabControl.TabCount - 1);
            
            ChromiumWebBrowser chrm; 
            if (File.Exists("kezdolap.ini"))
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    chrm = new ChromiumWebBrowser(reader.ReadLine());
                }
            }
            else
            {
                chrm = new ChromiumWebBrowser(kezdo_oldal);
            }
            chrm.Parent = tab;
            chrm.Dock = DockStyle.Fill;
            urlSav.Text = kezdo_oldal;
            chrm.AddressChanged += Chrm_AddressChanged;
            chrm.TitleChanged += Chrm_TitleChanged;
        }

        private void UrlSav_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Mehet();
            }
        }

        private void GoogleKeresSav_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                MehetGoogle();
            }
        }

        private void VisszaGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.visszaGomb, vissza);
        }

        private void EloreGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.eloreGomb, elore);
        }

        private void UjraGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.ujraGomb, ujra);
        }

        private void UrlKeresGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.urlKeresGomb, keres);
        }

        private void GoogleKeresGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.googleKeresGomb, keres);
        }

        private void KezdoGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.kezdoGomb, kezdolap);
        }

        private void InfGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.infGomb, beallitasok);
        }

        private void UjLapGomb_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(this.ujLapGomb, uj_lap);
        }

        public void BmLoad(string link)
        {
            ChromiumWebBrowser chrm = GetCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            chrm.Load(link);
        }
        public string BmUrlText()
        {
            return urlSav.Text;
        }

        private void HomepageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string http_text = kezdo_oldal;
            if (File.Exists("kezdolap.ini"))
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    http_text = reader.ReadLine();
                }
            }

            var ib = Interaction.InputBox(uj_kezdolap_cime, kezdolap_valtas, http_text);
            if (ib != "")
            { 
                using (StreamWriter writer = new StreamWriter("kezdolap.ini", false))
                {
                    writer.WriteLine(ib);
                }
            }
        }

        private void BookmarkseditToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Bookmarks bm = new Bookmarks(nyelv);
            bm.Show();
        }

        private void Hu_enToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult dialogResult = MessageBox.Show(change_lang_text, change_lang, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult == DialogResult.Yes)
            {
                using (StreamWriter sw = new StreamWriter("nyelv.ini", false))
                {
                    if (nyelv == "hu")
                    {
                        sw.Write("en");
                    }
                    else
                    {
                        sw.Write("hu");
                    }
                }
                System.Diagnostics.Process.Start(Application.ExecutablePath);
                Environment.Exit(0);
            }
        }
    }
}
