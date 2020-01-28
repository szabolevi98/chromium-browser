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

            string nyelv = "hu";

            if (File.Exists("nyelv.ini"))
            {
                using (StreamReader reader = new StreamReader("nyelv.ini"))
                {
                    nyelv = reader.ReadLine();
                }
            }

            Application.Run(new Form1(nyelv));
        }
    }
}
