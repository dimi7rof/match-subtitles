using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
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

    private void AppendLog(string message)
    {
        LogTextBox.Text += message + "\n";
        LogTextBox.ScrollToEnd();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        string folder = FolderPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
        {
            AppendLog("Please select a valid folder.");
            return;
        }

        StartButton.IsEnabled = false;
        LogTextBox.Clear();

        bool doCleanup = DeleteCleanupCheckBox.IsChecked == true;
        bool confirmDeletes = true;

        // Run the logic in a background task to keep UI responsive
        await Task.Run(() =>
        {
            SubtitleRenamerApp.RunWithOptions(
                folder,
                doCleanup,
                confirmDeletes,
                msg => Dispatcher.Invoke(() => AppendLog(msg)),
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
            var message = $"Are you sure you want to delete the following {itemPath}";

            var scrollViewer = new System.Windows.Controls.ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 18)
                },
                Height = 400
            };

            var yesButton = new System.Windows.Controls.Button
            {
                Content = "Yes",
                Width = 100,
                Margin = new Thickness(0, 0, 12, 0),
                IsDefault = true
            };
            var noButton = new System.Windows.Controls.Button
            {
                Content = "No",
                Width = 100,
                IsCancel = true
            };

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(24)
            };
            mainPanel.Children.Add(scrollViewer);
            mainPanel.Children.Add(buttonPanel);

            var dialog = new Window
            {
                Title = "Confirm Delete",
                Width = 900,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanMinimize,
                Owner = this,
                Content = mainPanel
            };

            bool result = false;
            yesButton.Click += (_, __) => { result = true; dialog.Close(); };
            noButton.Click += (_, __) => { result = false; dialog.Close(); };
            dialog.ShowDialog();
            return result;
        });
    }
}