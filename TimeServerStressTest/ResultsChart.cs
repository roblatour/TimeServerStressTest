using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TimeServerStressTest;

public sealed class ResultsChart : Control
{
    private static readonly Color SuccessfulColor = Color.FromArgb(0, 96, 106);
    private static readonly Color FailedColor = Color.FromArgb(230, 126, 34);
    private IReadOnlyList<StressTestResult> results = Array.Empty<StressTestResult>();

    public ResultsChart()
    {
        BackColor = Color.White;
        DoubleBuffered = true;
        ResizeRedraw = true;
        MinimumSize = new Size(600, 260);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<StressTestResult> Results
    {
        get => results;
        set
        {
            results = value ?? Array.Empty<StressTestResult>();
            Invalidate();
        }
    }

    public void SavePng(string path)
    {
        using var bitmap = CreateBitmap();
        bitmap.Save(path, ImageFormat.Png);
    }

    public byte[] CreateJpeg(out Size size)
    {
        using var bitmap = CreateBitmap();
        size = bitmap.Size;
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Jpeg);
        return stream.ToArray();
    }

    private Bitmap CreateBitmap()
    {
        var bitmap = new Bitmap(Math.Max(Width, MinimumSize.Width), Math.Max(Height, MinimumSize.Height));
        using var graphics = Graphics.FromImage(bitmap);
        DrawChart(graphics, new Rectangle(Point.Empty, bitmap.Size), useSparseWorkerLabels: true);
        return bitmap;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawChart(e.Graphics, ClientRectangle, useSparseWorkerLabels: true);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        Invalidate();
    }

    private void DrawChart(Graphics graphics, Rectangle bounds, bool useSparseWorkerLabels)
    {
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var axisBrush = new SolidBrush(ForeColor);
        using var gridPen = new Pen(Color.FromArgb(225, 229, 232));
        using var axisPen = new Pen(Color.FromArgb(130, 140, 145));
        using var successfulBrush = new SolidBrush(SuccessfulColor);
        using var failedBrush = new SolidBrush(FailedColor);

        const int left = 72;
        const int top = 54;
        const int right = 22;
        const int bottom = 50;
        var plot = Rectangle.FromLTRB(bounds.Left + left, bounds.Top + top, bounds.Right - right, bounds.Bottom - bottom);
        if (plot.Width < 20 || plot.Height < 20)
        {
            return;
        }

        const int gridCount = 5;
        var maximum = Math.Max(1d, results.Select(result => result.SuccessfulRequestsPerSecond + result.FailedRequestsPerSecond).DefaultIfEmpty().Max());
        DrawLegend(graphics, successfulBrush, failedBrush, plot.Right - 210, bounds.Top + 12);

        for (var index = 0; index <= gridCount; index++)
        {
            var y = plot.Bottom - plot.Height * index / gridCount;
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);

            if (results.Count > 0)
            {
                var label = Math.Round(maximum * index / gridCount).ToString("N0");
                var labelSize = graphics.MeasureString(label, Font);
                graphics.DrawString(label, Font, axisBrush, plot.Left - labelSize.Width - 8, y - labelSize.Height / 2);
            }
        }

        graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
        graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        var yAxisLabel = "Requests / Second";
        var yAxisLabelSize = graphics.MeasureString(yAxisLabel, Font);
        var graphicsState = graphics.Save();
        graphics.TranslateTransform(bounds.Left + 18, plot.Top + plot.Height / 2);
        graphics.RotateTransform(-90);
        graphics.DrawString(yAxisLabel, Font, axisBrush, -yAxisLabelSize.Width / 2, -yAxisLabelSize.Height / 2);
        graphics.Restore(graphicsState);

        var groupWidth = (double)plot.Width / results.Count;
        var barWidth = Math.Max(2d, Math.Min(40d, groupWidth * 0.6));
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var center = plot.Left + groupWidth * (index + 0.5);
            DrawStackedBar(
                graphics,
                successfulBrush,
                failedBrush,
                center - barWidth / 2,
                result.SuccessfulRequestsPerSecond,
                result.FailedRequestsPerSecond,
                maximum,
                plot,
                barWidth);

            if (!useSparseWorkerLabels || results.Count == 1 || result.Workers % 2 == 0)
            {
                var label = result.Workers.ToString("N0");
                var labelSize = graphics.MeasureString(label, Font);
                graphics.DrawString(label, Font, axisBrush, (float)(center - labelSize.Width / 2), plot.Bottom + 7);
            }
        }

        var axisLabel = "Concurrent Requests";
        var axisLabelSize = graphics.MeasureString(axisLabel, Font);
        graphics.DrawString(axisLabel, Font, axisBrush, plot.Left + (plot.Width - axisLabelSize.Width) / 2, bounds.Bottom - axisLabelSize.Height - 4);
    }

    private void DrawLegend(Graphics graphics, Brush successfulBrush, Brush failedBrush, int left, int top)
    {
        graphics.FillRectangle(successfulBrush, left, top + 3, 12, 12);
        graphics.DrawString("Successful", Font, Brushes.Black, left + 17, top);
        graphics.FillRectangle(failedBrush, left + 105, top + 3, 12, 12);
        graphics.DrawString("Unsuccessful", Font, Brushes.Black, left + 122, top);
    }

    private static void DrawStackedBar(
        Graphics graphics,
        Brush successfulBrush,
        Brush failedBrush,
        double left,
        double successfulValue,
        double failedValue,
        double maximum,
        Rectangle plot,
        double width)
    {
        var successfulHeight = Math.Max(0, Math.Min(plot.Height, plot.Height * successfulValue / maximum));
        var failedHeight = Math.Max(0, Math.Min(plot.Height - successfulHeight, plot.Height * failedValue / maximum));
        graphics.FillRectangle(successfulBrush, (float)left, (float)(plot.Bottom - successfulHeight), (float)width, (float)successfulHeight);
        graphics.FillRectangle(failedBrush, (float)left, (float)(plot.Bottom - successfulHeight - failedHeight), (float)width, (float)failedHeight);
    }
}
