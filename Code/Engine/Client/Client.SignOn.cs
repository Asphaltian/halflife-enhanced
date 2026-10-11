using System;
using System.Text;

namespace GoldSrc;

public enum ClientState
{
	Dedicated,
	Disconnected,
	Connecting,
	Connected,
	Uninitialized,
	Active
}

public sealed class ClientUserMessage
{
	public int Id;
	public int Size;
	public string Name;
}

partial class Client
{
	private const int SignOns = 2;
	private const int ConnectingBlockSize = 128;
	private const int DefaultBlockSize = 256;
	private const float MinDownloadMax = 16f;
	private const double MaxDownloadMax = 1024.0;
	private const int MaxLevelNameLength = 39;
	private const int MaxClientServerInfoLength = 511;
	private const int MaxLightStyleLength = 63;
	private const int MaxDescriptionNameLength = 255;
	private const int MaxDecalNameLength = 15;
	private const int DropCommandCount = 3;
	private const float ForcedLightGamma = 1.8f;

	private static readonly string[] StuffTextCommands =
	[
		"bind", "_set", "unbind", "retry", "quit", "_restart", "motd_write", "motdfile", "kill", "exit", "writecfg", "cl_filterstuffcmd", "unbindall"
	];

	private static readonly string[] StuffTextWords = ["motd_write", "retry", "_set", "quit", "kill"];
	private static readonly string[] StuffTextContains = ["bind ", "unbind ", "_restart", "exit", "exec", "writecfg", "cl_filterstuffcmd", "unbindall"];
	private static readonly string[] StuffTextFiltered = ["ex_interp ", "say ", "developer", "timerefresh", "rate", "cd ", "fps_max", "speak_enabled", "voice_enable", "sensitivity", "sys_ticrate", "setinfo", "removedemo", "volume", "mp3volume"];
	private static readonly string[] StuffTextPrefixes = ["cl_", "gl_", "m_", "r_", "hud_"];

	[ConVar( "cl_timeout", ConVarFlags.Saved )]
	private static float Timeout { get; set; } = 60f;

	[ConVar( "sp_decals", ConVarFlags.Saved )]
	private static float SinglePlayerDecalLimit { get; set; } = 4096f;

	[ConVar( "cl_resend" )]
	private static float Resend { get; set; } = 6f;

	[ConVar( "cl_filterstuffcmd", ConVarFlags.Saved )]
	private static float FilterStuffCmd { get; set; }

	private const float MinResend = 1.5f;
	private const float MaxResend = 20f;
	private const int MaxConnectRetries = 3;
	private const double ConnectTimeReset = -99999.0;

	private double _connectTime;
	private int _connectRetry;

	private readonly SizeBuf _dropCommand = new( "", 16, false );
	private readonly SizeBuf _resourceBlock = new( "Client to Server Resource Block", 0x10000, false );
	private readonly List<ClientUserMessage> _userMessages = [];
	private readonly string[] _lightStyles = new string[Server.MaxLightStyles];
	private readonly byte[] _clientDllMd5 = new byte[16];
	private readonly byte[] _userMessageName = new byte[Server.UserMessageNameSize];

	private ClientState _state = ClientState.Disconnected;
	private int _signon;
	private int _serverCount;
	private int _serverCrc;
	private int _mapCrc;
	private int _maxClients;
	private int _maxEdicts;
	private string _levelName = "";

	public string LevelName => _state < ClientState.Connected ? "" : _levelName;

	public bool HasLevelName => _levelName.Length != 0;

	public bool Dedicated => _state == ClientState.Dedicated;

	public bool Connected => _state >= ClientState.Connected;

	public bool CanForwardToServer => _state is ClientState.Connected or ClientState.Uninitialized or ClientState.Active;
	private string _serverInfo = "";

	public bool InLevel => _state == ClientState.Active && _signon == SignOns;

	private void CheckClientState()
	{
		if ( _state is ClientState.Connected or ClientState.Uninitialized && _signon == SignOns )
			_state = ClientState.Active;
	}

