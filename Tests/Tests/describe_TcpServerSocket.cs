using System.Net.Sockets;
using System.Net;
using System.Threading;
using NLog;
using System;
using System.Text;
using NSpec;

class describe_TcpServerSocket : nspec {
    const int Port = 1234;

    void when_created() {
        TcpServerSocket server = null;
        before = () => {
            server = new TcpServerSocket();
        };

        it["has no connected clients"] = () => server.connectedClients.should_be(0);
        it["is not listening"] = () => server.isConnected.should_be_false();

        it["can disconnect without triggering event"] = () => {
            server.OnDisconnect += (sender, e) => fail();
            server.Disconnect();
        };

        it["can listen"] = () => {
            server.Listen(Port);
            server.isConnected.should_be_true();
            // Cleanup
            server.Disconnect();
        };

        it["can not listen when address is used"] = () => {
            var blockingServer = new TcpServerSocket();
            blockingServer.Listen(Port);

            server.Listen(Port);
            server.isConnected.should_be_false();

            // Cleanup
            blockingServer.Disconnect();
        };

        it["can not send"] = () => server.Send(new byte[] { 1, 2 });

        context["when listening"] = () => {
            before = () => {
                server.Listen(Port);
            };

            after = () => {
                try {
                    server.Disconnect();
                } catch (Exception) {
                }
            };

            it["can disconnect"] = () => {
                var didDisconnect = false;
                server.OnDisconnect += (sender, e) => didDisconnect = true;
                server.Disconnect();
                didDisconnect.should_be_true();
                server.isConnected.should_be_false();
            };

            it["accepts connections"] = () => {
                var clientConnected = false;
                server.OnClientConnect += (sender, e) => clientConnected = true;
                createAndConnectClient(Port);
                server.connectedClients.should_be(1);
                clientConnected.should_be_true();
            };

            it["accepts multiple connections"] = () => {
                createAndConnectClient(Port);
                createAndConnectClient(Port);
                createAndConnectClient(Port);
                server.connectedClients.should_be(3);
            };

            context["when connection accepted"] = () => {
                Socket client1 = null;
                Socket client2 = null;
                before = () => {
                    client1 = createAndConnectClient(Port);
                    client2 = createAndConnectClient(Port);
                };

                it["can disconnect"] = () => {
                    server.Disconnect();
                    wait();
                    server.connectedClients.should_be(0);
                };

                it["receives client disconnect"] = () => {
                    var clientDidDisconntect = false;
                    server.OnReceive += (sender, e) => fail();
                    server.OnClientDisconnect += (sender, e) => clientDidDisconntect = true;
                    client1.Disconnect(false);
                    client1.Close();
                    wait();
                    server.connectedClients.should_be(1);
                    clientDidDisconntect.should_be_true();
                };

                it["receives message"] = () => {
                    var message = "Hello";
                    ReceiveEventArgs receiveEventArgs = null;
                    server.OnReceive += (sender, e) => receiveEventArgs = e;
                    client1.Send(Encoding.UTF8.GetBytes(message));
                    wait();
                    message.should_be(Encoding.UTF8.GetString(receiveEventArgs.bytes));
                    receiveEventArgs.client.should_not_be_null();
                };

                it["receives multiple messages"] = () => {
                    var message1 = "Hello1";
                    var message2 = "Hello2";
                    ReceiveEventArgs receiveEventArgs = null;
                    server.OnReceive += (sender, e) => receiveEventArgs = e;

                    client1.Send(Encoding.UTF8.GetBytes(message1));
                    wait();
                    message1.should_be(Encoding.UTF8.GetString(receiveEventArgs.bytes));

                    client1.Send(Encoding.UTF8.GetBytes(message2));
                    wait();
                    message2.should_be(Encoding.UTF8.GetString(receiveEventArgs.bytes));
                };

                it["can respond to client"] = () => {
                    var clientMessage = "Hello";
                    var serverMessage = "Hi";
                    var receivedMessage = string.Empty;
                    ReceiveEventArgs receiveEventArgs = null;
                    server.OnReceive += (sender, e) => receiveEventArgs = e;
                    client1.Send(Encoding.UTF8.GetBytes(clientMessage));
                    wait();

                    prepareForReceive(client1, msg => receivedMessage = msg);

                    server.SendWith(receiveEventArgs.client, Encoding.UTF8.GetBytes(serverMessage));
                    wait();

                    receivedMessage.should_be(serverMessage);
                };

                it["can send to all connected clients"] = () => {
                    var message = "Hello";
                    var client1ReceivedMessage = string.Empty;
                    var client2ReceivedMessage = string.Empty;
                    prepareForReceive(client1, msg => client1ReceivedMessage = msg);
                    prepareForReceive(client2, msg => client2ReceivedMessage = msg);

                    server.Send(Encoding.UTF8.GetBytes(message));
                    wait();

                    client1ReceivedMessage.should_be(message);
                    client2ReceivedMessage.should_be(message);
                };
            };
        };
    }

    void fail() {
        false.should_be_true();
    }

    void wait() {
        Thread.Sleep(20);
    }

    Socket createAndConnectClient(int port) {
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(IPAddress.Loopback, port);
        wait();
        return client;
    }

    void prepareForReceive(Socket socket, Action<string> onReceive) {
        var buffer = new byte[socket.ReceiveBufferSize];
        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
            ar => {
                var client = (Socket)ar.AsyncState;
                var bytesReceived = client.EndReceive(ar);
                var trimmedBuffer = new byte[bytesReceived];
                Array.Copy(buffer, trimmedBuffer, bytesReceived);
                onReceive(Encoding.UTF8.GetString(trimmedBuffer));
            }, socket);
    }
}

