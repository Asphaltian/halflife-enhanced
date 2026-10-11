using System;
using System.Globalization;
using System.Security.Cryptography;

namespace GoldSrc;

partial class Server
{
	public const int DisconnectMessage = 2;
	public const int StuffTextMessage = 9;
	public const int UpdateUserInfoMessage = 13;

	private const int MaxInfoString = 256;
	private const int MaxClientNameLength = 31;
	private const int MaxDuplicateNameLength = 28;
	private const int HashedCdKeySize = 64;
	private const double DefaultMessageInterval = 0.05;
	private const int MinRate = 1000;
	private const int MaxRate = 100000;
	private const int MinUpdateRate = 10;
	private const float MinUpdateRateCvar = 0.001f;

	private static int _userId = 1;

	private readonly ServerClient[] _clients = CreateClients();

	private IReadOnlyList<ServerClient> _clientList = [];

	private static ServerClient[] CreateClients()
	{
		var clients = new ServerClient[MaxClientsLimit];

		for ( var i = 0; i < clients.Length; i++ )
			clients[i] = new ServerClient( i );

		return clients;
	}

	[ConVar( "sv_maxupdaterate" )]
	private static float SvMaxUpdateRate { get; set; } = 30f;

	[ConVar( "sv_minupdaterate" )]
	private static float SvMinUpdateRate { get; set; } = 10f;

	[ConVar( "sv_minrate", ConVarFlags.Replicated )]
	private static float SvMinRate { get; set => Server.SetServerCvar( "sv_minrate", ref field, value ); }

	[ConVar( "sv_maxrate", ConVarFlags.Replicated )]
	private static float SvMaxRate { get; set => Server.SetServerCvar( "sv_maxrate", ref field, value ); }

	public IReadOnlyList<ServerClient> Clients
	{
		get
		{
			if ( _clientList.Count != MaxClients )
				_clientList = new ArraySegment<ServerClient>( _clients, 0, MaxClients );

			return _clientList;
		}
	}

	public ServerClient HostClient => _hostClient;

	public int CountPlayers()
	{
		var count = 0;

		for ( var i = 0; i < MaxClients; i++ )
		{
			var client = _clients[i];

			if ( client.Active || client.Spawned || client.Connected )
				count++;
		}

		return count;
	}

	public int CalcPing( ServerClient client )
	{
		if ( client.FakeClient )
			return 0;

		var count = Math.Min( UpdateBackup / 2, 16 );

		if ( count < 1 )
			return 0;

		var ping = 0f;
		var samples = 0;
		var sequence = client.Netchan.OutgoingSequence;

		for ( var i = 0; i < count; i++ )
		{
			sequence--;

			var pingTime = client.Frames[sequence & UpdateMask].PingTime;

			if ( pingTime > 0f )
			{
				ping += pingTime;
				samples++;
			}
		}

		if ( samples == 0 )
			return 0;

		ping /= samples;

		if ( ping < 0f )
			ping = 0f;

		return (int)(ping * 1000f);
	}

	public Edict HostPlayer => _hostClient is { } client ? GetEdict( client.EdictIndex ) : null;

	public ServerClient GetClient( Edict edict ) => GetClient( edict?.Index ?? 0 );

	public static bool IsDedicatedServer => Application.IsDedicatedServer;

	public bool SetClientListening( int receiver, int sender, bool listen )
	{
		receiver--;
		sender--;

		if ( receiver < 0 || receiver >= MaxClients || sender < 0 || sender >= MaxClients )
			return false;

		if ( listen )
			_clients[sender].VoiceStreams |= 1u << (receiver & 31);
		else
			_clients[sender].VoiceStreams &= ~(1u << (receiver & 31));

		return true;
	}

	public ServerClient GetClient( int edictIndex )
	{
		return edictIndex >= 1 && edictIndex <= MaxClients ? _clients[edictIndex - 1] : null;
	}

	public int GetPlayerUserId( Edict edict )
	{
		if ( !Active || edict is null )
			return -1;

		return GetClient( edict ) is { } client && client.EdictIndex == edict.Index ? client.UserId : -1;
	}

	public string GetInfoKeyBuffer( Edict edict ) => GetClient( edict )?.UserInfo ?? "";

	public int NumberOfEntities()
	{
		var count = 0;

		for ( var i = 0; i < _numEdicts; i++ )
		{
			if ( _edicts[i] is not null )
				count++;
		}

		return count;
	}

