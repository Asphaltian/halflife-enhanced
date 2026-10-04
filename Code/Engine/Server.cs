using System;
using System.IO;
using System.Threading.Tasks;
using Sandbox.Mounting;

namespace GoldSrc;

public sealed partial class Server : GameObjectSystem<Server>, IHotloadManaged
{
	public const string MountIdent = "hl1";
	public const string GameDirectory = "valve";
	public const string PlatformDirectory = "platform";
	public const string FallbackDirectory = "";

	public bool Active { get; private set; }
	public string Name { get; private set; }
	public double Time { get; private set; }
	public bool Paused { get; private set; }
	public const int MaxClientsLimit = 32;

	private static int _spawnCount;

	public int SpawnCount { get; private set; }

	public int MaxClients { get; set; } = 1;
	public bool Deathmatch { get; private set; }
	public bool Coop { get; private set; }

	public GlobalVars Globals { get; } = new();
	public GameDll GameDll { get; private set; }
	public Bsp.File WorldModel { get; private set; }

	public Server( Scene scene ) : base( scene )
	{
	}

	void IHotloadManaged.Created( IReadOnlyDictionary<string, object> state ) => Host.Hotloaded();

	public async Task<bool> SpawnServer( string mapName, string startSpot = null )
	{
		var modelName = $"maps/{mapName}.bsp";

		var loader = MountUtility.FindLoader( GetMountPath( modelName ) );
		if ( loader is null || await loader.GetOrCreate() is not byte[] data )
		{
			Log.Warning( $"Couldn't spawn server {modelName}" );
			return false;
		}

		if ( MapChangeConfigFile.Length > 2 )
		{
			Log.Trace( "Executing map change config file" );
			Cmd.AddText( $"exec {MapChangeConfigFile}\n" );
		}

		if ( mapName.Length > 2 )
			Cmd.AddText( $"exec maps/{mapName}_load.cfg\n" );

		Log.Info( $"Spawn Server {mapName}" );

		if ( Active && MaxClients > 1 )
		{
			foreach ( var client in Clients )
			{
				if ( (!client.Active && !client.Spawned && !client.Connected) || GetEdict( client.EdictIndex ) is not { } edict )
					continue;

				if ( edict.HasPrivateData )
					GameDll.ClientDisconnect( edict );
				else
					Log.Info( $"Skipping reconnect on {client.Name}, no pvPrivateData\n" );
			}
		}

		LogOpen();
		LogPrintf( $"Loading map \"{mapName}\"\n" );

		SpawnCount = ++_spawnCount;

		if ( CoopCvar != 0f )
			Cvar.SetValue( "deathmatch", 0f );

		Cvar.SetValue( "skill", Math.Clamp( (int)(Skill + 0.5), 0, 3 ) );

		if ( MaxClients == 1 )
			Cvar.SetValue( "sv_clienttrace", 1f );

		ClearEdicts();

		Paused = false;

		GameDll ??= GameDll.Load();

		InitDeltas();

		Active = false;
		_loading = true;
		_loadGame = false;
		_modelName = modelName;
		Name = mapName;
		Time = 1.0;
		WorldModel = new Bsp.File( data );

		_worldMapCrc = 0;

		if ( MaxClients > 1 )
		{
			if ( MapFileCrc( modelName, data ) is not { } crc )
			{
				Log.Info( $"Couldn't CRC server map:  {modelName}\n" );
				return false;
			}

			_worldMapCrc = crc;
		}

		Globals.Time = 1f;
		Globals.MapName = mapName;
		Globals.StartSpot = startSpot;

		Deathmatch = CoopCvar == 0f && DeathmatchCvar != 0f;
		Coop = CoopCvar != 0f;

		_maxEdicts = DefaultMaxEdicts + ((MaxClients - 1) * 15);
		_edicts = new Edict[_maxEdicts];
		_freeTime = new float[_maxEdicts];
		_numEdicts = MaxClients + 1;

		InitClientEdicts();

		PrecacheWorldModel( modelName );
		ClearWorld();
		ClearSounds();

		_messageStarted = false;
		_lastCheck = 0;
		_lastCheckTime = 0.0;
		ViewEntity = null;
		_checkPvs = null;
		_signon.Clear();
		_datagram.Clear();
		_reliableDatagram.Clear();
		_multicast.Clear();
		_spectator.Clear();

		Array.Clear( LightStyles );
		LoadVisibility();

		SetMoveVars();

		Listen();

		Active = true;
		return true;
	}

	public void ServerDeactivate()
	{
		if ( DisconnectConfigFile.Length > 2 )
		{
			Log.Trace( "Executing disconnect config file" );
			Cmd.AddText( $"exec {DisconnectConfigFile}\n" );
		}

		if ( Name is { Length: > 2 } )
			Cmd.AddText( $"exec maps/{Name}_unload.cfg\n" );

		Globals.Time = (float)Time;

		if ( Active )
			GameDll.ServerDeactivate();
	}

