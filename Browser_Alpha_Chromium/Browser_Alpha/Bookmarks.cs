using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace Browser_Alpha
{
    public partial class Bookmarks : Form
    {

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

        public Bookmarks(string Nyelv)
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
            else
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

            if (File.Exists("konyvjelzok.ini"))
            {
                using (StreamReader reader = new StreamReader("konyvjelzok.ini"))
                {
                    while (true)
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
        }

        private void eltavolitGomb_Click(object sender, EventArgs e)
        {
            try
            {
                listBox1.Items.RemoveAt(listBox1.SelectedIndex);
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
                url = (Application.OpenForms["Form1"] as Form1).bmUrlText();
            }

            var ib = Interaction.InputBox(webhely_cime, konyvjelzo_hozzaadasa, url);
            if (ib.Contains("."))
            { 
                listBox1.Items.Add(ib);
                using (StreamWriter sw = new StreamWriter("konyvjelzok.ini", true))
                {
                    sw.WriteLine(ib);
                }
            }
            else
            {
                MessageBox.Show(nem_url, hiba, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (Application.OpenForms["Form1"] != null)
            {
                (Application.OpenForms["Form1"] as Form1).bmLoad(listBox1.GetItemText(listBox1.SelectedItem));
            }
            this.Close();

        }

        private void Bookmarks_FormClosing(object sender, FormClosingEventArgs e)
        {
            using (StreamWriter sw = new StreamWriter("konyvjelzok.ini", false))
            {

                foreach (var item in listBox1.Items)
                {
                    sw.WriteLine(item.ToString());
                }
            }
        }
    }
}
