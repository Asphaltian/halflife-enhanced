using System;
using System.Text;

namespace GoldSrc;

partial class Server
{
	public const int PrintMessage = 8;
	public const int ServerInfoMessage = 11;
	public const int LightStyleMessage = 12;
	public const int SignOnNumMessage = 25;
	public const int CenterPrintMessage = 26;
	public const int RestoreMessage = 33;
	public const int NewUserMsgMessage = 39;
	public const int VoiceInitMessage = 52;
	public const int SendExtraInfoMessage = 54;

	public const int MaxServerInfoString = 512;
	public const int UserMessageNameSize = 16;

	private const int ConnectionMessageSize = 0x10000;
	private const int BroadcastCommandSize = 128;
	private const int MaxMapCycleLength = 0x2000;

	[ConVar( "mapcyclefile" )]
	private static string MapCycleFile { get; set; } = "mapcycle.txt";

	private static string _serverInfo = $"\\*gamedir\\{GameDirectory}";

	private readonly SizeBuf _connectionMessage = new( "New Connection", ConnectionMessageSize, false );
	private readonly SizeBuf _broadcastCommand = new( "Broadcast Command", BroadcastCommandSize, true );
	private readonly byte[] _clientDllMd5 = new byte[16];

	private string _modelName = "";
	private int _worldMapCrc;
	private bool _loadGame;

	public void SetLoadGame()
	{
		Paused = true;
		_loadGame = true;
	}

	public void ClientPrintf( string text )
	{
		if ( _hostClient is not { FakeClient: false } client )
			return;

		client.Message.WriteByte( PrintMessage );
		client.Message.WriteString( text );
	}

	public void ClientPrintf( ServerClient client, string text )
	{
		if ( client.FakeClient )
			return;

		client.Message.WriteByte( PrintMessage );
		client.Message.WriteString( text );
	}

	private void BroadcastPrintf( string text )
	{
		foreach ( var client in Clients )
		{
			if ( (!client.Active && !client.Spawned) || client.FakeClient )
				continue;

			client.Message.WriteByte( PrintMessage );
			client.Message.WriteString( text );
		}

		Log.Trace( text );
	}

