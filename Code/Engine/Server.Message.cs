using System;
using System.IO;

namespace GoldSrc;

public enum MessageDest
{
	Broadcast,
	One,
	All,
	Init,
	Pvs,
	Pas,
	PvsReliable,
	PasReliable,
	OneUnreliable,
	Spectator
}

public readonly record struct UserMessage( int Id, int Size, string Name );

partial class Server
{
	public const int TempEntityMessage = 23;
	public const int SetPauseMessage = 24;
	public const int IntermissionMessage = 30;
	public const int CdTrackMessage = 32;
	public const int WeaponAnimMessage = 35;
	public const int RoomTypeMessage = 37;
	public const int HltvMessage = 50;
	public const int DirectorMessage = 51;

	public const int FirstUserMessage = 64;
	public const int MaxUserMessageSize = 192;

	private const int LastEngineMessage = 59;

	private static List<UserMessage> UserMessages = [];
	private static List<UserMessage> NewUserMessages = [];
	private static int _nextUserMessage = FirstUserMessage;

	private const int DatagramSize = 6000;
	private const int MulticastSize = 0x400;
	private const int SignonSize = 0x8000;
	private const int MessageBufferSize = 0x200;

	private readonly SizeBuf _message = new( "MessageBegin/End", MessageBufferSize, true );
	private readonly SizeBuf _datagram = new( "Server Datagram", DatagramSize, false );
	private readonly SizeBuf _reliableDatagram = new( "Server Reliable Datagram", DatagramSize, false );
	private readonly SizeBuf _multicast = new( "Server Multicast Buffer", MulticastSize, false );
	private readonly SizeBuf _spectator = new( "Server Spectator Buffer", MulticastSize, false );
	private readonly SizeBuf _signon = new( "Server Signon Buffer", SignonSize, false );

	private const int MulticastBroadcast = 1;
	private const int MulticastPvs = 2;
	private const int MulticastPas = 4;
	private const int MulticastSkipHost = 0x80;

	private Vector3 _messageOrigin;
	private bool _messageStarted;
	private int _messageType;
	private MessageDest _messageDest;
	private Edict _messageEntity;

	public static int RegUserMsg( string name, int size )
	{
		if ( _nextUserMessage > 255 )
		{
			Log.Info( $"Not enough room to register message {name}" );
			return 0;
		}

		if ( name is null )
			return 0;

		if ( name.Length >= 12 )
		{
			Log.Info( $"Message name too long: {name}" );
			return 0;
		}

		if ( size > MaxUserMessageSize )
			return 0;

		foreach ( var message in UserMessages )
		{
			if ( message.Name == name )
				return message.Id;
		}

		var id = _nextUserMessage++;

		NewUserMessages.Insert( 0, new UserMessage( id, size, name ) );

		return id;
	}

	private static UserMessage? FindUserMessage( int id )
	{
		foreach ( var message in UserMessages )
		{
			if ( message.Id == id )
				return message;
		}

		foreach ( var message in NewUserMessages )
		{
			if ( message.Id == id )
				return message;
		}

		return null;
	}

	public void MessageBegin( MessageDest dest, int type, Vector3 origin = default, Edict entity = null )
	{
		if ( dest is MessageDest.One or MessageDest.OneUnreliable )
		{
			if ( entity is null )
				throw HostError( "MSG_ONE or MSG_ONE_UNRELIABLE with no target entity\n" );
		}
		else if ( entity is not null )
		{
			throw HostError( "Invalid message;  cannot use broadcast message with a target entity" );
		}

		if ( dest is MessageDest.PvsReliable or MessageDest.PasReliable )
			throw HostError( $"WriteDest_Parm: bad destination={(int)dest}" );

		if ( _messageStarted )
			throw HostError( $"New message started when msg '{_messageType}' has not been sent yet" );

		if ( type == 0 )
			throw HostError( "Tried to create a message with a bogus message type ( 0 )" );

		_messageStarted = true;
		_messageType = type;
		_messageDest = dest;
		_messageEntity = entity;
		_message.Clear();

		if ( dest is MessageDest.Pvs or MessageDest.Pas )
			_messageOrigin = origin;
	}

	public void WriteByte( int value ) => Write( "WRITE_BYTE" ).WriteByte( value );

	public void WriteChar( int value ) => Write( "WRITE_CHAR" ).WriteChar( value );

	public void WriteShort( int value ) => Write( "WRITE_SHORT" ).WriteShort( value );

	public void WriteLong( int value ) => Write( "PF_WriteLong_I" ).WriteLong( value );

	public void WriteAngle( float value ) => Write( "PF_WriteAngle_I" ).WriteAngle( value );

	public void WriteCoord( float value ) => Write( "PF_WriteCoord_I" ).WriteShort( (int)(value * 8f) );

