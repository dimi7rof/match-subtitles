using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace SubRename;

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
        using var dialog = new FolderBrowserDialog();
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
                msg => Dispatcher.Invoke(() => LogTextBox.Text += msg + "\n"),
                confirmDeletes ? ConfirmDelete : null
            );
        });

        StartButton.IsEnabled = true;
    }

    // Confirmation dialog for deletes
    private bool ConfirmDelete(string itemType, string itemPath)
    {
        return Dispatcher.Invoke(() =>
        {
            if (itemType == "files")
            {
                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete the following files?\n{itemPath}",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                return result == MessageBoxResult.Yes;
            }
            else
            {
                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete this {itemType}?\n{itemPath}",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                return result == MessageBoxResult.Yes;
            }
        });
    }
}