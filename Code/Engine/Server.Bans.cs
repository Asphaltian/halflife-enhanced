using System;
using System.Globalization;
using System.Text;

namespace GoldSrc;

partial class Server
{
	private const int MaxUserFilters = 32768;
	private const int MaxIdStringLength = 63;
	private const int IdTypeSteam = 1;
	private const int IdTypeValve = 2;
	private const ulong IndividualAccountBase = 0x0110000100000000;

	[ConVar( "sv_filterban" )]
	private static float SvFilterBan { get; set; } = 1f;

	[ConVar( "sv_logbans" )]
	private static float SvLogBans { get; set; }

	[ConVar( "bannedcfgfile" )]
	private static string BannedConfigFile { get; set; } = "banned.cfg";

	private struct UserFilter
	{
		public int IdType;
		public ulong SteamId;
		public float BanEndTime;
		public float BanTime;
	}

	private static List<UserFilter> _userFilters = [];

	private static string FilterIdString( int idType, ulong steamId )
	{
		var id = GetIdString( steamId );

		return idType == IdTypeValve && id.StartsWith( "STEAM_", StringComparison.Ordinal ) ? $"VALVE_{id[6..]}" : id;
	}

	private static bool CompareUserId( int idType1, ulong steamId1, int idType2, ulong steamId2 )
	{
		if ( idType1 != idType2 || idType1 is not (IdTypeSteam or IdTypeValve) )
			return false;

		return FilterIdString( idType1, steamId1 ).Equals( FilterIdString( idType2, steamId2 ), StringComparison.OrdinalIgnoreCase );
	}

	private static ulong SteamIdFromString( string text )
	{
		var parts = text.Split( ':' );

		if ( parts.Length != 3 )
			return 0;

		var account = ((uint)Common.Atoi( parts[2] ) << 1) | ((uint)Common.Atoi( parts[1] ) & 1);

		return IndividualAccountBase | account;
	}

	private static string ClampIdString( string text )
	{
		return text.Length > MaxIdStringLength ? text[..MaxIdStringLength] : text;
	}

	private static bool IsSteamIdString( string text )
	{
		return text.StartsWith( "STEAM_", StringComparison.OrdinalIgnoreCase ) || text.StartsWith( "VALVE_", StringComparison.OrdinalIgnoreCase );
	}

	private bool FilterUser( ulong steamId )
	{
		for ( var i = _userFilters.Count - 1; i >= 0; i-- )
		{
			var filter = _userFilters[i];

			if ( filter.BanEndTime == 0f || Host.RealTime < filter.BanEndTime )
			{
				if ( CompareUserId( IdTypeSteam, steamId, filter.IdType, filter.SteamId ) )
					return (int)SvFilterBan != 0;
			}
			else
			{
				_userFilters.RemoveAt( i );
			}
		}

		return SvFilterBan == 0f;
	}

	private static string CommandIssuer( Server server )
	{
		return Cmd.FromClient ? server.HostClient.Name : Application.IsDedicatedServer ? "Console" : Client.PlayerName;
	}

	[ConCmd( "banid" )]
	private static void BanId( params string[] arguments )
	{
		Cmd.SetArguments( "banid", arguments );

		if ( Cmd.Argc < 3 || Cmd.Argc > 8 )
		{
			Log.Info( "Usage:  banid <minutes> <uniqueid or #userid> { kick }\n" );
			Log.Info( "Use 0 minutes for permanent\n" );
			return;
		}

		var server = Current;
		var banTime = Common.Atof( Cmd.Argv( 1 ) );

		if ( banTime < 0.01 )
			banTime = 0f;

		var idString = ClampIdString( Cmd.Argv( 2 ) );
		var kick = Cmd.Argc >= 4 && Cmd.Argv( Cmd.Argc - 1 ).Equals( "kick", StringComparison.OrdinalIgnoreCase );
		var idType = 0;
		var steamId = 0ul;

		if ( idString.StartsWith( '#' ) )
		{
			string userIdText;

			if ( idString.Length == 1 )
			{
				if ( Cmd.Argc < 3 )
				{
					Log.Info( "Insufficient arguments to banid\n" );
					return;
				}

				userIdText = Cmd.Argv( 3 );
			}
			else
			{
				userIdText = idString[1..];
			}

			var userId = Common.Atoi( userIdText );
			ServerClient found = null;

			if ( server is not null )
			{
				foreach ( var client in server.Clients )
				{
					if ( (client.Active || client.Connected || client.Spawned) && !client.FakeClient && client.UserId == userId )
					{
						found = client;
						break;
					}
				}
			}

			if ( found is null )
			{
				Log.Info( $"SV_BanId_f:  Couldn't find #userid {(uint)userId}\n" );
				return;
			}

			idType = IdTypeSteam;
			steamId = found.SteamId;
		}
		else
		{
			if ( IsSteamIdString( idString ) )
				idString = ClampIdString( $"{Cmd.Argv( 2 )}:{Cmd.Argv( 4 )}:{Cmd.Argv( 6 )}" );

			ServerClient found = null;

			if ( server is not null )
			{
				foreach ( var client in server.Clients )
				{
					if ( (client.Active || client.Connected || client.Spawned) && !client.FakeClient && server.GetClientIdString( client ).Equals( idString, StringComparison.OrdinalIgnoreCase ) )
					{
						found = client;
						break;
					}
				}
			}

			if ( found is not null )
			{
				idType = IdTypeSteam;
				steamId = found.SteamId;
			}
			else if ( idString.Length != 0 )
			{
				idType = idString.StartsWith( "STEAM_", StringComparison.OrdinalIgnoreCase ) ? IdTypeSteam : IdTypeValve;
				steamId = SteamIdFromString( idString.Length > 6 ? idString[6..] : "" );
			}
		}

		var index = _userFilters.FindIndex( filter => CompareUserId( filter.IdType, filter.SteamId, idType, steamId ) );

		if ( index < 0 )
		{
			if ( _userFilters.Count >= MaxUserFilters )
			{
				Log.Info( "SV_BanId_f:  User filter list is full\n" );
				return;
			}

			index = _userFilters.Count;

			_userFilters.Add( default );
		}

		_userFilters[index] = new UserFilter
		{
			IdType = idType,
			SteamId = steamId,
			BanTime = banTime,
			BanEndTime = banTime == 0f ? 0f : (float)((banTime * 60.0) + Host.RealTime)
		};

		var duration = banTime == 0f ? "permanently" : $"for {banTime.ToString( "F2", CultureInfo.InvariantCulture )} minutes";
		var who = CommandIssuer( server );

		if ( !kick )
		{
			if ( SvLogBans != 0f )
				LogPrintf( $"Ban: \"<><{FilterIdString( idType, steamId )}><>\" was banned \"{duration}\" by \"{who}\"\n" );

			return;
		}

		if ( server is null )
			return;

		foreach ( var client in server.Clients )
		{
			if ( (!client.Active && !client.Connected && !client.Spawned) || client.FakeClient )
				continue;

			if ( !CompareUserId( IdTypeSteam, client.SteamId, idType, steamId ) )
				continue;

			if ( SvLogBans != 0f )
				LogPrintf( $"Ban: \"{client.Name}<{client.UserId}><{server.GetClientIdString( client )}><>\" was kicked and banned \"{duration}\" by \"{who}\"\n" );

			server.ClientPrintf( client, $"You have been kicked and banned {duration} by the server op.\n" );
			server.DropClient( client, false, "Kicked and banned" );

			return;
		}
	}

