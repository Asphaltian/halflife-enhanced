using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace GoldSrc;

public static partial class Host
{
	private static bool _bound;

	[ConVar( "developer" )]
	public static float Developer { get; set; }

	public static void Hotloaded()
	{
		_bound = false;

		Cmd.Hotloaded();
	}

	public static void Frame()
	{
		if ( !_bound && !Application.IsDedicatedServer )
		{
			_bound = true;

			if ( Client.PlayerName == Client.DefaultPlayerName )
				Client.PlayerName = Encoding.Latin1.GetString( Encoding.UTF8.GetBytes( Sandbox.Utility.Steam.PersonaName ) );
		}

		Cmd.Execute();
	}

	public static void Disconnect()
	{
		Client.Current?.Disconnect();
		Server.Current?.ShutdownServer();
		Client.Current?.ActivateGameUi();
	}

	[ConCmd( "setmaster" )]
	public static void SetMaster( params string[] arguments )
	{
		if ( Server.Current is not { Active: true } )
		{
			Log.Info( "Usage:\nSetmaster unavailable, start a server first.\n" );
			return;
		}

		if ( arguments.Length is < 1 or > 4 )
			Log.Info( "Usage:\nSetmaster <enable | disable>\n" );
	}

	[ConCmd( "maxplayers" )]
	public static void MaxPlayers( params string[] arguments )
	{
		if ( Server.Current is not { } server )
			return;

		if ( arguments.Length != 1 )
		{
			Log.Info( $"\"maxplayers\" is \"{server.MaxClients}\"" );
			return;
		}

		if ( server.Active )
		{
			Log.Info( "maxplayers cannot be changed while a server is running." );
			return;
		}

		var count = System.Math.Max( Common.Atoi( arguments[0] ), 1 );

		if ( count > Server.MaxClientsLimit )
		{
			Log.Info( $"\"maxplayers\" set to \"{Server.MaxClientsLimit}\"" );

			count = Server.MaxClientsLimit;
		}

		server.MaxClients = count;

		Cvar.SetValue( "deathmatch", count == 1 ? 0f : 1f );
	}

	private const int MaxMapCommandLength = 54;

	[ConCmd( "map" )]
	public static void MapCommand( string mapName = null )
	{
		if ( Server.Current is not { } server )
			return;

		if ( mapName is null )
		{
			Log.Info( "map <levelname> : changes server to specified map" );
			return;
		}

		if ( mapName.Length > MaxMapCommandLength )
		{
			Log.Info( "map change failed: command string is too long." );
			return;
		}

		if ( server.GameDll is null )
			server.InitializeGameDll();

		if ( mapName.Length > 4 && mapName.EndsWith( ".bsp", StringComparison.OrdinalIgnoreCase ) )
			mapName = mapName[..^4];

		if ( Server.FindMountPath( $"maps/{mapName}.bsp" ) is null )
		{
			Log.Info( $"map change failed: '{mapName}' not found on server." );
			return;
		}

		Client.Current?.BeginLoadingPlaque();

		_ = Map( server, mapName, false, shutdown: true );
	}

	[ConCmd( "save" )]
	public static void SaveGame( string saveName = null )
	{
		var server = Server.Current;
		if ( server is null || !server.IsValidSave() )
			return;

		if ( saveName is null )
		{
			Log.Info( "save <savename> : save a game" );
			return;
		}

		if ( saveName.StartsWith( '/' ) || saveName.Contains( ".." ) || saveName.Contains( ':' ) || saveName.Contains( '\\' ) )
		{
			Log.Info( $"Couldn't open {saveName} (contains illegal characters)." );
			return;
		}

		if ( SaveFileBase( saveName ) is not { } fileBase )
		{
			Log.Info( $"Couldn't open {saveName} (wrong file extension, must be .sav)" );
			return;
		}

		server.SaveGame( fileBase, server.SaveGameComment() );

		ClientDll.Loaded?.Engine.HudMessage( "GAMESAVED" );
	}

	private static string SaveFileBase( string saveName )
	{
		var extension = saveName.LastIndexOf( '.' );

		if ( extension < 0 )
			return saveName;

		return saveName[(extension + 1)..].Equals( "sav", StringComparison.OrdinalIgnoreCase ) ? saveName[..extension] : null;
	}

	[ConCmd( "autosave" )]
	public static void AutoSave()
	{
		if ( Server.Current is { } server && server.IsValidSave() )
			server.SaveGame( "autosave", server.SaveGameComment() );
	}

	[ConCmd( "load" )]
	public static void LoadGame( string saveName = null )
	{
		var server = Server.Current;
		if ( server is null )
			return;

		if ( saveName is null )
		{
			Log.Info( "load <savename> : load a game" );
			return;
		}

		if ( !Load( server, saveName ) )
			Log.Info( "Error loading saved game" );
	}

