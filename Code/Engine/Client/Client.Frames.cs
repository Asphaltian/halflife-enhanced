using System;

namespace GoldSrc;

public sealed class ClientFrame
{
	public const int MaxWeaponData = 64;

	public double ReceivedTime;
	public double Latency;
	public bool Invalid;
	public bool Choked;
	public double Time;

	public ushort ClientBytes;
	public ushort PlayerInfoBytes;
	public ushort PacketEntityBytes;
	public ushort TentityBytes;
	public ushort SoundBytes;
	public ushort EventBytes;
	public ushort UsrBytes;
	public ushort VoiceBytes;
	public ushort MsgBytes;

	public void ClearBytes()
	{
		ClientBytes = 0;
		PlayerInfoBytes = 0;
		PacketEntityBytes = 0;
		TentityBytes = 0;
		SoundBytes = 0;
		EventBytes = 0;
		UsrBytes = 0;
		VoiceBytes = 0;
		MsgBytes = 0;
	}

	public readonly EntityState[] PlayerState = new EntityState[Server.MaxClientsLimit];
	public readonly byte[] ClientData = new byte[LocalClientData.Size];
	public readonly byte[][] WeaponData = new byte[MaxWeaponData][];
	public readonly PacketEntities Entities = new();

	public ClientFrame()
	{
		for ( var i = 0; i < WeaponData.Length; i++ )
			WeaponData[i] = new byte[GoldSrc.WeaponData.Size];
	}
}

public sealed class LocalState
{
	public EntityState PlayerState;
	public LocalClientData Client;
	public readonly WeaponData[] WeaponData = new WeaponData[ClientFrame.MaxWeaponData];

	public void CopyFrom( LocalState other )
	{
		PlayerState = other.PlayerState;
		Client = other.Client;

		Array.Copy( other.WeaponData, WeaponData, WeaponData.Length );
	}
}

public struct ClientCommand
{
	public UserCmd Cmd;
	public float SentTime;
	public float ReceivedTime;
	public float FrameLerp;
	public bool ProcessedFuncs;
	public bool HeldBack;
	public int SendSize;
}

partial class Client
{
	[ConVar( "cl_clockreset" )]
	private static float ClockReset { get; set; } = 0.1f;

	private readonly NetChannel _netchan = new();
	private readonly ClientCommand[] _commands = new ClientCommand[UpdateBackup];
	private readonly LocalState[] _predictedFrames = new LocalState[UpdateBackup];
	private readonly Vector3[] _predictedOrigins = new Vector3[UpdateBackup];
	private readonly byte[] _nullClientData = new byte[LocalClientData.Size];
	private readonly byte[] _nullWeaponData = new byte[WeaponData.Size];
	private readonly WeaponData[] _frameWeaponData = new WeaponData[ClientFrame.MaxWeaponData];

	private int _playerNum;
	private int _parseCount;
	private int _parseCountMod;
	private int _lastIncomingSequence;
	private int _lastCommandAck;
	private const double MinClockDelta = 0.001;
	private const float DefaultFixTimeRate = 7.5f;

	[ConVar( "cl_fixtimerate" )]
	private static float FixTimeRate { get; set; } = DefaultFixTimeRate;

	private double _clockDelta;
	private double _messageTime;
	private double _oldMessageTime;
	private string _physInfo = "";
	private int _health;
	private int _weapons;
	private float _maxSpeed;
	private int _pushMsec;

	public int PlayerNum => _playerNum;

	public float ClientMaxSpeed => _maxSpeed;

	public bool IsNoClipping => GetFrame( _parseCountMod ).PlayerState[_playerNum].MoveType == (int)MoveType.NoClip;

	private ClientFrame GetFrame( int index ) => _frames[index & UpdateMask] ??= new ClientFrame();

	private LocalState GetPredictedFrame( int index ) => _predictedFrames[index & UpdateMask] ??= new LocalState();