	public void WriteString( string value ) => Write( "PF_WriteString_I" ).WriteString( value );

	public void WriteEntity( int value ) => Write( "PF_WriteEntity_I" ).WriteShort( value );

	public void MessageEnd()
	{
		if ( !_messageStarted )
			throw HostError( "MESSAGE_END called with no active message\n" );

		_messageStarted = false;

		if ( _messageEntity is not null && (_messageEntity.EntityFlags & EntityFlags.FakeClient) == EntityFlags.FakeClient )
			return;

		if ( _message.Overflowed )
			throw HostError( "MESSAGE_END called, but message buffer from .dll had overflowed\n" );

		var size = _message.CurSize;
		var variable = false;

		if ( _messageType > LastEngineMessage )
		{
			if ( FindUserMessage( _messageType ) is not { } message )
			{
				Log.Info( $"PF_MessageEnd_I:  Unknown User Msg {_messageType}" );
				return;
			}

			if ( message.Size == -1 )
			{
				variable = true;

				if ( size > MaxUserMessageSize )
				{
					throw HostError( $"PF_MessageEnd_I:  Refusing to send user message {message.Name} of {size} bytes to client, user message size limit is {MaxUserMessageSize} bytes\n" );
				}
			}
			else if ( message.Size != size )
			{
				throw HostError( $"User Msg '{message.Name}': {size} bytes written, expected {message.Size}\n" );
			}
		}

		SizeBuf buffer;

		switch ( _messageDest )
		{
			case MessageDest.Broadcast:
				buffer = _datagram;

				if ( buffer.MaxSize < buffer.CurSize + size )
					return;

				break;

			case MessageDest.One or MessageDest.OneUnreliable:
			{
				if ( GetClient( _messageEntity ) is not { } client )
					throw HostError( "WriteDest_Parm: not a client" );

				buffer = _messageDest == MessageDest.One ? client.Message : client.Datagram;

				if ( _messageType > LastEngineMessage && (!client.HasUserMessages || (!client.Active && !client.Spawned) || client.FakeClient) )
					return;

				break;
			}

			case MessageDest.All:
				buffer = _reliableDatagram;
				break;

			case MessageDest.Init:
				buffer = _signon;
				break;

			case MessageDest.Pvs or MessageDest.Pas:
				buffer = _multicast;
				break;

			case MessageDest.Spectator:
				buffer = _spectator;
				break;

			default:
				throw HostError( $"WriteDest_Parm: bad destination={(int)_messageDest}" );
		}

		buffer.WriteByte( _messageType );

		if ( variable )
			buffer.WriteByte( size );

		buffer.WriteBuf( _message.Data, size );

		if ( _messageDest == MessageDest.Pvs )
			Multicast( _messageEntity, _messageOrigin, MulticastPvs, false );
		else if ( _messageDest == MessageDest.Pas )
			Multicast( _messageEntity, _messageOrigin, MulticastPas, false );
	}

	private bool ValidClientMulticast( ServerClient client, int leaf, int to )
	{
		if ( MaxClients == 1 || client.Proxy )
			return true;

		var destination = to & ~MulticastSkipHost;
		byte[] mask;

		if ( destination == MulticastBroadcast )
			return true;

		if ( destination == MulticastPvs )
		{
			mask = WorldModel.LeafPvs( leaf );
		}
		else if ( destination == MulticastPas )
		{
			mask = LeafPas( leaf );
		}
		else
		{
			Log.Info( $"MULTICAST: Error {to}!\n" );
			return false;
		}

		var clientLeaf = GetEdict( client.EdictIndex ) is { } edict ? WorldModel.PointInLeaf( edict.Origin ) : 0;

		return clientLeaf >= 1 && (mask[(clientLeaf - 1) >> 3] & (1 << ((clientLeaf - 1) & 7))) != 0;
	}

	private void Multicast( Edict entity, Vector3 origin, int to, bool reliable )
	{
		var leaf = WorldModel.PointInLeaf( origin );
		var hostClient = _hostClient;

		if ( entity is not null && (hostClient is null || hostClient.EdictIndex != entity.Index) && GetClient( entity ) is { } owner && owner.EdictIndex == entity.Index )
			hostClient = owner;

		foreach ( var client in Clients )
		{
			if ( !client.Active )
				continue;

			if ( (to & MulticastSkipHost) != 0 && client == hostClient )
				continue;

			if ( !ValidClientMulticast( client, leaf, to ) )
				continue;

			var buffer = reliable ? client.Message : client.Datagram;

			if ( _multicast.CurSize < buffer.MaxSize - buffer.CurSize )
				buffer.Write( _multicast );
		}

		_multicast.Clear();
	}

	private SizeBuf Write( string function )
	{
		if ( !_messageStarted )
			throw HostError( $"{function} called with no active message\n" );

		return _message;
	}
}
