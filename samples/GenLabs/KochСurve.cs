using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

class KochСurve
{
    public KochСurve(Point start, Point end)
    {
        lines.Add(new Line(start, end));
    }
    List<Line> lines = new List<Line>();
    public int N = 0;

    public void Draw(Graphics g, Pen pen)
    {
        foreach (var line in lines)
        {
            line.Draw(g, pen);
        }
    }
    public void SetN(int n)
    {
        if (n < 0 || n > 6)
        {
            MessageBox.Show("N can be negative and no more than 6");
            return;
        }
        else if (n > N)
        {
            for (int i = 0; i < n - N; i++)
            {
                SpliteLineUp();
            }
            N = n;
        }
        else if (n < N)
        {
            for (int i = 0; i < N - n; i++)
            {
                SpliteLineDown();
            }
            N = n;
        }
    }
    private void SpliteLineUp()
    {
        var newLines = new List<Line>();
        foreach (var item in lines)
        {
            var temp = item.Split();
            newLines.AddRange(temp);
        }
        lines = newLines;
    }
    private void SpliteLineDown()
    {
        var newLines = new List<Line>();
        for (int i = 0; i < lines.Count; i += 4)
        {
            newLines.Add(new Line(lines[i].Start, lines[i + 3].End));
        }
        lines = newLines;
    }
}
