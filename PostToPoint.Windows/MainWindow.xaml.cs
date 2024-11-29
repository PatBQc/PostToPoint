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
    public partial class MainWindow : Window
    {
        private string _redditAppId;
        private string _redditAppSecret;
        private string _redditRedirectUri;
        private string _redditUsername;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
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

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    }
}