	[ConCmd( "removeid" )]
	private static void RemoveId( params string[] arguments )
	{
		Cmd.SetArguments( "removeid", arguments );

		if ( Cmd.Argc != 2 && Cmd.Argc != 6 )
		{
			Log.Info( "Usage:  removeid <uniqueid | #slotnumber>\n" );
			return;
		}

		var idString = ClampIdString( Cmd.Argv( 1 ) );

		if ( idString.Length == 0 )
		{
			Log.Info( "SV_RemoveId_f:  Id string is empty!\n" );
			return;
		}

		if ( idString[0] == '#' )
		{
			var slot = Common.Atoi( idString[1..] );

			if ( slot <= 0 || slot > _userFilters.Count )
			{
				Log.Info( $"SV_RemoveId_f:  invalid slot #{slot}\n" );
				return;
			}

			var removed = _userFilters[slot - 1];

			_userFilters.RemoveAt( slot - 1 );

			Log.Info( $"UserID filter removed for {idString}, id {FilterIdString( removed.IdType, removed.SteamId )}\n" );

			return;
		}

		if ( IsSteamIdString( idString ) )
			idString = ClampIdString( $"{Cmd.Argv( 1 )}:{Cmd.Argv( 3 )}:{Cmd.Argv( 5 )}" );

		for ( var i = 0; i < _userFilters.Count; i++ )
		{
			if ( !FilterIdString( _userFilters[i].IdType, _userFilters[i].SteamId ).Equals( idString, StringComparison.OrdinalIgnoreCase ) )
				continue;

			_userFilters.RemoveAt( i );

			Log.Info( $"UserID filter removed for {idString}\n" );

			return;
		}

		Log.Info( $"removeid: couldn't find {idString}\n" );
	}

	[ConCmd( "listid" )]
	private static void ListId()
	{
		if ( _userFilters.Count < 1 )
		{
			Log.Info( "UserID filter list: empty\n" );
			return;
		}

		Log.Info( $"UserID filter list: {_userFilters.Count} entries\n" );

		for ( var i = 0; i < _userFilters.Count; i++ )
		{
			var filter = _userFilters[i];
			var id = FilterIdString( filter.IdType, filter.SteamId );

			if ( filter.BanTime == 0f )
				Log.Info( $"{i + 1} {id} : permanent\n" );
			else
				Log.Info( $"{i + 1} {id} : {filter.BanTime.ToString( "F3", CultureInfo.InvariantCulture )} min\n" );
		}
	}

	[ConCmd( "writeid" )]
	private static void WriteId()
	{
		var name = BannedConfigFile;

		if ( string.IsNullOrEmpty( name ) )
		{
			Log.Info( "Couldn't open banned config file, no file name specified.\n" );
			return;
		}

		if ( name[0] == '/' || name.Contains( ':' ) || name.Contains( ".." ) || name.Contains( '\\' ) )
		{
			Log.Info( $"Couldn't open {name} (contains illegal characters).\n" );
			return;
		}

		if ( name.Length > 255 )
			name = name[..255];

		if ( System.IO.Path.GetExtension( name ).Length == 0 )
			name += ".cfg";

		if ( System.IO.Path.GetExtension( name ) != ".cfg" )
		{
			Log.Info( $"Couldn't open {name} (wrong file extension, must be .cfg).\n" );
			return;
		}

		Log.Info( $"Writing {name}.\n" );

		var text = new StringBuilder();

		foreach ( var filter in _userFilters )
		{
			if ( filter.BanTime == 0f )
				text.Append( $"banid 0.0 {FilterIdString( filter.IdType, filter.SteamId )}\n" );
		}

		WriteFile( name, Encoding.Latin1.GetBytes( text.ToString() ) );
	}
}
