using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

class ProxyChecker {
    public static HashSet<string> checkedProxies = new HashSet<string>();
    public static HashSet<string> goodProxies = new HashSet<string>();

    public static async Task Main() {
        string proxyFilePath = PromptProxyFile();
        if (string.IsNullOrEmpty(proxyFilePath)) {
            Console.WriteLine("No proxy file selected. Exiting...");
            return;
        }

        string[] proxyList = File.ReadAllLines(proxyFilePath);

        Console.WriteLine($"Loaded {proxyList.Length} proxies.");
        Console.Clear();

        string proxyDirectory = Path.GetDirectoryName(proxyFilePath);
        string savePath = Path.Combine(proxyDirectory, "good_proxies.txt");


        ServicePointManager.DefaultConnectionLimit = 20; // Set your preferred connection limit here

        int threadCount = Math.Min(Environment.ProcessorCount, proxyList.Length); // Limit threads based on available processors

        List<Task> tasks = new List<Task>();

        Parallel.ForEach(proxyList, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, async (proxy, state, index) => {
            if (!checkedProxies.Contains(proxy)) {
                await CheckProxyAsync(proxy, proxyList.Length, savePath);
            }
        });

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Proxy checking completed.");
        Console.WriteLine($"{goodProxies.Count + 1} good proxies.");
    }

    public static string PromptProxyFile() {
        Console.WriteLine("Please enter the path to the proxy file:");
        string input = Console.ReadLine();
        input = input.Trim('"');
        string proxyFilePath = input.Replace("\\", "\\\\");

        if (!File.Exists(proxyFilePath)) {
            Console.WriteLine("The specified proxy file does not exist.");
            return null;
        }

        return proxyFilePath;
    }

    public static async Task CheckProxyAsync(string proxy, int totalProxies, string savePath) {
        if (await CheckProxy(proxy)) {
            lock (checkedProxies) {
                if (!checkedProxies.Contains(proxy)) {
                    checkedProxies.Add(proxy);

                    Console.WriteLine($"\u001b[37m[{checkedProxies.Count}/{goodProxies.Count + 1}/{totalProxies}]\u001b[37m | \u001b[35mProxy:\u001b[32m [ {proxy} ] is VALID.");

                    // Save the proxy to a file on the desktop
                    try {
                        goodProxies.Add(proxy);
                        using (StreamWriter writer = new StreamWriter(savePath, true, Encoding.UTF8)) {
                            writer.WriteLine(proxy);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Error saving proxy: " + ex.Message);
                    }
                }
            }
        } else {
            lock (checkedProxies) {
                if (!checkedProxies.Contains(proxy)) {
                    checkedProxies.Add(proxy);

                    Console.WriteLine($"\u001b[37m[{checkedProxies.Count}/{totalProxies}]\u001b[37m | \u001b[35mProxy:\u001b[31m [ {proxy} ] is INVALID.");
                }
            }
        }
    }

    public static Task<bool> CheckProxy(string proxy) {
        string[] proxyParts = proxy.Split(':');
        string proxyAddress = proxyParts[0];
        int proxyPort = int.Parse(proxyParts[1]);

        try {
            IWebProxy _proxy = new WebProxy(proxyAddress, proxyPort);
            WebClient wc = new WebClient();
            wc.Timeout = 10000;
            wc.Proxy = _proxy;
            wc.Encoding = Encoding.UTF8;
            string result = wc.DownloadString("http://ip-api.com/line/?fields=8192");
            return Task.FromResult(true);
        } catch {
            return Task.FromResult(false);
        }
    }
}

public class WebClient : System.Net.WebClient {
    public int Timeout { get; set; }
    protected override WebRequest GetWebRequest(Uri uri) {
        WebRequest lWebRequest = base.GetWebRequest(uri);
        lWebRequest.Timeout = Timeout;
        ((HttpWebRequest)lWebRequest).ReadWriteTimeout = Timeout;
        return lWebRequest;
    }
}
