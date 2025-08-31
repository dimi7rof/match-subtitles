using System.Text;
using WinForms = System.Windows.Forms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SubtitleRenamerWpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        var result = dialog.ShowDialog();
        if (result == WinForms.DialogResult.OK)
        {
            FolderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        string folder = FolderPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
        {
            LogTextBox.Text += "Please select a valid folder.\n";
            return;
        }

        StartButton.IsEnabled = false;
        LogTextBox.Clear();

        bool deleteSubfolders = DeleteSubfoldersCheckBox.IsChecked == true;
        bool deleteUnrelated = DeleteUnrelatedFilesCheckBox.IsChecked == true;
        bool confirmDeletes = ConfirmBeforeDeleteCheckBox.IsChecked == true;

        // Run the logic in a background task to keep UI responsive
        await Task.Run(() =>
        {
            SubtitleRenamerApp.RunWithOptions(
                folder,
                deleteSubfolders,
                deleteUnrelated,
                confirmDeletes,
                msg => Dispatcher.Invoke(() => LogTextBox.Text += msg + "\n")
            );
        });

        StartButton.IsEnabled = true;
    }
}