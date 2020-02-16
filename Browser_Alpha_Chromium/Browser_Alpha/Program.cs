using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Browser_Alpha
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string nyelv = ""; 

            if (File.Exists("nyelv.ini")) //Ha a nyelv.ini létezik
            {
                using (StreamReader reader = new StreamReader("nyelv.ini"))
                {
                    nyelv = reader.ReadLine(); //Beolvassuk abból
                }
            }
            else //Ha nem
            {
                nyelv = "hu"; //Legyen magyar
            }

            Application.Run(new Form1(nyelv)); //Form1 megnyitása az adott nyelven
        }
    }
}
