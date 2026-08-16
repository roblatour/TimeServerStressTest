using System.Diagnostics;
using System.Reflection;

namespace TimeServerStressTest;

public partial class Form1 : Form
{
    private const string HelpUrl = "https://github.com/roblatour/TimeServerStressTest";
    private readonly List<StressTestResult> workflowResults = [];
    private CancellationTokenSource? testCancellation;
    private int testDurationSeconds;
    private bool isTestRunning;
    private DateTime? workflowStarted;
    private DateTime? workflowEnded;
    private NtpEndpoint? workflowEndpoint;
    private bool suppressStressTestWarning;

    public Form1()
    {
        InitializeComponent();
        ConfigureCurrentTestStatistics();
        ConfigureResultsTable();
        Text = GetWindowTitle();
        serverAddressTextBox.Text = UserPreferences.LoadServerAddress();
        ntpPortNumericUpDown.Value = UserPreferences.LoadNtpPort();
        concurrentTestsNumericUpDown.Value = UserPreferences.LoadConcurrentTests();
        durationNumericUpDown.Value = UserPreferences.LoadTestDurationSeconds();
        testDurationSeconds = (int)durationNumericUpDown.Value;
        remainingProgressBar.Visible = false;
        multiTestProgressBar.Visible = false;
        ResetResults();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var workingArea = Screen.FromControl(this).WorkingArea;
        if (workingArea.Width < MinimumSize.Width || workingArea.Height < MinimumSize.Height)
        {
            return;
        }

        var size = new Size(Math.Min(Width, workingArea.Width), Math.Min(Height, workingArea.Height));
        if (size == Size)
        {
            return;
        }

        Size = size;
        Location = new Point(
            workingArea.Left + (workingArea.Width - Width) / 2,
            workingArea.Top + (workingArea.Height - Height) / 2);
    }

    private static string GetWindowTitle()
    {
        var assembly = typeof(Form1).Assembly;
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? Application.ProductName;
        var version = assembly.GetName().Version?.ToString(2) ?? "0.0";
        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        var license = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "License")?.Value ?? string.Empty;

