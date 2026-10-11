using System;
using System.Security.Cryptography;

namespace GoldSrc;

partial class Client
{
	private const string LogoFileName = "tempdecal.wad";
	private const string CustomizationPrefix = "!MD5";
	private const int CustomizationNameLength = 36;

	[ConVar( "cl_allowupload", ConVarFlags.Saved )]
	private static float AllowUpload { get; set; } = 1f;

	private Customization PlayerCustomization( int player )
	{
		return (uint)player < Server.MaxClientsLimit && _players[player] is { Customizations.Count: > 0 } info ? info.Customizations[0] : null;
	}

	private (string Name, int Width, int Height) DecalTexture( int index )
	{
		if ( index >= 0 )
			return DecalWad.Textures[index];

		var player = ~index;

		if ( PlayerCustomization( player ) is not { InUse: true, Info: { } wad, Buffer: { } buffer } customization )
			throw HostError( $"Failed to load custom decal for player #{player}:{GetPlayerInfo( player )?.Name} using default decal 0.\n" );

		return wad.CacheGet( buffer, customization.UserData1 ) is { } image ? ("", image.Width, image.Height) : DecalWad.Textures[0];
	}

	private void ClearCustomizationList( List<Customization> list, bool cleanDecals )
	{
		foreach ( var customization in list )
		{
			if ( cleanDecals && _state == ClientState.Active && customization is { InUse: true, Info: not null, Resource.Type: ResourceKind.Decal } )
				DecalRemoveAll( ~customization.Resource.PlayerNum );
		}

		list.Clear();
	}

	private void ClearCustomizationLists( bool cleanDecals )
	{
		foreach ( var player in _players )
		{
			if ( player is not null )
				ClearCustomizationList( player.Customizations, cleanDecals );
		}
	}

	private void RegisterLogo()
	{
		if ( _state == ClientState.Dedicated )
			return;

		Hpak.FlushHostQueue();

		if ( Server.ReadFile( LogoFileName ) is not { Length: > 0 } data )
			return;

		if ( _customResources.Count >= Resource.MaxResources )
			throw HostError( "Too many resources on client." );

		var resource = new Resource
		{
			Type = ResourceKind.Decal,
			Name = LogoFileName,
			Flags = Resource.Custom,
			DownloadSize = data.Length,
			Md5 = MD5.HashData( data )
		};

		_customResources.Add( resource );

		Hpak.AddLump( false, Hpak.Custom, resource, data );
	}

	private void CreateLocalCustomizations()
	{
		ClearCustomizationLists( true );

		var list = GetPlayerInfo( _playerNum ).Customizations;

		for ( var i = 0; i < _customResources.Count; i++ )
		{
			if ( !Customization.Create( list, _customResources[i], _playerNum, 0, out _ ) )
				Con.DPrintf( $"Problem with client customization {i}, ignoring..." );
		}
	}

	private void ParseCustomization( MessageReader message )
	{
		var player = message.ReadByte();

		if ( player < 0 || player >= Server.MaxClientsLimit )
			throw HostError( "Bogus player index during customization parsing.\n" );

		var resource = new Resource { Type = (ResourceKind)message.ReadByte() };
		var name = message.ReadString();

		resource.Name = name.Length > Resource.MaxNameLength ? name[..Resource.MaxNameLength] : name;
		resource.Index = message.ReadShort();
		resource.DownloadSize = message.ReadLong();

		var flags = message.ReadByte();

		resource.Flags = flags & ~Resource.WasMissing;

		if ( (flags & Resource.Custom) != 0 )
			message.ReadBuf( Resource.Md5Size, resource.Md5 );

		resource.PlayerNum = player;

		if ( AllowDownload == 0f )
		{
			Con.DPrintf( "Refusing new resource, cl_allow_download set to 0\n" );
			return;
		}

		if ( _state == ClientState.Active && DownloadInGame == 0f )
		{
			Con.DPrintf( "Refusing new resource, cl_download_ingame set to 0\n" );
			return;
		}

		var list = GetPlayerInfo( player ).Customizations;

		if ( list.Find( x => x.Resource.Type == resource.Type ) is { } existing )
		{
			if ( _state == ClientState.Active && existing is { InUse: true, Info: not null, Resource.Type: ResourceKind.Decal } )
				DecalRemoveAll( ~player );

			list.Remove( existing );
		}

		var duplicate = list.Exists( x => x.Resource.Md5.AsSpan().SequenceEqual( resource.Md5 ) );

		if ( !Hpak.GetDataPointer( Hpak.Custom, resource ) )
		{
			resource.Flags |= Resource.WasMissing;

			_resourcesNeeded.Add( resource );

			Con.Printf( $"Requesting {resource.Name} from server\n" );

			StartResourceDownloading( "Custom resource propagation...\n", true );
			return;
		}

		if ( duplicate )
			Con.DPrintf( "Duplicate resource ignored for local client\n" );
		else if ( !Customization.Create( list, resource, player, Customization.FromHpak, out _ ) )
			Con.DPrintf( "Error loading customization\n" );
	}

