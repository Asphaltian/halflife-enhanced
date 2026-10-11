using System;
using Sandbox.Network;

namespace GoldSrc;

partial class Server
{
	private const string LoopbackAddress = "loopback";

	public const char ConnectionAccept = 'B';
	public const char ConnectionBadPassword = '8';
	public const char ConnectionReject = '9';
	public const string BadPassword = "BADPASSWORD";

	private ServerNetwork _network;

	private const string DefaultHostName = "Half-Life";

	public static string HostName
	{
		get => Networking.ServerName is { Length: > 0 } name ? name : DefaultHostName;
		set => Networking.ServerName = value;
	}

	[ConVar( "sv_use_steam_networking" )]
	private static float SvUseSteamNetworking { get; set; } = 1f;

	[ConVar( "sv_password" )]
	private static string SvPassword { get; set => Server.SetServerCvar( "sv_password", ref field, value ); } = "";

	[ConVar( "sv_proxies", ConVarFlags.Replicated )]
	private static float SvProxies { get; set => Server.SetServerCvar( "sv_proxies", ref field, value ); } = 1f;

	[ConVar( "sv_lan" )]
	private static float SvLan { get; set; }

	private void Listen()
	{
		if ( MaxClients <= 1 )
			return;

		if ( !_network.IsValid() )
		{
			var gameObject = new GameObject( true, "server network" );

			gameObject.NetworkMode = NetworkMode.Never;

			_network = gameObject.AddComponent<ServerNetwork>();
		}

		PublishServerData();

		if ( Networking.IsActive )
			return;

		Networking.CreateLobby( new LobbyConfig { MaxPlayers = MaxClients, Name = HostName, DestroyWhenHostLeaves = true } );
	}

	private void PublishServerData()
	{
		var bots = 0;

		foreach ( var client in Clients )
		{
			if ( client.FakeClient && (client.Active || client.Spawned || client.Connected) )
				bots++;
		}

		Networking.SetData( GameUI.MatchmakingServers.GameDirKey, GameDirectory );
		Networking.SetData( GameUI.MatchmakingServers.DescriptionKey, GameDll.GetGameDescription() );
		Networking.SetData( GameUI.MatchmakingServers.PasswordKey, SvPassword.Length != 0 && !SvPassword.Equals( "none", StringComparison.OrdinalIgnoreCase ) ? "1" : "0" );
		Networking.SetData( GameUI.MatchmakingServers.BotsKey, bots.ToString() );
		Networking.SetData( "map", Globals.MapName );
	}

	private const double PlayerDataInterval = 1.0;

	private double _nextPlayerData;
	private double _playerTimeBase;
	private string _playerData;

	private void PublishPlayerData()
	{
		if ( !_network.IsValid() || Application.IsDedicatedServer || Host.RealTime < _nextPlayerData )
			return;

		_nextPlayerData = Host.RealTime + PlayerDataInterval;

		if ( _playerTimeBase == 0 )
			_playerTimeBase = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Host.RealTime;

		var data = new System.Text.StringBuilder();

		foreach ( var client in Clients )
		{
			if ( !client.Active && !client.Spawned && !client.Connected )
				continue;

			data.Append( client.Name ).Append( GameUI.MatchmakingServers.PlayerFieldSeparator );
			data.Append( (int)(GetEdict( client.EdictIndex )?.Frags ?? 0f) ).Append( GameUI.MatchmakingServers.PlayerFieldSeparator );
			data.Append( (long)(_playerTimeBase + client.ConnectionStarted) ).Append( GameUI.MatchmakingServers.PlayerSeparator );
		}

		var text = data.ToString();

		if ( text == _playerData )
			return;

		_playerData = text;

		Networking.SetData( GameUI.MatchmakingServers.PlayersKey, text );
	}

	[ConVar( "sv_voiceenable", ConVarFlags.Saved | ConVarFlags.Replicated )]
	private static float SvVoiceEnable { get; set => Server.SetServerCvar( "sv_voiceenable", ref field, value ); } = 1f;