	public void SetUserInfo( string key, string value )
	{
		Info.SetValueForKey( ref _hostClient.UserInfo, key, value, MaxInfoString );

		_hostClient.SendInfo = true;
	}

	public void SetClientKeyValue( int clientIndex, ref string info, string key, string value )
	{
		if ( clientIndex <= 0 || clientIndex > MaxClients )
			return;

		if ( Info.ValueForKey( info, key ) == value )
			return;

		Info.SetValueForStarKey( ref info, key, value, MaxInfoString );

		var client = _clients[clientIndex - 1];

		client.SendInfo = true;
		client.SendInfoTime = 0.0;
	}

	public ServerClient ConnectClient( string userInfo, bool loopback ) => ConnectClient( userInfo, loopback, null, out _ );

	private ServerClient ConnectClient( string userInfo, bool loopback, Connection connection, out string rejectReason )
	{
		var address = loopback ? LoopbackAddress : connection.Address;

		ServerClient client = null;

		foreach ( var slot in Clients )
		{
			if ( (slot.Active || slot.Spawned || slot.Connected) && slot.Address == address )
			{
				client = slot;
				break;
			}
		}

		if ( !CheckUserInfo( ref userInfo, loopback, address, client, out var name, out rejectReason ) )
			return null;

		if ( client is not null )
		{
			if ( (client.Active || client.Spawned) && GetEdict( client.EdictIndex ) is { } edict )
				GameDll.ClientDisconnect( edict );

			Con.Printf( $"{address}:reconnect\n" );
		}
		else
		{
			foreach ( var slot in Clients )
			{
				if ( !slot.Active && !slot.Spawned && !slot.Connected )
				{
					client = slot;
					break;
				}
			}

			if ( client is null )
			{
				rejectReason = "Server is full.\n";
				return null;
			}
		}

		client.UserId = _userId++;
		client.Frames = new ServerFrame[UpdateBackup];

		for ( var i = 0; i < client.Frames.Length; i++ )
			client.Frames[i] = new ServerFrame();

		client.Netchan.Setup( false, loopback, address, loopback ? null : client.SendPacket, client.BlockSize );

		client.DeltaSequence = -1;
		client.ChokeCount = 0;
		client.LastCmd = default;
		client.ConnectTime = 0.0;
		client.CmdTime = 0.0;
		client.IgnoreCmdTime = 0.0;
		client.Latency = 0f;
		client.PacketLoss = 0f;
		client.SvTimeBase = 0.0;
		client.SendMessage = false;
		client.SkipMessage = false;
		client.PhysInfo = "";

		Array.Clear( client.Events );
		client.Connection = connection;
		client.SteamId = loopback ? Sandbox.Utility.Steam.SteamId.ValueUnsigned : connection.SteamId.ValueUnsigned;
		client.Address = address;
		client.Loopback = loopback;
		client.FakeClient = false;
		client.Proxy = false;
		client.Rate = 30000.0;
		client.NextMessageInterval = DefaultMessageInterval;
		client.NextMessageTime = Host.RealTime + DefaultMessageInterval;

		Con.DPrintf( loopback ? "Local connection.\n" : $"Client {name} connected\nAdr: {address}\n" );

		client.Active = false;
		client.Spawned = false;
		client.Connected = true;
		client.Uploading = false;
		client.UploadDoneRegistering = false;
		client.FullyConnected = false;

		ClearResourceLists( client );

		LogPrintf( $"\"{name}<{client.UserId}><{GetIdString( client.SteamId )}><>\" connected, address \"{address}\"\n" );

		client.UserInfo = userInfo.Length < MaxInfoString ? userInfo : userInfo[..(MaxInfoString - 1)];
		client.SendInfo = false;
		client.SendInfoTime = 0.0;

		ExtractFromUserInfo( client );

		Info.SetValueForStarKey( ref client.UserInfo, "*sid", client.SteamId.ToString( CultureInfo.InvariantCulture ), MaxInfoString );

		client.Message.Clear();
		client.Datagram.Clear();
		client.Datagram.Name = client.Name;

		if ( !loopback && FilterUser( client.SteamId ) )
		{
			rejectReason = "You have been banned from this server\n";

			DropClient( client, false, $"STEAM UserID {GetClientIdString( client )} is in server ban list\n" );

			return null;
		}

		return client;
	}