	[ConCmd( "reload" )]
	public static void Reload()
	{
		var server = Server.Current;
		if ( server is null || !server.Active || Client.Current is { DemoPlayback: true } )
			return;

		var mapName = server.Name;

		server.ClearGameState();

		if ( server.LatestSaveGame() is not { } saveName || !Load( server, saveName ) )
			_ = Map( server, mapName, false, false, shutdown: true );
	}

	private const int MaxSaveSlotDigits = 5;
	private const int MaxSaveSlot = 12;

	private static bool Load( Server server, string saveName )
	{
		if ( string.IsNullOrEmpty( saveName ) )
			return false;

		if ( saveName.Contains( ".." ) )
		{
			Log.Info( "Relative pathnames are not allowed." );
			return false;
		}

		if ( saveName[0] == '_' )
		{
			var digits = 0;

			while ( digits < MaxSaveSlotDigits && digits + 1 < saveName.Length && char.IsAsciiDigit( saveName[digits + 1] ) )
				digits++;

			var slot = Common.Atoi( saveName.Substring( 1, digits ) );

			if ( slot < 1 || slot > MaxSaveSlot )
				return false;

			saveName = $"Half-Life-{slot}";
		}

		if ( server.GameDll is null )
			server.InitializeGameDll();

		if ( SaveFileBase( saveName ) is not { } fileBase || server.LoadGame( fileBase ) is not { } header )
			return false;

		Client.Current?.BeginLoadingPlaque();

		Cvar.SetValue( "deathmatch", 0f );
		Cvar.SetValue( "coop", 0f );

		server.MaxClients = 1;

		Cvar.SetValue( "maxplayers", 1f );

		Client.Current?.BeginLoadingPlaque();

		_ = Map( server, header.MapName, true, shutdown: true );

		return true;
	}

	[ConCmd( "restart" )]
	public static void Restart()
	{
		if ( Server.Current is not { Active: true } server || Client.Current is { DemoPlayback: true } )
			return;

		Client.Current?.HostRestart();

		server.InactivateClients();
		server.ServerDeactivate();

		_ = Map( server, server.Name, false );
	}

	[ConCmd( "changelevel2" )]
	public static void ChangeLevel2( string levelName = null, string startSpot = null )
	{
		if ( levelName is null )
		{
			Log.Info( "changelevel2 <levelname> : continue game on a new level in the unit" );
			return;
		}

		_ = Server.Current?.ChangeLevel2( levelName, startSpot );
	}

	[ConCmd( "changelevel" )]
	public static void ChangeLevel( string levelName = null, string startSpot = null )
	{
		if ( levelName is null )
		{
			Log.Info( "changelevel <levelname> : continue game on a new level" );
			return;
		}

		if ( Server.Current is not { Active: true } server || Client.Current is { DemoPlayback: true } )
		{
			Log.Info( "Only the server may changelevel" );
			return;
		}

		if ( !Server.IsMapValid( levelName ) )
		{
			Log.Info( $"changelevel failed: '{levelName}' not found on server." );
			return;
		}

		Client.Current?.BeginLoadingPlaque();

		server.InactivateClients();
		server.ServerDeactivate();

		_ = Map( server, levelName, false, false, startSpot );
	}

	private static async Task Map( Server server, string mapName, bool loadGame, bool clearGameState = true, string startSpot = null, bool shutdown = false )
	{
		var client = Client.Current;

		if ( client is not null )
			await client.LoadingPlaquePresented();

		if ( shutdown )
		{
			server.ShutdownServer();
			client?.Disconnect();
		}

		client?.ClearMemory();

		if ( !loadGame && clearGameState )
			server.ClearGameState();

		if ( !await server.SpawnServer( mapName, startSpot ) )
		{
			client?.EndLoadingPlaque();
			return;
		}

		if ( loadGame )
		{
			if ( !server.LoadGamestate( mapName, true ) )
				server.LoadEntities();

			server.SetLoadGame();
			server.ActivateServer( false );
		}
		else
		{
			server.LoadEntities();
			server.ActivateServer( true );
		}

		server.LinkUserMessages();

		if ( shutdown && client is { Dedicated: false } )
			client.Connect();
	}

