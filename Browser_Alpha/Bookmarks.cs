using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO; //Fájlkezelés
using Microsoft.VisualBasic; //InputBox használatához kell, mert az a VisualBasic része

namespace Browser_Alpha
{
    public partial class Bookmarks : Form
    {
        private bool valtozott = false; //Igaz/Hamis változó hogy a végén csak akkor írjunk a fájlba ha változott valami

        public string nincs_kivalasztva;
        public string hiba;
        public string webhely_cime;
        public string konyvjelzo_hozzaadasa;
        public string nem_url;
        public string konyvjelzok;
        public string hozza_ad;
        public string eltavolit;
        public string eltavolit_mindent;

        public string nyelv;

        public Bookmarks(string Nyelv) //Konstruktor ami megkapja a nyelvet
        {
            this.nyelv = Nyelv;
            if (nyelv == "hu")
            {
                nincs_kivalasztva = "Nincs semmi kiválasztva!";
                webhely_cime = "Webhely címe";
                konyvjelzo_hozzaadasa = "Könyvjelző hozzáadása";
                nem_url = "Ez nem url cím!";
                konyvjelzok = "Könyvjelzők";
                hozza_ad = "Hozzáad";
                eltavolit = "Eltávolít";
                eltavolit_mindent = "Eltávolít mindent";
            }
            else if (nyelv == "en")
            {
                nincs_kivalasztva = "Please select something!";
                webhely_cime = "Webpage address";
                konyvjelzo_hozzaadasa = "Add bookmark";
                nem_url = "This is not an url address!";
                konyvjelzok = "Bookmarks";
                hozza_ad = "Add";
                eltavolit = "Remove";
                eltavolit_mindent = "Remove all";
            }
            else //Ismeretlen nyelv nyelv
            {
                MessageBox.Show("not_valid_lang", "nyelv.ini", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
            }
            InitializeComponent();
        }

        private void Bookmarks_Load(object sender, EventArgs e)
        {
            Text = konyvjelzok;
            hozzaAdGomb.Text = hozza_ad;
            eltavolitGomb.Text = eltavolit;
            eltavolitMindetGomb.Text = eltavolit_mindent;

            if (File.Exists("konyvjelzok.ini")) //Könyvjelzők feltöltése fájlból a listbox-ba ha létezik
            {
                using (StreamReader reader = new StreamReader("konyvjelzok.ini"))
                {
                    while (!reader.EndOfStream)
                    {
                        string sor = reader.ReadLine();
                        if (sor == null)
                            break;
                        listBox1.Items.Add(sor);
                    }
                }
            }
        }

        private void eltavolitMindetGomb_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            valtozott = true;
        }

        private void eltavolitGomb_Click(object sender, EventArgs e)
        {
            try
            {
                listBox1.Items.RemoveAt(listBox1.SelectedIndex);
                valtozott = true;
            }
            catch
            {
                MessageBox.Show(nincs_kivalasztva, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }

        private void hozzaAdGomb_Click(object sender, EventArgs e)
        {
            string url = "http://www.";
            if (Application.OpenForms["Form1"] != null)
            {
                url = (Application.OpenForms["Form1"] as Browser).bmUrlText(); //A jelenlegi megnyitott url-t írja be alapnak hozzáadásnál
            }

            var ib = Interaction.InputBox(webhely_cime, konyvjelzo_hozzaadasa, url); //VisualBasic inputbox az url címek megadásához
            if (ib.Contains(".") || ib == "localhost") //Ha nincs benne pont vagy a megadott szöveg nem "localhost", akkor nem lehet url cím.
            { 
                listBox1.Items.Add(ib); //A felhasználó által megadott adatot adjuk hozzá a listbox-hoz
                using (StreamWriter writer = new StreamWriter("konyvjelzok.ini", true))
                {
                    writer.WriteLine(ib);
                    valtozott = true;
                }
            }
            else
            {
                MessageBox.Show(nem_url, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e) //Dupla kattintással töltsük be azt az oldalt amire kattintott majd zárjuk be a könyvjelző kezelőt
        {
            if (Application.OpenForms["Form1"] != null)
            {
                (Application.OpenForms["Form1"] as Browser).bmLoad(listBox1.GetItemText(listBox1.SelectedItem));
            }
            this.Close();

        }

        private void Bookmarks_FormClosing(object sender, FormClosingEventArgs e) //Könyvjelzők fájlba írása
        {
            if (valtozott) //Csak akkor írjon ha történt változás
            {
                using (StreamWriter writer = new StreamWriter("konyvjelzok.ini", false))
                {
                    foreach (var item in listBox1.Items)
                    {
                        writer.WriteLine(item.ToString());
                    }
                }
            }
        }
    }
}
