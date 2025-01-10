using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PostToPoint.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string? _redditAppId;
        private string? _redditAppSecret;
        private string? _redditRedirectUri;
        private string? _redditUsername;

        private string? _redditToBlueskyPath;
        private string? _redditToBlogPath;
        private string? _redditToLinkedInPath;
        private string? _blogToBlueskyPath;
        private string? _blogToLinkedInPath;

        private string? _blogPostDirectory;
        private string? _rssDirectory;
        private string? _postContentDirectory;
        private string? _redirectDirectory;

        private bool _isProcessing;

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            Resources.Add("BooleanToVisibilityConverter", new BooleanToVisibilityConverter());

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Load settings from user settings
                RedditAppId = App.Options.RedditAppId;
                RedditAppSecret = App.Options.RedditAppSecret;
                RedditRedirectUri = App.Options.RedditRedirectUri;
                RedditUsername = App.Options.RedditUsername;
                SetRedditPassword(App.Options.RedditPassword);
                RedditToBlueskyPath = App.Options.PostToBlueskyPrompt;
                RedditToBlogPath = App.Options.PostToBlogPrompt;
                RedditToLinkedInPath = App.Options.PostToLinkedinPrompt;
                BlogToBlueskyPath = App.Options.BlogToBlueskyPrompt;
                BlogToLinkedInPath = App.Options.BlogToLinkedinPrompt;
                BlogPostDirectory = App.Options.BlogDirectory;
                RssDirectory = App.Options.RssDirectory;
                PostContentDirectory = App.Options.PostContentDirectory;
                RedirectDirectory = App.Options.RedirectDirectory;
            }));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool ValidateRequiredSettings()
        {
            var missingSettings = new List<string>();

            if (string.IsNullOrEmpty(RedditAppId))
                missingSettings.Add("Reddit App ID");
            if (string.IsNullOrEmpty(RedditAppSecret))
                missingSettings.Add("Reddit App Secret");
            if (string.IsNullOrEmpty(RedditRedirectUri))
                missingSettings.Add("Reddit Redirect URI");
            if (string.IsNullOrEmpty(RedditUsername))
                missingSettings.Add("Reddit Username");
            if (string.IsNullOrEmpty(GetRedditPassword()))
                missingSettings.Add("Reddit Password");
            if (string.IsNullOrEmpty(RedditToBlueskyPath))
                missingSettings.Add("Reddit to Bluesky Prompt File");
            if (string.IsNullOrEmpty(RssDirectory))
                missingSettings.Add("RSS Directory");
            if (string.IsNullOrEmpty(BlogPostDirectory))
                missingSettings.Add("Blog Post Directory");
            if (string.IsNullOrEmpty(PostContentDirectory))
                missingSettings.Add("Post Content Directory");
            if (string.IsNullOrEmpty(RedirectDirectory))
                missingSettings.Add("Redirect Directory");

            if (missingSettings.Count > 0)
            {
                MessageBox.Show(
                    $"Please configure the following settings before proceeding:\n\n{string.Join("\n", missingSettings)}",
                    "Missing Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public string? RedditAppId
        {
            get => _redditAppId;
            set
            {
                _redditAppId = value;
                OnPropertyChanged(nameof(RedditAppId));
            }
        }

        public string? RedditAppSecret
        {
            get => _redditAppSecret;
            set
            {
                _redditAppSecret = value;
                OnPropertyChanged(nameof(RedditAppSecret));
            }
        }

        public string? RedditRedirectUri
        {
            get => _redditRedirectUri;
            set
            {
                _redditRedirectUri = value;
                OnPropertyChanged(nameof(RedditRedirectUri));
            }
        }

        public string? RedditUsername
        {
            get => _redditUsername;
            set
            {
                _redditUsername = value;
                OnPropertyChanged(nameof(RedditUsername));
            }
        }

        // Method to get password (since PasswordBox doesn't support binding for security reasons)
        private string? GetRedditPassword()
        {
            return pwbRedditPassword?.Password;
        }

        // Method to set password
        private void SetRedditPassword(string? password)
        {
            if (pwbRedditPassword != null)
            {
                pwbRedditPassword.Password = password ?? string.Empty;
            }
        }

        public string? RedditToBlueskyPath
        {
            get => _redditToBlueskyPath;
            set
            {
                _redditToBlueskyPath = value;
                OnPropertyChanged(nameof(RedditToBlueskyPath));
            }
        }

        public string? RedditToBlogPath
        {
            get => _redditToBlogPath;
            set
            {
                _redditToBlogPath = value;
                OnPropertyChanged(nameof(RedditToBlogPath));
            }
        }

        public string? RedditToLinkedInPath
        {
            get => _redditToLinkedInPath;
            set
            {
                _redditToLinkedInPath = value;
                OnPropertyChanged(nameof(RedditToLinkedInPath));
            }
        }

        public string? BlogToBlueskyPath
        {
            get => _blogToBlueskyPath;
            set
            {
                _blogToBlueskyPath = value;
                OnPropertyChanged(nameof(BlogToBlueskyPath));
            }
        }

        public string? BlogToLinkedInPath
        {
            get => _blogToLinkedInPath;
            set
            {
                _blogToLinkedInPath = value;
                OnPropertyChanged(nameof(BlogToLinkedInPath));
            }
        }

        public string? BlogPostDirectory
        {
            get => _blogPostDirectory;
            set
            {
                _blogPostDirectory = value;
                OnPropertyChanged(nameof(BlogPostDirectory));
            }
        }

        public string? RssDirectory
        {
            get => _rssDirectory;
            set
            {
                _rssDirectory = value;
                OnPropertyChanged(nameof(RssDirectory));
            }
        }

        public string? PostContentDirectory
        {
            get => _postContentDirectory;
            set
            {
                _postContentDirectory = value;
                OnPropertyChanged(nameof(PostContentDirectory));
            }
        }

        public string? RedirectDirectory
        {
            get => _redirectDirectory;
            set
            {
                _redirectDirectory = value;
                OnPropertyChanged(nameof(RedirectDirectory));
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged(nameof(IsProcessing));
            }
        }

        // Browse button click handlers
        private void BtnBrowseRedditToBluesky_Click(object sender, RoutedEventArgs e)
        {
            RedditToBlueskyPath = BrowseForFile("Select Reddit to Bluesky Prompt File");
        }

        private void BtnBrowseRedditToBlog_Click(object sender, RoutedEventArgs e)
        {
            RedditToBlogPath = BrowseForFile("Select Reddit to Blog Prompt File");
        }

        private void BtnBrowseRedditToLinkedIn_Click(object sender, RoutedEventArgs e)
        {
            RedditToLinkedInPath = BrowseForFile("Select Reddit to LinkedIn Prompt File");
        }

        private void BtnBrowseBlogToBluesky_Click(object sender, RoutedEventArgs e)
        {
            BlogToBlueskyPath = BrowseForFile("Select Blog to Bluesky Prompt File");
        }

        private void BtnBrowseBlogToLinkedIn_Click(object sender, RoutedEventArgs e)
        {
            BlogToLinkedInPath = BrowseForFile("Select Blog to LinkedIn Prompt File");
        }

        // Helper method for file browsing
        private static string? BrowseForFile(string title)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }

            return null;
        }

        // Browse button click handlers
        private void BtnBrowseBlogPostDir_Click(object sender, RoutedEventArgs e)
        {
            BlogPostDirectory = BrowseForFolder("Select Blog Post Files Directory");
        }

        private void BtnBrowseRssDir_Click(object sender, RoutedEventArgs e)
        {
            RssDirectory = BrowseForFolder("Select RSS Files Directory");
        }

        private void BtnBrowsePostContentDir_Click(object sender, RoutedEventArgs e)
        {
            PostContentDirectory = BrowseForFolder("Select Post Content Directory");
        }

        private void BtnBrowseRedirectDir_Click(object sender, RoutedEventArgs e)
        {
            RedirectDirectory = BrowseForFolder("Select Redirect Directory");
        }

        // Helper method for folder browsing
        private static string? BrowseForFolder(string description)
        {
            using var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog(description);

            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string selectedPath = dialog.FileName;
                return selectedPath;
            }

            return null;
        }

        private async void BtnGenerateBlueskyRss_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsProcessing = true;

                if (!ValidateRequiredSettings())
                {
                    return;
                }

                // Perform the RSS feed generation
                await Task.Run(async () =>
                {
                    await GenerateBlueSkyRssFeedHelper.GenerateBlueSkyRssFeed(
                        RedditAppId!,
                        RedditRedirectUri!,
                        RedditAppSecret!,
                        RedditUsername!,
                        GetRedditPassword()!,
                        false,
                        "Bluesky Auto Post",
                        "RSS feed generated for Bluesky Auto Post from Reddit Upvotes and Saved posts",
                        "https://www.patb.ca/rss/bluesky-auto-post.rss",
                        System.IO.Path.Combine(RssDirectory!, "bluesky-auto-post.xml"),
                        BlogPostDirectory!,
                        App.Options.LlmChoice,
                        RedditToBlueskyPath!,
                        PostContentDirectory!,
                        RedirectDirectory!
                        );
                });

                MessageBox.Show("Bluesky RSS feed has been generated successfully!",
                              "Success",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while generating the RSS feed:\n\n{ex.Message}{Environment.NewLine}{Environment.NewLine}{ex}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async void BtnGenerateContentRssPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsProcessing = true;

                if (!ValidateRequiredSettings())
                {
                    return;
                }

                // Perform the RSS feed generation
                await Task.Run(async () =>
                {
                    await GenerateContentRssPageHelper.GenerateContentRssPage(
                        RedditAppId!,
                        RedditRedirectUri!,
                        RedditAppSecret!,
                        RedditUsername!,
                        GetRedditPassword()!,
                        false,
                        "Bluesky Auto Post",
                        "RSS feed generated for Bluesky Auto Post from Reddit Upvotes and Saved posts",
                        "https://www.patb.ca/rss/bluesky-auto-post.rss",
                        System.IO.Path.Combine(RssDirectory!, "bluesky-auto-post.xml"),
                        BlogPostDirectory!,
                        App.Options.LlmChoice,
                        RedditToBlueskyPath!,
                        PostContentDirectory!,
                        RedirectDirectory!
                        );
                });

                MessageBox.Show("Bluesky RSS feed content has been generated successfully!",
                              "Success",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while generating the RSS feed content:\n\n{ex.Message}{Environment.NewLine}{Environment.NewLine}{ex}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async void BtnGenerateEverything_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsProcessing = true;

                if (!ValidateRequiredSettings())
                {
                    return;
                }

                // Perform the RSS feed generation
                await Task.Run(async () =>
                {
                    await GenerateEverythingHelper.GenerateEverything(
                        RedditAppId!,
                        RedditRedirectUri!,
                        RedditAppSecret!,
                        RedditUsername!,
                        GetRedditPassword()!,
                        false,
                        "Bluesky Auto Post",
                        "RSS feed generated for Bluesky Auto Post from Reddit Upvotes and Saved posts",
                        "https://www.patb.ca/rss/bluesky-auto-post.rss",
                        System.IO.Path.Combine(RssDirectory!, "bluesky-auto-post.xml"),
                        BlogPostDirectory!,
                        App.Options.LlmChoice,
                        RedditToBlueskyPath!,
                        PostContentDirectory!,
                        RedirectDirectory!
                        );
                });

                MessageBox.Show("Bluesky RSS feed content has been generated successfully!",
                              "Success",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while generating the RSS feed content:\n\n{ex.Message}{Environment.NewLine}{Environment.NewLine}{ex}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        public class BooleanToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (bool)value ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (Visibility)value == Visibility.Visible;
            }
        }
    }
}