	private void ProcessCustomizationFile( Resource resource )
	{
		if ( _netchan.TempBuffer is not { } data )
			return;

		if ( resource.DownloadSize != data.Length )
		{
			Con.Printf( $"CL_ProcessFile:  Downloaded {data.Length} bytes for purported {resource.DownloadSize} byte file, ignoring download\n" );
			return;
		}

		if ( (resource.Flags & Resource.Custom) == 0 )
			return;

		Hpak.AddLump( true, Hpak.Custom, resource, data );

		var list = GetPlayerInfo( resource.PlayerNum ).Customizations;

		if ( list.Exists( x => x.Resource.Md5.AsSpan().SequenceEqual( resource.Md5 ) ) )
			Con.DPrintf( "Duplicate resource received and ignored.\n" );
		else if ( !Customization.Create( list, resource, resource.PlayerNum, Customization.FromHpak, out _ ) )
			Con.Printf( $"Unable to create custom decal for player {resource.PlayerNum}\n" );
	}

	[ConCmd( "upload" )]
	private static void BeginUploadCommand( params string[] arguments ) => Current?.BeginUpload( arguments.Length > 0 ? arguments[0] : "" );

	private void BeginUpload( string name )
	{
		if ( name.Length == 0 )
		{
			Con.Printf( "Upload without filename\n" );
			return;
		}

		if ( AllowUpload == 0f )
		{
			Con.Printf( "Ingoring decal upload ( cl_allow_upload is 0 )\n" );
			return;
		}

		if ( name.Length != CustomizationNameLength || !name.StartsWith( CustomizationPrefix, StringComparison.OrdinalIgnoreCase ) )
		{
			Con.Printf( "Ingoring upload of non-customization\n" );
			return;
		}

		var hash = Hpak.HexConvert( name[CustomizationPrefix.Length..], Resource.Md5Size * 2 );

		byte[] data = null;

		if ( Hpak.ResourceForHash( Hpak.Custom, hash, out var resource ) )
		{
			if ( !hash.AsSpan().SequenceEqual( resource.Md5 ) )
			{
				Con.DPrintf( "Bogus data retrieved from custom.hpk, attempting to delete entry\n" );

				Hpak.RemoveLump( name, resource );
				return;
			}

			if ( Hpak.GetDataPointer( Hpak.Custom, resource, out data ) )
			{
				var actual = MD5.HashData( data );

				if ( !actual.AsSpan().SequenceEqual( resource.Md5 ) )
				{
					Con.Printf( "HPAK_AddLump called with bogus lump, md5 mismatch\n" );
					Con.Printf( $"Purported:  {Hpak.Md5Print( resource.Md5 )}\n" );
					Con.Printf( $"Actual   :  {Hpak.Md5Print( actual )}\n" );
					Con.Printf( "Removing conflicting lump\n" );

					Hpak.RemoveLump( name, resource );
					return;
				}
			}
		}

		if ( data is not { Length: > 0 } )
		{
			Con.Printf( "Ingoring customization upload, couldn't find decal locally\n" );
			return;
		}

		_netchan.CreateFileFragmentsFromBuffer( name, data );
		_netchan.FragSend();
	}

