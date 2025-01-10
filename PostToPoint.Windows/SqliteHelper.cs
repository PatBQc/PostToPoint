using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PostToPoint.Windows
{
    public class SqliteHelper
    {
        private static readonly string DatabaseFilename = App.Options.SqlLiteDatabaseFilename ?? throw new InvalidOperationException("Database filename not configured");
        private static readonly string ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabaseFilename
        }.ToString();

        static SqliteHelper()
        {
            if (string.IsNullOrWhiteSpace(DatabaseFilename))
            {
                throw new InvalidOperationException("Database filename cannot be empty");
            }

            // Ensure the database file is created if it doesn't exist
            if (!System.IO.File.Exists(DatabaseFilename))
            {
                // Create the directory if it doesn't exist
                var directory = System.IO.Path.GetDirectoryName(DatabaseFilename);
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // Just opening a connection will create the database file if it doesn't exist
                using var connection = new SqliteConnection(ConnectionString);

                connection.Open();

                // Create a table for posts
                var command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS RedditPostsBluesky (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Created TEXT NOT NULL,
                        RedditPostId TEXT NOT NULL,
                        RedditTitle TEXT NOT NULL,
                        RedditPermalink TEXT NOT NULL,
                        BlueskyPost TEXT NOT NULL,
                        BlueskyShortUri TEXT NOT NULL,
                        BlueskyImageUri TEXT NOT NULL,
                        BlueskyVideoUri TEXT NOT NULL
                    );
                    """;
                command.ExecuteNonQuery();
            }
        }

        public static bool DoesPostExistInBluesky(RedditPostData postData)
        {
            ArgumentNullException.ThrowIfNull(postData);
            ArgumentNullException.ThrowIfNull(postData.Post);

            using var connection = new SqliteConnection(ConnectionString); 
            connection.Open();

            var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT 1 FROM RedditPostsBluesky WHERE RedditPostId = $postId;";
            selectCommand.Parameters.AddWithValue("$postId", postData.Post.Id);

            using var reader = selectCommand.ExecuteReader();
            return reader.Read();
        }

        public static void AppendRedditPostToBluesky(RedditPostData postData, string post, string shortUri, string imageUri, string videoUri)
        {
            ArgumentNullException.ThrowIfNull(postData);
            ArgumentNullException.ThrowIfNull(postData.Post);
            ArgumentNullException.ThrowIfNull(post);
            ArgumentNullException.ThrowIfNull(shortUri);
            ArgumentNullException.ThrowIfNull(imageUri);
            ArgumentNullException.ThrowIfNull(videoUri);

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                INSERT INTO RedditPostsBluesky (Created, RedditPostId, RedditTitle, RedditPermalink, BlueskyPost, BlueskyShortUri, BlueskyImageUri, BlueskyVideoUri)
                VALUES ($created, $postId, $title, $permalink, $post, $shortUri, $imageUri, $videoUri);
                """;
            insertCommand.Parameters.AddWithValue("$created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            insertCommand.Parameters.AddWithValue("$postId", postData.Post.Id);
            insertCommand.Parameters.AddWithValue("$title", postData.Title);
            insertCommand.Parameters.AddWithValue("$permalink", postData.Post.Permalink);
            insertCommand.Parameters.AddWithValue("$post", post);
            insertCommand.Parameters.AddWithValue("$shortUri", shortUri);
            insertCommand.Parameters.AddWithValue("$imageUri", imageUri);
            insertCommand.Parameters.AddWithValue("$videoUri", videoUri);

            insertCommand.ExecuteNonQuery();
        }
    }
}
