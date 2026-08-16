using System.Globalization;
using System.Text;

namespace TimeServerStressTest;

public static class PdfReportExporter
{
    private const int PageWidth = 792;
    private const int PageHeight = 612;
    private const int FontObjectNumber = 1;
    private const int ImageObjectNumber = 2;

    public static void Save(string path, IReadOnlyList<StressTestResult> results, byte[] chartJpeg, Size chartSize, NtpEndpoint endpoint, DateTime generatedAt)
    {
        var pages = CreatePages(results, endpoint, generatedAt);
        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(CreateStreamObject($"<< /Type /XObject /Subtype /Image /Width {chartSize.Width} /Height {chartSize.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {chartJpeg.Length} >>", chartJpeg));

        var pageObjectNumbers = new List<int>();
        for (var index = 0; index < pages.Count; index++)
        {
            var contentObjectNumber = objects.Count + 1;
            objects.Add(CreateStreamObject($"<< /Length {Encoding.ASCII.GetByteCount(pages[index])} >>", Encoding.ASCII.GetBytes(pages[index])));
            var pageObjectNumber = objects.Count + 1;
            pageObjectNumbers.Add(pageObjectNumber);
            var resources = index == 0
                ? $"<< /Font << /F1 {FontObjectNumber} 0 R >> /XObject << /Im0 {ImageObjectNumber} 0 R >> >>"
                : $"<< /Font << /F1 {FontObjectNumber} 0 R >> >>";
            objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Page /Parent {{PAGES}} 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources {resources} /Contents {contentObjectNumber} 0 R >>"));
        }

        var pagesObjectNumber = objects.Count + 1;
        for (var index = 0; index < pageObjectNumbers.Count; index++)
        {
            var pageIndex = pageObjectNumbers[index] - 1;
            var page = Encoding.ASCII.GetString(objects[pageIndex]).Replace("{PAGES}", pagesObjectNumber.ToString(CultureInfo.InvariantCulture));
            objects[pageIndex] = Encoding.ASCII.GetBytes(page);
        }

        var kids = string.Join(' ', pageObjectNumbers.Select(number => $"{number} 0 R"));
        objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {pageObjectNumbers.Count} >>"));
        var catalogObjectNumber = objects.Count + 1;
        objects.Add(Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages {pagesObjectNumber} 0 R >>"));

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            writer.Write(objects[index]);
            writer.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var crossReferenceOffset = stream.Position;
        writer.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        for (var index = 1; index < offsets.Count; index++)
        {
            writer.Write(Encoding.ASCII.GetBytes($"{offsets[index]:D10} 00000 n \n"));
        }

        writer.Write(Encoding.ASCII.GetBytes($"trailer\n<< /Size {objects.Count + 1} /Root {catalogObjectNumber} 0 R >>\nstartxref\n{crossReferenceOffset}\n%%EOF"));
    }

    private static List<string> CreatePages(IReadOnlyList<StressTestResult> results, NtpEndpoint endpoint, DateTime generatedAt)
    {
        var pages = new List<string>();
        var firstPage = new StringBuilder();
        var title = "NTP Stress Test Results for " + endpoint.Host;
        AddText(firstPage, (int)Math.Round((PageWidth - MeasureHelveticaTextWidth(title, 14)) / 2), 576, 14, title);
        firstPage.Append("q 720 0 0 205 36 351 cm /Im0 Do Q\n");
        AddTableHeader(firstPage, 333);
        var currentPage = firstPage;
        var rowY = 317;

        foreach (var result in results)
        {
            if (rowY < 50)
            {
                pages.Add(currentPage.ToString());
                currentPage = new StringBuilder();
                AddText(currentPage, 36, 576, 14, "NTP Stress Test Results");
                AddTableHeader(currentPage, 552);
                rowY = 536;
            }

            AddResultRow(currentPage, rowY, result);
            rowY -= 14;
        }

        AddText(currentPage, 36, 34, 8, $"Time Server Port: {endpoint.Port}");

        if (results.Count > 0)
        {
            AddText(currentPage, 36, 20, 8, $"Tests started: {results[0].Started:G}    Ended: {results[^1].Ended:G}");
        }

      //  AddText(currentPage, 36, 20, 8, $"Report generated: {generatedAt:G}");
        pages.Add(currentPage.ToString());
        return pages;
    }

    private static void AddTableHeader(StringBuilder content, int y)
    {
        AddText(content, 36, y, 7, "Concurrent Requests");
        AddText(content, 112, y, 7, "Total Requests");
        AddText(content, 174, y, 7, "Requests / Sec.");
        AddText(content, 244, y, 7, "Successes");
        AddText(content, 300, y, 7, "Failures");
        AddText(content, 350, y, 7, "Success Rate");
        AddText(content, 417, y, 7, "Started");
        AddText(content, 537, y, 7, "Ended");
        content.AppendFormat(CultureInfo.InvariantCulture, "36 {0} m 756 {0} l S\n", y - 3);
    }

    private static void AddResultRow(StringBuilder content, int y, StressTestResult result)
    {
        AddText(content, 36, y, 7, result.Workers.ToString("N0"));
        AddText(content, 112, y, 7, result.TotalRequests.ToString("N0"));
        AddText(content, 174, y, 7, result.RequestsPerSecond.ToString("N2"));
        AddText(content, 244, y, 7, result.SuccessfulRequests.ToString("N0"));
        AddText(content, 300, y, 7, result.FailedRequests.ToString("N0"));
        AddText(content, 350, y, 7, $"{result.SuccessRate:N2}%");
        AddText(content, 417, y, 7, result.Started.ToString("G"));
        AddText(content, 537, y, 7, result.Ended.ToString("G"));
    }

    private static void AddText(StringBuilder content, int x, int y, int fontSize, string value)
    {
        content.AppendFormat(CultureInfo.InvariantCulture, "BT /F1 {0} Tf {1} {2} Td ({3}) Tj ET\n", fontSize, x, y, Escape(value));
    }

    private static double MeasureHelveticaTextWidth(string value, int fontSize)
    {
        var glyphUnits = value.Sum(character => character switch
        {
            ' ' => 278,
            '.' => 278,
            '-' => 333,
            >= '0' and <= '9' => 556,
            'A' or 'B' or 'E' or 'K' or 'P' or 'S' or 'X' => 667,
            'C' or 'D' or 'H' or 'N' or 'R' or 'U' => 722,
            'F' or 'T' or 'Z' => 611,
            'G' => 778,
            'I' => 278,
            'J' => 500,
            'L' => 556,
            'M' => 833,
            'O' or 'Q' => 778,
            'V' => 667,
            'W' => 944,
            'Y' => 667,
            'a' or 'b' or 'd' or 'e' or 'g' or 'h' or 'n' or 'o' or 'p' or 'q' or 'u' => 556,
            'c' or 'k' or 's' or 'v' or 'x' or 'z' => 500,
            'f' or 't' => 278,
            'i' or 'j' or 'l' => 222,
            'm' => 833,
            'r' => 333,
            'w' => 722,
            'y' => 500,
            _ => 667
        });
        return glyphUnits * fontSize / 1000d;
    }

    private static byte[] CreateStreamObject(string dictionary, byte[] content)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes($"{dictionary}\nstream\n"));
        stream.Write(content);
        stream.Write(Encoding.ASCII.GetBytes("\nendstream"));
        return stream.ToArray();
    }

    private static string Escape(string value)
    {
        return new string(value.Select(character => character <= 127 ? character : '?').ToArray())
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}