	public void InactivateClients()
	{
		foreach ( var client in Clients )
		{
			if ( !client.Active && !client.Connected && !client.Spawned )
				continue;

			if ( client.FakeClient )
			{
				DropClient( client, false, "Dropping fakeclient on level change" );
				continue;
			}

			client.Active = false;
			client.Connected = true;
			client.Spawned = false;
			client.FullyConnected = false;
			client.HasUserMessages = false;
			client.SentNewResponse = false;

			client.Message.Clear();
			client.Datagram.Clear();
			client.Customizations.Clear();

			client.PhysInfo = "";
		}
	}

	public void DropClient( ServerClient client, bool crash, string reason )
	{
		if ( !crash )
		{
			if ( !client.FakeClient )
			{
				client.Message.WriteByte( DisconnectMessage );
				client.Message.WriteString( reason );
			}

			if ( client.Spawned && GetEdict( client.EdictIndex ) is { } edict )
				GameDll.ClientDisconnect( edict );

			Con.DPrintf( $"Dropped {client.Name} from server\nReason:  {reason}\n" );
		}

		client.Message.Clear();
		client.Datagram.Clear();
		client.Netchan.Clear();

		client.Active = false;
		client.Connected = false;
		client.HasUserMessages = false;
		client.FakeClient = false;
		client.Spawned = false;
		client.FullyConnected = false;
		client.Name = "";
		client.ConnectionStarted = Host.RealTime;
		client.Proxy = false;
		client.Customizations.Clear();
		client.Loopback = false;
		client.UserInfo = "";
		client.PhysInfo = "";
		client.Address = "";
		client.SteamId = 0;

		if ( client.Connection is { } connection )
		{
			client.Connection = null;

			connection.Kick( reason );
		}

		if ( Active )
			FullClientUpdate( client, _reliableDatagram );
	}

	private void FullClientUpdate( ServerClient client, SizeBuf buffer )
	{
		var info = client.UserInfo.Length < MaxInfoString ? client.UserInfo : client.UserInfo[..(MaxInfoString - 1)];

		Info.RemovePrefixedKeys( ref info, '_' );

		buffer.WriteByte( UpdateUserInfoMessage );
		buffer.WriteByte( client.Slot );
		buffer.WriteLong( client.UserId );
		buffer.WriteString( info );
		buffer.WriteBuf( MD5.HashData( new byte[HashedCdKeySize] ), 16 );
	}

	private static string TrimWhitespace( string text )
	{
		static bool IsSpace( char character ) => character is ' ' or '\t' or '\r' or '\n';

		var start = 0;

		while ( start < text.Length && IsSpace( text[start] ) )
			start++;

		var end = text.Length;

		do
		{
			end--;
		}
		while ( end >= 1 && IsSpace( text[end] ) );

		return end - start + 1 > 0 ? text[start..(end + 1)] : "";
	}

