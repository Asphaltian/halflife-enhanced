using System;

namespace GoldSrc;

partial class Client
{
	public const int MaxModels = 512;

	private const int MaxSounds = 512;
	private const int MaxEvents = 256;
	private const int MaxDecalNames = 512;

	[ConVar( "cl_allowdownload", ConVarFlags.Saved )]
	public static float AllowDownload { get; set; } = 1f;

	[ConVar( "cl_download_ingame", ConVarFlags.Saved )]
	private static float DownloadInGame { get; set; } = 1f;

	private readonly SizeBuf _resourceBatch = new( "Client to Server Resource Batch", 0x10000, false );
	private readonly SizeBuf _resourceRegistration = new( "Resource Registration", 0x5000, false );
	private readonly List<Resource> _resourcesNeeded = [];
	private readonly List<Resource> _resourcesOnHand = [];
	private readonly List<Resource> _customResources = [];
	private readonly Model[] _modelPrecache = new Model[MaxModels];
	private readonly Sfx[] _soundPrecache = new Sfx[MaxSounds];
	private readonly string[] _eventPrecache = new string[MaxEvents];
	private readonly string[] _decalNames = new string[MaxDecalNames];

	private int _modelPrecacheCount;
	private string _downloadUrl = "";
	private bool _customDownload;

	public Model GetModelByIndex( int index ) => index >= 0 && index < MaxModels ? _modelPrecache[index] : null;

	public BrushModel GetBrushModel( int index ) => GetModelByIndex( index ) as BrushModel;

	public Studio.File GetStudioModel( int index ) => (GetModelByIndex( index ) as StudioModel)?.File;

	public SpriteModel GetSpriteModel( int index ) => GetModelByIndex( index ) as SpriteModel;

	public Sandbox.Model GetRenderModel( int index ) => GetModelByIndex( index )?.RenderModel;

	public int ModelFrames( int index ) => Server.ModelFrames( GetModelByIndex( index ) );

	public BrushModel WorldModel => _modelPrecache[1] as BrushModel;

	private Bsp.File WorldFile => WorldModel?.File;

