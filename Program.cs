using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using SocksSharp;
using SocksSharp.Proxy;

class ProxyChecker {


    public static async Task Main() {
        Console.Title = "Proxy Checker By Yablo";
        WelcomeMessage.WelcomeMessage.ShowMessage();

        string proxyFilePath = PromptProxyFile();
        if (string.IsNullOrEmpty(proxyFilePath)) {
            Console.WriteLine("No proxy file selected. Exiting...");
            return;
        }

        string[] proxyList = File.ReadAllLines(proxyFilePath);
        int proxyListLength = proxyList.Length;

        Console.WriteLine($"Loaded {proxyListLength} proxies.");
        Console.Clear();

        int proxyType = PromptProxyType();

        Console.Clear();

        int counter = 0;
        ConcurrentBag<string> goodProxies = new ConcurrentBag<string>();

        string proxyDirectory = Path.GetDirectoryName(proxyFilePath);
        string savePath = Path.Combine(proxyDirectory, "good_proxies.txt");

        Parallel.ForEach(proxyList, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (proxy) => {
                int index = Interlocked.Increment(ref counter);

                bool isProxyWorking = await CheckProxy(proxy, proxyType);

                if (isProxyWorking) {
                    Console.WriteLine($"\u001b[37m[{index}/{proxyListLength}]\u001b[37m | \u001b[35mProxy:\u001b[32m [ {proxy} ] is VALID.");
                    goodProxies.Add(proxy);
                    try {
                        using (StreamWriter writer = new StreamWriter(savePath, true, System.Text.Encoding.UTF8)) {
                            writer.WriteLine(proxy);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Error saving proxy: " + ex.Message);
                    }
                } else {
                    Console.WriteLine($"\u001b[37m[{index}/{proxyListLength}]\u001b[37m | \u001b[35mProxy:\u001b[31m [ {proxy} ] is INVALID.");
                }
            });


        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"\nTotal good proxies: {goodProxies.Count}");
        Console.Clear();

        Console.WriteLine($"\nTotal good proxies: {goodProxies.Count}");
        await Console.Out.WriteLineAsync($"Proxies saved into {savePath}");

        Console.ReadLine();
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

    public static int PromptProxyType() {
        while (true) {
            Console.WriteLine("Proxy type:");
            Console.WriteLine("1 | HTTP");
            Console.WriteLine("2 | SOCKS4");
            Console.WriteLine("3 | SOCKS5");

            if (int.TryParse(Console.ReadLine(), out int proxyType) && (proxyType >= 1 && proxyType <= 3)) {
                return proxyType;
            } else {
                Console.WriteLine("Invalid proxy type. Please try again.");
            }
        }
    }

    public static async Task<bool> CheckProxy(string proxy, int proxyType) {
        string[] proxyParts = proxy.Split(':');
        string proxyAddress = proxyParts[0];
        int proxyPort = int.Parse(proxyParts[1]);

        var settings = new ProxySettings() {
            Host = proxyAddress,
            Port = proxyPort
        };


        try {

            switch (proxyType) {
                case 1:
                    // Check for HTTP proxy
                    using (var httpClientHandler = new HttpClientHandler {
                        Proxy = new WebProxy(proxyAddress, proxyPort),
                        UseProxy = true,
                        AllowAutoRedirect = false
                    })
                    using (var httpClient = new HttpClient(httpClientHandler)) {
                        httpClient.Timeout = TimeSpan.FromSeconds(10);
                        var response = await httpClient.GetAsync("https://www.example.com");
                        return response.IsSuccessStatusCode;
                    }

                case 2:
                    // Check for SOCKS4 proxy

                    using (var client = new TcpClient()) {
                        await client.ConnectAsync(proxyAddress, proxyPort);
                        var stream = client.GetStream();

                        // Send the SOCKS4 handshake
                        byte[] handshake = new byte[9];
                        handshake[0] = 4; // SOCKS4 version
                        handshake[1] = 1; // Command: establish TCP/IP stream
                        handshake[2] = (byte)(proxyPort / 256);
                        handshake[3] = (byte)(proxyPort % 256);
                        byte[] ipBytes = IPAddress.Parse("0.0.0.1").GetAddressBytes(); // Destination IP (dummy IP)
                        Array.Copy(ipBytes, 0, handshake, 4, 4);
                        handshake[8] = 0; // Null byte to terminate the user ID field

                        await stream.WriteAsync(handshake, 0, handshake.Length);

                        // Read the SOCKS4 response
                        byte[] response = new byte[8];
                        await stream.ReadAsync(response, 0, response.Length);

                        bool isProxyWorking = response[1] == 0x5A; // Check if response code is 0x5A (request granted)

                        return isProxyWorking;
                    }
                case 3:
                    // Check for SOCKS5 proxy
                    using (var proxyClientHandler = new ProxyClientHandler<Socks5>(settings))
                    using (var httpClient = new HttpClient(proxyClientHandler)) {
                        var response = await httpClient.GetAsync("http://example.com/");
                        return response.IsSuccessStatusCode;
                    }
            }

        } catch {
            // Proxy is not working
        }

        return false;
    }
}