	private readonly VoiceTransmitter[] _voiceTransmitters = new VoiceTransmitter[MaxClientsLimit];

	private void UpdateVoiceTransmitters()
	{
		var listening = _network.IsValid() && Networking.IsActive && Networking.IsHost;

		for ( var i = 0; i < _voiceTransmitters.Length; i++ )
		{
			var client = i < Clients.Count ? Clients[i] : null;
			var connection = client is null || client.FakeClient || (!client.Active && !client.Spawned && !client.Connected)
				? null
				: client.Connection ?? (client.Address == LoopbackAddress ? Connection.Local : null);

			if ( !listening || connection is null )
			{
				if ( _voiceTransmitters[i].IsValid() )
					_voiceTransmitters[i].GameObject.Destroy();

				_voiceTransmitters[i] = null;

				continue;
			}

			var transmitter = _voiceTransmitters[i];

			if ( !transmitter.IsValid() || transmitter.Network.Owner != connection )
			{
				if ( transmitter.IsValid() )
					transmitter.GameObject.Destroy();

				var gameObject = new GameObject( true, "voice" );

				transmitter = _voiceTransmitters[i] = gameObject.AddComponent<VoiceTransmitter>();

				gameObject.NetworkSpawn( connection );
			}

			transmitter.ClientSlot = i;
			transmitter.ListenerMask = SvVoiceEnable != 0f ? client.VoiceStreams : 0u;
		}
	}

	private void DestroyVoiceTransmitters()
	{
		for ( var i = 0; i < _voiceTransmitters.Length; i++ )
		{
			if ( _voiceTransmitters[i].IsValid() )
				_voiceTransmitters[i].GameObject.Destroy();

			_voiceTransmitters[i] = null;
		}
	}

	private void StopListening()
	{
		DestroyVoiceTransmitters();

		if ( _network.IsValid() )
			_network.GameObject.Destroy();

		_network = null;

		if ( Networking.IsActive && Networking.IsHost )
			Networking.Disconnect();
	}

	private ServerClient FindClient( Connection connection )
	{
		foreach ( var client in Clients )
		{
			if ( client.Connection == connection )
				return client;
		}

		return null;
	}

	public void ConnectClient( Connection connection, string userInfo )
	{
		if ( !Active || connection is null || connection.IsHost )
			return;

		using ( Rpc.FilterInclude( connection ) )
		{
			if ( ConnectClient( userInfo, false, connection, out var rejectReason ) is not null )
				Net.OutOfBandPrint( $"{ConnectionAccept}" );
			else if ( rejectReason == BadPassword )
				Net.OutOfBandPrint( $"{ConnectionBadPassword}{BadPassword}" );
			else
				Net.OutOfBandPrint( $"{ConnectionReject}{rejectReason}" );
		}
	}

	public void GetPacket( Connection connection, byte[] packet )
	{
		if ( Active && FindClient( connection ) is { Connected: true } client )
			client.Netchan.Incoming.Enqueue( packet );
	}

	public void DisconnectClient( Connection connection )
	{
		if ( FindClient( connection ) is not { } client )
			return;

		client.Connection = null;

		DropClient( client, false, "Client sent 'drop'" );
	}

