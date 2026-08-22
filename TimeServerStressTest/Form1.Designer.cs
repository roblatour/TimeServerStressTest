namespace TimeServerStressTest;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;
    private Panel contentPanel = null!;
    private Label serverAddressLabel = null!;
    private ComboBox serverAddressTextBox = null!;
    private Label ntpPortLabel = null!;
    private NumericUpDown ntpPortNumericUpDown = null!;
    private Label durationLabel = null!;
    private NumericUpDown durationNumericUpDown = null!;
    private Label concurrentTestsLabel = null!;
    private NumericUpDown concurrentTestsNumericUpDown = null!;
    private Button startButton = null!;
    private Button multiTestButton = null!;
    private Button stopButton = null!;
    private Button helpButton = null!;
    private GroupBox resultsGroupBox = null!;
    private Label totalRequestsTitleLabel = null!;
    private Label requestsPerSecondTitleLabel = null!;
    private Label successfulRequestsTitleLabel = null!;
    private Label failedRequestsTitleLabel = null!;
    private Label totalRequestsValueLabel = null!;
    private Label requestsPerSecondValueLabel = null!;
    private Label successfulRequestsValueLabel = null!;
    private Label failedRequestsValueLabel = null!;
    private ProgressBar remainingProgressBar = null!;
    private GroupBox chartGroupBox = null!;
    private ResultsChart resultsChart = null!;
    private GroupBox summaryGroupBox = null!;
    private DataGridView resultsDataGridView = null!;
    private Label workflowTimingLabel = null!;
    private Button createReportButton = null!;
    private ProgressBar multiTestProgressBar = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        contentPanel = new Panel();
        groupBox5 = new GroupBox();
        createReportButton = new Button();
        groupBox4 = new GroupBox();
        serverAddressLabel = new Label();
        serverAddressTextBox = new ComboBox();
        ntpPortNumericUpDown = new NumericUpDown();
        ntpPortLabel = new Label();
        groupBox3 = new GroupBox();
        groupBox7 = new GroupBox();
        groupBox2 = new GroupBox();
        label1 = new Label();
        multiTestButton = new Button();
        groupBox1 = new GroupBox();
        startButton = new Button();
        concurrentTestsNumericUpDown = new NumericUpDown();
        concurrentTestsLabel = new Label();
        durationLabel = new Label();
        durationNumericUpDown = new NumericUpDown();
        groupBox6 = new GroupBox();
        button1 = new Button();
        closeButton = new Button();
        helpButton = new Button();
        resultsGroupBox = new GroupBox();
        totalRequestsTitleLabel = new Label();
        requestsPerSecondTitleLabel = new Label();
        successfulRequestsTitleLabel = new Label();
        stopButton = new Button();
        failedRequestsTitleLabel = new Label();
        totalRequestsValueLabel = new Label();
        requestsPerSecondValueLabel = new Label();
        successfulRequestsValueLabel = new Label();
        failedRequestsValueLabel = new Label();
        remainingProgressBar = new ProgressBar();
        multiTestProgressBar = new ProgressBar();
        chartGroupBox = new GroupBox();
        resultsChart = new ResultsChart();
        workflowTimingLabel = new Label();
        summaryGroupBox = new GroupBox();
        resultsDataGridView = new DataGridView();
        contentPanel.SuspendLayout();
        groupBox5.SuspendLayout();
        groupBox4.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)ntpPortNumericUpDown).BeginInit();
        groupBox3.SuspendLayout();
        groupBox7.SuspendLayout();
        groupBox2.SuspendLayout();
        groupBox1.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)concurrentTestsNumericUpDown).BeginInit();
        ((System.ComponentModel.ISupportInitialize)durationNumericUpDown).BeginInit();
        groupBox6.SuspendLayout();
        resultsGroupBox.SuspendLayout();
        chartGroupBox.SuspendLayout();
        summaryGroupBox.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)resultsDataGridView).BeginInit();
        SuspendLayout();
        // 
        // contentPanel
        // 
        contentPanel.AutoScroll = true;
        contentPanel.Controls.Add(groupBox5);
        contentPanel.Controls.Add(groupBox4);
        contentPanel.Controls.Add(groupBox3);
        contentPanel.Controls.Add(closeButton);
        contentPanel.Controls.Add(helpButton);
        contentPanel.Controls.Add(resultsGroupBox);
        contentPanel.Controls.Add(chartGroupBox);
        contentPanel.Controls.Add(workflowTimingLabel);
        contentPanel.Controls.Add(summaryGroupBox);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 0);
        contentPanel.Name = "contentPanel";
        contentPanel.Size = new Size(1094, 1040);
        contentPanel.TabIndex = 0;
        // 
        // groupBox5
        // 
        groupBox5.Controls.Add(createReportButton);
        groupBox5.Location = new Point(918, 12);
        groupBox5.Name = "groupBox5";
        groupBox5.Size = new Size(155, 168);
        groupBox5.TabIndex = 22;
        groupBox5.TabStop = false;
        groupBox5.Text = "Create Report";
        // 
        // createReportButton
        // 
        createReportButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        createReportButton.Enabled = false;
        createReportButton.Location = new Point(16, 117);
        createReportButton.Name = "createReportButton";
        createReportButton.Size = new Size(120, 30);
        createReportButton.TabIndex = 16;
        createReportButton.Text = "Create Report";
        createReportButton.UseVisualStyleBackColor = true;
        createReportButton.Click += SaveResultsButton_Click;
        // 
        // groupBox4
        // 
        groupBox4.Controls.Add(serverAddressLabel);
        groupBox4.Controls.Add(serverAddressTextBox);
        groupBox4.Controls.Add(ntpPortNumericUpDown);
        groupBox4.Controls.Add(ntpPortLabel);
        groupBox4.Location = new Point(27, 12);
        groupBox4.Name = "groupBox4";
        groupBox4.Size = new Size(230, 168);
        groupBox4.TabIndex = 21;
        groupBox4.TabStop = false;
        groupBox4.Text = "Time Server Identification";
        // 
        // serverAddressLabel
        // 
        serverAddressLabel.AutoSize = true;
        serverAddressLabel.Location = new Point(15, 28);
        serverAddressLabel.Name = "serverAddressLabel";
        serverAddressLabel.Size = new Size(200, 15);
        serverAddressLabel.TabIndex = 0;
        serverAddressLabel.Text = "Time server host name or IP address:";
        // 
        // serverAddressTextBox
        // 
        serverAddressTextBox.FormattingEnabled = true;
        serverAddressTextBox.Location = new Point(15, 46);
        serverAddressTextBox.Name = "serverAddressTextBox";
        serverAddressTextBox.Size = new Size(200, 23);
        serverAddressTextBox.Sorted = true;
        serverAddressTextBox.TabIndex = 1;
        serverAddressTextBox.KeyDown += ServerAddressComboBox_KeyDown;
        // 
        // ntpPortNumericUpDown
        // 
        ntpPortNumericUpDown.Location = new Point(15, 102);
        ntpPortNumericUpDown.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
        ntpPortNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        ntpPortNumericUpDown.Name = "ntpPortNumericUpDown";
        ntpPortNumericUpDown.Size = new Size(90, 23);
        ntpPortNumericUpDown.TabIndex = 3;
        ntpPortNumericUpDown.Value = new decimal(new int[] { 123, 0, 0, 0 });
        // 
        // ntpPortLabel
        // 
        ntpPortLabel.AutoSize = true;
        ntpPortLabel.Location = new Point(15, 82);
        ntpPortLabel.Name = "ntpPortLabel";
        ntpPortLabel.Size = new Size(32, 15);
        ntpPortLabel.TabIndex = 2;
        ntpPortLabel.Text = "Port:";
        // 
        // groupBox3
        // 
        groupBox3.Controls.Add(groupBox7);
        groupBox3.Controls.Add(groupBox6);
        groupBox3.Location = new Point(271, 12);
        groupBox3.Name = "groupBox3";
        groupBox3.Size = new Size(633, 168);
        groupBox3.TabIndex = 20;
        groupBox3.TabStop = false;
        // 
        // groupBox7
        // 
        groupBox7.Controls.Add(groupBox2);
        groupBox7.Controls.Add(groupBox1);
        groupBox7.Controls.Add(durationLabel);
        groupBox7.Controls.Add(durationNumericUpDown);
        groupBox7.Location = new Point(137, 14);
        groupBox7.Name = "groupBox7";
        groupBox7.Size = new Size(490, 148);
        groupBox7.TabIndex = 21;
        groupBox7.TabStop = false;
        groupBox7.Text = "Stress Testing";
        groupBox7.Enter += groupBox7_Enter;
        // 
        // groupBox2
        // 
        groupBox2.Controls.Add(label1);
        groupBox2.Controls.Add(multiTestButton);
        groupBox2.Location = new Point(246, 44);
        groupBox2.Name = "groupBox2";
        groupBox2.Size = new Size(230, 100);
        groupBox2.TabIndex = 19;
        groupBox2.TabStop = false;
        groupBox2.Text = "Multi Stress Test";
        // 
        // label1
        // 
        label1.AutoSize = true;
        label1.Location = new Point(6, 30);
        label1.Name = "label1";
        label1.Size = new Size(158, 15);
        label1.TabIndex = 10;
        label1.Text = "Concurrent Requests: 0 - 100";
        // 
        // multiTestButton
        // 
        multiTestButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        multiTestButton.Location = new Point(10, 60);
        multiTestButton.Name = "multiTestButton";
        multiTestButton.Size = new Size(200, 30);
        multiTestButton.TabIndex = 9;
        multiTestButton.Text = "Start multiple stress tests";
        multiTestButton.UseVisualStyleBackColor = true;
        multiTestButton.Click += MultiTestButton_Click;
        // 
        // groupBox1
        // 
        groupBox1.Controls.Add(startButton);
        groupBox1.Controls.Add(concurrentTestsNumericUpDown);
        groupBox1.Controls.Add(concurrentTestsLabel);
        groupBox1.Location = new Point(10, 43);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(225, 100);
        groupBox1.TabIndex = 18;
        groupBox1.TabStop = false;
        groupBox1.Text = "Single Stress Test";
        // 
        // startButton
        // 
        startButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        startButton.Location = new Point(7, 60);
        startButton.Name = "startButton";
        startButton.Size = new Size(200, 30);
        startButton.TabIndex = 8;
        startButton.Text = "Start a single stress test";
        startButton.UseVisualStyleBackColor = true;
        startButton.Click += StartButton_Click;
        // 
        // concurrentTestsNumericUpDown
        // 
        concurrentTestsNumericUpDown.Location = new Point(133, 30);
        concurrentTestsNumericUpDown.Name = "concurrentTestsNumericUpDown";
        concurrentTestsNumericUpDown.Size = new Size(73, 23);
        concurrentTestsNumericUpDown.TabIndex = 7;
        concurrentTestsNumericUpDown.Value = new decimal(new int[] { 20, 0, 0, 0 });
        // 
        // concurrentTestsLabel
        // 
        concurrentTestsLabel.AutoSize = true;
        concurrentTestsLabel.Location = new Point(6, 32);
        concurrentTestsLabel.Name = "concurrentTestsLabel";
        concurrentTestsLabel.Size = new Size(120, 15);
        concurrentTestsLabel.TabIndex = 6;
        concurrentTestsLabel.Text = "Concurrent Requests:";
        // 
        // durationLabel
        // 
        durationLabel.AutoSize = true;
        durationLabel.Location = new Point(10, 22);
        durationLabel.Name = "durationLabel";
        durationLabel.Size = new Size(139, 15);
        durationLabel.TabIndex = 4;
        durationLabel.Text = "Test Durations (seconds):";
        // 
        // durationNumericUpDown
        // 
        durationNumericUpDown.Location = new Point(155, 20);
        durationNumericUpDown.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
        durationNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        durationNumericUpDown.Name = "durationNumericUpDown";
        durationNumericUpDown.Size = new Size(67, 23);
        durationNumericUpDown.TabIndex = 5;
        durationNumericUpDown.Value = new decimal(new int[] { 15, 0, 0, 0 });
        // 
        // groupBox6
        // 
        groupBox6.Controls.Add(button1);
        groupBox6.Location = new Point(10, 14);
        groupBox6.Name = "groupBox6";
        groupBox6.Size = new Size(121, 148);
        groupBox6.TabIndex = 20;
        groupBox6.TabStop = false;
        groupBox6.Text = "Single Test";
        // 
        // button1
        // 
        button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        button1.Location = new Point(6, 104);
        button1.Name = "button1";
        button1.Size = new Size(109, 30);
        button1.TabIndex = 9;
        button1.Text = "Start a single test";
        button1.UseVisualStyleBackColor = true;
        button1.Click += SingleTestButton_Click;
        // 
        // closeButton
        // 
        closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        closeButton.Location = new Point(971, 999);
        closeButton.Name = "closeButton";
        closeButton.Size = new Size(100, 30);
        closeButton.TabIndex = 17;
        closeButton.Text = "Close";
        closeButton.UseVisualStyleBackColor = true;
        closeButton.Click += closeButton_Click;
        // 
        // helpButton
        // 
        helpButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        helpButton.Location = new Point(844, 999);
        helpButton.Name = "helpButton";
        helpButton.Size = new Size(100, 30);
        helpButton.TabIndex = 11;
        helpButton.Text = "About";
        helpButton.UseVisualStyleBackColor = true;
        helpButton.Click += HelpButton_Click;
        // 
        // resultsGroupBox
        // 
        resultsGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        resultsGroupBox.Controls.Add(totalRequestsTitleLabel);
        resultsGroupBox.Controls.Add(requestsPerSecondTitleLabel);
        resultsGroupBox.Controls.Add(successfulRequestsTitleLabel);
        resultsGroupBox.Controls.Add(stopButton);
        resultsGroupBox.Controls.Add(failedRequestsTitleLabel);
        resultsGroupBox.Controls.Add(totalRequestsValueLabel);
        resultsGroupBox.Controls.Add(requestsPerSecondValueLabel);
        resultsGroupBox.Controls.Add(successfulRequestsValueLabel);
        resultsGroupBox.Controls.Add(failedRequestsValueLabel);
        resultsGroupBox.Controls.Add(remainingProgressBar);
        resultsGroupBox.Controls.Add(multiTestProgressBar);
        resultsGroupBox.Location = new Point(27, 186);
        resultsGroupBox.Name = "resultsGroupBox";
        resultsGroupBox.Size = new Size(1050, 212);
        resultsGroupBox.TabIndex = 12;
        resultsGroupBox.TabStop = false;
        resultsGroupBox.Text = "Test Progress";
        // 
        // totalRequestsTitleLabel
        // 
        totalRequestsTitleLabel.Location = new Point(0, 0);
        totalRequestsTitleLabel.Name = "totalRequestsTitleLabel";
        totalRequestsTitleLabel.Size = new Size(100, 23);
        totalRequestsTitleLabel.TabIndex = 0;
        // 
        // requestsPerSecondTitleLabel
        // 
        requestsPerSecondTitleLabel.Location = new Point(0, 0);
        requestsPerSecondTitleLabel.Name = "requestsPerSecondTitleLabel";
        requestsPerSecondTitleLabel.Size = new Size(100, 23);
        requestsPerSecondTitleLabel.TabIndex = 1;
        // 
        // successfulRequestsTitleLabel
        // 
        successfulRequestsTitleLabel.Location = new Point(0, 0);
        successfulRequestsTitleLabel.Name = "successfulRequestsTitleLabel";
        successfulRequestsTitleLabel.Size = new Size(100, 23);
        successfulRequestsTitleLabel.TabIndex = 2;
        // 
        // stopButton
        // 
        stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        stopButton.Enabled = false;
        stopButton.Location = new Point(20, 167);
        stopButton.Name = "stopButton";
        stopButton.Size = new Size(1010, 30);
        stopButton.TabIndex = 10;
        stopButton.Text = "Stop testing";
        stopButton.UseVisualStyleBackColor = true;
        stopButton.Click += StopButton_Click;
        // 
        // failedRequestsTitleLabel
        // 
        failedRequestsTitleLabel.Location = new Point(0, 0);
        failedRequestsTitleLabel.Name = "failedRequestsTitleLabel";
        failedRequestsTitleLabel.Size = new Size(100, 23);
        failedRequestsTitleLabel.TabIndex = 3;
        // 
        // totalRequestsValueLabel
        // 
        totalRequestsValueLabel.Location = new Point(0, 0);
        totalRequestsValueLabel.Name = "totalRequestsValueLabel";
        totalRequestsValueLabel.Size = new Size(100, 23);
        totalRequestsValueLabel.TabIndex = 4;
        // 
        // requestsPerSecondValueLabel
        // 
        requestsPerSecondValueLabel.Location = new Point(0, 0);
        requestsPerSecondValueLabel.Name = "requestsPerSecondValueLabel";
        requestsPerSecondValueLabel.Size = new Size(100, 23);
        requestsPerSecondValueLabel.TabIndex = 5;
        // 
        // successfulRequestsValueLabel
        // 
        successfulRequestsValueLabel.Location = new Point(0, 0);
        successfulRequestsValueLabel.Name = "successfulRequestsValueLabel";
        successfulRequestsValueLabel.Size = new Size(100, 23);
        successfulRequestsValueLabel.TabIndex = 6;
        // 
        // failedRequestsValueLabel
        // 
        failedRequestsValueLabel.Location = new Point(0, 0);
        failedRequestsValueLabel.Name = "failedRequestsValueLabel";
        failedRequestsValueLabel.Size = new Size(100, 23);
        failedRequestsValueLabel.TabIndex = 7;
        // 
        // remainingProgressBar
        // 
        remainingProgressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        remainingProgressBar.Location = new Point(20, 112);
        remainingProgressBar.Name = "remainingProgressBar";
        remainingProgressBar.Size = new Size(1010, 18);
        remainingProgressBar.Style = ProgressBarStyle.Continuous;
        remainingProgressBar.TabIndex = 8;
        // 
        // multiTestProgressBar
        // 
        multiTestProgressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        multiTestProgressBar.Location = new Point(20, 136);
        multiTestProgressBar.Name = "multiTestProgressBar";
        multiTestProgressBar.Size = new Size(1010, 18);
        multiTestProgressBar.Style = ProgressBarStyle.Continuous;
        multiTestProgressBar.TabIndex = 9;
        multiTestProgressBar.Visible = false;
        // 
        // chartGroupBox
        // 
        chartGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        chartGroupBox.Controls.Add(resultsChart);
        chartGroupBox.Location = new Point(24, 423);
        chartGroupBox.Name = "chartGroupBox";
        chartGroupBox.Size = new Size(1050, 300);
        chartGroupBox.TabIndex = 13;
        chartGroupBox.TabStop = false;
        chartGroupBox.Text = "Completed Run Analysis";
        // 
        // resultsChart
        // 
        resultsChart.BackColor = Color.White;
        resultsChart.Dock = DockStyle.Fill;
        resultsChart.Location = new Point(3, 19);
        resultsChart.MinimumSize = new Size(600, 260);
        resultsChart.Name = "resultsChart";
        resultsChart.Size = new Size(1044, 278);
        resultsChart.TabIndex = 0;
        // 
        // workflowTimingLabel
        // 
        workflowTimingLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        workflowTimingLabel.AutoSize = true;
        workflowTimingLabel.Location = new Point(21, 1014);
        workflowTimingLabel.Name = "workflowTimingLabel";
        workflowTimingLabel.Size = new Size(111, 15);
        workflowTimingLabel.TabIndex = 15;
        workflowTimingLabel.Text = "Testing: not started.";
        // 
        // summaryGroupBox
        // 
        summaryGroupBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        summaryGroupBox.Controls.Add(resultsDataGridView);
        summaryGroupBox.Location = new Point(21, 738);
        summaryGroupBox.Name = "summaryGroupBox";
        summaryGroupBox.Size = new Size(1053, 250);
        summaryGroupBox.TabIndex = 14;
        summaryGroupBox.TabStop = false;
        summaryGroupBox.Text = "Executed Tests";
        // 
        // resultsDataGridView
        // 
        resultsDataGridView.AllowUserToAddRows = false;
        resultsDataGridView.AllowUserToDeleteRows = false;
        resultsDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resultsDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultsDataGridView.BackgroundColor = Color.White;
        resultsDataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        resultsDataGridView.EditMode = DataGridViewEditMode.EditProgrammatically;
        resultsDataGridView.Location = new Point(3, 19);
        resultsDataGridView.MultiSelect = false;
        resultsDataGridView.Name = "resultsDataGridView";
        resultsDataGridView.ReadOnly = true;
        resultsDataGridView.RowHeadersVisible = false;
        resultsDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultsDataGridView.Size = new Size(1047, 228);
        resultsDataGridView.TabIndex = 0;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1094, 1040);
        Controls.Add(contentPanel);
        MinimumSize = new Size(1100, 700);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        contentPanel.ResumeLayout(false);
        contentPanel.PerformLayout();
        groupBox5.ResumeLayout(false);
        groupBox4.ResumeLayout(false);
        groupBox4.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)ntpPortNumericUpDown).EndInit();
        groupBox3.ResumeLayout(false);
        groupBox7.ResumeLayout(false);
        groupBox7.PerformLayout();
        groupBox2.ResumeLayout(false);
        groupBox2.PerformLayout();
        groupBox1.ResumeLayout(false);
        groupBox1.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)concurrentTestsNumericUpDown).EndInit();
        ((System.ComponentModel.ISupportInitialize)durationNumericUpDown).EndInit();
        groupBox6.ResumeLayout(false);
        resultsGroupBox.ResumeLayout(false);
        chartGroupBox.ResumeLayout(false);
        summaryGroupBox.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)resultsDataGridView).EndInit();
        ResumeLayout(false);
    }

    private static void ConfigureResultLabel(Label label, string text, int top)
    {
        label.AutoSize = true;
        label.Location = new Point(20, top);
        label.Text = text;
    }

    private static void ConfigureValueLabel(Label label, int left, int top)
    {
        label.AutoSize = true;
        label.Location = new Point(left, top);
        label.Text = "0";
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string headerText, string name)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = headerText,
            Name = name,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
    }

    private Button closeButton;
    private GroupBox groupBox1;
    private GroupBox groupBox3;
    private GroupBox groupBox2;
    private GroupBox groupBox4;
    private Label label1;
    private GroupBox groupBox5;
    private GroupBox groupBox6;
    private Button button1;
    private GroupBox groupBox7;
}
