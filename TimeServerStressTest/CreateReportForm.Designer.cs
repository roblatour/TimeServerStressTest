using System.Windows.Forms.Integration;

namespace TimeServerStressTest;

partial class CreateReportForm
{
    private System.ComponentModel.IContainer components = null;
    private CheckBox createPdfReportCheckBox = null!;
    private CheckBox viewPdfReportCheckBox = null!;
    private CheckBox createCsvReportCheckBox = null!;
    private CheckBox viewCsvReportCheckBox = null!;
    private Label notesLabel = null!;
    private ElementHost notesElementHost = null!;
    private Button clearButton = null!;
    private Button createButton = null!;
    private Button cancelButton = null!;

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
        createPdfReportCheckBox = new CheckBox();
        viewPdfReportCheckBox = new CheckBox();
        createCsvReportCheckBox = new CheckBox();
        viewCsvReportCheckBox = new CheckBox();
        notesLabel = new Label();
        notesElementHost = new ElementHost();
        clearButton = new Button();
        createButton = new Button();
        cancelButton = new Button();
        SuspendLayout();
        // 
        // createPdfReportCheckBox
        // 
        createPdfReportCheckBox.AutoSize = true;
        createPdfReportCheckBox.Location = new Point(20, 20);
        createPdfReportCheckBox.Name = "createPdfReportCheckBox";
        createPdfReportCheckBox.Size = new Size(95, 19);
        createPdfReportCheckBox.TabIndex = 0;
        createPdfReportCheckBox.Text = "Create report";
        createPdfReportCheckBox.UseVisualStyleBackColor = true;
        createPdfReportCheckBox.CheckedChanged += CreatePdfReportCheckBox_CheckedChanged;
        // 
        // viewPdfReportCheckBox
        // 
        viewPdfReportCheckBox.AutoSize = true;
        viewPdfReportCheckBox.Location = new Point(180, 20);
        viewPdfReportCheckBox.Name = "viewPdfReportCheckBox";
        viewPdfReportCheckBox.Size = new Size(157, 19);
        viewPdfReportCheckBox.TabIndex = 1;
        viewPdfReportCheckBox.Text = "View report once created";
        viewPdfReportCheckBox.UseVisualStyleBackColor = true;
        // 
        // createCsvReportCheckBox
        // 
        createCsvReportCheckBox.AutoSize = true;
        createCsvReportCheckBox.Location = new Point(20, 52);
        createCsvReportCheckBox.Name = "createCsvReportCheckBox";
        createCsvReportCheckBox.Size = new Size(99, 19);
        createCsvReportCheckBox.TabIndex = 2;
        createCsvReportCheckBox.Text = "Create csv file";
        createCsvReportCheckBox.UseVisualStyleBackColor = true;
        createCsvReportCheckBox.CheckedChanged += CreateCsvReportCheckBox_CheckedChanged;
        // 
        // viewCsvReportCheckBox
        // 
        viewCsvReportCheckBox.AutoSize = true;
        viewCsvReportCheckBox.Location = new Point(180, 52);
        viewCsvReportCheckBox.Name = "viewCsvReportCheckBox";
        viewCsvReportCheckBox.Size = new Size(161, 19);
        viewCsvReportCheckBox.TabIndex = 3;
        viewCsvReportCheckBox.Text = "View csv file once created";
        viewCsvReportCheckBox.UseVisualStyleBackColor = true;
        // 
        // notesLabel
        // 
        notesLabel.AutoSize = true;
        notesLabel.Location = new Point(20, 92);
        notesLabel.Name = "notesLabel";
        notesLabel.Size = new Size(41, 15);
        notesLabel.TabIndex = 4;
        notesLabel.Text = "Notes:";
        // 
        // notesElementHost
        // 
        notesElementHost.Location = new Point(20, 112);
        notesElementHost.Name = "notesElementHost";
        notesElementHost.Size = new Size(580, 115);
        notesElementHost.TabIndex = 5;
        // 
        // clearButton
        // 
        clearButton.Location = new Point(20, 250);
        clearButton.Name = "clearButton";
        clearButton.Size = new Size(80, 30);
        clearButton.TabIndex = 6;
        clearButton.Text = "Clear notes";
        clearButton.UseVisualStyleBackColor = true;
        clearButton.Click += ClearButton_Click;
        // 
        // createButton
        // 
        createButton.DialogResult = DialogResult.OK;
        createButton.Location = new Point(410, 250);
        createButton.Name = "createButton";
        createButton.Size = new Size(90, 30);
        createButton.TabIndex = 7;
        createButton.Text = "Create";
        createButton.UseVisualStyleBackColor = true;
        // 
        // cancelButton
        // 
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Location = new Point(510, 250);
        cancelButton.Name = "cancelButton";
        cancelButton.Size = new Size(90, 30);
        cancelButton.TabIndex = 8;
        cancelButton.Text = "Cancel";
        cancelButton.UseVisualStyleBackColor = true;
        // 
        // CreateReportForm
        // 
        AcceptButton = createButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = cancelButton;
        ClientSize = new Size(620, 300);
        Controls.Add(cancelButton);
        Controls.Add(createButton);
        Controls.Add(clearButton);
        Controls.Add(notesElementHost);
        Controls.Add(notesLabel);
        Controls.Add(viewCsvReportCheckBox);
        Controls.Add(createCsvReportCheckBox);
        Controls.Add(viewPdfReportCheckBox);
        Controls.Add(createPdfReportCheckBox);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CreateReportForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ResumeLayout(false);
        PerformLayout();
    }
}