	private void CheckForResend()
	{
		if ( _state == ClientState.Disconnected )
		{
			if ( Server.Get( Scene ) is { Active: true } )
			{
				ConnectLocal();
				return;
			}

			if ( !Networking.IsClient )
				return;

			_state = ClientState.Connecting;
			_spectator = false;
			_connectTime = ConnectTimeReset;
			_connectRetry = 0;
		}

		if ( _state != ClientState.Connecting )
			return;

		if ( Resend < MinResend )
			Cvar.SetValue( "cl_resend", MinResend );
		else if ( Resend > MaxResend )
			Cvar.SetValue( "cl_resend", MaxResend );

		if ( Host.RealTime - _connectTime < Resend )
			return;

		if ( _connectRetry > MaxConnectRetries )
		{
			ExplainDisconnection( true, "#GameUI_CouldNotContactGameServer" );

			_connectTime = ConnectTimeReset;
			_connectRetry = 0;
			_state = ClientState.Disconnected;

			Disconnect();
			return;
		}

		_connectTime = Host.RealTime;

		Log.Info( _connectRetry == 0 ? $"Connecting to {Connection.Host.Address}...\n" : $"Retrying {Connection.Host.Address}...\n" );

		_connectRetry++;

		Net.SendConnectPacket( UserInfoString() );
	}

	public void ConnectionlessPacket( string packet )
	{
		if ( packet.Length == 0 )
			return;

		switch ( packet[0] )
		{
			case Server.ConnectionAccept:
				if ( _state == ClientState.Connected )
				{
					if ( !_demoPlayback )
						Log.Trace( "Duplicate connect ack. received.  Ignored.\n" );

					return;
				}

				_netchan.Setup( true, false, Connection.Host.Address, Net.SendPacketToServer, BlockSize );

				Log.Info( $"Connection accepted by {Connection.Host.Address}\n" );

				ConnectionAccepted();
				return;

			case Server.ConnectionBadPassword:
				if ( _state != ClientState.Connecting )
					return;

				Log.Info( packet.AsSpan( 1 ).StartsWith( Server.BadPassword ) ? packet[(1 + Server.BadPassword.Length)..] : packet[1..] );

				ExplainDisconnection( false, "#GameUI_ServerConnectionFailedBadPassword" );

				Log.Info( "Invalid server password.\n" );

				Disconnect();
				return;

			case Server.ConnectionReject:
				if ( _state != ClientState.Connecting )
					return;

				ExplainDisconnection( true, packet[1..] );
				Disconnect();
				return;
		}
	}

	public void Connect()
	{
		Disconnect();
		ConnectLocal();
	}

	private void ConnectLocal()
	{
		ClearMessageStats();

		_state = ClientState.Connecting;
		_spectator = false;

		if ( Server.Get( Scene ).ConnectLocalClient() is not { } slot )
			return;

		_netchan.Setup( true, true, slot.Address, slot.Netchan.Incoming.Enqueue, BlockSize );
		slot.Netchan.Send = _netchan.Incoming.Enqueue;

		Log.Trace( "Connection accepted.\n" );

		ConnectionAccepted();
	}

	private void ConnectionAccepted()
	{
		_state = ClientState.Connected;

		_netchan.Message.WriteByte( Server.StringCommand );
		_netchan.Message.WriteString( "new" );

		_demoNum = -1;
		_signon = 0;
		_validSequence = 0;
		_lastOutgoingCommand = -1;
		_nextCmdTime = (float)Host.RealTime;
	}

	public void GetPacket( byte[] packet )
	{
		if ( !_netchan.Loopback )
			_netchan.Incoming.Enqueue( packet );
	}

	public void HostRestart()
	{
		if ( _state != ClientState.Dedicated )
			_state = ClientState.Disconnected;

		Sound?.StopAllSounds();
	}

	public void Reconnect()
	{
		if ( _state is ClientState.Dedicated or ClientState.Disconnected or ClientState.Connecting )
			return;

		BeginLoadingPlaque();

		_signon = 0;
		_state = ClientState.Connected;

		_netchan.Clear();
		_netchan.Message.Clear();
		_netchan.Message.WriteByte( Server.StringCommand );
		_netchan.Message.WriteString( "new" );
	}

	public void Disconnect()
	{
		_connectTime = ConnectTimeReset;
		_connectRetry = 0;

		Sound?.StopAllSounds();

		if ( _demoPlayback )
		{
			StopPlayback();
		}
		else if ( _state is ClientState.Active or ClientState.Connected or ClientState.Uninitialized or ClientState.Connecting )
		{
			if ( _demoRecording )
				StopRecording();

			if ( _netchan.Send is not null )
			{
				_dropCommand.Clear();
				_dropCommand.WriteByte( Server.StringCommand );
				_dropCommand.WriteString( "dropclient\n" );

				for ( var i = 0; i < DropCommandCount; i++ )
					_netchan.Transmit( _dropCommand );
			}

			_state = ClientState.Disconnected;

			Server.Get( Scene )?.ShutdownServer();
		}

		if ( Networking.IsClient )
			Networking.Disconnect();

		_timeDemo = false;
		_demoPlayback = false;
		_signon = 0;

		ClearState();

		_netchan.Clear();

		if ( Server.Get( Scene ) is not { Active: true } )
		{
			Cvar.Set( "sv_allow_autoaim", "0" );
			Cvar.Set( "sv_allow_shaders", "0" );
		}

		_gameUi?.DisconnectFromServer();
	}

