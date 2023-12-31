using System;
using System.Collections.Generic;
using Godot;
using Riptide;
using Riptide.Utils;

namespace Networking
{
  public partial class NetworkManager : Node
  {
    public static bool SafeMode = true; // If safe mode, use tcp since portforwarding sometime's doesn't work on udp for me.

    public static Server LocalServer;
    public static Client LocalClient;
    public static bool IsHost => LocalServer != null;
    public static Action<ServerConnectedEventArgs> ClientConnected;

    private static NetworkManager s_Me;

    private Dictionary<string, int> _nameIndexes = new Dictionary<string, int>();

    public override void _Ready()
    {
      s_Me = this;

      RiptideLogger.Initialize(GD.Print, GD.Print, GD.PushWarning, GD.PushError, false);
    }

    public override void _PhysicsProcess(double delta)
    {
      if (LocalServer != null) LocalServer.Update();
      if (LocalClient != null) LocalClient.Update();
    }

    public static NodeType SpawnNetworkSafe<NodeType>(PackedScene packedScene, string baseName, int authority = 1) where NodeType : Node
    {
      NodeType node = packedScene.Instantiate<NodeType>();

      if (!s_Me._nameIndexes.ContainsKey(baseName)) s_Me._nameIndexes.Add(baseName, 0);

      node.Name = baseName + " " + s_Me._nameIndexes[baseName];

      node.SetMultiplayerAuthority(authority);

      s_Me._nameIndexes[baseName]++;

      return node;
    }

    public static void SendRpcToServer(NetworkPointUser source, string name, Action<Message> messageBuilder = null, MessageSendMode messageSendMode = MessageSendMode.Reliable)
    {
      Message message = Message.Create(messageSendMode, 0);
      message.AddString(name);
      message.AddString(source.GetPath());

      messageBuilder?.Invoke(message);

      LocalClient.Send(message);
    }

    public static void SendRpcToClients(NetworkPointUser source, string name, Action<Message> messageBuilder = null, MessageSendMode messageSendMode = MessageSendMode.Reliable)
    {
      Message message = Message.Create(messageSendMode, 0);
      message.AddString(name);
      message.AddString(source.GetPath());

      messageBuilder?.Invoke(message);

      LocalServer.SendToAll(message);
    }

    public static void BounceRpcToClients(NetworkPointUser source, string name, Action<Message> messageBuilder = null, MessageSendMode messageSendMode = MessageSendMode.Reliable)
    {
      Message message = Message.Create(messageSendMode, 1);
      message.AddString(name);
      message.AddString(source.GetPath());

      messageBuilder?.Invoke(message);

      LocalClient.Send(message);
    }

    public static bool Host()
    {
      if (SafeMode)
      {
        LocalServer = new Server(new Riptide.Transports.Tcp.TcpServer());
      }
      else
      {
        LocalServer = new Server(new Riptide.Transports.Udp.UdpServer());
      }

      try
      {
        LocalServer.Start(25566, 2, 0, false);
      }
      catch
      {
        LocalServer = null;

        return false;
      }

      LocalServer.MessageReceived += s_Me.OnMessageRecieved;

      LocalServer.ClientConnected += s_Me.OnClientConnected;

      Join("127.0.0.1");

      return true;
    }

    public static bool Join(string address)
    {
      if (SafeMode)
      {
        LocalClient = new Client(new Riptide.Transports.Tcp.TcpClient());
      }
      else
      {
        LocalClient = new Client(new Riptide.Transports.Udp.UdpClient());
      }

      LocalClient.Connect(address + ":25566", 5, 0, null, false);

      LocalClient.MessageReceived += s_Me.OnMessageRecieved;

      return true;
    }

    public static bool IsOwner(Node node)
    {
      return node.GetMultiplayerAuthority() == LocalClient.Id;
    }

    private void HandleMessage(Message message)
    {
      string name = message.GetString();

      string path = message.GetString();

      if (!HasNode(path))
      {
        if (message.SendMode == MessageSendMode.Reliable) GD.PushWarning("Ignoring Reliable Rpc " + name + " for node " + path + " because the node does not exist!");

        return;
      }

      GetNode<NetworkPointUser>(path).NetworkPoint.HandleMessage(name, message);
    }

    private void OnMessageRecieved(Object _, MessageReceivedEventArgs eventArguments)
    {
      if (eventArguments.MessageId == 1)
      {
        Message relayMessage = Message.Create(eventArguments.Message.SendMode, 0);

        while (eventArguments.Message.UnreadBits > 0)
        {
          int bitsToWrite = Math.Min(eventArguments.Message.UnreadBits, 8);

          byte bits;

          eventArguments.Message.GetBits(bitsToWrite, out bits);

          relayMessage.AddBits(bits, bitsToWrite);
        }

        LocalServer.SendToAll(relayMessage);

        return;
      }

      HandleMessage(eventArguments.Message);
    }

    private void OnClientConnected(object server, ServerConnectedEventArgs eventArguments)
    {
      ClientConnected?.Invoke(eventArguments);
    }
  }
}