namespace DrainAffinity
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;

    using CommandLine.Text;

    internal class Program
    {
        private static string ContentDirectory;

        private static string WorkingDirectory;

        private static bool NoDelay;

        private static bool NoGallery;

        private static bool NoScraps;

        private static readonly List<string> receipts = new List<string>();

        private static CookieContainer _cookies;

        static void Main(string[] args)
        {
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                var helpText = HelpText.AutoBuild(options);
                helpText.AdditionalNewLineAfterOption = false;
                helpText.Copyright = "Released to the Public Domain by BlueOtter 2015";
                helpText.Heading = "DrainAffinity.exe - A program to download a whole user gallery and scraps collection from FurAffinity";
                Console.WriteLine(helpText.ToString());
                Console.ReadLine();
                return;
            }

            var username = options.Username;
            var password = options.Password;
            var target = options.Target;
            ContentDirectory = options.Directory;
            WorkingDirectory = Path.Combine(options.Directory, ".work");
            NoDelay = options.NoDelay;
            NoGallery = options.NoGallery;
            NoScraps = options.NoScraps;
            var receiptsFile = Path.Combine(WorkingDirectory, "history.log");

            if (!Directory.Exists(ContentDirectory))
                Directory.CreateDirectory(ContentDirectory);

            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);

            ReadReceiptsFromDisk(receiptsFile);

            var loginTask = Login(username, password);
            loginTask.Wait();

            foreach (var targetArtist in target.Contains(",") ? target.Split(',').Select(s => s.Trim()).ToArray() : new[] { target.Trim() })
            {
                var crawlTask = CrawlUser(targetArtist);
                crawlTask.Wait();
                WriteReceiptsToDisk(receiptsFile);
                Console.WriteLine("Work complete: {0} files downloaded for {1}\r\n", crawlTask.Result, targetArtist);
            }

            Console.ReadLine();
        }

        public static async Task<bool> Login(string username, string password)
        {
            const string url = "https://www.furaffinity.net/login/?ref=http://www.furaffinity.net/";
            var cookieFAFile = Path.Combine(WorkingDirectory, "cookies_fa.txt");

            List<Cookie> cloudFlareCookies;

            // Get CloudFlare cookie
            {
                var http = (HttpWebRequest)WebRequest.Create(url);
                http.AllowAutoRedirect = false;
                http.CookieContainer = new CookieContainer();
                http.KeepAlive = true;
                http.Method = "GET";

                var random = new Random(Environment.TickCount);
                switch (random.Next(1, 12))
                {
                    case 1:
                        http.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.89 Safari/537.36";
                        break;
                    case 2:
                        http.UserAgent = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36";
                        break;
                    case 3:
                        http.UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.1 Safari/537.36";
                        break;
                    case 4:
                        http.UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.0 Safari/537.36";
                        break;
                    case 5:
                        http.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; AS; rv:11.0) like Gecko";
                        break;
                    case 6:
                        http.UserAgent = "Mozilla/5.0 (compatible, MSIE 11, Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko";
                        break;
                    case 7:
                        http.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.6; Windows NT 6.1; Trident/5.0; InfoPath.2; SLCC1; .NET CLR 3.0.4506.2152; .NET CLR 3.5.30729; .NET CLR 2.0.50727) 3gpp-gba UNTRUSTED/1.0";
                        break;
                    case 8:
                        http.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/6.0)";
                        break;
                    case 9:
                        http.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; Trident/6.0)";
                        break;
                    case 10:
                        http.UserAgent = "Mozilla/5.0 (Windows; U; MSIE 9.0; Windows NT 9.0; en-US)";
                        break;
                    default:
                        http.UserAgent = "Mozilla/5.0 (Windows NT 6.3; rv:36.0) Gecko/20100101 Firefox/36.0";
                        break;
                }

                http.Referer = "https://www.furaffinity.net";

                var httpResponse = (HttpWebResponse)(await http.GetResponseAsync());
                cloudFlareCookies = httpResponse.Cookies.Cast<Cookie>().ToList();
            }

            // Login
            if (!File.Exists(cookieFAFile))
            {
                var http = (HttpWebRequest)WebRequest.Create(url);
                http.AllowAutoRedirect = false;
                http.CookieContainer = new CookieContainer();
                http.KeepAlive = true;
                http.Method = "POST";
                http.ContentType = "application/x-www-form-urlencoded";
                var postData = string.Format("action={0}&retard_protection=1&name={1}&pass={2}&login={3}",
                    "login",
                    HttpUtility.UrlEncode(username),
                    HttpUtility.UrlEncode(password),
                    "Login+to%C2%A0FurAffinity");
                var dataBytes = Encoding.UTF8.GetBytes(postData);
                http.ContentLength = dataBytes.Length;
                http.Headers.Add("Origin", "https://www.furaffinity.net");
                http.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2272.89 Safari/537.36";
                http.Referer = "https://www.furaffinity.net/login";
                using (var postStream = await http.GetRequestStreamAsync())
                {
                    await postStream.WriteAsync(dataBytes, 0, dataBytes.Length);
                }

                var httpResponse = (HttpWebResponse)(await http.GetResponseAsync());

                var allCookies = new List<Cookie>(cloudFlareCookies);
                _cookies = new CookieContainer();
                foreach (var c in cloudFlareCookies)
                    _cookies.Add(c);
                _cookies.Add(httpResponse.Cookies);

                allCookies.AddRange(httpResponse.Cookies.Cast<Cookie>());

                WriteCookiesToDisk(cookieFAFile, allCookies);
                return true;
            }

            // Retrieve login cookies
            _cookies = ReadCookiesFromDisk(cookieFAFile);
            return false;
        }

        public static async Task<bool> SaveFileToDisk(string url, string file, bool overwrite)
        {
            if (!overwrite && File.Exists(file))
                return false;

            var http = (HttpWebRequest)WebRequest.Create(url);
            http.CookieContainer = _cookies;
            var httpResponse2 = (HttpWebResponse)(await http.GetResponseAsync());
            var responseStream = httpResponse2.GetResponseStream();
            if (responseStream == null)
                return false;

            using (var localFileStream = new FileStream(file, FileMode.Create))
            {
                var buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    await localFileStream.WriteAsync(buffer, 0, bytesRead);

                localFileStream.Close();
            }

            return true;
        }

        public static void WriteCookiesToDisk(string file, List<Cookie> cookieJar)
        {
            using (var stream = File.Create(file))
            {
                try
                {
                    Console.Out.Write("Writing cookies to disk... ");
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(stream, cookieJar);
                    Console.Out.WriteLine("Done.");
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine("Problem writing cookies to disk: " + e.GetType());
                }
            }
        }

        public static CookieContainer ReadCookiesFromDisk(string file)
        {
            try
            {
                using (var stream = File.Open(file, FileMode.Open))
                {
                    Console.Out.Write("Reading cookies from disk... ");
                    var formatter = new BinaryFormatter();
                    Console.Out.WriteLine("Done.");
                    var deserialized = (List<Cookie>)formatter.Deserialize(stream);
                    var ret = new CookieContainer();
                    foreach (var cookie in deserialized)
                        ret.Add(cookie);
                    return ret;
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Problem reading cookies from disk: " + e.GetType());
                return new CookieContainer();
            }
        }

        public static void WriteReceiptsToDisk(string file)
        {
            using (var stream = File.Create(file))
            {
                try
                {
                    Console.Out.Write("Writing receipts to disk... ");
                    var formatter = new BinaryFormatter();
                    formatter.Serialize(stream, receipts);
                    Console.Out.WriteLine("Done.");
                }
                catch (Exception e)
                {
                    Console.Out.WriteLine("Problem writing receipts to disk: " + e.GetType());
                }
            }
        }

        public static void ReadReceiptsFromDisk(string file)
        {
            if (!File.Exists(file))
                return;

            try
            {
                using (var stream = File.Open(file, FileMode.Open))
                {
                    Console.Out.Write("Reading receipts from disk... ");
                    var formatter = new BinaryFormatter();
                    Console.Out.WriteLine("Done.");
                    var deserialized = (List<string>)formatter.Deserialize(stream);
                    receipts.AddRange(deserialized);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Problem reading receipts from disk: " + e.GetType());
            }
        }


        public static async Task<int> CrawlUser(string target)
        {
            // Get gallery page
            Console.Out.WriteLine("Crawling user '{0}'", target);
            var downloadCount = 0;

            if (!NoGallery)
                for (var page = 1; page < 50; page++)
                {
                    var galleryUrl = string.Format("http://www.furaffinity.net/gallery/{0}/{1}/", target, page);
                    var galleryFile = Path.Combine(WorkingDirectory, string.Format("{0}_gallery_{1}.html", target, page));
                    await SaveFileToDisk(galleryUrl, galleryFile, false);

                    var thisDownloadCount = await QueueWork(new WorkRequest(target, WorkRequestAction.ParseGallery)
                                                     {
                                                         Page = page,
                                                         Url = galleryFile
                                                     });

                    downloadCount += thisDownloadCount;

                    if (thisDownloadCount == 0)
                        break;
                }

            if (!NoScraps)
                for (var page = 1; page < 50; page++)
                {
                    var scrapsUrl = string.Format("http://www.furaffinity.net/scraps/{0}/{1}/", target, page);
                    var scrapsFile = Path.Combine(WorkingDirectory, string.Format("{0}_scraps_{1}.html", target, page));
                    await SaveFileToDisk(scrapsUrl, scrapsFile, false);

                    var thisDownloadCount = await QueueWork(new WorkRequest(target, WorkRequestAction.ParseScraps)
                                    {
                                        Page = page,
                                        Url = scrapsFile
                                    });

                    downloadCount += thisDownloadCount;

                    if (thisDownloadCount == 0)
                        break;
                }

            return downloadCount;
        }

        public static async Task<int> QueueWork(params WorkRequest[] requests)
        {
            var downloadCount = 0;

            foreach (var request in requests)
            {
                var page = request.Page;
                var target = request.Target;
                var url = request.Url;

                switch (request.Action)
                {
                    case WorkRequestAction.ParseGallery:
                    {
                        var result = await ParseGalleryPage(target, url, page.Value, "gallery");
                        if (result.Success)
                            downloadCount += await QueueWork(result.Subtasks);
                        break;
                    }
                    case WorkRequestAction.ParseScraps:
                    {
                        var result = await ParseGalleryPage(target, url, page.Value, "scraps");
                        if (result.Success)
                            downloadCount += await QueueWork(result.Subtasks);
                        break;
                    }
                    case WorkRequestAction.ViewImage:
                    {
                        if (!receipts.Contains(url))
                        {
                            var result = await ParseViewImagePage(target, url);
                            if (result.Success)
                            {
                                //Console.Out.WriteLine("Queuing work for ViewImage activity: {0}", url);
                                downloadCount += await QueueWork(result.Subtasks);
                                receipts.Add(url);
                            }
                        }
                        break;
                    }
                    case WorkRequestAction.DownloadContent:
                    {
                        var targetPath = Path.Combine(ContentDirectory, target);
                        if (!Directory.Exists(targetPath))
                            Directory.CreateDirectory(targetPath);

                        var saveFile = url.Substring(url.LastIndexOf('/') + 1);
                        var file = Path.Combine(targetPath, saveFile);
                        if (!File.Exists(file))
                        {
                            if (await SaveFileToDisk(url, file, false))
                            {
                                Console.Out.WriteLine("Downloaded '{0}''s file {1}", target, saveFile);
                                downloadCount++;

                                var random = new Random(Environment.TickCount);
                                var sleep = NoDelay
                                    ? 300
                                    : Convert.ToInt32(500 * Math.Log10(random.Next(10, 10000)));
                                System.Threading.Thread.Sleep(sleep);
                            }
                        }
                        break;
                    }
                }
            }

            return downloadCount;
        }

        public static async Task<WorkResult> ParseGalleryPage(string target, string file, int page, string type)
        {
            Console.Out.WriteLine("Parsing {2} for user '{0}', page {1}", target, page, type);

            using (var sr = new StreamReader(file))
            {
                var content = await sr.ReadToEndAsync();
                sr.Close();

                var thumbnails = Regex.Matches(content, "t-image\"><u><s><a href=\"/view/(?<num>\\d+)/\">");

                // Randomize order of pulls.
                var random = new Random(Environment.TickCount);

                if (thumbnails.Count > 0)
                {
                    var subtasks = thumbnails.Cast<Match>().OrderBy(m => random.Next()).Select(m => new WorkRequest(target, WorkRequestAction.ViewImage)
                                                                        {
                                                                            Url = string.Format("https://www.furaffinity.net/view/{0}/", m.Groups["num"].Value)
                                                                        }).ToArray();

                    return new WorkResult(WorkRequestAction.ParseGallery, true)
                           {
                               Subtasks = subtasks
                           };
                }
            }

            return new WorkResult(WorkRequestAction.ParseGallery, false);
        }

        public static async Task<WorkResult> ParseViewImagePage(string target, string url)
        {
            var file = Path.GetTempFileName();
            await SaveFileToDisk(url, file, true); // Must be true for temp file.

            using (var sr = new StreamReader(file))
            {
                var content = await sr.ReadToEndAsync();
                sr.Close();

                var match = Regex.Match(content, "<a href=\"(?<file>[^\"]+)\">Download");
                if (match.Success && match.Groups["file"].Success)
                {
                    var downloadUrl = match.Groups["file"].Value;
                    return new WorkResult(WorkRequestAction.ViewImage, true)
                           {
                               Subtasks = new[]
                                          {
                                              new WorkRequest(target, WorkRequestAction.DownloadContent)
                                              {
                                                  Url = "https:" + downloadUrl
                                              }
                                          }
                           };
                }
            }

            return new WorkResult(WorkRequestAction.ViewImage, false);
        }
    }
}