	public int FindModelIndex( string name )
	{
		for ( var i = 1; i < _modelPrecacheCount; i++ )
		{
			if ( _modelPrecache[i] is { } model && model.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return 0;
	}

	public int LoadModel( string name )
	{
		for ( var i = 1; i < MaxModels && _modelPrecache[i] is { } model; i++ )
		{
			if ( model.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	public int PrecacheEvent( int type, string name )
	{
		for ( var i = 1; i < MaxEvents && _eventPrecache[i] is { } eventName; i++ )
		{
			if ( eventName.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return 0;
	}

	public int DecalIndexFromName( string name )
	{
		if ( name.Length > MaxDecalNameLength )
			name = name[..MaxDecalNameLength];

		if ( name.StartsWith( '}' ) )
			name = $"{{{name[1..]}";

		for ( var i = 0; i < MaxDecalNames; i++ )
		{
			if ( _decalNames[i] is { Length: > 0 } decalName && decalName == name )
				return i;
		}

		return 0;
	}

	private void ParseResourceLocation( MessageReader message )
	{
		var url = message.ReadString();

		if ( !url.StartsWith( "http://", StringComparison.OrdinalIgnoreCase ) && !url.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( !url.EndsWith( '/' ) )
			url = $"{url}/";

		_downloadUrl = url.Length > 127 ? url[..127] : url;

		Log.Trace( $"Using {_downloadUrl} as primary download location\n" );
	}

	private void ParseResourceList( MessageReader message )
	{
		message.StartBitReading();

		var total = (int)message.ReadBits( 12 );

		for ( var i = 0; i < total; i++ )
		{
			var resource = new Resource { Type = (ResourceKind)message.ReadBits( 4 ) };
			var name = message.ReadBitString();

			resource.Name = name.Length > Resource.MaxNameLength ? name[..Resource.MaxNameLength] : name;
			resource.Index = (int)message.ReadBits( 12 );
			resource.DownloadSize = (int)message.ReadBits( 24 );

			var flags = (int)message.ReadBits( 3 );

			resource.Flags = flags & ~Resource.WasMissing;

			if ( (flags & Resource.Custom) != 0 )
				message.ReadBitData( resource.Md5, Resource.Md5Size );

			if ( message.ReadBits( 1 ) != 0 )
				message.ReadBitData( resource.Reserved, Resource.ReservedSize );

			_resourcesNeeded.Add( resource );
		}

		if ( message.ReadBits( 1 ) != 0 )
			throw new NotSupportedException( "consistency list is not ported" );

		message.EndBitReading();

		StartResourceDownloading( "Verifying and downloading resources...\n", false );
	}

	private static bool FileMissing( string fileName ) => Server.FileSize( fileName ) is 0 or -1;

	private void StartResourceDownloading( string text, bool custom )
	{
		if ( text is not null )
			Log.Trace( text );

		foreach ( var resource in _resourcesNeeded )
		{
			var missing = resource.Type switch
			{
				ResourceKind.Sound => resource.Name[0] != '*' && FileMissing( $"sound/{resource.Name}" ),
				ResourceKind.Skin or ResourceKind.Generic or ResourceKind.EventScript => FileMissing( resource.Name ),
				ResourceKind.Model => resource.Name[0] != '*' && resource.Flags != 0 && FileMissing( resource.Name ),
				ResourceKind.Decal => (resource.Flags & Resource.Custom) != 0,
				_ => false
			};

			if ( missing )
				resource.Flags |= Resource.WasMissing;
		}

		if ( !custom )
			_state = ClientState.Uninitialized;

		_customDownload = custom;

		BatchResourceRequest();
	}

	private void MoveToOnHandList( Resource resource )
	{
		_resourcesNeeded.Remove( resource );
		_resourcesOnHand.Add( resource );
	}

	private bool CheckFile( SizeBuf buffer, string fileName )
	{
		if ( AllowDownload == 0f )
		{
			Log.Trace( "Download refused, cl_allow_download is 0\n" );
			return true;
		}

		if ( _state == ClientState.Active && DownloadInGame == 0f )
		{
			Log.Trace( "In-game download refused...\n" );
			return true;
		}

		if ( Server.FileExists( fileName ) )
			return true;

		if ( _demoPlayback )
		{
			Log.Info( $"Warning! File {fileName} missing during demo playback." );
			return true;
		}

		if ( _downloadUrl.Length != 0 )
			throw new NotSupportedException( "HTTP download is not ported" );

		buffer.WriteByte( Server.StringCommand );
		buffer.WriteString( $"dlfile {fileName}" );

		return false;
	}

	private void BatchResourceRequest()
	{
		var buffer = _resourceBatch;

		buffer.Clear();

		foreach ( var resource in _resourcesNeeded.ToArray() )
		{
			if ( (resource.Flags & Resource.WasMissing) == 0 )
			{
				MoveToOnHandList( resource );
				continue;
			}

			if ( _state == ClientState.Active && DownloadInGame == 0f )
			{
				Log.Info( $"Skipping in game download of {resource.Name}\n" );

				MoveToOnHandList( resource );
				continue;
			}

			if ( !Server.IsSafeFileToDownload( resource.Name ) )
			{
				_resourcesNeeded.Remove( resource );

				Log.Info( $"Invalid file type...skipping download of {resource.Name}\n" );
				continue;
			}

			switch ( resource.Type )
			{
				case ResourceKind.Sound:
					if ( resource.Name[0] == '*' || CheckFile( buffer, $"sound/{resource.Name}" ) )
						MoveToOnHandList( resource );

					break;

				case ResourceKind.Skin:
					MoveToOnHandList( resource );
					break;

				case ResourceKind.Model:
					if ( resource.Name[0] == '*' || CheckFile( buffer, resource.Name ) )
						MoveToOnHandList( resource );

					break;

				case ResourceKind.Decal:
					if ( Hpak.GetDataPointer( Hpak.Custom, resource ) )
					{
						MoveToOnHandList( resource );
					}
					else if ( (resource.Flags & Resource.Requested) == 0 )
					{
						buffer.WriteByte( Server.StringCommand );
						buffer.WriteString( $"dlfile {CustomizationPrefix}{Hpak.Md5Print( resource.Md5 )}" );

						resource.Flags |= Resource.Requested;
					}

					break;

				case ResourceKind.Generic:
				case ResourceKind.EventScript:
					if ( CheckFile( buffer, resource.Name ) )
						MoveToOnHandList( resource );

					break;
			}
		}

		if ( _state == ClientState.Disconnected )
			return;

		if ( buffer.CurSize == 0 && PrecacheResources() )
			RegisterResources( buffer );

		_netchan.CreateFragments( buffer );
		_netchan.FragSend();
	}

	private void ParseFileTransferFailed( MessageReader message )
	{
		var name = message.ReadString();

		if ( name.Length == 0 )
			throw HostError( "CL_ParseFileTxferFailed:  empty filename\n" );

		if ( !_demoPlayback )
			ProcessFile( false, name );
	}

	private void ProcessFile( bool successfully, string name )
	{
		if ( !successfully )
			Log.Info( $"Error: server failed to transmit file '{(name[0] == '!' ? "customization" : name)}'\n" );
		else if ( name[0] != '!' )
			Log.Info( $"Processing {name}\n" );

		var soundName = name.StartsWith( "sound/", StringComparison.OrdinalIgnoreCase ) ? name["sound/".Length..] : name;
		var hash = name.StartsWith( CustomizationPrefix, StringComparison.OrdinalIgnoreCase ) ? Hpak.HexConvert( soundName[CustomizationPrefix.Length..], Resource.Md5Size * 2 ) : null;

		foreach ( var resource in _resourcesNeeded )
		{
			if ( hash is not null )
			{
				if ( !resource.Md5.AsSpan().SequenceEqual( hash ) )
					continue;
			}
			else if ( !resource.Name.Equals( resource.Type == ResourceKind.Generic ? name : soundName, StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			if ( successfully )
				resource.Flags &= ~Resource.WasMissing;

			if ( name[0] == '!' )
				ProcessCustomizationFile( resource );

			_netchan.TempBuffer = null;

			MoveToOnHandList( resource );
			break;
		}

		if ( _state == ClientState.Disconnected )
			return;

		if ( _resourcesNeeded.Count == 0 )
		{
			var buffer = _resourceRegistration;

			buffer.Clear();

			if ( PrecacheResources() )
				RegisterResources( buffer );

			if ( buffer.CurSize > 0 )
			{
				_netchan.CreateFragments( buffer );
				_netchan.FragSend();
			}
		}

		if ( _netchan.TempBuffer is not null )
			Log.Info( $"CL_ProcessFile: Received a decal {soundName}, but didn't find it in resources needed list!\n" );

		_netchan.TempBuffer = null;
	}

	private bool PrecacheResources()
	{
		foreach ( var resource in _resourcesOnHand )
		{
			if ( (resource.Flags & Resource.Precached) != 0 )
				continue;

			switch ( resource.Type )
			{
				case ResourceKind.Sound:
					if ( (resource.Flags & Resource.WasMissing) != 0 )
					{
						_soundPrecache[resource.Index] = null;
						break;
					}

					_soundPrecache[resource.Index] = Sound.PrecacheSound( resource.Name );

					if ( _soundPrecache[resource.Index] is null && (resource.Flags & Resource.FatalIfMissing) != 0 )
					{
						ExplainDisconnection( true, $"Cannot continue without sound {resource.Name}, disconnecting." );
						Disconnect();

						return false;
					}

					break;

				case ResourceKind.Skin:
				case ResourceKind.Generic:
					break;

				case ResourceKind.Model:
					if ( _modelPrecacheCount <= resource.Index )
						_modelPrecacheCount = Math.Min( resource.Index + 1, MaxModels );

					if ( resource.Name[0] == '*' )
						break;

					_modelPrecache[resource.Index] = ModelCache.ForName( resource.Name );

					if ( _modelPrecache[resource.Index] is null )
					{
						if ( resource.Flags != 0 )
							Log.Info( $"Model {resource.Name} not found and not available from server\n" );

						if ( (resource.Flags & Resource.FatalIfMissing) != 0 )
						{
							ExplainDisconnection( true, $"Cannot continue without model {resource.Name}, disconnecting." );
							Disconnect();

							return false;
						}
					}

					break;

				case ResourceKind.Decal:
					if ( (resource.Flags & Resource.Custom) == 0 && resource.Index < MaxDecalNames )
						_decalNames[resource.Index] = resource.Name.Length > MaxDecalNameLength ? resource.Name[..MaxDecalNameLength] : resource.Name;

					break;

				case ResourceKind.EventScript:
					_eventPrecache[resource.Index] = resource.Name;

					if ( !Server.FileExists( resource.Name ) )
					{
						Log.Info( $"Script {resource.Name} not found and not available from server\n" );

						if ( (resource.Flags & Resource.FatalIfMissing) != 0 )
						{
							ExplainDisconnection( true, $"Cannot continue without script {resource.Name}, disconnecting." );
							Disconnect();

							return false;
						}
					}

					break;

				default:
					Log.Trace( "Unknown resource type\n" );
					break;
			}

			resource.Flags |= Resource.Precached;
		}

		return true;
	}

	private void RegisterResources( SizeBuf buffer )
	{
		if ( _customDownload || (_signon == SignOns && _state == ClientState.Active) )
		{
			_customDownload = false;
			return;
		}

		if ( _modelPrecache[1] is not { } world || _maxClients < 1 )
		{
			Log.Info( "Client world model is NULL\n" );

			ExplainDisconnection( true, "Client world model is NULL\n" );
			Disconnect();

			return;
		}

		if ( world.Name.StartsWith( "maps/", StringComparison.OrdinalIgnoreCase ) && world.Name.Contains( ".bsp" ) )
		{
			foreach ( var resource in _resourcesOnHand )
			{
				if ( resource.Type != ResourceKind.Model || resource.Name[0] != '*' )
					continue;

				_modelPrecache[resource.Index] = ModelCache.ForName( resource.Name );

				if ( _modelPrecache[resource.Index] is not null )
					continue;

				Log.Info( $"Model {resource.Name} not found\n" );

				if ( (resource.Flags & Resource.FatalIfMissing) != 0 )
				{
					ExplainDisconnection( true, $"Cannot continue without model {resource.Name}, disconnecting." );
					Disconnect();

					break;
				}
			}
		}

		if ( _state == ClientState.Disconnected )
			return;

		var worldEntity = GetEntity( 0 );

		worldEntity.CurState.ModelIndex = 1;
		worldEntity.PacketState.ModelIndex = 1;

		Log.Trace( "Setting up renderer...\n" );

		ClearParticles();
		ClearBeams();
		ClearDecals();
		InitLevel();
		LoadWaterPalettes();
		CreateLightVolume( world.Name );

		_viewNodeFrames = new int[WorldFile.Nodes.Length];
		_viewLeafFrames = new int[WorldFile.Leafs.Length];
		_viewFrameCount = 0;
		_viewLeaf = -1;

		buffer.WriteByte( Server.StringCommand );
		buffer.WriteString( $"spawn {_serverCount} {Common.Munge2( _mapCrc, (-1 - _serverCount) & 0xFF )}" );
	}
}
