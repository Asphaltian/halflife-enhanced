namespace GoldSrc;

public static class Net
{
	[Rpc.Host]
	public static void SendConnectPacket( string userInfo )
	{
		Server.Get( Game.ActiveScene )?.ConnectClient( Rpc.Caller, userInfo );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	public static void OutOfBandPrint( string packet )
	{
		Client.Current?.ConnectionlessPacket( packet );
	}

	[Rpc.Host( NetFlags.Unreliable )]
	public static void SendPacketToServer( byte[] packet )
	{
		Server.Get( Game.ActiveScene )?.GetPacket( Rpc.Caller, packet );
	}

	[Rpc.Broadcast( NetFlags.Unreliable | NetFlags.HostOnly )]
	public static void SendPacketToClient( byte[] packet )
	{
		Client.Current?.GetPacket( packet );
	}
}