	public void ClearMemory()
	{
		_signon = 0;

		ClearClientState();

		Server.Get( Scene )?.ClearClientStates();
	}

	private void ClearState()
	{
		var server = Server.Get( Scene );

		if ( server is not { Active: true } )
		{
			ModelCache.Clear();
			ClearMemory();
		}

		ClearClientState();

		_netchan.Message.Clear();

		ClearLights();
		_playerLight = null;

		Array.Clear( _lightStyles );

		ClearTempEntities();
	}

	private static void ExplainDisconnection( bool print, string text )
	{
		if ( print && !text.StartsWith( '#' ) )
			Log.Info( $"{text}\n" );
	}

	private static void ForceCvars( bool multiplayer )
	{
		if ( !multiplayer )
			return;

		if ( FullBright != 0f )
			Cvar.Set( "r_fullbright", "0" );

		if ( SoundEngine.ShowCvar != 0f )
			Cvar.Set( "snd_show", "0" );

		if ( ChaseActive != 0f )
			Cvar.Set( "chase_active", "0" );

		if ( !Game.CheatsEnabled && ViewEntity.DrawEntities != 1f )
			Cvar.Set( "r_drawentities", "1.0" );

		if ( LightGamma < ForcedLightGamma )
			Cvar.Set( "lightgamma", "1.8" );
	}

