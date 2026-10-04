namespace GoldSrc;

public sealed class ServerFrame
{
	public const int MaxWeaponData = 64;

	public double SentTime;
	public float PingTime;

	public readonly byte[] ClientData = new byte[LocalClientData.Size];
	public readonly byte[][] WeaponData = new byte[MaxWeaponData][];
	public readonly PacketEntities Entities = new();

	public ServerFrame()
	{
		for ( var i = 0; i < WeaponData.Length; i++ )
			WeaponData[i] = new byte[GoldSrc.WeaponData.Size];
	}
}

public sealed class ServerClient( int slot )
{
	public const int MessageSize = NetChannel.MessageSize;
	public const int DatagramSize = 6000;

	public int Slot => slot;

	public int EdictIndex => slot + 1;

	public bool Active;
	public bool Spawned;
	public bool FullyConnected;
	public bool Connected;
	public bool Uploading;
	public bool HasUserMessages;
	public bool SentNewResponse;
	public int SendResourceCount;
	public int CrcValue;
	public bool FakeClient;
	public bool Proxy;
	public bool Loopback;

	public int UserId;
	public ulong SteamId;
	public Connection Connection;
	public uint VoiceStreams;

	public void SendPacket( byte[] packet )
	{
		if ( Connection is null )
			return;

		using ( Rpc.FilterInclude( Connection ) )
			Net.SendPacketToClient( packet );
	}
	public string Address = "";
	public string UserInfo = "";
	public string PhysInfo = "";
	public string Name = "";
	public bool SendInfo;
	public double SendInfoTime;

	public int TopColor;
	public int BottomColor;
	public bool LocalWeapons;
	public bool LagCompensation;
	public bool VoiceLoopback;

	public double ConnectionStarted;
	public double NextMessageTime;
	public double NextMessageInterval;
	public bool SendMessage;
	public bool SkipMessage;

	public int ChokeCount;
	public int DeltaSequence = -1;
	public UserCmd LastCmd;
	public double ConnectTime;
	public double CmdTime;
	public double IgnoreCmdTime;
	public float Latency;
	public double NextPing;
	public float PacketLoss;
	public double SvTimeBase;

	public const int MaxEventQueue = 64;
	public const int MinBlockSize = 256;
	public const int MaxBlockSize = 1024;

	public readonly EventInfo[] Events = new EventInfo[MaxEventQueue];

	public ServerFrame[] Frames = [];

	public readonly NetChannel Netchan = new();
	public readonly SizeBuf Datagram = new( "", DatagramSize, true );

	public SizeBuf Message => Netchan.Message;

	public double Rate
	{
		get => Netchan.Rate;
		set => Netchan.Rate = value;
	}

	public int BlockSize()
	{
		if ( !Active || !Spawned || !Connected || !FullyConnected )
			return MaxBlockSize;

		var value = Info.ValueForKey( UserInfo, "cl_dlmax" );

		return value.Length == 0 ? MinBlockSize : System.Math.Clamp( Common.Atoi( value ), MinBlockSize, MaxBlockSize );
	}
}
