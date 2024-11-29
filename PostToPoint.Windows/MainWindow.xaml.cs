using System.ComponentModel;
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
        private string _redditAppId;
        private string _redditAppSecret;
        private string _redditRedirectUri;
        private string _redditUsername;

        private string _redditToBlueskyPath;
        private string _redditToBlogPath;
        private string _redditToLinkedInPath;
        private string _blogToBlueskyPath;
        private string _blogToLinkedInPath;


        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
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
            }));

        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public string RedditAppId
        {
            get => _redditAppId;
            set
            {
                _redditAppId = value;
                OnPropertyChanged(nameof(RedditAppId));
            }
        }

        public string RedditAppSecret
        {
            get => _redditAppSecret;
            set
            {
                _redditAppSecret = value;
                OnPropertyChanged(nameof(RedditAppSecret));
            }
        }

        public string RedditRedirectUri
        {
            get => _redditRedirectUri;
            set
            {
                _redditRedirectUri = value;
                OnPropertyChanged(nameof(RedditRedirectUri));
            }
        }

        public string RedditUsername
        {
            get => _redditUsername;
            set
            {
                _redditUsername = value;
                OnPropertyChanged(nameof(RedditUsername));
            }
        }

        // Method to get password (since PasswordBox doesn't support binding for security reasons)
        private string GetRedditPassword()
        {
            return pwbRedditPassword.Password;
        }

        // Method to set password
        private void SetRedditPassword(string password)
        {
            pwbRedditPassword.Password = password;
        }

        public string RedditToBlueskyPath
        {
            get => _redditToBlueskyPath;
            set
            {
                _redditToBlueskyPath = value;
                OnPropertyChanged(nameof(RedditToBlueskyPath));
            }
        }

        public string RedditToBlogPath
        {
            get => _redditToBlogPath;
            set
            {
                _redditToBlogPath = value;
                OnPropertyChanged(nameof(RedditToBlogPath));
            }
        }

        public string RedditToLinkedInPath
        {
            get => _redditToLinkedInPath;
            set
            {
                _redditToLinkedInPath = value;
                OnPropertyChanged(nameof(RedditToLinkedInPath));
            }
        }

        public string BlogToBlueskyPath
        {
            get => _blogToBlueskyPath;
            set
            {
                _blogToBlueskyPath = value;
                OnPropertyChanged(nameof(BlogToBlueskyPath));
            }
        }

        public string BlogToLinkedInPath
        {
            get => _blogToLinkedInPath;
            set
            {
                _blogToLinkedInPath = value;
                OnPropertyChanged(nameof(BlogToLinkedInPath));
            }
        }

        // Browse button click handlers
        private void btnBrowseRedditToBluesky_Click(object sender, RoutedEventArgs e)
        {
            RedditToBlueskyPath = BrowseForFile("Select Reddit to Bluesky Prompt File");
        }

        private void btnBrowseRedditToBlog_Click(object sender, RoutedEventArgs e)
        {
            RedditToBlogPath = BrowseForFile("Select Reddit to Blog Prompt File");
        }

        private void btnBrowseRedditToLinkedIn_Click(object sender, RoutedEventArgs e)
        {
            RedditToLinkedInPath = BrowseForFile("Select Reddit to LinkedIn Prompt File");
        }

        private void btnBrowseBlogToBluesky_Click(object sender, RoutedEventArgs e)
        {
            BlogToBlueskyPath = BrowseForFile("Select Blog to Bluesky Prompt File");
        }

        private void btnBrowseBlogToLinkedIn_Click(object sender, RoutedEventArgs e)
        {
            BlogToLinkedInPath = BrowseForFile("Select Blog to LinkedIn Prompt File");
        }

        // Helper method for file browsing
        private string BrowseForFile(string title)
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

    }
}