	private void ParseServerInfo( MessageReader message )
	{
		Hpak.CheckSize( "custom" );

		Log.Trace( "Serverinfo packet received.\n" );

		ClearState();

		_hudScreenSize = new Vector2( ScreenWidth, ScreenHeight );
		_clientDll.VidInit();

		_demoWaiting = false;

		var protocol = message.ReadLong();

		if ( protocol != Host.ProtocolVersion && !_demoPlayback )
		{
			Log.Info( $"Server returned version {protocol}, not {Host.ProtocolVersion}\n" );
			return;
		}

		_serverCount = message.ReadLong();

		if ( _demoPlayback )
			_serverCount = ++_demoServerCount;

		_serverCrc = message.ReadLong();

		Sound.ServerCount = _serverCount;

		message.ReadBuf( _clientDllMd5.Length, _clientDllMd5 );

		_maxClients = message.ReadByte();

		if ( _maxClients < 1 || _maxClients > Server.MaxClientsLimit )
		{
			Log.Info( $"Bad maxclients ({_maxClients}) from server\n" );
			return;
		}

		Cvar.SetValue( "r_decals", _maxClients > 1 ? MultiplayerDecalLimit : SinglePlayerDecalLimit );

		ForceCvars( _maxClients > 1 );

		_maxEdicts = Server.DefaultMaxEdicts + ((_maxClients - 1) * 15);
		_playerNum = message.ReadByte();
		_serverCrc = Common.UnMunge3( _serverCrc, (-1 - _playerNum) & 0xFF );

		CreateLocalCustomizations();

		message.ReadByte();

		var gameDirectory = message.ReadString();

		if ( gameDirectory.Length == 0 )
		{
			Log.Info( "Server didn't specify a gamedir, assuming no change\n" );
		}
		else if ( !gameDirectory.Equals( Server.GameDirectory, StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Info( $"Server is running game {gameDirectory}.  Restart in that game to connect.\n" );

			Host.Disconnect();
			return;
		}

		Log.Trace( $"Remote host:  {message.ReadString()}\n" );

		var levelName = message.ReadString();

		_levelName = levelName.Length > MaxLevelNameLength ? levelName[..MaxLevelNameLength] : levelName;

		while ( true )
		{
			var value = message.ReadByte();

			if ( value is -1 or 0 or 0xFF )
				break;
		}

		message.ReadByte();

		CheckCrcs( _levelName );

		_demoServerCount = _serverCount;
		_state = ClientState.Connected;

		BeginDemoStartup();

		_netchan.Message.WriteByte( Server.StringCommand );
		_netchan.Message.WriteString( "sendres" );
	}

	private void CheckCrcs( string levelName )
	{
		if ( Server.Get( Scene ) is not { Active: true } )
		{
			var map = Server.LoadMountFile( levelName );

			if ( map is null || Server.MapFileCrc( levelName, map ) is not { } crc )
			{
				if ( map is not null )
				{
					ExplainDisconnection( true, "#GameUI_ConnectionFailed" );

					throw HostError( "Disconnected" );
				}

				if ( AllowDownload == 0f )
				{
					ExplainDisconnection( true, $"Refusing to download map {levelName}, (cl_allow_download is 0 ) disconnecting.\n" );

					throw HostError( "Disconnected" );
				}

				throw new NotSupportedException( "map download is not ported" );
			}

			_mapCrc = crc;

			if ( _serverCrc != _mapCrc && !_demoPlayback )
			{
				ExplainDisconnection( true, $"Your map [{levelName}] differs from the server's." );

				throw HostError( "Disconnected" );
			}
		}
	}

	private void ParseSendExtraInfo( MessageReader message )
	{
		var fallback = message.ReadString();

		message.ReadByte();

		if ( fallback.Length != 0 )
			throw new NotSupportedException( "fallback game directory is not ported" );
	}

	private static void ParseDeltaDescription( MessageReader message )
	{
		var name = message.ReadString();

		if ( name.Length == 0 )
			throw HostError( "CL_Parse_DeltaDescription:  Illegible description name\n" );

		if ( name.Length > MaxDescriptionNameLength )
			name = name[..MaxDescriptionNameLength];

		message.StartBitReading();

		Delta.SetRegistration( name, Delta.ReadDescription( message ) );

		message.EndBitReading();
	}

	private void ParseNewUserMsg( MessageReader message )
	{
		var id = message.ReadByte();
		var size = message.ReadByte();

		if ( size == 255 )
			size = -1;

		message.ReadBuf( _userMessageName.Length, _userMessageName );

		var name = StructImage.ReadString( _userMessageName, 0, _userMessageName.Length - 1 );
		var found = false;

		foreach ( var userMessage in _userMessages )
		{
			if ( userMessage.Id == id )
				userMessage.Id = 0;

			if ( !userMessage.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				continue;

			found = true;

			userMessage.Id = id;
			userMessage.Size = size;
		}

		if ( !found )
			_userMessages.Insert( 0, new ClientUserMessage { Id = id, Size = size, Name = name } );
	}

	private void ParseLightStyle( MessageReader message )
	{
		var style = message.ReadByte();

		if ( style >= Server.MaxLightStyles )
			throw HostError( "svc_lightstyle > MAX_LIGHTSTYLES" );

		var value = message.ReadString();

		_lightStyles[style] = value.Length > MaxLightStyleLength ? value[..MaxLightStyleLength] : value;
	}

	private void ParseRestore( MessageReader message )
	{
		RestoreDecals( message.ReadString() );

		var count = message.ReadByte();

		for ( var i = 0; i < count; i++ )
			Log.Info( $"Loading decals from {message.ReadString()}\n" );
	}

	private void ParseSignonNum( MessageReader message )
	{
		var signon = message.ReadByte();

		if ( signon <= _signon )
		{
			Log.Info( $"Received signon {signon} when at {_signon}\n" );

			Host.Disconnect();
			return;
		}

		_signon = signon;

		SignonReply();
	}

	private void SignonReply()
	{
		Log.Trace( $"CL_SignonReply: {_signon}\n" );

		switch ( _signon )
		{
			case 1:
				_netchan.Message.WriteByte( Server.StringCommand );
				_netchan.Message.WriteString( "sendents" );
				break;

			case 2:
				EndLoadingPlaque();

				_gameUi?.RunFrame( ScreenWidth, ScreenHeight, true, MaxClients > 1 );

				GameUiConnectToServer();
				break;
		}
	}

	private void ParseStuffText( MessageReader message )
	{
		var text = message.ReadString();

		Cmd.TokenizeString( text );

		var command = Cmd.Argv( 0 );

		if ( command.Equals( "allow_autoaim", StringComparison.OrdinalIgnoreCase ) )
		{
			Cvar.Set( "sv_allow_autoaim", Cmd.Argv( 1 ).StartsWith( '0' ) ? "0" : "1" );
			return;
		}

		if ( command.Equals( "allow_shaders", StringComparison.OrdinalIgnoreCase ) )
		{
			Cvar.Set( "sv_allow_shaders", Cmd.Argv( 1 ).StartsWith( '0' ) ? "0" : "1" );
			return;
		}

		if ( ValidStuffText( text ) )
			Cmd.AddText( text );
		else
			Log.Info( $"Server tried to send invalid command:\"{text}\"\n" );
	}

	private static bool AtWordStart( string text, int index, bool lineBreaks )
	{
		if ( index == 0 )
			return true;

		var previous = text[index - 1];

		return previous is ' ' or ';' || (lineBreaks && previous is '\n' or '\r');
	}

	private static bool HasWord( string text, string word )
	{
		var index = text.IndexOf( word, StringComparison.OrdinalIgnoreCase );

		return index >= 0 && AtWordStart( text, index, true );
	}

	public bool FilteredClientCmd( string command )
	{
		if ( command is null || !ValidStuffText( command ) )
			return false;

		Cmd.AddFilteredText( command );
		Cmd.AddFilteredText( "\n" );

		return true;
	}

	private bool ValidStuffText( string text )
	{
		if ( IsSinglePlayerGame && !_demoPlayback )
			return true;

		Cmd.TokenizeString( text );

		var command = Cmd.Argv( 0 );

		if ( command.StartsWith( "connect", StringComparison.OrdinalIgnoreCase ) )
			return false;

		foreach ( var name in StuffTextCommands )
		{
			if ( command.Contains( name, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		var valid = !text.StartsWith( "alias ", StringComparison.OrdinalIgnoreCase );
		var connect = text.IndexOf( "connect ", StringComparison.OrdinalIgnoreCase );
		var reconnect = text.IndexOf( "reconnect", StringComparison.OrdinalIgnoreCase );

		if ( connect >= 0 )
		{
			if ( reconnect >= 0 && reconnect + 2 == connect )
				connect = text.IndexOf( "connect ", connect + 1, StringComparison.OrdinalIgnoreCase );

			if ( connect >= 0 )
				valid = false;
		}

		foreach ( var word in StuffTextWords )
		{
			if ( HasWord( text, word ) )
				valid = false;
		}

		if ( HasWord( text, "motdfile" ) )
			valid = false;

		foreach ( var name in StuffTextContains )
		{
			if ( text.Contains( name, StringComparison.OrdinalIgnoreCase ) )
				valid = false;
		}

		if ( FilterStuffCmd <= 0f )
			return valid;

		foreach ( var name in StuffTextFiltered )
		{
			if ( text.Contains( name, StringComparison.OrdinalIgnoreCase ) )
				valid = false;
		}

		foreach ( var prefix in StuffTextPrefixes )
		{
			var index = text.IndexOf( prefix, StringComparison.OrdinalIgnoreCase );

			if ( index >= 0 && AtWordStart( text, index, false ) )
				valid = false;
		}

		foreach ( var character in Encoding.Latin1.GetBytes( text ) )
		{
			if ( (sbyte)character < ' ' && character != '\n' )
				valid = false;
		}

		return valid;
	}

	[ConCmd( "fullserverinfo" )]
	private static void FullServerInfoCommand( params string[] arguments )
	{
		if ( arguments.Length != 1 )
		{
			Log.Info( "usage: fullserverinfo <complete info string>\n" );
			return;
		}

		if ( Current is not { } client )
			return;

		client._serverInfo = arguments[0].Length > MaxClientServerInfoLength ? arguments[0][..MaxClientServerInfoLength] : arguments[0];
	}

	private void SendResourceListBlock( MessageReader message )
	{
		if ( _state is ClientState.Dedicated or ClientState.Disconnected )
		{
			Log.Info( "custom resource list not valid -- not connected\n" );
			return;
		}

		if ( message.ReadLong() != _serverCount && !_demoPlayback )
		{
			Log.Info( "CL_SendResourceListBlock_f from different level\n" );
			return;
		}

		var start = message.ReadLong();

		if ( start < 0 || start > _customResources.Count )
		{
			Log.Info( "custom resource list request out of range\n" );
			return;
		}

		var buffer = _resourceBlock;

		buffer.Clear();
		buffer.WriteByte( Server.ResourceListCommand );
		buffer.WriteShort( _customResources.Count );

		for ( var i = start; i < _customResources.Count; i++ )
		{
			var resource = _customResources[i];

			buffer.WriteString( resource.Name );
			buffer.WriteByte( (int)resource.Type );
			buffer.WriteShort( resource.Index );
			buffer.WriteLong( resource.DownloadSize );
			buffer.WriteByte( resource.Flags );

			if ( (resource.Flags & Resource.Custom) != 0 )
				buffer.Write( resource.Md5, Resource.Md5Size );
		}

		_netchan.CreateFragments( buffer );
		_netchan.FragSend();
	}

	private int BlockSize()
	{
		if ( _state != ClientState.Active )
			return ConnectingBlockSize;

		var value = Common.Atof( DownloadMax );

		return value >= MinDownloadMax ? (int)Math.Min( (double)value, MaxDownloadMax ) : DefaultBlockSize;
	}
}
