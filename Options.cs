namespace DrainAffinity
{
    using CommandLine;

    public class Options
    {
        [Option('u', "username", Required = true, HelpText = "Your FurAffinity username")]
        public string Username { get; set; }

        [Option('p', "password", Required = true, HelpText = "Your FurAffinity password")]
        public string Password { get; set; }

        [Option('t', "target", Required = true, HelpText = "FurAffinity username for user to download.  If a comma is present, this will be treated as a list of targets.")]
        public string Target { get; set; }

        [Option('d', "directory", Required = false, HelpText = "The directory to save downloaded files in", DefaultValue = @"c:\drainaffinity\")]
        public string Directory { get; set; }

        [Option("nogallery", Required=false, HelpText="If specified, gallery images won't be downloaded.  Just scrap ones.", DefaultValue = false)]
        public bool NoGallery { get; set; }

        [Option("noscraps", Required = false, HelpText = "If specified, scraps images won't be downloaded.  Just gallery ones.", DefaultValue = false)]
        public bool NoScraps { get; set; }

        [Option("nodelay", Required = false, HelpText = "If specified, images will download very quickly.  You'll also probably get identified and banned.", DefaultValue = false)]
        public bool NoDelay { get; set; }
    }
}
