using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

public sealed class GenerationLabirint : IDisposable
{
    /// <summary>
    /// Создаёт генератор лабиринта с пошаговой генерацией.
    /// </summary>
    /// <param name="picture">Изображение для отрисовки.</param>
    /// <param name="timer">Таймер для пошаговой генерации.</param>
    /// <param name="startPosition">Начальная позиция генерации.</param>
    /// <param name="rectSize">Размер клетки.</param>
    public GenerationLabirint(
        PictureBox picture,
        Timer timer,
        Point startPosition,
        int rectSize)
    {
        pictureBox = picture ?? throw new ArgumentNullException(nameof(picture));
        this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
graphics = default!;
        CreateSettings(startPosition, rectSize);
    }

    /// <summary>
    /// Создаёт генератор лабиринта с пошаговой генерацией.
    /// </summary>
    /// <param name="picture">Изображение для отрисовки.</param>
    /// <param name="timer">Таймер для пошаговой генерации.</param>
    /// <param name="startPosition">Начальная позиция генерации.</param>
    /// <param name="rectSize">Размер клетки.</param>
    /// <param name="colorWall">Цвет стены/фона.</param>
    /// <param name="colorHead">Цвет текущей позиции.</param>
    public GenerationLabirint(
        PictureBox picture,
        Timer timer,
        Point startPosition,
        int rectSize,
        Brush colorWall,
        Brush colorHead)
    {
        pictureBox = picture ?? throw new ArgumentNullException(nameof(picture));
        this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
        ColorWall = colorWall ?? throw new ArgumentNullException(nameof(colorWall));
        ColorHead = colorHead ?? throw new ArgumentNullException(nameof(colorHead));
        graphics = default!;
        CreateSettings(startPosition, rectSize);
    }

    /// <summary>
    /// Создаёт генератор лабиринта только для полной генерации.
    /// </summary>
    /// <param name="picture">Изображение для отрисовки.</param>
    /// <param name="startPosition">Начальная позиция генерации.</param>
    /// <param name="rectSize">Размер клетки.</param>
    /// <param name="colorWall">Цвет стены/фона.</param>
    public GenerationLabirint(
        PictureBox picture,
        Point startPosition,
        int rectSize,
        Brush colorWall)
    {
        pictureBox = picture ?? throw new ArgumentNullException(nameof(picture));
        ColorWall = colorWall ?? throw new ArgumentNullException(nameof(colorWall));
        graphics = default!;
        CreateSettings(startPosition, rectSize);
    }

    /// <summary>
    /// Создаёт генератор лабиринта только для полной генерации.
    /// </summary>
    /// <param name="picture">Изображение для отрисовки.</param>
    /// <param name="startPosition">Начальная позиция генерации.</param>
    /// <param name="rectSize">Размер клетки.</param>
    public GenerationLabirint(
        PictureBox picture,
        Point startPosition,
        int rectSize)
    {
        pictureBox = picture ?? throw new ArgumentNullException(nameof(picture));
        graphics = default!;
        CreateSettings(startPosition, rectSize);
    }

    private readonly PictureBox pictureBox;
    private readonly Timer? timer;

    private Graphics graphics;
    private int rectSize;
    private int rectX;
    private int rectY;

    private int[,] screen = null!;
    private Block[,] blocks = null!;

    private readonly Random random = new();
    private readonly Stack<Point> positions = new();

    private Point nowPosition;
    private Point startPosition;
    private Point lastPosition;

    private Brush ColorWall { get; } = Brushes.Brown;
    private Brush ColorHead { get; } = Brushes.Aqua;

