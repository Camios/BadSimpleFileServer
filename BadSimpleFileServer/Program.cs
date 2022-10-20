// See https://aka.ms/new-console-template for more information
using System;
using System.Net;
using System.Net.Sockets;

object mLock = new();
//string fileName = Environment.ExpandEnvironmentVariables("%TEMP%") + "\\test.data";
string testFileName = Environment.ExpandEnvironmentVariables("%TEMP%") + "\\100meg.test";
string gzFileName = Environment.ExpandEnvironmentVariables("%TEMP%") + "\\100meg.test.gz";

Console.WriteLine("Listening on 127.0.0.1:40808!");
Console.WriteLine($"Serves a single file from \"{testFileName}\" or a gzip compressed");
Console.WriteLine("Commands:");
Console.WriteLine("d - disconnect open connection");
Console.WriteLine("q - quit");

//Server.SimpleHttpListenerExample("http://localhost:40808/");

using var ctsApp = new CancellationTokenSource();
bool toBeDisconnected = false;
Task.Run(() => TcpListenerExample("127.0.0.1", 40808, ctsApp.Token));

while (true)
{
    Console.WriteLine("Waiting for input:");
    var consoleKeyInfo = Console.ReadKey();
    var upperChar = char.ToUpper(consoleKeyInfo.KeyChar);
    Console.WriteLine($"Got: '{upperChar}'");
    switch (upperChar)
    {
        case 'D':
            Console.WriteLine();
            lock (mLock)
            {
                Console.WriteLine("Disconnect requested");
                toBeDisconnected = true;
            }
            break;
        case 'Q':
            Console.WriteLine();
            Console.WriteLine("Quit requested");
            ctsApp.Cancel();
            Environment.Exit(0);
            break;
    }
}


