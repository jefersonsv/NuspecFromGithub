using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuspecFromGithub
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CommandLineParser.CommandLineParser parser = new CommandLineParser.CommandLineParser();

            ValueArgument<string> project = new ValueArgument<string>(
                'p', "project", "Specify the project path file");
            project.Optional = false;

            ValueArgument<string> github = new ValueArgument<string>(
                'g', "github", "Specify the username/repository of github");
            github.Optional = false;

            SwitchArgument force = new SwitchArgument('f', "force", "Force recreate file", false);

            parser.Arguments.Add(project);
            parser.Arguments.Add(force);
            parser.Arguments.Add(github);

            try
            {
                parser.ParseCommandLine(args);

                // Test project patclienh
                var fullPath = Path.GetFullPath(project.Value);
                if (!System.IO.Directory.Exists(fullPath))
                {
                    throw new CommandLineArgumentException($"The path {fullPath} don't exist", "p");
                }

                var ret = RunProgram("nuget", "spec " + (force.Value ? " -force" : string.Empty), fullPath);
                var fileCreated = new Regex(@".*'(?<value>.*?)'.*").Match(string.Join(Environment.NewLine, ret.Select(s => s.Value))).Groups["value"].Value;

                var fullPathFileCreated = Path.Combine(fullPath, fileCreated);

                XDocument doc = XDocument.Parse(File.ReadAllText(fullPathFileCreated));

                // Get information
                var repo = GetByUrl($"https://api.github.com/repos/{github.Value}");
                var author = GetByUrl(repo["owner"]["url"].ToString());
                var license = GetByUrl(repo["license"]["url"].ToString());
                var master = GetByUrl(repo["branches_url"].ToString().Replace("{/branch}", "/" + repo["default_branch"].ToString()));
                var tags = GetByUrl(repo["tags_url"].ToString());

                // Get assemblyinfo
                var assemblyInfo = Directory.GetFiles(fullPath, "AssemblyInfo.cs", SearchOption.AllDirectories);

                var xml = doc.Root.Element("metadata");
                xml.Element("id").Value = repo["name"].Value<string>();

                if (assemblyInfo.FirstOrDefault() != null)
                {
                    var file = File.ReadAllText(assemblyInfo.First());
                    xml.Element("version").Value = new Regex(@"\[assembly: AssemblyFileVersion\(""(?<semver>[0-9]+\.[0-9]+\.[0-9]+\.0)""\)\]", RegexOptions.Singleline | RegexOptions.IgnoreCase).Match(file).Groups["semver"].Value;
                }
                else
                {
                    xml.Element("version").Value = "1.0.0";
                }
                xml.Element("title").Value = repo["description"].Value<string>();
                xml.Element("authors").Value = author["name"].Value<string>();
                xml.Element("owners").Value = author["name"].Value<string>();
                xml.Element("licenseUrl").Value = license["html_url"].Value<string>();
                xml.Element("projectUrl").Value = repo["html_url"].Value<string>();

                // Get icon
                var logo = Directory.GetFiles(fullPath, "logo.png", SearchOption.AllDirectories);
                if (logo.FirstOrDefault() != null)
                {
                    xml.Element("iconUrl").Value = $"https://github.com/{github}/" + repo["default_branch"].ToString() + "/logo.png";
                }
                else
                {
                    xml.Element("iconUrl").Value = "https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/NuGet_project_logo.svg/220px-NuGet_project_logo.svg.png";
                }
                xml.Element("description").Value = repo["description"].Value<string>();
                xml.Element("releaseNotes").Value = master["commit"]["commit"]["message"].Value<string>();
                //doc.Element("copyright").Value = repo["html_url"].ToString();
                xml.Element("tags").Value = string.Join(" ", tags.SelectTokens("/").Values<string>());

                doc.Save(fullPathFileCreated);
            }
            catch (CommandLineArgumentException ex)
            {
                if (args.Count() > 0)
                {
                    Console.WriteLine(ex.Message);
                }
                parser.ShowUsage();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static JToken GetByUrl(string url)
        {
            HttpClient client = GetClient();
            var get = client.GetAsync(url);
            var result = get.Result.Content.ReadAsStringAsync().Result;
            return Newtonsoft.Json.Linq.JToken.Parse(result);
        }

        public static HttpClient GetClient()
        {
            var proxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
            var uri = new Uri(proxy);

            var httpClientHandler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://{uri.Authority}", false, null, new NetworkCredential()
                {
                    UserName = uri.UserInfo.Split(':').First(),
                    Password = uri.UserInfo.Split(':').Last()
                }),
                UseProxy = true
            };

            var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0 Mozilla/5.0 (Macintosh; Intel Mac OS X x.y; rv:42.0) Gecko/20100101 Firefox/42.0.");
            return client;
        }

        public static KeyValuePair<bool, string>[] RunProgram(string exe, string arguments, string workingDirectory)
        {
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                if (!workingDirectory.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
                    workingDirectory += System.IO.Path.DirectorySeparatorChar.ToString();
            }

            var openShellWindow = false;

            Process process = new Process();
            process.StartInfo.FileName = exe;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = openShellWindow;
            process.StartInfo.RedirectStandardOutput = !openShellWindow;
            process.StartInfo.RedirectStandardError = !openShellWindow;
            process.Start();
            string output = openShellWindow ? string.Empty : process.StandardOutput.ReadToEnd();
            string err = openShellWindow ? string.Empty : process.StandardError.ReadToEnd();

            List<KeyValuePair<bool, string>> t = new List<KeyValuePair<bool, string>>();
            if (!string.IsNullOrEmpty(output))
            {
                t.Add(new KeyValuePair<bool, string>(true, output));
            }

            if (!string.IsNullOrEmpty(err))
            {
                t.Add(new KeyValuePair<bool, string>(false, err));
            }

            process.WaitForExit();
            return t.ToArray();
        }
    }
}