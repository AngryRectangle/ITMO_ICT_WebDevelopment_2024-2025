# Задание 2
Реализовать клиентскую и серверную часть приложения. Клиент запрашивает выполнение математической операции, параметры которой вводятся с клавиатуры. Сервер обрабатывает данные и возвращает результат клиенту.

Варианты операций:
- Теорема Пифагора.
- Решение квадратного уравнения.
- Поиск площади трапеции. 
- Поиск площади параллелограмма.

Порядок выбора варианта: Выбирается по порядковому номеру в журнале (пятый студент получает вариант 1 и т.д.).

# Требования
- Обязательно использовать библиотеку socket.
- Реализовать с помощью протокола TCP.

# Ход работы
Мой номер 21, мой вариант 1. Теорема Пифагора.
Всё реализовано, как и в первой лабораторной работе, только вместо ожидания конкретного сообщения от клиента,
ожидается пока клиент отправит 8 байт (по 4 байта на каждое число), а сервер отправит ему в ответ 4 байта (результат).

# Листинг кода
#### Сервер
```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Server;

class Program
{
    static async Task Main(string[] args)
    {
        var listenPort = 22102;
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, listenPort));

        // https://stackoverflow.com/questions/38191968/c-sharp-udp-an-existing-connection-was-forcibly-closed-by-the-remote-host
        const int SIO_UDP_CONNRESET = -1744830452;
        socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

        EndPoint client = new IPEndPoint(IPAddress.Any, listenPort);

        var clients = new ConcurrentDictionary<EndPoint, Connection>();
        var buffer = new byte[1024];
        while (true)
        {
            var received = await socket.ReceiveFromAsync(buffer, client);
            if (!clients.TryGetValue(received.RemoteEndPoint, out var connection))
            {
                connection = new Connection(received.RemoteEndPoint);
                clients.TryAdd(received.RemoteEndPoint, connection);
            }

            var isTargetKeysFound = Receive(connection, buffer.AsSpan(0, received.ReceivedBytes));
            if (!isTargetKeysFound)
                continue;

            var aValue = BitConverter.ToInt32(connection.ReceivedData.ToArray(), 0);
            var bValue = BitConverter.ToInt32(connection.ReceivedData.ToArray(), 4);
            var cValue = (float)Math.Sqrt(aValue * aValue + bValue * bValue);
            var dataToSend = BitConverter.GetBytes(cValue);
            connection.SendingData = dataToSend;
            connection.SentBytes = 0;
            
            Console.WriteLine($"Received a: {aValue}, b: {bValue}, sending c: {cValue} to client {received.RemoteEndPoint}");
            await Send(socket, connection);
        }
    }

    private static async Task Send(Socket socket, Connection connection)
    {
        var bufferToSend = connection.SendingData.AsMemory(connection.SentBytes,
            connection.SendingData.Length - connection.SentBytes);
        connection.SentBytes += await socket.SendToAsync(bufferToSend, connection.Client);
    }

    private static bool Receive(Connection connection, ReadOnlySpan<byte> receivedBytes)
    {
        connection.ReceivedData.AddRange(receivedBytes.ToArray());

        if (connection.ReceivedData.Count < 8)
            return false;

        return true;
    }

    private class Connection
    {
        public readonly EndPoint Client;
        public readonly List<byte> ReceivedData = new();
        public int SentBytes;
        public byte[]? SendingData;

        public Connection(EndPoint client)
        {
            Client = client;
        }
    }
}
```

#### Клиент
```csharp
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
```