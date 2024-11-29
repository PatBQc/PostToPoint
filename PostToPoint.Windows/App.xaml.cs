using CommandLine;
using CommandLine.Text;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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

            // Parse the command line arguments to replace args with the contents of config files
            // if an argument starts with @, read the file and replace the argument with the contents of the file
            List<string> args = new List<string>();
            foreach(var arg in e.Args)
            {
                if (arg.StartsWith("@"))
                {
                    args.AddRange(File.ReadAllLines(arg.Substring(1)));
                }
                else
                {
                    args.Add(arg);
                }
            }

            // Explicitly declare the result variable
            ParserResult<CommandLineOptions> result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            
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
                    // This code runs if there are any parsing errors (like missing required parameters)
                    var helpText = HelpText.AutoBuild(result, h => 
                    {
                        h.AdditionalNewLineAfterOption = false;
                        h.Heading = "PostToPoint v" + Assembly.GetExecutingAssembly().GetName().Version;
                        h.AddPostOptionsLine("You can place all your arguments in a file, simply passing @file.ext in place of the arguments.  Can also be combined with real CLI arguments, the file will be expanded 'in place' where you put it.");
                        return HelpText.DefaultParsingErrorsHandler(result, h);
                    });
                    Console.WriteLine(helpText);
                    Debug.WriteLine(helpText);

                    // You might want to exit the application or show an error dialog
                    MessageBox.Show("Invalid command line arguments. Check --help for usage.");
                    Environment.Exit(1);
                });
        }
    }

}