	private Texture CustomDecalTexture( int player )
	{
		if ( PlayerCustomization( player ) is not { Info: { } wad, Buffer: { } buffer } customization || wad.CacheGet( buffer, customization.UserData1 ) is not { } image )
			return Texture.Load( Server.GetMountPath( "decals/0.vtex" ) );

		return image.Texture ??= CreateCustomDecalTexture( image );
	}

	private static Texture CreateCustomDecalTexture( CustomDecalImage image )
	{
		var count = image.Width * image.Height;
		var last = (image.Palette.Length / 3 - 1) * 3;
		var pixels = new byte[count * 4];

		Span<byte> table = stackalloc byte[256];

		for ( var i = 0; i < table.Length; i++ )
			table[i] = (byte)Math.Clamp( (int)(Math.Pow( i / 255.0, _uploadTexGamma / _uploadGamma ) * 255.0), 0, 255 );

		for ( var i = 0; i < count; i++ )
		{
			var index = image.Pixels[i];

			if ( image.AlphaTest && index == 255 )
				continue;

			var color = image.AlphaTest ? index * 3 : last;

			pixels[(i * 4) + 0] = table[image.Palette[color + 0]];
			pixels[(i * 4) + 1] = table[image.Palette[color + 1]];
			pixels[(i * 4) + 2] = table[image.Palette[color + 2]];
			pixels[(i * 4) + 3] = image.AlphaTest ? (byte)255 : index;
		}

		BlendTransparentPixels( pixels, image.Width, image.Height );

		var levels = new List<byte[]> { pixels };
		var size = pixels.Length;
		var width = image.Width;
		var height = image.Height;

		while ( width > 1 || height > 1 )
		{
			var level = MipMap( levels[^1], width, height );

			width = Math.Max( 1, width >> 1 );
			height = Math.Max( 1, height >> 1 );
			size += level.Length;

			levels.Add( level );
		}

		var data = new byte[size];
		var position = 0;

		for ( var i = levels.Count - 1; i >= 0; i-- )
		{
			levels[i].CopyTo( data, position );

			position += levels[i].Length;
		}

		return Texture.Create( image.Width, image.Height ).WithData( data ).WithMips( levels.Count ).Finish();
	}

	private static byte[] MipMap( byte[] source, int width, int height )
	{
		var stride = width * 4;
		var rows = height >> 1;
		var result = new byte[Math.Max( 1, width >> 1 ) * Math.Max( 1, height >> 1 ) * 4];
		var input = 0;
		var output = 0;

		for ( var i = 0; i < rows; i++, input += stride )
		{
			for ( var j = 0; j < stride; j += 8, output += 4, input += 8 )
			{
				for ( var channel = 0; channel < 4; channel++ )
					result[output + channel] = (byte)((source[input + channel] + source[input + 4 + channel] + source[input + stride + channel] + source[input + stride + 4 + channel]) >> 2);
			}
		}

		return result;
	}

	private static void BlendTransparentPixels( byte[] pixels, int width, int height )
	{
		for ( var y = 0; y < height; y++ )
		{
			for ( var x = 0; x < width; x++ )
			{
				var pixel = ((y * width) + x) * 4;

				if ( BitConverter.ToUInt32( pixels, pixel ) != 0 )
					continue;

				var red = 0;
				var green = 0;
				var blue = 0;
				var count = 0;

				for ( var i = x - 1; i <= x + 1; i++ )
				{
					for ( var j = y - 1; j <= y + 1; j++ )
					{
						if ( i < 0 || i >= width || j < 0 || j >= height )
							continue;

						var source = ((j * width) + i) * 4;

						if ( pixels[source + 3] == 0 )
							continue;

						red += pixels[source + 0];
						green += pixels[source + 1];
						blue += pixels[source + 2];
						count++;
					}
				}

				if ( count == 0 )
					count = 1;

				pixels[pixel + 0] = (byte)(red / count);
				pixels[pixel + 1] = (byte)(green / count);
				pixels[pixel + 2] = (byte)(blue / count);
			}
		}
	}
}