        return $"{title} v{version} - {copyright} - {license}";
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        await StartWorkflowAsync(isMultiTest: false);
    }

    private async void MultiTestButton_Click(object? sender, EventArgs e)
    {
        await StartWorkflowAsync(isMultiTest: true);
    }

    private async Task StartWorkflowAsync(bool isMultiTest)
    {
        if (!NtpEndpoint.TryParse(serverAddressTextBox.Text, (int)ntpPortNumericUpDown.Value, out var endpoint))
        {
            MessageBox.Show(this, "Enter a valid host name or IP address + port.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            serverAddressTextBox.Focus();
            return;
        }

        if (!ConfirmStressTest())
        {
            return;
        }

        testDurationSeconds = (int)durationNumericUpDown.Value;
        var singleTestWorkers = (int)concurrentTestsNumericUpDown.Value;
        workflowStarted = null;
        workflowEnded = null;
        workflowEndpoint = endpoint;
        workflowResults.Clear();
        RefreshWorkflowResults();
        isTestRunning = true;
        testCancellation = new CancellationTokenSource();
        multiTestProgressBar.Visible = isMultiTest;
        multiTestProgressBar.Value = 0;
        SetTestState(isRunning: true);
        remainingProgressBar.Visible = true;

        try
        {


            var maximumWorkers = isMultiTest ? NtpStressRunner.MaximumConcurrentWorkers : singleTestWorkers;
            long maximumRequests = 0;
            var testEnded = DateTime.Now;
            for (var workers = isMultiTest ? 1 : singleTestWorkers; workers <= maximumWorkers; workers++)
            {
                if (testCancellation.IsCancellationRequested)
                {
                    break;
                }

                var result = await RunTestAsync(endpoint!, workers, maximumRequests);
                workflowResults.Add(result);
                testEnded = result.Ended;
                RefreshWorkflowResults();
                if (isMultiTest)
                {
                    multiTestProgressBar.Value = workers;
                }

                if (testCancellation.IsCancellationRequested || !isMultiTest || workers == maximumWorkers)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), testCancellation.Token);
            }
            workflowEnded = testEnded;  // testing here   


            RefreshWorkflowResults();

        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            isTestRunning = false;
            RefreshWorkflowResults();
            testCancellation?.Dispose();
            testCancellation = null;
            remainingProgressBar.Visible = false;
            multiTestProgressBar.Visible = false;
            SetTestState(isRunning: false);
        }
    }

    private async Task<StressTestResult> RunTestAsync(NtpEndpoint endpoint, int workers, long maximumRequests)
    {
        ResetResults();
        var testStarted = DateTime.Now;
        workflowStarted ??= testStarted;
        workflowEnded = null;

        RefreshWorkflowResults();

        var progress = new Progress<StressSnapshot>(UpdateResults);
        var snapshot = await Task.Run(() => new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(testDurationSeconds),
            workers,
            maximumRequests,
            progress,
            testCancellation!.Token));
        var testEnded = DateTime.Now;
        UpdateResults(snapshot);
        return new StressTestResult(
            workers,
            snapshot.TotalRequests,
            snapshot.SuccessfulRequests,
            snapshot.FailedRequests,
            testStarted,
            testEnded,
            snapshot.Elapsed,
            testCancellation!.IsCancellationRequested ? StressTestStatus.Stopped : StressTestStatus.Completed);
    }

    private bool ConfirmStressTest()
    {

        /*
#if DEBUG
        // do not show the warning in debug builds
        return true;
#endif 
        */

        if (suppressStressTestWarning)
        {
            return true;
        }

        using var warningDialog = new Form
        {
            AutoScaleMode = AutoScaleMode.Font,
            ClientSize = new Size(620, 280),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            Text = "Time Server Stress Test Warning"
        };
        var warningLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 20),
            Size = new Size(580, 160),
            Text = "Use this application only to stress test internal time servers which you are authorized to stress test.\r\n\r\n" +
            "Stress testing external time servers or public time-server pools will most likely cause your external IP address to be blocked or banned.\r\n\r\n" +
            "Stress testing internal time servers or internal time server pools may also cause your machine's internal IP address to be blocked or banned.\r\n\r\n" +
            "Continue only if you are authorized to stress test the specified time server and know that in doing your machine's IP address or external address will not be blocked or banned."
        };
        var suppressWarningCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(20, 200),
            Text = "Do not show this message again for this session"
        };
        var continueButton = new Button
        {
            DialogResult = DialogResult.OK,
            Location = new Point(420, 230),
            Size = new Size(90, 30),
            Text = "Continue",
            UseVisualStyleBackColor = true
        };
        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(520, 230),
            Size = new Size(90, 30),
            Text = "Cancel",
            UseVisualStyleBackColor = true
        };
        warningDialog.Controls.Add(warningLabel);
        warningDialog.Controls.Add(suppressWarningCheckBox);
        warningDialog.Controls.Add(continueButton);
        warningDialog.Controls.Add(cancelButton);
        warningDialog.Shown += (_, _) => cancelButton.Focus();
        warningDialog.AcceptButton = cancelButton;
        warningDialog.CancelButton = cancelButton;

        var confirmed = warningDialog.ShowDialog(this) == DialogResult.OK;
        suppressStressTestWarning = confirmed && suppressWarningCheckBox.Checked;
        return confirmed;
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        stopButton.Enabled = false;
        testCancellation?.Cancel();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UserPreferences.Save(
            serverAddressTextBox.Text,
            (int)ntpPortNumericUpDown.Value,
            (int)concurrentTestsNumericUpDown.Value,
            (int)durationNumericUpDown.Value);
        base.OnFormClosing(e);
    }

    private void HelpButton_Click(object? sender, EventArgs e)
    {
        Process.Start(new ProcessStartInfo(HelpUrl) { UseShellExecute = true });
    }

    private void SaveResultsButton_Click(object? sender, EventArgs e)
    {
        var generatedAt = DateTime.Now;
        var completedAt = workflowResults[^1].Ended;
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "pdf",
            Filter = "PDF document (*.pdf)|*.pdf",
            FileName = $"Time Server Stress Test Report {completedAt:yyyy-MM-dd HH-mm-ss}.pdf",
            Title = "Save Results"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var endpoint = workflowEndpoint ?? throw new InvalidOperationException("The tested time server details are unavailable.");
            var chartJpeg = resultsChart.CreateJpeg(out var chartSize);
            PdfReportExporter.Save(dialog.FileName, workflowResults, chartJpeg, chartSize, endpoint, generatedAt);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureCurrentTestStatistics()
    {
        ConfigureStatistic(totalRequestsTitleLabel, totalRequestsValueLabel, "Total requests:", 28);
        ConfigureStatistic(requestsPerSecondTitleLabel, requestsPerSecondValueLabel, "Requests per second:", 48);
        ConfigureStatistic(successfulRequestsTitleLabel, successfulRequestsValueLabel, "Successful requests:", 68);
        ConfigureStatistic(failedRequestsTitleLabel, failedRequestsValueLabel, "Failed requests:", 88);
    }

    private static void ConfigureStatistic(Label titleLabel, Label valueLabel, string title, int top)
    {
        titleLabel.AutoSize = true;
        titleLabel.Location = new Point(20, top);
        titleLabel.Text = title;
        valueLabel.AutoSize = true;
        valueLabel.Location = new Point(190, top);
        valueLabel.Text = "0";
    }

    private void UpdateResults(StressSnapshot snapshot)
    {
        totalRequestsValueLabel.Text = snapshot.TotalRequests.ToString("N0");
        requestsPerSecondValueLabel.Text = snapshot.RequestsPerSecond.ToString("N0");
        successfulRequestsValueLabel.Text = $"{snapshot.SuccessfulRequests:N0} ({GetPercentage(snapshot.SuccessfulRequests, snapshot.TotalRequests):N2}%)";
        failedRequestsValueLabel.Text = $"{snapshot.FailedRequests:N0} ({GetPercentage(snapshot.FailedRequests, snapshot.TotalRequests):N2}%)";
        remainingProgressBar.Maximum = Math.Max(1, testDurationSeconds);
        remainingProgressBar.Value = remainingProgressBar.Maximum - Math.Clamp((int)Math.Ceiling(snapshot.Remaining.TotalSeconds), 0, remainingProgressBar.Maximum);
    }

    private void ResetResults()
    {
        UpdateResults(new StressSnapshot(0, 0, 0, 0, TimeSpan.FromSeconds(testDurationSeconds)));
    }

    private void ConfigureResultsTable()
    {
        if (resultsDataGridView.Columns.Count != 0)
        {
            return;
        }

        resultsDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        resultsDataGridView.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        resultsDataGridView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        resultsDataGridView.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        foreach (var (name, header) in new[]
        {
            ("workersColumn", "Concurrent Requests"),
            ("totalRequestsColumn", "Total Requests"),
            ("requestsPerSecondColumn", "Requests / Second"),
            ("successesColumn", "Successes"),
            ("failuresColumn", "Failures"),
            ("successRateColumn", "Success Rate"),
            ("startedColumn", "Started"),
            ("endedColumn", "Ended")
        })
        {
            resultsDataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Name = name,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }

        ResizeResultsTableColumns();
    }

    private void ResizeResultsTableColumns()
    {
        resultsDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        resultsDataGridView.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);

        foreach (DataGridViewColumn column in resultsDataGridView.Columns)
        {
            column.MinimumWidth = column.Width;
            column.FillWeight = column.Width;
        }

        resultsDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void RefreshWorkflowResults()
    {
        resultsDataGridView.Rows.Clear();

        // testing here
        if (testCancellation != null && testCancellation.IsCancellationRequested)
        {
            workflowResults.RemoveAll(result => result.Status == StressTestStatus.Stopped);
        }

        foreach (var result in workflowResults)
        {
            resultsDataGridView.Rows.Add(
                result.Workers.ToString("N0"),
                result.TotalRequests.ToString("N0"),
                result.RequestsPerSecond.ToString("N2"),
                result.SuccessfulRequests.ToString("N0"),
                result.FailedRequests.ToString("N0"),
                $"{result.SuccessRate:N2}%",
                result.Started.ToString("G"),
                result.Ended.ToString("G"));
        }

        ResizeResultsTableColumns();
        resultsChart.Results = workflowResults.ToArray();
        createReportButton.Enabled = !isTestRunning && workflowResults.Count > 0;
        workflowTimingLabel.Text = workflowStarted is null
            ? "Testing: not started."
            : workflowEnded is null
                ? $"Testing started: {workflowStarted.Value:G}"
                : $"Testing started: {workflowStarted.Value:G}    Ended: {workflowEnded.Value:G}";
    }

    private void SetTestState(bool isRunning)
    {
        serverAddressTextBox.Enabled = !isRunning;
        ntpPortNumericUpDown.Enabled = !isRunning;
        durationNumericUpDown.Enabled = !isRunning;
        concurrentTestsNumericUpDown.Enabled = !isRunning;
        startButton.Enabled = !isRunning;
        multiTestButton.Enabled = !isRunning;
        stopButton.Enabled = isRunning;
        createReportButton.Enabled = !isRunning && workflowResults.Count > 0;
    }

    private static double GetPercentage(long part, long whole)
    {
        return whole == 0 ? 0 : (double)part / whole * 100;
    }

    private void closeButton_Click(object sender, EventArgs e)
    {
        this.Close();
    }
}