	public void ShutdownServer()
	{
		if ( !Active )
			return;

		Active = false;
		Paused = false;

		foreach ( var client in Clients )
		{
			if ( client.Active || client.Connected )
				DropClient( client, false, "Server shutting down" );
		}

		StopListening();

		ClearEdicts();
		ClearSounds();

		if ( _shadow.IsValid() )
			_shadow.GameObject.Destroy();
	}

	public void LoadEntities()
	{
		LoadFromFile( WorldModel.Entities );
	}

	private const int BspHeaderSize = 124;
	private const int BspLumps = 15;

	public static int? MapFileCrc( string fileName, byte[] data )
	{
		if ( data.Length < BspHeaderSize )
		{
			Log.Info( $"Could not read BSP header for map [{fileName}].\n" );
			return null;
		}

		var version = BitConverter.ToInt32( data, 0 );

		if ( version != Bsp.File.QuakeVersion && version != Bsp.File.Version )
		{
			Log.Info( $"Map [{fileName}] has incorrect BSP version ({version} should be {Bsp.File.Version}).\n" );
			return null;
		}

		var entities = GameDirectory.Equals( "bshift", StringComparison.OrdinalIgnoreCase ) ? 1 : 0;
		var crc = Crc32.Initial;

		for ( var i = 0; i < BspLumps; i++ )
		{
			if ( i == entities )
				continue;

			var offset = BitConverter.ToInt32( data, 4 + (i * 8) );
			var length = BitConverter.ToInt32( data, 8 + (i * 8) );

			if ( length <= 0 )
				continue;

			if ( offset < 0 || offset + length > data.Length )
				return null;

			crc = Crc32.Process( crc, data.AsSpan( offset, length ) );
		}

		return unchecked((int)crc);
	}

	public void ActivateServer( bool runPhysics )
	{
		SvNewUnit = 0f;

		GameDll.ServerActivate( this );

		_loading = false;

		if ( !runPhysics )
		{
			Physics( 0.001 );
		}
		else if ( MaxClients > 1 )
		{
			for ( var i = 0; i < 16; i++ )
				Physics( 0.8 );
		}
		else
		{
			Physics( 0.1 );
			Physics( 0.1 );
		}

		CreateBaseline();
		CreateResourceList();
		SendServerInfoToClients();

		Log.Trace( MaxClients < 2 ? "Game started\n" : $"{MaxClients} player server started\n" );

		LogPrintf( $"Started map \"{Name}\" (CRC \"{_worldMapCrc}\")\n" );
	}

	public void LinkUserMessages() => LinkNewUserMessages();

	public static bool IsMapValid( string mapName ) => MountUtility.FindLoader( GetMountPath( $"maps/{mapName}.bsp" ) ) is not null;

	[SkipHotload]
	private static readonly Dictionary<string, string> MountPaths = new( StringComparer.OrdinalIgnoreCase );

	private static string[] _searchPaths = [GameDirectory];

	public static void SetGameDirectory()
	{
		var paths = new List<string>();

		if ( Registry.ReadString( "User Token 2" ).Length != 0 || Registry.ReadString( "User Token 3" ).Length != 0 )
			paths.Add( $"{GameDirectory}_lv" );

		if ( Registry.ReadInt( "addons_folder" ) > 0 )
			paths.Add( $"{GameDirectory}_addon" );

		if ( Registry.ReadInt( "hdmodels" ) > 0 )
			paths.Add( $"{GameDirectory}_hd" );

		paths.Add( GameDirectory );
		paths.Add( $"{GameDirectory}_downloads" );

		_searchPaths = [.. paths];

		MountPaths.Clear();
	}

	private static bool MountFileExists( string path )
	{
		return MountUtility.FindLoader( path ) is not null
			|| MountUtility.FindLoader( $"{path}.vmdl" ) is not null
			|| MountUtility.FindLoader( $"{path}.vtex" ) is not null
			|| MountUtility.FindLoader( $"{path}.vsnd" ) is not null;
	}

	public static string GetMountPath( string fileName )
	{
		if ( MountPaths.TryGetValue( fileName, out var cached ) )
			return cached;

		var path = string.Join( '/', fileName.Split( ['/', '\\'], StringSplitOptions.RemoveEmptyEntries ) );
		var result = $"mount://{MountIdent}/{GameDirectory}/{path}";

		foreach ( var directory in _searchPaths )
		{
			var candidate = $"mount://{MountIdent}/{directory}/{path}";

			if ( !MountFileExists( candidate ) )
				continue;

			result = candidate;
			break;
		}

		return MountPaths[fileName] = result;
	}

	public static string FindMountPath( string fileName, string resourceExtension = "" )
	{
		var path = GetMountPath( fileName );

		if ( MountUtility.FindLoader( $"{path}{resourceExtension}" ) is not null )
			return path;

		path = $"mount://{MountIdent}/{PlatformDirectory}/{string.Join( '/', fileName.Split( ['/', '\\'], StringSplitOptions.RemoveEmptyEntries ) )}";

		return MountUtility.FindLoader( $"{path}{resourceExtension}" ) is not null ? path : null;
	}

	private static Exception HostError( string message )
	{
		return new InvalidDataException( message );
	}
}