void TcpListenerExample(string host, int port, CancellationToken appCancellationToken)
{
    TcpListener server = null!;
    try
    {
        // Set the TcpListener on port 13000.
        IPAddress localAddr = IPAddress.Parse(host);

        // TcpListener server = new TcpListener(port);
        server = new TcpListener(localAddr, port);

        // Start listening for client requests.
        server.Start();

        // Buffer for reading data
        Byte[] bytes = new Byte[1024];
        String data = null!;

        // Enter the listening loop.
        while (true && !appCancellationToken.IsCancellationRequested)
        {
            Console.Write("Waiting for a connection... ");

            // Perform a blocking call to accept requests.
            // You could also use server.AcceptSocket() here.
            using TcpClient client = server.AcceptTcpClient();

            try
            {
                Console.WriteLine("Connected!");

                data = null!;

                // Get a stream object for reading and writing
                using NetworkStream stream = client.GetStream();
                using var testFileStream = File.OpenRead(testFileName);
                using var gzFileStream = File.OpenRead(gzFileName);

                int bytesReadFromClient;
                toBeDisconnected = false;

                // Loop to receive all the data sent by the client. For this Http client we'll just do it once 
                while (!toBeDisconnected && (bytesReadFromClient = stream.Read(bytes, 0, bytes.Length)) != 0 && !appCancellationToken.IsCancellationRequested)
                {
                    // Parse the request

                    // Translate data bytes to a ASCII string.
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, bytesReadFromClient);

                    if (bytesReadFromClient == bytes.Length)
                    {
                        throw new IOException($"This server can only handle requests of {bytes.Length} bytes or less.");
                    }
                    //Console.WriteLine("Received: {0}", data);


                    bool isRange = false;
                    long from = long.MaxValue;
                    long? to = null;
                    
                    string[] lines = data.Split("\r\n");
                    if (lines.Length == 0)
                    {
                        Console.WriteLine("Didn't get any lines - not a valid HTTP request: {0}", data);
                        continue;
                    }

                    string[] startLineParts = lines[0].Split(' ');

                    FileStream fileStream;
                    FileInfo fi;
                    if (startLineParts.Length != 3 || !startLineParts[1].EndsWith("gz"))
                    {
                        fileStream = testFileStream;
                        fi = new FileInfo(testFileName);
                    }
                    else
                    {
                        fileStream = gzFileStream;
                        fi = new FileInfo(gzFileName);
                    }

                    if (fi.Length == 0)
                        throw new IOException($"File {fi.FullName} has no content");

                    Console.WriteLine("Received request: {0}", lines[0]); // not doing a sophisticated HTTP quest tpye check
                    for (int j = 1; j < lines.Length; j++)
                    {
                        if (lines[j].Length == 0)
                            break;

                        string[] headers = lines[j].Split(":");
                        if (headers.Length == 2)
                        {
                            if (headers[0] == "Range")
                            {
                                string[] ranges = headers[1].Split(new string[] { "=", "-", ",", ", ", " " }, StringSplitOptions.RemoveEmptyEntries);
                                if (ranges.Length == 2 || ranges.Length == 3)
                                {
                                    if (ranges[0] == "bytes")
                                    {
                                        from = Convert.ToInt64(ranges[1]);
                                        if (ranges.Length > 2)
                                        {
                                            to = Convert.ToInt64(ranges[2]);
                                        }
                                        isRange = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Non-byte or multi-byte ranges (${lines[j]}) are not supported. Returning full content");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Non-byte or multi-byte ranges (${lines[j]}) are not supported. Returning full content");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Skipping header {headers[0]}: {headers[1]}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Skipping header {headers[0]}: {headers[1]}");
                        }
                    }

                    // Construct a response.
                    byte[] buffer = new byte[1024];
                    long totalBytesSent = 0;
                    long contentLength;

                    // need to write raw HTTP prefix before body
                    // e.g.
                    // HTTP/1.1 200 OK
                    // Content-Type: application/octet-stream
                    // Last-Modified: Sun, 17 Apr 2022 03:32:06 GMT
                    // Accept-Ranges: bytes
                    // ETag: ""374b9ab6b52d81:0""
                    // Server: Microsoft-IIS/10.0
                    // X-Powered-By: ASP.NET
                    // Date: Wed, 27 Apr 2022 10:45:49 GMT
                    // Content-Length: {fi.Length}
                    // <blank-line>
                    // <response-body>
                    string topstuff = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\n";
                    if (isRange)
                    {
                        if (to == null)
                        {
                            contentLength = fi.Length - from;
                            to = fi.Length - 1;
                        }
                        else
                        {
                            contentLength = to.Value - from + 1;
                        }
                        fileStream.Position = from;
                        topstuff += $"Content-Length: {contentLength}\r\nContent-Range: bytes {from}-{to}/{fi.Length}\r\n\r\n";
                    }
                    else
                    {
                        contentLength = fi.Length;
                        topstuff += $"Content-Length: {contentLength}\r\n\r\n";
                    }

                    // TODO return 4xx if range doesn't fit within fi.Length

                    byte[] headerBuffer = System.Text.Encoding.UTF8.GetBytes(topstuff);
                    stream.Write(headerBuffer, 0, headerBuffer.Length);

                    try
                    {
                        Console.WriteLine();
                        while (true && !appCancellationToken.IsCancellationRequested)
                        {
                            lock (mLock)
                            {
                                if (toBeDisconnected)
                                {
                                    Console.WriteLine($"\rDisconnected client. Bytes sent {totalBytesSent} of {fi.Length} ({totalBytesSent * 100.0 / fi.Length}%)");
                                    break;
                                }
                            }
                            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0)
                            {
                                Console.Write($"\rAll {totalBytesSent} bytes sent");
                                break;
                            }
                            totalBytesSent += bytesRead;
                            stream.Write(buffer, 0, buffer.Length);
                            Console.Write($"\rBytes sent {totalBytesSent}");
                        }

                        if (contentLength == totalBytesSent)
                        {
                            Console.Write($"\rAll {totalBytesSent} bytes sent");
                            Console.WriteLine();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                // Shutdown and end the connection
                client.Close();
            }
        }
    }
    catch (SocketException e)
    {
        Console.WriteLine("SocketException: {0}", e);
    }
    finally
    {
        server.Stop();
    }

    Console.WriteLine("\nHit enter to continue...");
    Console.Read();
}