	private bool IsConnectedName( string name, ServerClient reconnecting )
	{
		foreach ( var client in Clients )
		{
			if ( client != reconnecting && client.Connected && client.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	private bool CheckUserInfo( ref string userInfo, bool loopback, string address, ServerClient reconnecting, out string name, out string rejectReason )
	{
		name = "";
		rejectReason = null;

		if ( !loopback && SvPassword.Length != 0 && !SvPassword.Equals( "none", StringComparison.OrdinalIgnoreCase ) && SvPassword != Info.ValueForKey( userInfo, "password" ) )
		{
			Log.Info( $"{address}:  password failed\n" );

			rejectReason = BadPassword;
			return false;
		}

		if ( userInfo.Length <= 4 || userInfo.Contains( "\\\\" ) || userInfo[^1] == '\\' )
		{
			rejectReason = "Unknown HLTV client type.\n";
			return false;
		}

		Info.RemoveKey( ref userInfo, "password" );

		var value = Info.ValueForKey( userInfo, "name" );

		if ( value.Length == 0 )
			value = "unnamed";
		else if ( value.Length > MaxClientNameLength )
			value = value[..MaxClientNameLength];

		var characters = value.ToCharArray();

		for ( var i = 0; i < characters.Length; i++ )
		{
			if ( characters[i] is '%' or '&' )
				characters[i] = ' ';
		}

		for ( var i = 0; i < characters.Length && characters[i] == '#'; i++ )
			characters[i] = ' ';

		name = TrimWhitespace( new string( characters ) );

		if ( !Unicode.Validate( name ) )
			name = Unicode.Repair( name );

		Info.SetValueForKey( ref userInfo, "name", name, MaxInfoString );

		value = Info.ValueForKey( userInfo, "name" );

		if ( value.Length == 0 || value.Equals( "console", StringComparison.OrdinalIgnoreCase ) || value.Contains( ".." ) || value.Contains( '"' ) || value.Contains( '\\' ) )
		{
			Info.SetValueForKey( ref userInfo, "name", "unamed", MaxInfoString );

			name = ClampName( Info.ValueForKey( userInfo, "name" ) );
		}
		else
		{
			var changed = false;

			for ( var duplicate = 1; IsConnectedName( value, reconnecting ); duplicate++ )
			{
				if ( value[0] == '(' )
				{
					if ( value.Length > 2 && value[2] == ')' )
						value = value[3..];
					else if ( value.Length > 3 && value[3] == ')' )
						value = value[4..];
				}

				Info.SetValueForKey( ref userInfo, "name", ClampName( $"({duplicate}){(value.Length > MaxDuplicateNameLength ? value[..MaxDuplicateNameLength] : value)}" ), MaxInfoString );

				value = Info.ValueForKey( userInfo, "name" );
				changed = true;
			}

			if ( changed )
				name = ClampName( value );
		}

		value = Info.ValueForKey( userInfo, "*hltv" );

		if ( value.Length == 0 )
			return true;

		switch ( Common.Atoi( value ) )
		{
			case 0:
				return true;

			case 1:
				{
					var proxies = 0;

					foreach ( var client in Clients )
					{
						if ( (client.Active || client.Spawned || client.Connected) && client.Proxy )
							proxies++;
					}

					if ( proxies < SvProxies || reconnecting is not null )
						return true;

					rejectReason = "Proxy slots are full.\n";
					return false;
				}

			case 3:
				rejectReason = "Please connect to HLTV master proxy.\n";
				return false;

			default:
				rejectReason = "Unknown HLTV client type.\n";
				return false;
		}
	}

	private static string ClampName( string name )
	{
		return name.Length > MaxClientNameLength ? name[..MaxClientNameLength] : name;
	}

	private static string GetIdString( ulong steamId )
	{
		if ( SvLan != 0f )
			return "STEAM_ID_LAN";

		if ( steamId == 0 )
			return "STEAM_ID_PENDING";

		var account = (uint)steamId;

		return $"STEAM_0:{account & 1}:{account >> 1}";
	}

	public string GetClientIdString( ServerClient client )
	{
		return client.Loopback ? "VALVE_ID_LOOPBACK" : GetIdString( client.SteamId );
	}

	public string GetPlayerAuthId( Edict edict )
	{
		if ( !Active || edict is null )
			return "";

		if ( GetClient( edict ) is not { } client || client.EdictIndex != edict.Index )
			return "";

		return client.FakeClient ? "BOT" : GetIdString( client.SteamId );
	}
}
