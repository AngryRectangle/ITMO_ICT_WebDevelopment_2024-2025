using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client;

class Program
{
    public static async Task Main(string[] args)
    {
        var targetPort = 22102;
        var targetAddress = IPAddress.Parse("127.0.0.1");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var targetEndPoint = new IPEndPoint(targetAddress, targetPort);

        var a = 0;
        var b = 0;
        Console.WriteLine("Write a and b of equation a^2 + b^2 = c^2 to get c:");
        var aB = Console.ReadLine().Split(' ');
        if (aB.Length != 2 || !int.TryParse(aB[0], out a) || !int.TryParse(aB[1], out b))
        {
            Console.WriteLine("Invalid input, exiting.");
            return;
        }

        var dataToSend = BitConverter.GetBytes(a).Concat(BitConverter.GetBytes(b)).ToArray();
        var sentBytes = 0;
        var receivedBytes = new byte[1024];
        var receivedBytesCount = 0;

        while (true)
        {
            try
            {
                // Пока нет никакого ответа от сервера шлём сообщения снова
                if (receivedBytesCount == 0)
                {
                    sentBytes += await socket.SendToAsync(dataToSend.AsMemory(sentBytes, dataToSend.Length - sentBytes), targetEndPoint);
                    if (sentBytes == dataToSend.Length)
                        sentBytes = 0;
                }

                try
                {
                    var newlyReceived = await socket.ReceiveAsync(receivedBytes);
                    Console.WriteLine("Received from server:");
                    Console.Write(BitConverter.ToSingle(receivedBytes, 0));
                    receivedBytesCount += newlyReceived;
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.TimedOut)
                    {
                        Console.WriteLine("Server is not responding, trying to send message again.");
                    }
                    else
                    {
                        Console.WriteLine(e);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}