using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfContextMenuEventArgs = System.Windows.Controls.ContextMenuEventArgs;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using WpfSpellCheck = System.Windows.Controls.SpellCheck;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfXmlLanguage = System.Windows.Markup.XmlLanguage;

namespace TimeServerStressTest;

internal sealed partial class CreateReportForm : Form
{
    private readonly WpfTextBox spellCheckedNotesTextBox;
    private readonly string customDictionaryPath;
    private readonly Uri customDictionaryUri;

    public CreateReportForm(string title, UserPreferences.ReportSettings settings)
    {
        InitializeComponent();
        customDictionaryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeServerStressTest",
            "custom-dictionary.lex");
        customDictionaryUri = new Uri(customDictionaryPath, UriKind.Absolute);
        spellCheckedNotesTextBox = CreateNotesTextBox();
        notesElementHost.Child = spellCheckedNotesTextBox;
        LoadCustomDictionary();

        Text = $"{title} - Create Reports";
        createPdfReportCheckBox.Checked = settings.CreatePdfReport;
        viewPdfReportCheckBox.Checked = settings.ViewPdfReportAfterCreation;
        createCsvReportCheckBox.Checked = settings.CreateCsvReport;
        viewCsvReportCheckBox.Checked = settings.ViewCsvReportAfterCreation;
        UpdateViewReportOptions();
        spellCheckedNotesTextBox.Text = settings.Notes;
    }

    public UserPreferences.ReportSettings GetSettings() => new(spellCheckedNotesTextBox.Text, createPdfReportCheckBox.Checked, createCsvReportCheckBox.Checked, viewPdfReportCheckBox.Checked, viewCsvReportCheckBox.Checked, string.Empty, string.Empty);

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        if (spellCheckedNotesTextBox.Text.Length == 0 || MessageBox.Show(this, "Clear the notes?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            spellCheckedNotesTextBox.Clear();
        }
    }

    private void CreatePdfReportCheckBox_CheckedChanged(object? sender, EventArgs e) => UpdateViewReportOptions();

    private void CreateCsvReportCheckBox_CheckedChanged(object? sender, EventArgs e) => UpdateViewReportOptions();

    private void UpdateViewReportOptions()
    {
        viewPdfReportCheckBox.Enabled = createPdfReportCheckBox.Checked;
        viewCsvReportCheckBox.Enabled = createCsvReportCheckBox.Checked;
    }

    private WpfTextBox CreateNotesTextBox()
    {
        var textBox = new WpfTextBox
        {
            AcceptsReturn = true,
            Language = WpfXmlLanguage.GetLanguage("en-US"),
            VerticalScrollBarVisibility = WpfScrollBarVisibility.Auto
        };
        WpfSpellCheck.SetIsEnabled(textBox, true);
        textBox.ContextMenuOpening += NotesTextBox_ContextMenuOpening;
        return textBox;
    }

    private void LoadCustomDictionary()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(customDictionaryPath)!);
        if (!File.Exists(customDictionaryPath))
        {
            File.WriteAllText(customDictionaryPath, "#LID 1033" + Environment.NewLine);
        }

        WpfSpellCheck.GetCustomDictionaries(spellCheckedNotesTextBox).Add(customDictionaryUri);
    }

    private void NotesTextBox_ContextMenuOpening(object? sender, WpfContextMenuEventArgs e)
    {
        var spellingError = spellCheckedNotesTextBox.GetSpellingError(spellCheckedNotesTextBox.CaretIndex);
        if (spellingError is null)
        {
            e.Handled = true;
            return;
        }

        var contextMenu = new WpfContextMenu();
        foreach (var suggestion in spellingError.Suggestions.Take(5))
        {
            var correction = suggestion;
            var item = new WpfMenuItem { Header = correction };
            item.Click += (_, _) => spellingError.Correct(correction);
            contextMenu.Items.Add(item);
        }

        var word = GetWordAtCaret();
        var addToDictionaryItem = new WpfMenuItem { Header = $"Add '{word}' to dictionary" };
        addToDictionaryItem.Click += (_, _) => AddWordToCustomDictionary(word);
        contextMenu.Items.Add(addToDictionaryItem);
        spellCheckedNotesTextBox.ContextMenu = contextMenu;
    }

    private void AddWordToCustomDictionary(string word)
    {
        if (File.ReadLines(customDictionaryPath).Any(existingWord => string.Equals(existingWord, word, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        File.AppendAllText(customDictionaryPath, word + Environment.NewLine);
        var dictionaries = WpfSpellCheck.GetCustomDictionaries(spellCheckedNotesTextBox);
        dictionaries.Clear();
        dictionaries.Add(customDictionaryUri);
        WpfSpellCheck.SetIsEnabled(spellCheckedNotesTextBox, false);
        WpfSpellCheck.SetIsEnabled(spellCheckedNotesTextBox, true);
    }

    private string GetWordAtCaret()
    {
        var text = spellCheckedNotesTextBox.Text;
        var index = Math.Clamp(spellCheckedNotesTextBox.CaretIndex, 0, text.Length);
        if (index == text.Length || !IsWordCharacter(text[index]))
        {
            index--;
        }

        var end = index + 1;
        while (index >= 0 && IsWordCharacter(text[index]))
        {
            index--;
        }

        return text[(index + 1)..end];
    }

    private static bool IsWordCharacter(char character) => char.IsLetter(character) || character == '\'';
}
