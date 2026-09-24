using System;
using System.Drawing;
using System.Windows.Forms;
public partial class Form1: Form
{
    public Form1()
    {
        InitializeComponent();
    }

    private int RectSZ = 10;

    private GenerationLabirint labirint;

    private void Form1_Load(object sender, EventArgs e)
    {
        pictureBox.Image = new Bitmap(Width, Height);
    }

    private void button1_Click(object sender, EventArgs e)
    {
        groupBox.Location = new Point(-400, -400);
        groupBox.Enabled = false;

        RectSZ = int.TryParse(Size.Text, out int SZ) ? SZ : 0;

        var x = int.TryParse(XPos.Text, out int X) ? Limit(X, Width / RectSZ) : 0;
        var y = int.TryParse(YPos.Text, out int Y) ? Limit(Y, Height / RectSZ) : 0;

        labirint = new GenerationLabirint(pictureBox, timer1, new Point(x, y), RectSZ);

        Start();
    }
    private int Limit(int number, int MaxDiapazon)
    {
        return number < 0 ? 0 : number >= MaxDiapazon ? MaxDiapazon - 1 : number;
    }
    private void Start()
    {
        if (FastGenerate.Checked)
        {
            labirint.GenerationFull();
        }
        else
        {
            timer1.Start();
        }
    }
    private void Timer1_Tick(object sender, EventArgs e)
    {
        labirint.GenerationOnTick();
    }
}