	private bool IsDuplicateName( ServerClient client, string name )
	{
		foreach ( var other in Clients )
		{
			if ( other != client && other.Active && other.Spawned && other.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	private void ExtractFromUserInfo( ServerClient client )
	{
		var raw = Info.ValueForKey( client.UserInfo, "name" );
		var name = raw.Length > MaxClientNameLength ? raw[..MaxClientNameLength] : raw;
		var characters = name.ToCharArray();

		for ( var i = 0; i < characters.Length && characters[i] == '#'; i++ )
			characters[i] = ' ';

		name = TrimWhitespace( new string( characters ) );

		if ( !Unicode.Validate( name ) )
			name = Unicode.Repair( name );

		if ( name.Length < 1 || name.Equals( "console", StringComparison.OrdinalIgnoreCase ) || name.Contains( ".." ) || name.Contains( '"' ) || name.Contains( '\\' ) )
			Info.SetValueForKey( ref client.UserInfo, "name", "unnamed", MaxInfoString );
		else if ( raw != name )
			Info.SetValueForKey( ref client.UserInfo, "name", name, MaxInfoString );

		name = Info.ValueForKey( client.UserInfo, "name" );

		for ( var duplicate = 1; IsDuplicateName( client, name ); duplicate++ )
		{
			if ( name.Length > MaxClientNameLength )
				name = name[..MaxDuplicateNameLength];

			if ( name.Length > 2 && name[0] == '(' )
			{
				if ( name[2] == ')' )
					name = name[3..];
				else if ( name.Length > 3 && name[3] == ')' )
					name = name[4..];
			}

			name = $"({duplicate}){(name.Length > MaxDuplicateNameLength ? name[..MaxDuplicateNameLength] : name)}";

			if ( name.Length > MaxClientNameLength )
				name = name[..MaxClientNameLength];

			Info.SetValueForKey( ref client.UserInfo, "name", name, MaxInfoString );

			name = Info.ValueForKey( client.UserInfo, "name" );
		}

		GameDll.ClientUserInfoChanged( GetEdict( client.EdictIndex ), ref client.UserInfo );

		name = Info.ValueForKey( client.UserInfo, "name" );

		client.Name = name.Length > MaxClientNameLength ? name[..MaxClientNameLength] : name;

		if ( GetEdict( client.EdictIndex ) is { } edict )
			edict.NetName = client.Name;

		var value = Info.ValueForKey( client.UserInfo, "rate" );

		if ( value.Length != 0 )
			client.Rate = Math.Clamp( Common.Atoi( value ), MinRate, MaxRate );

		value = Info.ValueForKey( client.UserInfo, "topcolor" );

		if ( value.Length != 0 )
			client.TopColor = Common.Atoi( value );
		else
			Con.DPrintf( $"topcolor unchanged for {client.Name}\n" );

		value = Info.ValueForKey( client.UserInfo, "bottomcolor" );

		if ( value.Length != 0 )
			client.BottomColor = Common.Atoi( value );
		else
			Con.DPrintf( $"bottomcolor unchanged for {client.Name}\n" );

		value = Info.ValueForKey( client.UserInfo, "cl_updaterate" );

		if ( value.Length != 0 )
			client.NextMessageInterval = 1f / Math.Max( Common.Atoi( value ), MinUpdateRate );

		value = Info.ValueForKey( client.UserInfo, "cl_lw" );
		client.LocalWeapons = value.Length != 0 && Common.Atoi( value ) != 0;

		value = Info.ValueForKey( client.UserInfo, "cl_lc" );
		client.LagCompensation = value.Length != 0 && Common.Atoi( value ) != 0;

		value = Info.ValueForKey( client.UserInfo, "*hltv" );
		client.Proxy = value.Length != 0 && Common.Atoi( value ) == 1;

		if ( client.NextMessageInterval == 0.0 )
			client.NextMessageInterval = DefaultMessageInterval;

		if ( SvMaxUpdateRate <= MinUpdateRateCvar && SvMaxUpdateRate != 0f )
			Cvar.Set( "sv_maxupdaterate", "30.0" );

		if ( SvMinUpdateRate <= MinUpdateRateCvar && SvMinUpdateRate != 0f )
			Cvar.Set( "sv_minupdaterate", "1.0" );

		if ( SvMaxUpdateRate != 0f && client.NextMessageInterval < 1.0 / SvMaxUpdateRate )
			client.NextMessageInterval = 1.0 / SvMaxUpdateRate;

		if ( SvMinUpdateRate != 0f && 1.0 / SvMinUpdateRate < client.NextMessageInterval )
			client.NextMessageInterval = 1.0 / SvMinUpdateRate;

		if ( SvMaxRate > 0f && SvMaxRate < client.Rate )
			client.Rate = Math.Min( SvMaxRate, MaxRate );

		if ( SvMinRate > 0f && client.Rate < SvMinRate )
			client.Rate = Math.Max( SvMinRate, MinRate );
	}

	private void UpdateToReliableMessages()
	{
		foreach ( var client in Clients )
		{
			if ( !client.Active && !client.Spawned && !client.Connected )
				continue;

			if ( client.SendInfo && client.SendInfoTime <= Host.RealTime )
			{
				client.SendInfo = false;
				client.SendInfoTime = Host.RealTime + 1.0;

				ExtractFromUserInfo( client );
				FullClientUpdate( client, _reliableDatagram );
			}

			if ( !client.FakeClient && (client.Active || client.Connected) && NewUserMessages.Count != 0 )
				SendUserReg( client.Message, NewUserMessages );
		}

		LinkNewUserMessages();

		foreach ( var client in Clients )
		{
			if ( client.FakeClient || !client.Active )
				continue;

			if ( client.Message.CurSize + _reliableDatagram.CurSize < client.Message.MaxSize )
				client.Message.Write( _reliableDatagram );
			else
				client.Netchan.CreateFragments( _reliableDatagram );

			if ( client.Datagram.CurSize + _datagram.CurSize < client.Datagram.MaxSize )
				client.Datagram.Write( _datagram );
			else
				Con.DPrintf( $"Warning:  Ignoring unreliable datagram for {client.Name}, would overflow\n" );

			if ( client.Proxy && client.Datagram.CurSize + _spectator.CurSize < client.Datagram.MaxSize )
				client.Datagram.Write( _spectator );
		}

		_reliableDatagram.Clear();
		_datagram.Clear();
		_spectator.Clear();
	}
}
