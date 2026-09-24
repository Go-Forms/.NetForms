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

namespace FrameworkApp
{
    public partial class Form1 : Form
    {
        // Written for Windows: a backslash path next to the executable.
        private readonly Image closed = Image.FromFile(Directory.GetCurrentDirectory() + @"\Picture\closed.png");

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            label1.Text = File.ReadAllText(@"Data\score.txt") + Legacy.Score.Best;
        }
    }
}
