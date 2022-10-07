// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Sockets;

Console.WriteLine("Hello, World!");

//Server.SimpleHttpListenerExample("http://localhost:40808/");

Server.TcpListenerExample("127.0.0.1", 40808);

public class Server
{
    public static void TcpListenerExample(string host, int port)
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
            while (true)
            {
                Console.Write("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                using TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Connected!");

                data = null!;

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                int i;

                // Loop to receive all the data sent by the client.
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Translate data bytes to a ASCII string.
                    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Received: {0}", data);

                    // Process the data sent by the client.
                    data = data.ToUpper();

                    byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                    // Send back a response.
                    //stream.Write(msg, 0, msg.Length);

                    string fileName = Environment.ExpandEnvironmentVariables("%TEMP%") + "\\test.data";
                    FileInfo fi = new FileInfo(fileName);
                    // Construct a response.
                    using var fileStream = File.OpenRead(fileName);
                    byte[] buffer = new byte[1024];
                    long totalBytes = 0;
                    //s.ContentLength64 = fi.Length;
                    // TODO need to write raw HTTP prefix before body
                    //                    string topstuff = @$"""
                    //HTTP/1.1 200 OK
                    //Content-Type: application/octet-stream
                    //Last-Modified: Sun, 17 Apr 2022 03:32:06 GMT
                    //Accept-Ranges: bytes
                    //ETag: ""374b9ab6b52d81:0""
                    //Server: Microsoft-IIS/10.0
                    //X-Powered-By: ASP.NET
                    //Date: Wed, 27 Apr 2022 10:45:49 GMT
                    //Content-Length: {fi.Length}

                    //""";
                    string topstuff = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nAccept-Ranges: bytes\r\nContent-Length: {fi.Length}\r\n\r\n";
                    byte[] headerBuffer = System.Text.Encoding.UTF8.GetBytes(topstuff);
                    stream.Write(headerBuffer, 0, headerBuffer.Length);

                    try
                    {
                        while (true)
                        {
                            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0)
                                break;
                            totalBytes += bytesRead;
                            stream.Write(buffer, 0, buffer.Length);
                            Console.WriteLine($"\rBytes sent {totalBytes}");

                            // TODO  - testing deliberately cutting it short
                            stream.Close();
                            throw new Exception();
                        }
                        //string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
                        //  byte[] buffer  = System.Text.Encoding.UTF8.GetBytes(responseString);
                        // Get a response stream and write the response to it.
                        // You must close the output stream.
                        stream.Close();
                    }
                    catch
                    {
                        Console.ReadLine();
                    }

                    //listener.Stop();
                    //Console.WriteLine("Sent: {0}", data);
                }

                // Shutdown and end the connection
                client.Close();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
                        Console.ReadLine();
        }
        finally
        {
            server.Stop();
        }

        Console.WriteLine("\nHit enter to continue...");
        Console.Read();
    }
    // This example requires the System and System.Net namespaces.
    public static void SimpleHttpListenerExample(params string[] prefixes)
    {
        if (!HttpListener.IsSupported)
        {
            Console.WriteLine("Windows XP SP2 or Server 2003 is required to use the HttpListener class.");
            return;
        }
        // URI prefixes are required,
        // for example "http://localhost:40808/".
        if (prefixes == null || prefixes.Length == 0)
            throw new ArgumentException("prefixes");

        // Create a listener.
        HttpListener listener = new HttpListener();
        // Add the prefixes.
        foreach (string s in prefixes)
        {
            listener.Prefixes.Add(s);
        }
        listener.Start();
        Console.WriteLine("Listening...");
        // Note: The GetContext method blocks while waiting for a request.
        HttpListenerContext context = listener.GetContext();
        HttpListenerRequest request = context.Request;
        // Obtain a response object.
        HttpListenerResponse response = context.Response;
        string fileName = Environment.ExpandEnvironmentVariables("%TEMP%") + "\\test.data";
        FileInfo fi = new FileInfo(fileName);
        // Construct a response.
        using var fileStream = File.OpenRead(fileName);
        byte[] buffer = new byte[1024];
        long totalBytes = 0;
        Stream output = response.OutputStream;
        response.ContentLength64 = fi.Length;
        try
        { 
        while (true)
        {
            int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
                break;
            totalBytes += bytesRead;
            output.Write(buffer, 0, buffer.Length);
            Console.WriteLine($"\rBytes sent {totalBytes}");

            // TODO  - testing deliberately cutting it short
            throw new Exception("Test");
        }
        //string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
        //  byte[] buffer  = System.Text.Encoding.UTF8.GetBytes(responseString);
        // Get a response stream and write the response to it.
        // You must close the output stream.
        output.Close();
        }
        catch {
            Console.ReadLine();
        }

        listener.Stop();
    }
}