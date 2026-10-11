using System;

namespace GoldSrc;

public enum AlertType
{
	Notice,
	Console,
	AiConsole,
	Warning,
	Error,
	Logged
}

public enum PrintType
{
	Console,
	Center,
	Chat
}

partial class Server
{
	public const int SetViewMessage = 5;
	public const int ParticleMessage = 18;
	public const int CrosshairAngleMessage = 47;

	public void AlertMessage( AlertType type, string message )
	{
		if ( type == AlertType.Logged && MaxClients > 1 )
		{
			LogPrintf( message );
			return;
		}

		if ( Host.Developer == 0f )
			return;

		if ( type == AlertType.AiConsole && Host.Developer < 2f )
			return;

		Con.Printf( type switch
		{
			AlertType.Notice => $"NOTE:  {message}",
			AlertType.Warning => $"WARNING:  {message}",
			AlertType.Error => $"ERROR:  {message}",
			_ => message
		} );
	}

	public void ClientPrintf( Edict edict, PrintType type, string message )
	{
		var index = edict.Index;

		if ( index < 1 || index > MaxClients )
		{
			Con.Printf( "tried to sprint to a non-client\n" );
			return;
		}

		var client = _clients[index - 1];

		if ( client.FakeClient )
			return;

		switch ( type )
		{
			case PrintType.Console:
			case PrintType.Chat:
				client.Message.WriteByte( PrintMessage );
				client.Message.WriteString( message );
				break;

			case PrintType.Center:
				client.Message.WriteByte( CenterPrintMessage );
				client.Message.WriteString( message );
				break;

			default:
				Con.Printf( $"invalid PRINT_TYPE {(int)type}\n" );
				break;
		}
	}

	public int DropToFloor( Edict edict )
	{
		var end = edict.Origin;

		end.z -= 256f;

		var trace = Move( edict.Origin, edict.Mins, edict.Maxs, end, (int)MoveKind.Normal, edict, (edict.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip );

		if ( trace.AllSolid )
			return -1;

		if ( trace.Fraction == 1f )
			return 0;

		edict.Origin = trace.EndPos;
		LinkEdict( edict, false );

		edict.EntityFlags |= EntityFlags.OnGround;
		edict.GroundEntity = trace.Ent;

		return 1;
	}

	private const int MaxParticleDatagramSize = 0x1760;

	public void ParticleEffect( Vector3 origin, Vector3 direction, float color, float count )
	{
		if ( _datagram.CurSize > MaxParticleDatagramSize )
			return;

		_datagram.WriteByte( ParticleMessage );

		for ( var i = 0; i < 3; i++ )
			_datagram.WriteCoord( origin[i] );

		for ( var i = 0; i < 3; i++ )
			_datagram.WriteChar( Math.Clamp( (int)(direction[i] * 16f), -128, 127 ) );

		_datagram.WriteByte( (int)count );
		_datagram.WriteByte( (int)color );
	}

	public void CrosshairAngle( Edict client, float pitch, float yaw )
	{
		if ( client.Index < 1 || client.Index > MaxClients || (client.EntityFlags & EntityFlags.FakeClient) == EntityFlags.FakeClient )
			return;

		if ( pitch > 180f )
			pitch -= 360f;

		if ( pitch < -180f )
			pitch += 360f;

		if ( yaw > 180f )
			yaw -= 360f;

		if ( yaw < -180f )
			yaw += 360f;

		var buffer = GetClient( client ).Message;

		buffer.WriteByte( CrosshairAngleMessage );
		buffer.WriteChar( (int)(pitch * 5f) );
		buffer.WriteChar( (int)(yaw * 5f) );
	}

	public void SetView( Edict client, Edict viewEntity )
	{
		if ( client.Index < 1 || client.Index > MaxClients )
			throw HostError( "PF_setview_I: not a client" );

		if ( (client.EntityFlags & EntityFlags.FakeClient) == EntityFlags.FakeClient )
			return;

		ViewEntity = viewEntity;

		var buffer = GetClient( client ).Message;

		buffer.WriteByte( SetViewMessage );
		buffer.WriteShort( viewEntity.Index );
	}

	public Edict ViewEntity { get; private set; }

	public int DecalIndex( string name )
	{
		for ( var i = 0; i < DecalWad.Textures.Length; i++ )
		{
			if ( DecalWad.Textures[i].Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	public void StaticDecal( Vector3 origin, int decalIndex, int entityIndex, int modelIndex )
	{
		_signon.WriteByte( TempEntityMessage );
		_signon.WriteByte( (int)TempEntity.BspDecal );
		_signon.WriteCoord( origin.x );
		_signon.WriteCoord( origin.y );
		_signon.WriteCoord( origin.z );
		_signon.WriteShort( decalIndex );
		_signon.WriteShort( entityIndex );

		if ( entityIndex != 0 )
			_signon.WriteShort( modelIndex );
	}

	public List<DecalEntry> SavedDecals( string fileName )
	{
		return _saveDirectory.TryGetValue( Common.FileBase( fileName ), out var saved ) ? saved.Decals : null;
	}
}
