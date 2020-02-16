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
using System.IO; //Fájlkezelés
using Microsoft.VisualBasic; //InputBox használatához kell, mert az a VisualBasic része
//Cefsharp namescape-ek betöltése a Chromium használatához
using CefSharp;
using CefSharp.WinForms;
using CefSharp.WinForms.Internals;
using CefSharp.Example;
using CefSharp.Example.Handlers;


namespace Browser_Alpha
{
    public partial class Form1 : Form
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

        ToolTip tp = new ToolTip(); //Tooltip hogy később amikor a gombok fölé visszük az egeret kitudjuk iratni a gomb nevét

        public Form1(string Nyelv) //Konstruktor ami megkapja a nyelvet majd annak függvényében tölti fel a fentebb telálható stringeket
        {
            if (Nyelv == "hu") //Magyar
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
                informacio_szoveg = "A programot készítette: Szabó Levente\n\nAz ikonok a flaticon.com/free-icon oldalról származnak.\n\n.Net verzió: 4.7.2\nAlkalmazás verzió: 1.0.0.1\nBöngésző motor: Chromium (Cefsharp)\n\nCopyright © 2020-2021";
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
            else if (Nyelv == "en") //Angol
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
                informacio_szoveg = "Program created by: Szabó Levente\n\nIcons from flaticon.com/free-icon.\n\n.Net version: 4.7.2\nProgram version: 1.0.0.1\nBrowser engine: Chromium (Cefsharp)\n\nCopyright © 2020-2021";
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
            else //Ismeretlen nyelv esetén egy hibaüzenet kiírása majd bezárás
            {
                MessageBox.Show("not_valid_lang", "nyelv.ini", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
                
            }
            this.nyelv = Nyelv; //Elmentjük a nyelvet hogy később át tudjuk adni a könyvjelző ablaknak
            InitializeComponent();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            //Nyelvválasztónál pipa a jelenlegi nyelvre, a másikat pedig kattinthatóvá tesszük
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

            Text = cim + " v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(); //Programverzió megjelenítése a form címében
            googleKeresSav.Text = kereso_szoveg; //Google keresés sáv szövegének feltöltése

            this.tabControl.Padding = new Point(18, 4); //Padding a tab controlra hogy később elférjen majd az X a lap bezárásához

            //Legördülő menüpontok iránya esztétika miatt
            bookmarksToolStripMenuItem.DropDownDirection = ToolStripDropDownDirection.Left;
            changelangToolStripMenuItem.DropDownDirection = ToolStripDropDownDirection.BelowLeft;

            ((ToolStripDropDownMenu)bookmarksToolStripMenuItem.DropDown).ShowImageMargin = false; //Képmargó tiltása a könyvjelzők gyors megjelenítésénél mert nem használjuk

            //ToolStrip menüpontok szövegének feltöltése
            enToolStripMenuItem.Text = angol;
            huToolStripMenuItem.Text = magyar;
            chrminfToolStripMenuItem.Text = chrm_inf;
            proginfToolStripMenuItem.Text = prog_inf;
            changelangToolStripMenuItem.Text = change_lang;
            bookmarksToolStripMenuItem.Text = konyvjelzok;
            bookmarkseditToolStripMenuItem.Text = konyvjelzo_kezelo;
            homepageToolStripMenuItem.Text = kezdolap_valtas;

            //Chromium inicializálása hogy tudjuk később használni
            CefSettings settings = new CefSettings();
            Cef.Initialize(settings);

            ChromiumWebBrowser chrm;
            if (File.Exists("kezdolap.ini")) //Ha a kezdőlapot átírtuk tehát a fájl létezik azt töltse be
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    chrm = new ChromiumWebBrowser(reader.ReadLine());
                }
            }
            else //ellenkező esetben az alapértelmezettet
            {
                chrm = new ChromiumWebBrowser(kezdo_oldal);
            }
            chrm.Parent = tabControl.SelectedTab; //Megadjuk hogy a tabcontrolra töltse a Chromiumot
            chrm.Dock = DockStyle.Fill; //Töltse ki, esztétika
            chrm.AddressChanged += Chrm_AddressChanged; //Url cím változás az url címsávban
            chrm.TitleChanged += Chrm_TitleChanged; //Weboldal title cím változása majd a tabcontrol szövegében jelenjen meg
            chrm.DownloadHandler = new DownloadHandler(); //Letöltések a böngészőn keresztül engedélyezése
        }

        private ChromiumWebBrowser getCurrentChrm() //Későbbiekben sokat lesz használva, és így csak egyszer kell hibát kezelni
        {
            try
            {
                return (ChromiumWebBrowser)tabControl.SelectedTab.Controls[0]; //Kiválasztott lapra adja vissza a Chromiumot
            }
            catch (Exception) //Ha nincs lap vagy egyéb hiba jelentkezik akkor a catch messagebox figyelmeztetés után új lapot hoz létre Chromiummal
            {
                MessageBox.Show(nincs_lap, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
                ujLapGomb.PerformClick(); 
                return (ChromiumWebBrowser)tabControl.SelectedTab.Controls[0];
            }

        }

        private void textBox2_Click(object sender, EventArgs e) //Ha belekattintunk a "Google keresés..." sávba akkor az alapértelmett szöveget törölje, így nem kell a felhasználónak visszatörölni
        {
            googleKeresSav.Clear();
        }

        private void mehet() //Url navigálásnál (gomb + enter lenyomás) használt metódus
        {
            if (urlSav.Text == "") //Ha a sáv üres, hibaüzenet
            {
                MessageBox.Show(nincs_url, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                ChromiumWebBrowser chrm = getCurrentChrm(); //Chromium meghívása az adott lapon (Sokat lesz használva, többször nem kommentelem)
                chrm.DownloadHandler = new DownloadHandler(); //Letöltések engedélyezése
                if (chrm != null)
                {
                    if (urlSav.Text.Contains(".") || urlSav.Text == "localhost")
                    {
                        chrm.Load(urlSav.Text);
                    }
                    else //Ha nincs pont, se nem localhost akkor google keresést hajtson végre mert valószínűleg a felhasználó azt akarta
                    {
                        chrm.Load(google_http + urlSav.Text);
                    }

                }
            }
        }
        private void mehet2() //Google keresésnél (gomb + enter lenyomás) használt metódus
        {
            if (googleKeresSav.Text == "" || googleKeresSav.Text == kereso_szoveg)
            {
                MessageBox.Show(nincs_szoveg, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                ChromiumWebBrowser chrm = getCurrentChrm();
                chrm.DownloadHandler = new DownloadHandler();
                if (chrm != null)
                {
                    chrm.Load(google_http + googleKeresSav.Text);
                    googleKeresSav.Clear();
                    googleKeresSav.Text = kereso_szoveg;
                }

            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown(); //Form bezárásánál a Chromiumot ajánlott leállítani így
        }

        //CefSharp.WinForms.Internals tartalmazza az InvokeOnUiThreadIfRequired-t
        private void Chrm_TitleChanged(object sender, TitleChangedEventArgs e) //Címváltozás esetén a tabcontrol frissüljön az új címmel
        {
            string tmp = "";
            if (e.Title.Length > 20) //Ha túl hosszú akkor a 17. karakter után legyen 3 pont, így max 20 karakter lehet ami még nem zavaró
                tmp = e.Title.Substring(0, 17) + "...";
            else
            {
                tmp = e.Title;
            }
            this.InvokeOnUiThreadIfRequired(() => tabControl.SelectedTab.Text = tmp); 
        }
        private void Chrm_AddressChanged(object sender, AddressChangedEventArgs e) //Url Címváltozás esetén az url sáv frissüljön 
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

        private void chrminfToolStripMenuItem_Click(object sender, EventArgs e) //Chrome verzió megjelenítése menü fül
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            chrm.Load("chrome://version/");
        }

        private void proginfToolStripMenuItem_Click(object sender, EventArgs e) //Program információ megjelenítése menü fül
        {
            MessageBox.Show(informacio_szoveg, nevjegy, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tabControl_DrawItem(object sender, DrawItemEventArgs e) //X bezáró gomb kirajzolása
        {
            e.Graphics.DrawString(this.tabControl.TabPages[e.Index].Text, e.Font, Brushes.Black, e.Bounds.Left + 12, e.Bounds.Top + 4);
            e.DrawFocusRectangle();
            e.Graphics.DrawString("X ", e.Font, Brushes.Black, e.Bounds.Right - 17, e.Bounds.Top + 4); //X kirajzolása 
        }

        private void tabControl_MouseDown(object sender, MouseEventArgs e) //X gombra kattintva bezárja az adott lapot
        {
            for (int i = 0; i < this.tabControl.TabPages.Count; i++) //Az összes lapon végigmegyünk egy for ciklussal
            {
                Rectangle r = tabControl.GetTabRect(i); //téglalap lekérése
                Rectangle xGomb = new Rectangle(r.Right - 15, r.Top + 4, 9, 7); //téglalap pozíció megadása
                if (xGomb.Contains(e.Location)) //Ha az egerünk benne van abban a téglalapban (amiben az X is van) akkor
                {
                    this.tabControl.TabPages.RemoveAt(i); //Bezárjuk a kiválasztott lapot
                }
            }
        }

        private void tabControl_Click(object sender, EventArgs e) //TabControl (lap kezelő) másik lapra való kattintás
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            urlSav.Text = chrm.Address; //A Chrm_AddressChanged tab váltás esetén nem képes lekezelni a változást, így itt manuálisan állítjuk be az url sáv szövegét
        }

        private void visszaGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                if (chrm.CanGoBack) //Csak akkor menjen vissza ha vissza tud, így nem lesz leállás
                {
                    chrm.Back(); //Visszalépés
                }
            }
        }

        private void eloreGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                if (chrm.CanGoForward) //Csak akkor menjen előre ha előre tud, így nem lesz leállás
                {
                    chrm.Forward(); //Előre lépés
                }
            }
        }

        private void ujraGomb_Click(object sender, EventArgs e) //Weblap újratöltése
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (chrm != null)
            {
                chrm.Refresh(); //Újratöltés
                chrm.Load(urlSav.Text); //Néha nem elég a refresh bizonyos weboldalaknál, így egy load-ot is tettem ide
            }
        }

        private void urlKeresGomb_Click(object sender, EventArgs e) //Url kerés gomb
        {
            mehet();
        }

        private void googleKeresGomb_Click(object sender, EventArgs e) //Google keresés gomb
        {
            mehet2();
        }

        private void kezdoGomb_Click(object sender, EventArgs e)
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            if (File.Exists("kezdolap.ini")) //Ha létezik a fájl tehát módosítottuk már a kezdőlapot
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    chrm.Load(reader.ReadLine()); //Akkor azt töltse be
                }
            }
            else
            {
                chrm.Load(kezdo_oldal); //ellenkező esetben az alapot
            }
        }

        private void infGomb_Click(object sender, EventArgs e) //Beállítások gomb
        {
            contextMenuStrip1.Show(infGomb, new Point(infGomb.Width - contextMenuStrip1.Width, infGomb.Height)); //Egész legördülő menü megjelenítése

            //A következőkben a könyvjelzők gyors megjelenítésével kell foglalkozni, hogy ne kelljen a könyvjelző kezelőt megnyitni állandóan
            bookmarksToolStripMenuItem.Enabled = false; //Az elején tegyük a gyors megjelenítőt kikapcsoltra mert még nem biztos hogy van könyvjelző
            if (File.Exists("konyvjelzok.ini")) //Ha a fájl létezik
            {
                bookmarksToolStripMenuItem.DropDown.Items.Clear(); //Egy törlés az előzőekre mert betöltünk mindent újra, így nem lesz duplikáció, illetve lehet változott valami azóta
                using (StreamReader reader = new StreamReader("konyvjelzok.ini")) //Mivel fentebb megbizonyosodtunk hogy létezik így beolvassuk a fájlt
                {
                    while (!reader.EndOfStream)
                    {
                        string sor = reader.ReadLine();
                        if (sor == null) //Ha esetleg lenne a végén egy üres sor akkor azt hagyja figyelmen kívül
                            break;
                        bookmarksToolStripMenuItem.DropDown.Items.Add(sor, null, Bm_Kat); //Hozzáadjuk a sorokat a könyvjelzők legördülő menüjébe (DropDown_Click majd a kattintás miatt kell)

                    }
                }
                bookmarksToolStripMenuItem.Enabled = true; //És engedélyezzük a könyvjelzők gyors megjelenítését, kattintható lesz
            }
        }

        private void Bm_Kat(object sender, EventArgs e) //Könyvjelző kattintásra nyissa meg a weboldalt, a senderből tudjuk melyikre kattint
        {
            ToolStripItem link = sender as ToolStripItem;
            if (link != null)
            {
                ChromiumWebBrowser chrm = getCurrentChrm();
                chrm.DownloadHandler = new DownloadHandler();
                chrm.Load(link.Text);
            }
        }

        private void ujLapGomb_Click(object sender, EventArgs e) //Új lap létrehozása
        {
            TabPage tab = new TabPage(); //tab objectum létrehozása
            tabControl.Controls.Add(tab); //lap hozzáadása
            tabControl.SelectTab(tabControl.TabCount - 1); //kiválasztása
            
            //Szoksásos Chromium betöltése azt új lapra mint fentebb a form loadnál (nem kommentelem újra, ugyanaz)
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

        private void urlSav_KeyDown(object sender, KeyEventArgs e) //Mivel sokan nem szeretnek a keresés gombra kattintani, ezért ha leütjük az entert akkor is "mehet()"
        {
            if (e.KeyCode == Keys.Enter)
            {
                mehet();
            }
        }

        private void googleKeresSav_KeyDown(object sender, KeyEventArgs e) //Mivel sokan nem szeretnek a keresés gombra kattintani, ezért ha leütjük az entert akkor is "mehet2()"
        {
            if (e.KeyCode == Keys.Enter)
            {
                mehet2();
            }
        }


        //Fent deklarált tooltip segítségével kiírjuk a gombok nevét ha fölé visszük az egeret (mert ugye ikonok vannak csak a gombon)
        private void visszaGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.visszaGomb, vissza);
        }

        private void eloreGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.eloreGomb, elore);
        }

        private void ujraGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.ujraGomb, ujra);
        }

        private void urlKeresGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.urlKeresGomb, keres);
        }

        private void googleKeresGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.googleKeresGomb, keres);
        }

        private void kezdoGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.kezdoGomb, kezdolap);
        }

        private void infGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.infGomb, beallitasok);
        }

        private void ujLapGomb_MouseHover(object sender, EventArgs e)
        {
            tp.SetToolTip(this.ujLapGomb, uj_lap);
        }

        public void bmLoad(string link) //Könyvjelző betöltése a könyvjelző kezelő formról
        {
            ChromiumWebBrowser chrm = getCurrentChrm();
            chrm.DownloadHandler = new DownloadHandler();
            chrm.Load(link);
        }
        public string bmUrlText() //Könyvjelző kezelő form letudja ezzel kérdezni a jelenlegi url-t
        {
            return urlSav.Text;
        }

        private void homepageToolStripMenuItem_Click(object sender, EventArgs e) //Kezdőlap váltás menü gomb
        {
            //Ha van kezdolap.ini akkor abból olvassa be a jelenlegi, ellenkező esetben marad az alap
            string http_text = kezdo_oldal;
            if (File.Exists("kezdolap.ini"))
            {
                using (StreamReader reader = new StreamReader("kezdolap.ini"))
                {
                    http_text = reader.ReadLine();
                }
            }

            //inputbox segítségével a felhasználó megadhatja az új kezdőlapot
            var ib = Interaction.InputBox(uj_kezdolap_cime, kezdolap_valtas, http_text);
            if (ib != "")
            { 
                using (StreamWriter sw = new StreamWriter("kezdolap.ini", false))
                {
                    sw.WriteLine(ib);
                }
            }
        }

        private void bookmarkseditToolStripMenuItem_Click(object sender, EventArgs e) //Könyvjelző kezelő form megnyitása
        {
            Bookmarks bm = new Bookmarks(nyelv);//átadjuk neki a nyelvet, így ott is a jelenlegi nyelven lesznek a szövegek
            bm.Show(); //Megjelenítjük a formot
        }

        private void hu_enToolStripMenuItem_Click(object sender, EventArgs e) //Nyelv váltás menü
        {
            DialogResult dialogResult = MessageBox.Show(change_lang_text, change_lang, MessageBoxButtons.YesNo, MessageBoxIcon.Question); //Messagebox-al megkérdezzük hogy akarja-e
            if (dialogResult == DialogResult.Yes) //Ha a válasz igen
            {
                using (StreamWriter sw = new StreamWriter("nyelv.ini", false))
                {
                    //Beleírjuk a másik nyelvet a nyelv.ini-be
                    if (nyelv == "hu")
                    {
                        sw.Write("en");
                    }
                    else
                    {
                        sw.Write("hu");
                    }
                }
                System.Diagnostics.Process.Start(Application.ExecutablePath); //Elindítjuk újra a programot ami már az új nyelven nyílik meg
                Environment.Exit(0); //A jelenlegit bezárjuk
            }
        }
    }
}