    private void CreateSettings(Point startPosition, int rectSize)
    {
        if (rectSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rectSize),
                rectSize,
                "Размер клетки должен быть больше нуля.");
        }

        if (pictureBox.Image == null)
        {
            throw new InvalidOperationException(
                "У PictureBox отсутствует Image.");
        }

        this.rectSize = rectSize;

        rectX = pictureBox.Width / rectSize;
        rectY = pictureBox.Height / rectSize;

        if (rectX <= 0 || rectY <= 0)
        {
            throw new InvalidOperationException(
                "Размер PictureBox недостаточен для заданного размера клетки.");
        }

        if (startPosition.X < 0 ||
            startPosition.X >= rectX ||
            startPosition.Y < 0 ||
            startPosition.Y >= rectY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startPosition),
                startPosition,
                "Начальная позиция находится за пределами поля.");
        }

        graphics = Graphics.FromImage(pictureBox.Image);

        screen = new int[rectX, rectY];
        blocks = new Block[rectX, rectY];

        for (int x = 0; x < rectX; x++)
        {
            for (int y = 0; y < rectY; y++)
            {
                blocks[x, y] = new Block(
                    rectSize,
                    new Point(x, y));
            }
        }

        this.startPosition = startPosition;
        nowPosition = startPosition;
        lastPosition = startPosition;
    }

    /// <summary>
    /// Выполняет один шаг генерации лабиринта.
    /// </summary>
    public void GenerationOnTick()
    {
        screen[nowPosition.X, nowPosition.Y] = 1;

        List<Point> points = GetPoints();

        NearDraw();

        if (points.Count == 0)
        {
            if (!GoBackPosition())
            {
                timer?.Stop();
                return;
            }

            if (nowPosition == startPosition)
            {
                NearDraw();
                timer?.Stop();
            }

            return;
        }

        GoNextPosition(points);
    }

    /// <summary>
    /// Полностью генерирует лабиринт.
    /// </summary>
    public void GenerationFull()
    {
        while (true)
        {
            screen[nowPosition.X, nowPosition.Y] = 1;

            List<Point> points = GetPoints();

            if (points.Count == 0)
            {
                if (!GoBackPosition())
                {
                    break;
                }

                if (nowPosition == startPosition)
                {
                    break;
                }

                continue;
            }

            GoNextPosition(points);
        }

        ForeachDraw();
    }

    private bool GoBackPosition()
    {
        if (positions.Count == 0)
        {
            return false;
        }

        lastPosition = nowPosition;
        nowPosition = positions.Pop();

        return true;
    }

    private void GoNextPosition(List<Point> points)
    {
        positions.Push(nowPosition);

        Point step = points[random.Next(points.Count)];

        DeleteWall(step);

        lastPosition = nowPosition;

        nowPosition = new Point(
            nowPosition.X + step.X,
            nowPosition.Y + step.Y);
    }

    private void DeleteWall(Point step)
    {
        if (step.Y == 1)
        {
            blocks[
                nowPosition.X + step.X,
                nowPosition.Y + step.Y
            ].UP = false;
        }
        else if (step.Y == -1)
        {
            blocks[
                nowPosition.X,
                nowPosition.Y
            ].UP = false;
        }
        else if (step.X == 1)
        {
            blocks[
                nowPosition.X + step.X,
                nowPosition.Y + step.Y
            ].LEFT = false;
        }
        else if (step.X == -1)
        {
            blocks[
                nowPosition.X,
                nowPosition.Y
            ].LEFT = false;
        }
    }

    private void NearDraw()
    {
        graphics.FillRectangle(
            ColorWall,
            lastPosition.X * rectSize,
            lastPosition.Y * rectSize,
            rectSize,
            rectSize);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Point offset = new(x, y);

                if (IsOutsideField(offset))
                {
                    continue;
                }

                blocks[
                    nowPosition.X + x,
                    nowPosition.Y + y
                ].Draw(graphics);
            }
        }

        graphics.FillRectangle(
            ColorHead,
            nowPosition.X * rectSize,
            nowPosition.Y * rectSize,
            rectSize,
            rectSize);

        pictureBox.Invalidate();
    }

    private void ForeachDraw()
    {
        graphics.FillRectangle(
            ColorWall,
            0,
            0,
            pictureBox.Width,
            pictureBox.Height);

        foreach (Block block in blocks)
        {
            block.Draw(graphics);
        }

        pictureBox.Invalidate();
    }

    private bool IsOutsideField(Point point)
    {
        int x = point.X + nowPosition.X;
        int y = point.Y + nowPosition.Y;

        return x < 0 ||
               x >= rectX ||
               y < 0 ||
               y >= rectY;
    }

    private List<Point> GetPoints()
    {
        List<Point> points = new();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                // Диагональные направления и (0, 0) не подходят.
                if (Math.Abs(x) == Math.Abs(y))
                {
                    continue;
                }

                Point offset = new(x, y);

                if (IsOutsideField(offset))
                {
                    continue;
                }

                if (screen[
                        nowPosition.X + x,
                        nowPosition.Y + y] == 1)
                {
                    continue;
                }

                points.Add(offset);
            }
        }

        return points;
    }

    public void Dispose()
    {
        graphics.Dispose();
    }
}
