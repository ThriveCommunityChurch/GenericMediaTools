using CommandLine;

namespace DupeCleaner
{
    public class Options
    {
        [Option('p', "Path", Required = true, HelpText = "The file path to check for duplicates in. Can search within all subdirectories (see appsettings.json)")]
        public string Path { get; set; }
    }
}