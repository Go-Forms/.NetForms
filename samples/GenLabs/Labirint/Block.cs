using System.Drawing;


class Block
{
    public Block(int Size, Point CRD)
    {
        RectSZ = Size;
        this.CRD.X = CRD.X * RectSZ;
        this.CRD.Y = CRD.Y * RectSZ;
        pen = new Pen(brush, 1);
    }

    public int RectSZ;
    public Point CRD;

    public bool UP = true;
    public bool LEFT = true;
    private Brush brush = Brushes.Black;
    private Pen pen;
    public void Draw(Graphics g)
    {
        if (UP == true)
        {
            g.DrawLine(pen, CRD.X, CRD.Y, CRD.X + RectSZ, CRD.Y);
        }
        if (LEFT == true)
        {
            g.DrawLine(pen, CRD.X , CRD.Y, CRD.X, CRD.Y  + RectSZ);
        }
    }
}