	private void ParseTime( MessageReader message )
	{
		_oldMessageTime = _messageTime;
		_messageTime = message.ReadFloat();

		if ( MaxClients == 1 )
			_clientTime = _messageTime;

		var delta = _clientTime - _messageTime;

		if ( Math.Abs( delta ) <= ClockReset )
		{
			if ( delta != 0.0 )
				_clockDelta = delta;
		}
		else
		{
			_clockDelta = 0.0;
			_clientTime = _messageTime;
		}

		if ( _clientTime < _oldClientTime )
			_oldClientTime = _clientTime;
	}

	private void AdjustClock()
	{
		if ( _clockDelta == 0.0 || FixTimeRate == 0f || Math.Abs( _clockDelta ) < MinClockDelta )
			return;

		if ( FixTimeRate < 0f )
			FixTimeRate = DefaultFixTimeRate;

		var msec = (double)(int)(_clockDelta * 1000.0);
		var sign = msec < 0.0 ? 1.0 : -1.0;
		var adjustment = Math.Min( Math.Abs( msec ), FixTimeRate ) / 1000.0 * sign;

		if ( Math.Abs( adjustment ) < Math.Abs( _clockDelta ) )
		{
			_clockDelta += adjustment;
			_clientTime += adjustment;
		}

		if ( _clientTime < _oldClientTime )
			_oldClientTime = _clientTime;
	}

	private void ParseClientData( MessageReader message )
	{
		var clientDelta = Delta.LookupRegistration( "clientdata_t" );
		var weaponDelta = Delta.LookupRegistration( "weapon_data_t" );

		_parseCount = _netchan.IncomingSequence;

		for ( var i = _lastIncomingSequence + 1; i < _parseCount; i++ )
		{
			var skipped = GetFrame( i );

			if ( skipped.ReceivedTime >= 0.0 )
			{
				skipped.ReceivedTime = -1.0;
				skipped.Latency = 0.0;
			}
		}

		_parseCountMod = _parseCount & UpdateMask;

		var frame = GetFrame( _parseCountMod );

		frame.Time = _messageTime;

		ref var command = ref _commands[_netchan.IncomingAcknowledged & UpdateMask];
		var sentTime = (double)command.SentTime;

		command.ReceivedTime = (float)Host.RealTime;
		frame.ReceivedTime = Host.RealTime;
		frame.ClearBytes();

		if ( _lastCommandAck != -1 )
		{
			var predicted = GetPredictedFrame( _lastIncomingSequence - _lastCommandAck + _netchan.IncomingAcknowledged );
			var data = LocalClientData.Read( frame.ClientData );

			for ( var i = 0; i < _frameWeaponData.Length; i++ )
				_frameWeaponData[i] = WeaponData.Read( frame.WeaponData[i] );

			_clientDll.TxferPredictionData( ref frame.PlayerState[_playerNum], predicted.PlayerState, ref data, predicted.Client, _frameWeaponData, predicted.WeaponData );

			data.Write( frame.ClientData );

			for ( var i = 0; i < _frameWeaponData.Length; i++ )
				_frameWeaponData[i].Write( frame.WeaponData[i] );
		}

		_lastCommandAck = _netchan.IncomingAcknowledged;
		_lastIncomingSequence = _netchan.IncomingSequence;

		frame.Latency = Host.RealTime - sentTime;

		_latency = frame.Latency;

		message.StartBitReading();

		ClientFrame from = null;

		if ( message.ReadBits( 1 ) != 0 )
			from = GetFrame( (int)message.ReadBits( 8 ) );

		clientDelta.ParseDelta( message, from?.ClientData ?? _nullClientData, frame.ClientData, _messageTime );

		while ( message.ReadBits( 1 ) != 0 )
		{
			var index = (int)message.ReadBits( 6 );

			weaponDelta.ParseDelta( message, from?.WeaponData[index] ?? _nullWeaponData, frame.WeaponData[index], _messageTime );
		}

		message.EndBitReading();

		var parsed = LocalClientData.Read( frame.ClientData );

		_physInfo = parsed.PhysInfo;
		_health = (int)parsed.Health;
		_weapons = parsed.Weapons;
		_maxSpeed = parsed.MaxSpeed;
		_pushMsec = parsed.PushMsec;
	}
}
