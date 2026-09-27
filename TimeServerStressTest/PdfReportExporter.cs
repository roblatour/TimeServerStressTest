using System.Globalization;
using System.Text;

namespace TimeServerStressTest;

public static class PdfReportExporter
{
    private const int PageWidth = 792;
    private const int PageHeight = 612;
    private const int FontObjectNumber = 1;
    private const int ImageObjectNumber = 2;

    public static void Save(string path, IReadOnlyList<StressTestResult> results, byte[] chartJpeg, Size chartSize, NtpEndpoint endpoint, DateTime generatedAt, string notes, StressTestMode testMode, int testDurationSeconds, int maximumRequestsPerSecond)
    {
        var pages = CreatePages(results, endpoint, generatedAt, notes, testMode, testDurationSeconds, maximumRequestsPerSecond);
        WritePdf(path, pages, chartJpeg, chartSize);
    }

    internal static void SaveFunctional(string path, IReadOnlyList<FunctionalTestResult> results, NtpEndpoint endpoint, DateTime started, DateTime ended, string notes)
    {
        var pages = new List<string>();
        var content = new StringBuilder();
        AddText(content, 36, 576, 14, "NTP Functional Test Results for " + endpoint.Host);
        AddText(content, 36, 548, 10, "Functional tests");
        AddText(content, 36, 528, 9, "No.");
        AddText(content, 72, 528, 9, "Test");
        AddText(content, 410, 528, 9, "Result");
        var y = 510;
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (y < 100)
            {
                pages.Add(content.ToString());
                content = new StringBuilder();
                AddText(content, 36, 576, 14, "NTP Functional Test Results");
                AddText(content, 36, 548, 9, "No.");
                AddText(content, 72, 548, 9, "Test");
                AddText(content, 410, 548, 9, "Result");
                y = 530;
            }

            AddText(content, 36, y, 9, (index + 1).ToString(CultureInfo.InvariantCulture));
            AddText(content, 72, y, 9, result.Name);
            var color = result.Status switch
            {
                FunctionalTestStatus.Pass => "0 0.5 0",
                FunctionalTestStatus.Fail => "0.85 0 0",
                _ => "0.65 0.5 0"
            };
            content.Append(color).Append(" rg\n");
            AddText(content, 410, y, 9, result.Status switch
            {
                FunctionalTestStatus.Pass => "Pass",
                FunctionalTestStatus.Fail => "Fail",
                _ => "Could not run"
            });
            content.Append("0 0 0 rg\n");
            y -= 20;
        }

