using System;

namespace GoldSrc;

partial class Server
{
	public const int CustomizationMessage = 46;

	private const string CustomizationPrefix = "!MD5";
	private const int CustomizationNameLength = 36;
	private const int LastResourceKind = 6;
	private const float UnlimitedUploadSize = 1073741824f;
	private const float Megabyte = 1048576f;

	[ConVar( "sv_send_logos" )]
	private static float SvSendLogos { get; set; } = 1f;

	private static void ClearResourceLists( ServerClient client )
	{
		client.ResourcesNeeded.Clear();
		client.ResourcesOnHand.Clear();
	}

	public void ClearClientStates()
	{
		foreach ( var client in Clients )
		{
			client.Customizations.Clear();

			ClearResourceLists( client );
		}
	}

	private static void MoveToOnHandList( ServerClient client, Resource resource )
	{
		client.ResourcesNeeded.Remove( resource );
		client.ResourcesOnHand.Add( resource );
	}

	private void ParseResourceList( ServerClient client )
	{
		var message = _netMessage;
		var total = message.ReadShort();

		if ( SvUploadMaxCount > 0f )
			total = Math.Clamp( total, 0, (int)SvUploadMaxCount );

		ClearResourceLists( client );

		var maxSize = (int)(SvUploadMax <= 0f ? UnlimitedUploadSize : SvUploadMax * Megabyte);

		for ( var i = 0; i < total; i++ )
		{
			var resource = new Resource();
			var name = message.ReadString();

			resource.Name = name.Length > Resource.MaxNameLength ? name[..Resource.MaxNameLength] : name;
			resource.Type = (ResourceKind)message.ReadByte();
			resource.Index = message.ReadShort();
			resource.DownloadSize = message.ReadLong();

			var flags = message.ReadByte();

			resource.Flags = flags & ~Resource.WasMissing;

			if ( (flags & Resource.Custom) != 0 )
				message.ReadBuf( Resource.Md5Size, resource.Md5 );

			client.ResourcesNeeded.Add( resource );

			if ( message.BadRead || (int)resource.Type > LastResourceKind || resource.DownloadSize < 0 || resource.DownloadSize > maxSize )
			{
				ClearResourceLists( client );
				return;
			}
		}

		if ( SvAllowUpload != 0f )
			Log.Trace( "Verifying and uploading resources...\n" );

		Span<int> sizes = stackalloc int[LastResourceKind + 1];

		var totalSize = 0;

		foreach ( var resource in client.ResourcesNeeded )
		{
			totalSize += resource.DownloadSize;
			sizes[resource.Type == ResourceKind.Model && resource.Index == 1 ? (int)ResourceKind.World : (int)resource.Type] += resource.DownloadSize;
		}

		if ( totalSize != 0 )
		{
			Log.Trace( $"Custom resources total {totalSize / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.Model] != 0 )
				Log.Trace( $"  Models:  {sizes[(int)ResourceKind.Model] / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.Sound] != 0 )
				Log.Trace( $"  Sounds:  {sizes[(int)ResourceKind.Sound] / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.Decal] != 0 )
				Log.Trace( $"  Decals:  {sizes[(int)ResourceKind.Decal] / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.Skin] != 0 )
				Log.Trace( $"  Skins :  {sizes[(int)ResourceKind.Skin] / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.Generic] != 0 )
				Log.Trace( $"  Generic :  {sizes[(int)ResourceKind.Generic] / 1024.0:F2}K\n" );

			if ( sizes[(int)ResourceKind.EventScript] != 0 )
				Log.Trace( $"  Events  :  {sizes[(int)ResourceKind.EventScript] / 1024.0:F2}K\n" );

			Log.Trace( "----------------------\n" );

			var request = 0;

			foreach ( var resource in client.ResourcesNeeded )
			{
				if ( resource.Type != ResourceKind.Decal || Hpak.ResourceForHash( Hpak.Custom, resource.Md5, out _ ) || resource.DownloadSize == 0 )
					continue;

				resource.Flags |= Resource.WasMissing;
				request += resource.DownloadSize;
			}

			if ( request > maxSize )
			{
				ClearResourceLists( client );
				return;
			}

			Log.Trace( request > 1024 ? $"Resources to request: {request / 1024f:F2}K\n" : $"Resources to request: {request} bytes\n" );
		}

		client.Uploading = true;
		client.UploadDoneRegistering = false;

		BatchUploadRequest( client );
	}

	private void BatchUploadRequest( ServerClient client )
	{
		foreach ( var resource in client.ResourcesNeeded.ToArray() )
		{
			if ( (resource.Flags & Resource.WasMissing) == 0 )
			{
				MoveToOnHandList( client, resource );
				continue;
			}

			if ( resource.Type != ResourceKind.Decal )
				continue;

			if ( (resource.Flags & Resource.Custom) == 0 )
			{
				Log.Info( "Non customization in upload queue!\n" );

				MoveToOnHandList( client, resource );
				continue;
			}

			if ( Hpak.GetDataPointer( Hpak.Custom, resource ) || SvAllowUpload == 0f )
			{
				MoveToOnHandList( client, resource );
				continue;
			}

			client.Message.WriteByte( StuffTextMessage );
			client.Message.WriteString( $"upload \"{CustomizationPrefix}{Hpak.Md5Print( resource.Md5 )}\"\n" );
		}
	}

	private void ProcessFile( ServerClient client, string name )
	{
		if ( name[0] != '!' )
		{
			Log.Info( $"Ignoring non-customization file upload of {name}\n" );
			return;
		}

		var hash = Hpak.HexConvert( name[CustomizationPrefix.Length..], Resource.Md5Size * 2 );
		var data = client.Netchan.TempBuffer;

		foreach ( var resource in client.ResourcesNeeded )
		{
			if ( !resource.Md5.AsSpan().SequenceEqual( hash ) )
				continue;

			if ( resource.DownloadSize != data.Length )
			{
				Log.Info( $"SV_ProcessFile:  Downloaded {data.Length} bytes for purported {resource.DownloadSize} byte file\n" );
				return;
			}

			Hpak.AddLump( true, Hpak.Custom, resource, data );

			resource.Flags &= ~Resource.WasMissing;

			MoveToOnHandList( client, resource );

			if ( client.Customizations.Exists( x => x.Resource.Md5.AsSpan().SequenceEqual( resource.Md5 ) ) )
				Log.Trace( "Duplicate resource received and ignored.\n" );
			else if ( !Customization.Create( client.Customizations, resource, -1, Customization.FromHpak | Customization.WadOnly | Customization.IgnoreInit, out _ ) )
				Log.Info( $"Error parsing custom decal from {client.Name}\n" );

			return;
		}

		Log.Info( "SV_ProcessFile:  Unrequested decal\n" );
	}

	private void CreateCustomizationList( ServerClient client )
	{
		client.Customizations.Clear();

		foreach ( var resource in client.ResourcesOnHand )
		{
			if ( client.Customizations.Exists( x => x.Resource.Md5.AsSpan().SequenceEqual( resource.Md5 ) ) )
			{
				Log.Trace( $"SV_CreateCustomization list, ignoring dup. resource for player {client.Name}\n" );
				continue;
			}

			if ( !Customization.Create( client.Customizations, resource, -1, Customization.FromHpak | Customization.WadOnly, out var customization ) )
			{
				Log.Info( SvAllowUpload != 0f ? $"Ignoring invalid custom decal from {client.Name}\n" : $"Ignoring custom decal from {client.Name}\n" );
				continue;
			}

			GameDll.PlayerCustomization( GetEdict( client.EdictIndex ), customization );
		}
	}

	private static void WriteCustomization( SizeBuf message, int slot, Resource resource )
	{
		message.WriteByte( CustomizationMessage );
		message.WriteByte( slot );
		message.WriteByte( (int)resource.Type );
		message.WriteString( resource.Name );
		message.WriteShort( resource.Index );
		message.WriteLong( resource.DownloadSize );
		message.WriteByte( resource.Flags );

		if ( (resource.Flags & Resource.Custom) != 0 )
			message.Write( resource.Md5, Resource.Md5Size );
	}

	private void RegisterResources( ServerClient client )
	{
		client.Uploading = false;

		foreach ( var resource in client.ResourcesOnHand )
		{
			CreateCustomizationList( client );

			foreach ( var other in Clients )
			{
				if ( (other.Active || other.Spawned) && !other.FakeClient && other != client )
					WriteCustomization( other.Message, client.Slot, resource );
			}
		}
	}

	private void PropagateCustomizations( ServerClient client )
	{
		foreach ( var other in Clients )
		{
			if ( (!other.Active && !other.Spawned) || other.FakeClient )
				continue;

			foreach ( var customization in other.Customizations )
			{
				if ( customization.InUse )
					WriteCustomization( client.Message, other.Slot, customization.Resource );
			}
		}
	}

	private void RegisterUploads()
	{
		foreach ( var client in Clients )
		{
			if ( (!client.Active && !client.Spawned) || !client.Uploading || client.UploadDoneRegistering || client.ResourcesNeeded.Count != 0 )
				continue;

			_hostClient = client;

			RegisterResources( client );
			PropagateCustomizations( client );

			if ( SvAllowUpload != 0f )
				Log.Trace( "Custom resource propagation complete.\n" );

			client.UploadDoneRegistering = true;
		}
	}

	private bool BeginCustomizationDownload( ServerClient client, string name )
	{
		if ( name.Length != CustomizationNameLength || !name.StartsWith( CustomizationPrefix, StringComparison.OrdinalIgnoreCase ) || SvSendLogos == 0f )
			return false;

		var hash = Hpak.HexConvert( name[CustomizationPrefix.Length..], Resource.Md5Size * 2 );

		if ( !Hpak.ResourceForHash( Hpak.Custom, hash, out var resource ) )
			return true;

		Hpak.GetDataPointer( Hpak.Custom, resource, out var data );

		if ( data is not { Length: > 0 } )
			return true;

		client.Netchan.CreateFileFragmentsFromBuffer( name, data );
		client.Netchan.FragSend();

		return true;
	}
}
