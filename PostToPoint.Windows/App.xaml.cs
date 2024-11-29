using CommandLine;
using CommandLine.Text;
using System.Configuration;
using System.Data;
using System.Windows;

namespace PostToPoint.Windows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static CommandLineOptions Options = new CommandLineOptions();

        [STAThread]
        public static void Main2(string[] args)
        {
            var application = new App();
            application.InitializeComponent();
            application.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Explicitly declare the result variable
            ParserResult<CommandLineOptions> result = Parser.Default.ParseArguments<CommandLineOptions>(e.Args);

            // Parse the command line arguments
            result
                .WithParsed(opts =>
                {
                    // This code runs if all required parameters are provided
                    // and parsing succeeds

                    //if (opts.Verbose)
                    //{
                    //    Console.WriteLine($"Processing file: {opts.InputFile}");
                    //    Console.WriteLine($"Port: {opts.Port}");

                    //    if (opts.OutputFile != null)
                    //        Console.WriteLine($"Output file: {opts.OutputFile}");

                    //    if (opts.Tags != null)
                    //        Console.WriteLine($"Tags: {string.Join(", ", opts.Tags)}");
                    //}

                    Options = opts;
                })
                .WithNotParsed(errors =>
                {
                    // This code runs if there are any parsing errors
                    // (like missing required parameters)

                    var helpText = HelpText.AutoBuild(result);
                    Console.WriteLine(helpText);

                    // You might want to exit the application or show an error dialog
                    MessageBox.Show("Invalid command line arguments. Check --help for usage.");
                    Environment.Exit(1);
                });
        }
    }

}