        var lines = notes.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').SelectMany(line => line.Length == 0 ? new[] { "" } : line.Chunk(95).Select(chars => new string(chars))).ToArray();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            if (y - (lines.Length + 1) * 12 < 70)
            {
                pages.Add(content.ToString());
                content = new StringBuilder();
                AddText(content, 36, 576, 14, "NTP Functional Test Results");
                y = 548;
            }
            y -= 12;
            AddText(content, 36, y, 8, "Notes:");
            foreach (var line in lines)
            {
                y -= 12;
                if (y < 64)
                {
                    pages.Add(content.ToString());
                    content = new StringBuilder();
                    AddText(content, 36, 576, 14, "NTP Functional Test Results");
                    y = 548;
                }
                AddText(content, 36, y, 8, line);
            }
        }

        AddText(content, 36, 34, 8, $"Time Server Port: {endpoint.Port}");
        AddText(content, 36, 20, 8, $"Tests started: {started:G}    Ended: {ended:G}");
        pages.Add(content.ToString());
        WritePdf(path, pages, null, default);
    }

    private static void WritePdf(string path, IReadOnlyList<string> pages, byte[]? chartJpeg, Size chartSize)
    {
        var objects = new List<byte[]>();
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        if (chartJpeg is not null)
        {
            objects.Add(CreateStreamObject($"<< /Type /XObject /Subtype /Image /Width {chartSize.Width} /Height {chartSize.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {chartJpeg.Length} >>", chartJpeg));
        }

        var pageObjectNumbers = new List<int>();
        for (var index = 0; index < pages.Count; index++)
        {
            var contentObjectNumber = objects.Count + 1;
            objects.Add(CreateStreamObject($"<< /Length {Encoding.ASCII.GetByteCount(pages[index])} >>", Encoding.ASCII.GetBytes(pages[index])));
            var pageObjectNumber = objects.Count + 1;
            pageObjectNumbers.Add(pageObjectNumber);
            var resources = index == 0 && chartJpeg is not null
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

    private static List<string> CreatePages(IReadOnlyList<StressTestResult> results, NtpEndpoint endpoint, DateTime generatedAt, string notes, StressTestMode testMode, int testDurationSeconds, int maximumRequestsPerSecond)
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
            if (rowY < 64)
            {
                pages.Add(currentPage.ToString());
                currentPage = new StringBuilder();
                AddText(currentPage, 36, 576, 14, "NTP Stress Test Results");
                AddTableHeader(currentPage, 552);
                rowY = 536;
            }

            AddResultRow(currentPage, rowY, result);
            rowY -= 16;
        }

        var noteLines = string.IsNullOrWhiteSpace(notes)
            ? []
            : notes.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .SelectMany(line => line.Length == 0 ? [string.Empty] : line.Chunk(95).Select(characters => new string(characters)))
                .ToArray();
        (double Minimum, double Maximum)? keyResults = results.Count == 0
            ? null
            : (results.Min(result => result.SuccessfulRequestsPerSecond), results.Max(result => result.SuccessfulRequestsPerSecond));
        var detailLineCount = 3 + (keyResults is null ? 0 : 2) + (noteLines.Length == 0 ? 0 : noteLines.Length + 1);
        if (rowY - (detailLineCount - 1) * 12 < 50)
        {
            pages.Add(currentPage.ToString());
            currentPage = new StringBuilder();
            AddText(currentPage, 36, 576, 14, "NTP Stress Test Results");
            rowY = 552;
        }

        AddText(currentPage, 36, rowY, 8, $" ");
        rowY -= 12;
        AddText(currentPage, 36, rowY, 8, $"Options: Test mode: {testMode}; Test duration: {testDurationSeconds:N0} seconds; Max requests/second: {maximumRequestsPerSecond:N0}");
        rowY -= 12;
        AddText(currentPage, 36, rowY, 8, $" ");

        if (keyResults is not null)
        {
            rowY -= 12;
            AddText(currentPage, 36, rowY, 8, $"Key results: Minimum Successful Requests/Second {keyResults.Value.Minimum:N2}; Maximum Successful Requests/Second {keyResults.Value.Maximum:N2}");
            rowY -= 12;
            AddText(currentPage, 36, rowY, 8, $" ");
        }

        if (noteLines.Length > 0)
        {
            rowY -= 12;
            AddText(currentPage, 36, rowY, 8, "Notes:");
            foreach (var line in noteLines)
            {
                rowY -= 12;
                AddText(currentPage, 36, rowY, 8, line);
            }
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
        AddText(content, 174, y, 7, "Requests/Second");
        AddText(content, 244, y, 7, "Successes");
        AddText(content, 300, y, 7, "Failures");
        AddText(content, 350, y, 7, "Losses");
        AddText(content, 395, y, 7, "Success Rate");
        AddText(content, 460, y, 7, "Successful Requests/Second");
        AddText(content, 570, y, 7, "Started");
        AddText(content, 680, y, 7, "Ended");
        content.AppendFormat(CultureInfo.InvariantCulture, "36 {0} m 756 {0} l S\n", y - 3);
    }

    private static void AddResultRow(StringBuilder content, int y, StressTestResult result)
    {
        AddText(content, 36, y, 7, result.Workers.ToString("N0"));
        AddText(content, 112, y, 7, result.TotalRequests.ToString("N0"));
        AddText(content, 174, y, 7, result.IsSingleRequest ? "N/A" : result.RequestsPerSecond.ToString("N2"));
        AddText(content, 244, y, 7, result.SuccessfulRequests.ToString("N0"));
        AddText(content, 300, y, 7, result.FailedRequests.ToString("N0"));
        AddText(content, 350, y, 7, result.LostRequests.ToString("N0"));
        AddText(content, 395, y, 7, $"{result.SuccessRate:N2}%");
        AddText(content, 460, y, 7, result.SuccessfulRequestsPerSecond.ToString("N2"));
        AddText(content, 570, y, 7, result.Started.ToString("G"));
        AddText(content, 680, y, 7, result.Ended.ToString("G"));
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
