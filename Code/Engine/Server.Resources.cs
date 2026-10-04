using System;
using System.Text;

namespace GoldSrc;

partial class Server
{
	public const int ResourceListMessage = 43;
	public const int ResourceRequestMessage = 45;
	public const int FileTransferFailedMessage = 49;
	public const int ResourceLocationMessage = 56;

	public const int ResourceListCommand = 5;

	private const int MaxDownloadUrlLength = 128;
	private const int MaxFileNameLength = 260;

	private static readonly string[] UnsafeFileNames =
	[
		".cfg", ".rc", ".lst", ".exe", ".vbs", ".com", ".bat", ".dll", ".ini", ".log", "halflife.wad", "pak0.pak", "xeno.wad",
		".so", ".dylib", ".sys", ".asi", ".mix", ".flt"
	];

	[ConVar( "sv_downloadurl" )]
	private static string SvDownloadUrl { get; set; } = "";

	[ConVar( "sv_allowdownload" )]
	private static float SvAllowDownload { get; set; } = 1f;

	[ConVar( "sv_send_resources" )]
	private static float SvSendResources { get; set; } = 1f;

	[ConVar( "sv_allow_dlfile" )]
	private static float SvAllowDlFile { get; set; } = 1f;

	[ConVar( "sv_allowupload", ConVarFlags.Replicated )]
	private static float SvAllowUpload { get; set => Server.SetServerCvar( "sv_allowupload", ref field, value ); } = 1f;

	private readonly List<Resource> _resources = [];

	public static int FileSize( string fileName )
	{
		if ( FileSystem.Data.FileExists( fileName ) )
			return (int)FileSystem.Data.FileSize( fileName );

		if ( LoadMountFile( fileName ) is { } data )
			return data.Length;

		return LoadMountText( fileName ) is { } text ? Encoding.UTF8.GetByteCount( text ) : -1;
	}

	public static bool IsSafeFileToDownload( string fileName )
	{
		if ( fileName is null )
			return false;

		var lower = (fileName.Length > MaxFileNameLength ? fileName[..MaxFileNameLength] : fileName).ToLowerInvariant();

		if ( lower.Contains( ':' ) || lower.Contains( ".." ) || lower.Contains( '~' ) )
			return false;

		if ( lower.Length != 0 && lower[0] is '/' or '\\' )
			return false;

		if ( lower.IndexOf( '.' ) != lower.LastIndexOf( '.' ) )
			return false;

		foreach ( var name in UnsafeFileNames )
		{
			if ( lower.Contains( name, StringComparison.OrdinalIgnoreCase ) )
				return false;
		}

		return true;
	}

	private void AddResource( ResourceKind type, string name, int index, int size, int flags )
	{
		if ( _resources.Count >= Resource.MaxResources )
			throw HostError( "Too many resources on server." );

		_resources.Add( new Resource
		{
			Type = type,
			Name = name.Length > Resource.MaxNameLength ? name[..Resource.MaxNameLength] : name,
			Index = index,
			DownloadSize = size,
			Flags = flags
		} );
	}

	private void CreateResourceList()
	{
		_resources.Clear();

		var sentence = false;

		for ( var i = 1; i < _soundPrecache.Count; i++ )
		{
			var name = _soundPrecache[i];

			if ( name[0] == '!' )
			{
				if ( !sentence )
				{
					sentence = true;

					AddResource( ResourceKind.Sound, "!", i, 0, Resource.FatalIfMissing );
				}

				continue;
			}

			AddResource( ResourceKind.Sound, name, i, MaxClients > 1 ? FileSize( $"sound/{name}" ) : 0, 0 );
		}

		for ( var i = 1; i < _modelNames.Count; i++ )
		{
			var name = _modelNames[i];

			AddResource( ResourceKind.Model, name, i, MaxClients > 1 && name[0] != '*' ? FileSize( name ) : 0, _modelFlags[i] );
		}

		for ( var i = 0; i < DecalWad.Textures.Length; i++ )
			AddResource( ResourceKind.Decal, DecalWad.Textures[i].Name, i, DecalWad.Sizes[i], 0 );

		for ( var i = 1; i < _events.Count; i++ )
			AddResource( ResourceKind.EventScript, _events[i], i, _eventSizes[i], Resource.FatalIfMissing );
	}

	[ConCmd( "sendres" )]
	private static void SendResourcesCommand()
	{
		if ( Cmd.FromClient )
			Current?.SendResources();
	}

	private void SendResources()
	{
		var client = _hostClient;

		if ( client.Spawned && !client.Active )
			return;

		if ( MaxClients > 1 && client.SendResourceCount > 1 )
			return;

		client.SendResourceCount++;

		var message = _connectionMessage;

		message.Name = "SendResources";
		message.Clear();

		message.WriteByte( ResourceRequestMessage );
		message.WriteLong( SpawnCount );
		message.WriteLong( 0 );

		if ( SvDownloadUrl.Length != 0 && SvDownloadUrl.Length < MaxDownloadUrlLength )
		{
			message.WriteByte( ResourceLocationMessage );
			message.WriteString( SvDownloadUrl );
		}

		message.WriteByte( ResourceListMessage );
		message.StartBitWriting();
		message.WriteBits( (uint)_resources.Count, 12 );

		foreach ( var resource in _resources )
		{
			message.WriteBits( (uint)resource.Type, 4 );
			message.WriteBitString( resource.Name );
			message.WriteBits( (uint)resource.Index, 12 );
			message.WriteBits( (uint)resource.DownloadSize, 24 );
			message.WriteBits( (uint)(resource.Flags & (Resource.FatalIfMissing | Resource.WasMissing)), 3 );

			if ( (resource.Flags & Resource.Custom) != 0 )
				message.WriteBitData( resource.Md5, Resource.Md5Size );

			if ( Array.TrueForAll( resource.Reserved, value => value == 0 ) )
			{
				message.WriteBits( 0, 1 );
			}
			else
			{
				message.WriteBits( 1, 1 );
				message.WriteBitData( resource.Reserved, Resource.ReservedSize );
			}
		}

		message.WriteBits( 0, 1 );
		message.EndBitWriting();

		client.Netchan.CreateFragments( message );
		client.Netchan.FragSend();
	}

	private void ParseResourceList( ServerClient client )
	{
		var total = _netMessage.ReadShort();

		if ( total > 0 )
			throw new NotSupportedException( "clc_resourcelist with custom resources is not ported" );

		if ( SvAllowUpload != 0f )
			Log.Trace( "Verifying and uploading resources...\n" );

		client.Uploading = true;
	}

	[ConCmd( "dlfile" )]
	private static void BeginFileDownloadCommand( params string[] arguments )
	{
		if ( arguments.Length < 1 || !Cmd.FromClient )
			return;

		Current?.BeginFileDownload( arguments[0] );
	}

	private void BeginFileDownload( string name )
	{
		var client = _hostClient;

		if ( name.Length == 0 )
			return;

		if ( IsSafeFileToDownload( name ) && SvAllowDownload != 0f )
		{
			if ( name[0] == '!' )
				throw new NotSupportedException( "customization download is not ported" );

			var redirected = SvDownloadUrl.Length != 0 && SvDownloadUrl.Length < MaxDownloadUrlLength && SvAllowDlFile == 0f;

			if ( !client.FullyConnected && SvSendResources != 0f && !redirected && FileExists( name ) )
				throw new NotSupportedException( "file transfer is not ported" );
		}

		client.Message.WriteByte( FileTransferFailedMessage );
		client.Message.WriteString( name );
	}
}