	[ConCmd( "dropclient" )]
	private static void DropCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "dropclient", arguments );
			return;
		}

		if ( Current is not { } server )
			return;

		server.BroadcastPrintf( $"{server._hostClient.Name} dropped\n" );
		server.DropClient( server._hostClient, false, "Client sent 'drop'" );
	}

	private void BroadcastCommand( string text )
	{
		if ( !Active )
			return;

		var message = _broadcastCommand;

		message.Clear();
		message.WriteByte( StuffTextMessage );
		message.WriteString( text );

		if ( message.Overflowed )
			throw HostError( $"SV_BroadcastCommand:  Overflowed on {text}, {message.MaxSize} is max size\n" );

		foreach ( var client in Clients )
		{
			if ( (client.Active || client.Connected || client.Spawned) && !client.FakeClient )
				client.Message.Write( message );
		}
	}

	[ConCmd( "serverinfo" )]
	private static void ServerInfoCommand( params string[] arguments )
	{
		if ( arguments.Length == 0 )
		{
			Log.Info( "Server info settings:\n" );
			Info.Print( _serverInfo );
			return;
		}

		if ( arguments.Length != 2 )
		{
			Log.Info( "usage: serverinfo [ <key> <value> ]\n" );
			return;
		}

		if ( arguments[0].StartsWith( '*' ) )
		{
			Log.Info( "Star variables cannot be changed.\n" );
			return;
		}

		Info.SetValueForKey( ref _serverInfo, arguments[0], arguments[1], MaxServerInfoString );

		if ( ConsoleSystem.GetValue( arguments[0] ) is not null )
			ConsoleSystem.SetValue( arguments[0], arguments[1] );

		Current?.BroadcastCommand( $"fullserverinfo \"{_serverInfo}\"\n" );
	}

	private static void SendUserReg( SizeBuf message, List<UserMessage> list )
	{
		var name = new byte[UserMessageNameSize];

		foreach ( var userMessage in list )
		{
			Array.Clear( name );
			Encoding.Latin1.GetBytes( userMessage.Name, name );

			message.WriteByte( NewUserMsgMessage );
			message.WriteByte( userMessage.Id );
			message.WriteByte( userMessage.Size );
			message.WriteBuf( name, UserMessageNameSize );
		}
	}

	private static void LinkNewUserMessages()
	{
		UserMessages.AddRange( NewUserMessages );
		NewUserMessages.Clear();
	}

	private void SendServerInfo( SizeBuf message, ServerClient client )
	{
		if ( Host.Developer != 0f || MaxClients > 1 )
		{
			message.WriteByte( PrintMessage );
			message.WriteString( $"{(char)2}\nBUILD {Host.BuildNumber()} SERVER (0 CRC)\nServer # {SpawnCount}\n" );
		}

		var playerNum = client.EdictIndex - 1;

		message.WriteByte( ServerInfoMessage );
		message.WriteLong( Host.ProtocolVersion );
		message.WriteLong( SpawnCount );
		message.WriteLong( Common.Munge3( _worldMapCrc, (-1 - playerNum) & 0xFF ) );
		message.WriteBuf( _clientDllMd5, _clientDllMd5.Length );
		message.WriteByte( MaxClients );
		message.WriteByte( playerNum );
		message.WriteByte( CoopCvar == 0f && DeathmatchCvar != 0f ? 1 : 0 );
		message.WriteString( GameDirectory );
		message.WriteString( HostName );
		message.WriteString( _modelName );

		var mapCycle = LoadMountText( MapCycleFile );

		if ( string.IsNullOrEmpty( mapCycle ) )
			message.WriteString( "mapcycle failure" );
		else
			message.WriteString( mapCycle.Length > MaxMapCycleLength ? mapCycle[..(MaxMapCycleLength - 1)] : mapCycle );

		message.WriteByte( 0 );

		message.WriteByte( SendExtraInfoMessage );
		message.WriteString( FallbackDirectory );
		message.WriteByte( Game.CheatsEnabled ? 1 : 0 );

		WriteDeltaDescriptions( message );

		SetMoveVars();
		WriteMoveVars( message );

		message.WriteByte( CdTrackMessage );
		message.WriteByte( Globals.CdAudioTrack );
		message.WriteByte( Globals.CdAudioTrack );

		message.WriteByte( SetViewMessage );
		message.WriteShort( client.EdictIndex );

		client.Spawned = false;
		client.Connected = true;
		client.FullyConnected = false;
	}

	[ConCmd( "new" )]
	private static void NewCommand()
	{
		if ( Cmd.FromClient )
			Current?.New();
	}

	private void New()
	{
		var client = _hostClient;

		if ( client.HasUserMessages && client.SentNewResponse )
			return;

		if ( client.Spawned && !client.Active )
			return;

		var edict = GetEdict( client.EdictIndex );

		client.Connected = true;
		client.ConnectionStarted = Host.RealTime;
		client.SendResourceCount = 0;

		client.Message.Clear();
		client.Datagram.Clear();
		client.Netchan.Clear();

		var message = _connectionMessage;

		message.Name = "New Connection";
		message.Clear();

		SendServerInfo( message, client );
		SendUserReg( message, UserMessages );

		client.HasUserMessages = true;

		if ( (client.Active || client.Spawned) && edict is not null )
			GameDll.ClientDisconnect( edict );

		var rejectReason = "Connection rejected by game\n";

		if ( !GameDll.ClientConnect( edict, client.Name, client.Address, ref rejectReason ) )
		{
			client.Message.WriteByte( StuffTextMessage );
			client.Message.WriteString( $"echo {rejectReason}\n" );

			DropClient( client, false, $"Server refused connection because:  {rejectReason}" );
			return;
		}

		message.WriteByte( StuffTextMessage );
		message.WriteString( $"fullserverinfo \"{_serverInfo}\"\n" );
		message.WriteByte( StuffTextMessage );
		message.WriteString( $"allow_shaders {(SvAllowShaders != 0f ? 1 : 0)}\n" );
		message.WriteByte( StuffTextMessage );
		message.WriteString( $"allow_autoaim {(SvAllowAutoaim != 0f ? 1 : 0)}\n" );

		foreach ( var other in Clients )
		{
			if ( other == client || other.Active || other.Connected || other.Spawned )
				FullClientUpdate( other, message );
		}

		client.Netchan.CreateFragments( message );
		client.Netchan.FragSend();
		client.SentNewResponse = true;
	}

	[ConCmd( "spawn" )]
	private static void SpawnCommand( params string[] arguments )
	{
		if ( Current is not { } server )
			return;

		if ( !Cmd.FromClient )
			Cmd.SetArguments( "spawn", arguments );

		server.Spawn();
	}

	private void Spawn()
	{
		if ( Cmd.Argc != 3 )
		{
			Log.Info( "spawn is not valid\n" );
			return;
		}

		if ( !Cmd.FromClient )
		{
			Log.Info( "spawn is not valid from the console\n" );
			return;
		}

		var client = _hostClient;

		if ( client.Spawned )
		{
			Log.Info( "spawn is not valid when already spawned\n" );
			return;
		}

		client.CrcValue = Common.UnMunge2( Common.Atoi( Cmd.Argv( 2 ) ), (-1 - SpawnCount) & 0xFF );

		if ( Client.Current is not { DemoPlayback: true } && Common.Atoi( Cmd.Argv( 1 ) ) != SpawnCount )
		{
			New();
			return;
		}

		var message = _connectionMessage;

		message.Name = "Spawning";
		message.Clear();
		message.Write( _signon );

		WriteSpawn( message, client );

		message.WriteByte( VoiceInitMessage );
		message.WriteString( "" );
		message.WriteByte( 0 );

		client.Netchan.CreateFragments( message );
		client.Netchan.FragSend();
	}

	private void WriteSpawn( SizeBuf message, ServerClient client )
	{
		if ( !_loadGame )
		{
			_loading = true;

			var player = CreateEdict( client.EdictIndex, "player" );

			player.Colormap = player.Index;
			player.NetName = client.Name;

			if ( client.Proxy )
				player.EntityFlags |= EntityFlags.Proxy;

			Globals.Time = (float)Time;

			GameDll.ClientPutInServer( player );

			_loading = false;
		}
		else
		{
			if ( client.Proxy )
			{
				Log.Info( "ERROR! Spectator mode doesn't work with saved game.\n" );
				return;
			}

			Paused = false;
		}

		client.Message.Clear();
		client.Datagram.Clear();

		message.WriteByte( TimeMessage );
		message.WriteFloat( (float)Time );

		client.SendInfo = true;

		foreach ( var other in Clients )
		{
			if ( other == client || other.Active || other.Connected || other.Spawned )
				FullClientUpdate( other, message );
		}

		for ( var i = 0; i < MaxLightStyles; i++ )
		{
			message.WriteByte( LightStyleMessage );
			message.WriteByte( i );
			message.WriteString( LightStyles[i] );
		}

		if ( !client.Proxy )
		{
			var edict = GetEdict( client.EdictIndex );

			message.WriteByte( SetAngleMessage );
			message.WriteHiresAngle( edict.ViewAngle.x );
			message.WriteHiresAngle( edict.ViewAngle.y );
			message.WriteHiresAngle( 0f );

			WriteClientdataToMessage( client, message );

			if ( _loadGame )
			{
				var current = new SaveRestoreData();

				Globals.SaveData = current;
				GameDll.ParmsChangeLevel( this );

				message.WriteByte( RestoreMessage );
				message.WriteString( $"{SaveDirectory}{Name}.HL2" );
				message.WriteByte( current.LevelList.Count );

				foreach ( var level in current.LevelList )
					message.WriteString( level.MapName );

				_loadGame = false;
				Globals.SaveData = null;
			}
		}

		message.WriteByte( SignOnNumMessage );
		message.WriteByte( 1 );

		client.ConnectTime = 0.0;
		client.IgnoreCmdTime = 0.0;
		client.CmdTime = 0.0;

		client.Active = true;
		client.Spawned = true;
		client.Connected = true;
		client.FullyConnected = false;
	}

	[ConCmd( "sendents" )]
	private static void SendEntsCommand( params string[] arguments )
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "sendents", arguments );
			return;
		}

		Current?.SendEnts();
	}

	private void SendEnts()
	{
		var client = _hostClient;

		if ( client.Active && client.Spawned && client.Connected )
			client.FullyConnected = true;
	}

	private void SendServerInfoToClients()
	{
		var message = _connectionMessage;

		message.Name = "Activate Server";
		message.Clear();

		foreach ( var client in Clients )
		{
			if ( (!client.Active && !client.Connected) || client.FakeClient )
				continue;

			_hostClient = client;

			client.Netchan.Clear();

			if ( MaxClients < 2 )
			{
				SendServerInfo( message, client );
			}
			else
			{
				client.Message.WriteByte( StuffTextMessage );
				client.Message.WriteString( "reconnect\n" );
				client.Netchan.Transmit( null );
			}

			SendUserReg( message, UserMessages );

			client.HasUserMessages = true;
			client.Netchan.CreateFragments( message );
			client.Netchan.FragSend();

			message.Clear();
		}
	}
}