	[ConCmd( "god", ConVarFlags.Cheat )]
	public static void God()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "god", [] );
			return;
		}

		var server = Server.Current;

		if ( server.Deathmatch )
			return;

		var player = server.HostPlayer;

		player.EntityFlags ^= EntityFlags.GodMode;

		server.ClientPrintf( (player.EntityFlags & EntityFlags.GodMode) == EntityFlags.GodMode ? "godmode ON\n" : "godmode OFF\n" );
	}

	[ConCmd( "notarget" )]
	public static void NoTarget()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "notarget", [] );
			return;
		}

		var server = Server.Current;

		if ( server.Deathmatch )
			return;

		var player = server.HostPlayer;

		player.EntityFlags ^= EntityFlags.NoTarget;

		server.ClientPrintf( (player.EntityFlags & EntityFlags.NoTarget) == EntityFlags.NoTarget ? "notarget ON\n" : "notarget OFF\n" );
	}

	[ConCmd( "fly" )]
	public static void Fly()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "fly", [] );
			return;
		}

		var server = Server.Current;

		if ( server.Deathmatch )
			return;

		var player = server.HostPlayer;

		if ( player.MoveType != MoveType.Fly )
		{
			player.MoveType = MoveType.Fly;
			server.ClientPrintf( "flymode ON\n" );
			return;
		}

		player.MoveType = MoveType.Walk;
		server.ClientPrintf( "flymode OFF\n" );
	}

	[ConCmd( "kill" )]
	public static void Kill()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "kill", [] );
			return;
		}

		var server = Server.Current;
		var player = server.HostPlayer;

		if ( player.Health <= 0f )
		{
			server.ClientPrintf( "Can't suicide -- already dead!\n" );
			return;
		}

		if ( server.HostClient is { Active: true, Connected: true, Spawned: true } )
		{
			server.Globals.Time = (float)server.Time;
			server.GameDll.ClientKill( player );
			return;
		}

		server.ClientPrintf( "Can't kill yourself -- you're not connected!\n" );
	}

	[ConCmd( "echo" )]
	public static void Echo( params string[] arguments )
	{
		Log.Info( string.Concat( arguments.Select( argument => $"{argument} " ) ) );
	}

	[ConCmd( "setinfo" )]
	public static void SetInfo( params string[] arguments )
	{
		if ( arguments.Length == 0 )
		{
			Info.Print( Client.UserInfoString() );
			return;
		}

		if ( arguments.Length != 2 )
		{
			Log.Info( "usage: setinfo [ <key> <value> ]" );
			return;
		}

		if ( !Cmd.FromClient )
		{
			Client.SetUserInfoKey( arguments[0], arguments[1] );
			Cmd.ForwardToServer( "setinfo", arguments );
			return;
		}

		Server.Current.SetUserInfo( arguments[0], arguments[1] );
	}

	[ConCmd( "pause" )]
	public static void Pause()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "pause", [] );
			return;
		}

		Server.Current.TogglePause();
	}

	[ConCmd( "setpause" )]
	public static void SetPause()
	{
		if ( Client.Current is not { HasLevelName: true } )
			return;

		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "setpause", [] );
			return;
		}

		Server.Current.SetPause( true );
	}

	[ConCmd( "unpause" )]
	public static void Unpause()
	{
		if ( Client.Current is not { HasLevelName: true } )
			return;

		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "unpause", [] );
			return;
		}

		Server.Current.SetPause( false );
	}

	[ConCmd( "noclip", ConVarFlags.Cheat )]
	public static void NoClip()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "noclip", [] );
			return;
		}

		var server = Server.Current;

		if ( server.Deathmatch )
			return;

		var player = server.HostPlayer;

		if ( player.MoveType != MoveType.NoClip )
		{
			player.MoveType = MoveType.NoClip;
			server.ClientPrintf( "noclip ON\n" );
			return;
		}

		player.MoveType = MoveType.Walk;
		player.OldOrigin = player.Origin;
		server.ClientPrintf( "noclip OFF\n" );

		if ( server.TestEntityPosition( player ) is null )
			return;

		MathLib.AngleVectors( player.ViewAngle, out var forward, out var right, out var up );

		if ( !FindPassableSpace( server, player, forward, 1f )
			&& !FindPassableSpace( server, player, right, 1f )
			&& !FindPassableSpace( server, player, right, -1f )
			&& !FindPassableSpace( server, player, up, 1f )
			&& !FindPassableSpace( server, player, up, -1f )
			&& !FindPassableSpace( server, player, forward, -1f ) )
		{
			Log.Trace( "Can't find the world\n" );
		}

		player.Origin = player.OldOrigin;
	}

	private static bool FindPassableSpace( Server server, Edict edict, Vector3 direction, float step )
	{
		for ( var i = 0; i < 100; i++ )
		{
			edict.Origin += step * direction;

			if ( server.TestEntityPosition( edict ) is null )
			{
				edict.OldOrigin = edict.Origin;
				return true;
			}
		}

		return false;
	}
}
