using System;
using System.Text;

namespace GoldSrc;

partial class Host
{
	private const int BspVersion = 30;
	private const int MaxMapPathLength = 64;
	private const string StatusLogFile = "status.log";
	private const int GameAppId = 70;

	private const int CvarArchive = 1;
	private const int CvarUserInfo = 2;
	private const int CvarServer = 4;

	public static void Version()
	{
		Con.Printf( $"Protocol version {ProtocolVersion}\nExe version {VersionString()} ({ProductName()})\n" );
		Con.Printf( $"Exe build: 19:06:31 {BuildDate} ({BuildNumber()})\n" );
	}

	[ConCmd( "maps" )]
	private static void Maps( params string[] arguments )
	{
		if ( arguments.Length != 1 )
		{
			Con.Printf( "Usage:  maps <substring>\nmaps * for full listing\n" );
			return;
		}

		var substring = arguments[0];

		if ( substring.Length == 0 )
			return;

		ListMaps( substring[0] == '*' ? null : substring );
	}

	private static void ListMaps( string substring )
	{
		Con.Printf( "-------------\n" );

		for ( var showOutdated = 1; showOutdated >= 0; showOutdated-- )
		{
			foreach ( var name in Server.FindFiles( "maps", ".bsp" ) )
			{
				if ( !string.IsNullOrEmpty( substring ) && string.Compare( name, 0, substring, 0, substring.Length, StringComparison.OrdinalIgnoreCase ) != 0 )
					continue;

				var path = $"maps/{name}";

				if ( path.Length >= MaxMapPathLength )
				{
					Con.Printf( $"Map name too long: {name}\n" );
					continue;
				}

				var version = 0;

				if ( Server.LoadMountFile( path ) is { Length: >= 4 } data )
					version = BitConverter.ToInt32( data, 0 );
				else
					Con.Printf( $"Could not open map {path}\n" );

				if ( version == BspVersion )
				{
					if ( showOutdated == 0 )
						Con.Printf( $"{name}\n" );
				}
				else if ( showOutdated != 0 )
				{
					Con.Printf( $"OUTDATED:  {name}\n" );
				}
			}
		}
	}

	[ConCmd( "users" )]
	private static void Users()
	{
		if ( Server.Current is not { Active: true } server )
		{
			Con.Printf( "Can't 'users', not running a server\n" );
			return;
		}

		var count = 0;

		Con.Printf( "userid : uniqueid : name\n" );
		Con.Printf( "------ : ---------: ----\n" );

		foreach ( var client in server.Clients )
		{
			if ( (!client.Active && !client.Spawned && !client.Connected) || client.FakeClient || client.Name.Length == 0 )
				continue;

			Con.Printf( $"{client.UserId,6} : {server.GetClientIdString( client )} : {client.Name}\n" );

			count++;
		}

		Con.Printf( $"{count} users\n" );
	}

	[ConCmd( "user" )]
	private static void User( params string[] arguments )
	{
		if ( Server.Current is not { Active: true } server )
		{
			Con.Printf( "Can't 'user', not running a server\n" );
			return;
		}

		if ( arguments.Length != 1 )
		{
			Con.Printf( "Usage: user <username / userid>\n" );
			return;
		}

		var userId = Common.Atoi( arguments[0] );

		foreach ( var client in server.Clients )
		{
			if ( (!client.Active && !client.Spawned && !client.Connected) || client.FakeClient || client.Name.Length == 0 )
				continue;

			if ( client.UserId == userId || client.Name == arguments[0] )
			{
				Info.Print( client.UserInfo );
				return;
			}
		}

		Con.Printf( "User not in server.\n" );
	}

	private static void StatusPrintf( Server server, bool conPrint, bool log, string text )
	{
		if ( !conPrint )
		{
			server.ClientPrintf( text );
		}
		else
		{
			Con.Printf( text );
		}

		if ( log )
			Server.AppendFile( StatusLogFile, text );
	}

	private static bool StatusBegin( string name, string[] arguments, out Server server, out bool conPrint, out bool log )
	{
		server = Server.Current;
		conPrint = !Cmd.FromClient;
		log = false;

		if ( conPrint && server is not { Active: true } )
		{
			Cmd.ForwardToServer( name, arguments );
			return false;
		}

		if ( arguments.Length == 1 && arguments[0].Equals( "log", StringComparison.OrdinalIgnoreCase ) )
		{
			Server.RemoveFile( StatusLogFile );

			log = true;
		}

		return true;
	}

	private static int StatusMap( Server server, bool conPrint, bool log )
	{
		var origin = Client.Current?.ViewOrigin ?? default;

		StatusPrintf( server, conPrint, log, $"map     :  {server.Name} at: {(int)origin.x} x, {(int)origin.y} y, {(int)origin.z} z\n" );

		var players = server.CountPlayers();

		StatusPrintf( server, conPrint, log, $"players :  {players} active ({server.MaxClients} max)\n\n" );

		return players;
	}

	public static void Status( params string[] arguments )
	{
		if ( !StatusBegin( "status", arguments, out var server, out var conPrint, out var log ) )
			return;

		StatusPrintf( server, conPrint, log, $"hostname:  {Cvar.GetString( "hostname" )}\n" );
		StatusPrintf( server, conPrint, log, $"version :  {ProtocolVersion}/{VersionString()} {BuildNumber()} insecure  ({GameAppId})\n" );

		var players = StatusMap( server, conPrint, log );

		StatusPrintf( server, conPrint, log, "#      name userid uniqueid frag time ping loss adr\n" );

		var spectators = 0;
		var slots = 0;
		var delay = 0;

		for ( var i = 0; i < server.MaxClients; i++ )
		{
			var client = server.Clients[i];

			if ( !client.Active )
				continue;

			var seconds = (int)(RealTime - client.Netchan.ConnectTime);
			var minutes = seconds / 60;
			var hours = 0;

			if ( minutes != 0 )
			{
				seconds %= 60;
				hours = minutes / 60;

				if ( hours != 0 )
					minutes %= 60;
			}

			var id = client.FakeClient ? "BOT" : server.GetClientIdString( client );
			var quotedName = $"\"{client.Name}\"";

			StatusPrintf( server, conPrint, log, $"#{i + 1,2} {quotedName,8} {client.UserId} {id}" );

			if ( !client.Proxy )
			{
				StatusPrintf( server, conPrint, log, $" {(int)server.GetEdict( client.EdictIndex ).Frags,3}" );
			}
			else
			{
				if ( Info.ValueForKey( client.UserInfo, "hspecs" ) is { Length: > 0 } hspecs )
					spectators = Common.Atoi( hspecs );

				if ( Info.ValueForKey( client.UserInfo, "hslots" ) is { Length: > 0 } hslots )
					slots = Common.Atoi( hslots );

				if ( Info.ValueForKey( client.UserInfo, "hdelay" ) is { Length: > 0 } hdelay )
					delay = Common.Atoi( hdelay );

				StatusPrintf( server, conPrint, log, $" hltv:{(uint)spectators}/{(uint)slots} delay:{(uint)delay}" );
			}

			if ( hours == 0 )
				StatusPrintf( server, conPrint, log, $" {minutes:D2}:{seconds:D2}" );
			else
				StatusPrintf( server, conPrint, log, $" {hours,2}:{minutes:D2}:{seconds:D2}" );

			if ( !client.Proxy )
				StatusPrintf( server, conPrint, log, $" {server.CalcPing( client ),4}  {(int)client.PacketLoss,3}" );

			if ( (!conPrint && !client.Proxy) || client.Loopback )
				StatusPrintf( server, conPrint, log, "\n" );
			else
				StatusPrintf( server, conPrint, log, $" {client.Netchan.Address}\n" );
		}

		StatusPrintf( server, conPrint, log, $"{players} users\n" );
	}

	private static string StatusColumn( string text, int width )
	{
		return (text.Length > width ? text[..width] : text).PadRight( width );
	}

	private static string StatusRow( string index, string name, string userId, string uniqueId, string frag, string time, string ping, string loss, string address )
	{
		return $"{StatusColumn( index, 2 )}\t{StatusColumn( name, 9 )}\t{StatusColumn( userId, 7 )}\t{StatusColumn( uniqueId, 20 )}\t{StatusColumn( frag, 4 )}\t{StatusColumn( time, 8 )}\t{StatusColumn( ping, 4 )}\t{StatusColumn( loss, 4 )}\t{StatusColumn( address, 21 )}\n";
	}

	[ConCmd( "stat" )]
	private static void Stat( params string[] arguments )
	{
		if ( !StatusBegin( "stat", arguments, out var server, out var conPrint, out var log ) )
			return;

		StatusPrintf( server, conPrint, log, $"hostname:  {Cvar.GetString( "hostname" )}\n" );
		StatusPrintf( server, conPrint, log, $"version :  {ProtocolVersion}/{VersionString()} {BuildNumber()} insecure\n" );

		var players = StatusMap( server, conPrint, log );

		StatusPrintf( server, conPrint, log, StatusRow( "# ", "name    ", "userid   ", "uniqueid ", "frag", "time    ", "ping", "loss", "adr" ) );

		for ( var i = 0; i < server.MaxClients; i++ )
		{
			var client = server.Clients[i];

			if ( !client.Active )
				continue;

			var seconds = (int)(RealTime - client.Netchan.ConnectTime);
			var minutes = seconds / 60;
			string time;

			if ( minutes == 0 )
				time = $"{minutes:D2}:{seconds:D2}";
			else if ( minutes / 60 == 0 )
				time = $"{minutes:D2}:{seconds % 60:D2}";
			else
				time = $"{minutes / 60,-2}:{minutes % 60:D2}:{seconds % 60:D2}";

			StatusPrintf( server, conPrint, log, StatusRow(
				$"{i + 1,-2}",
				$"\"{client.Name}\"",
				$"{client.UserId,-7}",
				server.GetClientIdString( client ),
				$"{(int)server.GetEdict( client.EdictIndex ).Frags,-4}",
				time,
				$"{server.CalcPing( client ),-4}",
				$"{(int)client.PacketLoss,-4}",
				conPrint ? client.Netchan.Address : "" ) );
		}

		StatusPrintf( server, conPrint, log, $"{players} users\n" );
	}

	[ConCmd( "ping" )]
	private static void Ping()
	{
		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer( "ping", [] );
			return;
		}

		var server = Server.Current;

		server.ClientPrintf( "Client ping times:\n" );

		foreach ( var client in server.Clients )
		{
			if ( client.Active )
				server.ClientPrintf( $"{server.CalcPing( client ),4} {client.Name}\n" );
		}
	}

	public static void Kick()
	{
		if ( Cmd.Argc < 2 )
		{
			Con.Printf( "usage:  kick < name > | < # userid >\n" );
			return;
		}

		var server = Server.Current;
		var caller = server?.HostClient;

		if ( !Cmd.FromClient )
		{
			if ( server is not { Active: true } )
			{
				Cmd.ForwardToServer();
				return;
			}
		}
		else if ( !caller.Loopback )
		{
			server.ClientPrintf( "You can't 'kick' because you are not a server operator\n" );
			return;
		}

		var argument = Cmd.Argv( 1 );
		var messageStart = 2;
		var steamId = false;
		ServerClient target = null;

		if ( argument.Length == 0 || argument[0] != '#' )
		{
			foreach ( var client in server.Clients )
			{
				if ( (client.Active || client.Connected) && client.Name.Equals( argument, StringComparison.OrdinalIgnoreCase ) )
				{
					target = client;
					break;
				}
			}
		}
		else
		{
			var id = argument[1..];

			if ( Cmd.Argc >= 3 && id.Length == 0 )
			{
				id = Cmd.Argv( 2 );
				messageStart = 3;
			}

			var userId = Common.Atoi( id );
			var idString = id.Length > 63 ? id[..63] : id;

			if ( idString.StartsWith( "STEAM_", StringComparison.OrdinalIgnoreCase ) || idString.StartsWith( "VALVE_", StringComparison.OrdinalIgnoreCase ) )
			{
				idString = $"{id}:{Cmd.Argv( messageStart + 1 )}:{Cmd.Argv( messageStart + 3 )}";

				if ( idString.Length > 62 )
					idString = idString[..62];

				messageStart += 4;
				steamId = true;
			}

			foreach ( var client in server.Clients )
			{
				if ( !client.Active && !client.Connected )
					continue;

				if ( (userId != 0 && client.UserId == userId) || server.GetClientIdString( client ).Equals( idString, StringComparison.OrdinalIgnoreCase ) )
				{
					target = client;
					break;
				}
			}
		}

		if ( target is null )
			return;

		var who = Cmd.FromClient ? caller.Name : Application.IsDedicatedServer ? "Console" : Client.PlayerName;

		if ( target.Loopback )
		{
			Con.Printf( "The local player cannot be kicked!\n" );
			return;
		}

		if ( Cmd.Argc > messageStart )
		{
			var skip = 0;

			for ( var i = 1; i < messageStart; i++ )
				skip += Cmd.Argv( i ).Length + 1;

			if ( steamId )
				skip -= 4;

			if ( skip <= Cmd.Args.Length )
			{
				var message = Cmd.Args[skip..];

				server.ClientPrintf( target, $"Kicked by {who}: {message}\n" );

				Server.LogPrintf( $"Kick: \"{target.Name}<{target.UserId}><{server.GetClientIdString( target )}><>\" was kicked by \"{who}\" (message \"{message}\")\n" );

				server.DropClient( target, false, $"Kicked :{message}" );
				return;
			}
		}

		server.ClientPrintf( target, $"Kicked by {who}\n" );

		Server.LogPrintf( $"Kick: \"{target.Name}<{target.UserId}><{server.GetClientIdString( target )}><>\" was kicked by \"{who}\"\n" );

		server.DropClient( target, false, "Kicked" );
	}

	[ConCmd( "tell" )]
	private static void Tell( params string[] arguments )
	{
		Cmd.SetArguments( "tell", arguments );

		if ( !Cmd.FromClient )
		{
			Cmd.ForwardToServer();
			return;
		}

		if ( Cmd.Argc < 3 )
			return;

		var server = Server.Current;
		var text = $"{server.HostClient.Name} TELL: ";
		var message = Cmd.Args;

		if ( message.StartsWith( '"' ) )
			message = message.Length > 1 ? message[1..^1] : "";

		if ( message.Length > 62 - text.Length )
			message = message[..(62 - text.Length)];

		if ( message.IndexOf( Cmd.Argv( 1 ), StringComparison.Ordinal ) is >= 0 and var found )
			message = message[(found + Cmd.Argv( 1 ).Length)..];

		text += message + "\n";

		foreach ( var client in server.Clients )
		{
			if ( !client.Active || !client.Spawned || client.FakeClient )
				continue;

			if ( !client.Name.Equals( Cmd.Argv( 1 ), StringComparison.OrdinalIgnoreCase ) )
				continue;

			server.MessageBegin( MessageDest.One, Server.RegUserMsg( "SayText", -1 ), default, server.GetEdict( client.EdictIndex ) );
			server.WriteByte( 0 );
			server.WriteString( text );
			server.MessageEnd();

			return;
		}
	}

	[ConCmd( "interp" )]
	private static void Interp()
	{
		Client.StudioInterpolation = !Client.StudioInterpolation;

		Con.Printf( Client.StudioInterpolation ? "Frame Interpolation ON\n" : "Frame Interpolation OFF\n" );
	}

	[ConCmd( "sizeup" )]
	private static void SizeUp()
	{
		if ( Client.ViewSize < 120f )
			Client.ViewSize += 10f;
	}

	[ConCmd( "sizedown" )]
	private static void SizeDown()
	{
		if ( Client.ViewSize > 30f )
			Client.ViewSize -= 10f;
	}

	private static int CompareLowerCase( string a, string b ) => string.CompareOrdinal( a.ToLowerInvariant(), b.ToLowerInvariant() );

	private static SortedDictionary<string, int> CvarFlags()
	{
		var cvars = new SortedDictionary<string, int>( Comparer<string>.Create( CompareLowerCase ) )
		{
			["volume"] = CvarArchive,
			["sensitivity"] = CvarArchive,
			["hostname"] = 0,
			["voice_loopback"] = 0
		};

		foreach ( var type in Game.TypeLibrary.GetTypes() )
		{
			if ( type.Namespace is not ("GoldSrc" or "HalfLife") && type.Namespace?.StartsWith( "GoldSrc.", StringComparison.Ordinal ) != true )
				continue;

			foreach ( var member in type.Members )
			{
				if ( member.GetCustomAttribute<ConVarAttribute>() is not { Name: { } name } attribute || attribute is ConCmdAttribute )
					continue;

				var flags = 0;

				if ( (attribute.Flags & ConVarFlags.Saved) != 0 || attribute.Saved )
					flags |= CvarArchive;

				if ( (attribute.Flags & ConVarFlags.UserInfo) != 0 )
					flags |= CvarUserInfo;

				if ( (attribute.Flags & ConVarFlags.Replicated) != 0 || Server.IsProtectedCvar( name ) )
					flags |= CvarServer;

				cvars[name] = flags;
			}
		}

		return cvars;
	}

	public static bool IsServerCvar( string name ) => CvarFlags().TryGetValue( name, out var flags ) && (flags & CvarServer) != 0;

	[ConCmd( "cmdlist" )]
	private static void CmdList( params string[] arguments )
	{
		const string logFile = "cmdlist.txt";

		string partial = null;
		var logging = false;

		if ( arguments.Length >= 1 )
		{
			if ( arguments[0].Equals( "?", StringComparison.OrdinalIgnoreCase ) )
			{
				Con.Printf( "CmdList           : List all commands\nCmdList [Partial] : List commands starting with 'Partial'\nCmdList log [Partial] : Logs commands to file \"cmdlist.txt\" in the gamedir.\n" );
				return;
			}

			if ( arguments[0].Equals( "log", StringComparison.OrdinalIgnoreCase ) )
			{
				logging = true;

				if ( arguments.Length == 2 )
					partial = arguments[1];
			}
			else
			{
				partial = arguments[0];
			}
		}

		var count = 0;
		var log = new StringBuilder();

		Con.Printf( "Command List\n--------------\n" );

		foreach ( var name in Cmd.CommandNames() )
		{
			if ( partial is not null && string.Compare( name, 0, partial, 0, partial.Length, StringComparison.OrdinalIgnoreCase ) != 0 )
				continue;

			var line = $"{(name.Length > 16 ? name[..16] : name),-16}\n";

			Con.Printf( line );

			log.Append( line );

			count++;
		}

		if ( arguments.Length == 1 && !string.IsNullOrEmpty( partial ) )
			Con.Printf( $"--------------\n{count,3} Commands for [{partial}]\nCmdList ? for syntax\n" );
		else
			Con.Printf( $"--------------\n{count,3} Total Commands\nCmdList ? for syntax\n" );

		if ( logging )
			FileSystem.Data.WriteAllText( logFile, log.ToString() );
	}

	[ConCmd( "cvarlist" )]
	private static void CvarList( params string[] arguments )
	{
		string partial = null;
		string logFile = null;
		var archiveOnly = false;
		var serverOnly = false;

		if ( arguments.Length >= 1 )
		{
			if ( arguments[0].Equals( "?", StringComparison.OrdinalIgnoreCase ) )
			{
				Con.Printf( "CvarList           : List all cvars\nCvarList [Partial] : List cvars starting with 'Partial'\nCvarList log [Partial] : Logs cvars to file \"cvarlist.txt\" in the gamedir.\n" );
				return;
			}

			if ( arguments[0].Equals( "log", StringComparison.OrdinalIgnoreCase ) )
			{
				var index = 0;

				while ( true )
				{
					logFile = $"cvarlist{index:D2}.txt";

					if ( !FileSystem.Data.FileExists( logFile ) )
						break;

					index++;

					if ( index > 99 )
					{
						Con.Printf( "Can't cvarlist! Too many existing cvarlist output files in the gamedir!\n" );
						return;
					}
				}

				if ( arguments.Length == 2 )
					partial = arguments[1];
			}
			else if ( arguments[0].Equals( "-a", StringComparison.OrdinalIgnoreCase ) )
			{
				archiveOnly = true;
			}
			else if ( arguments[0].Equals( "-s", StringComparison.OrdinalIgnoreCase ) )
			{
				serverOnly = true;
			}
			else
			{
				partial = arguments[0];
			}
		}

		var count = 0;
		var log = new StringBuilder();

		Con.Printf( "CVar List\n--------------\n" );

		foreach ( var (name, flags) in CvarFlags() )
		{
			if ( archiveOnly && (flags & CvarArchive) == 0 )
				continue;

			if ( serverOnly && (flags & CvarServer) == 0 )
				continue;

			if ( partial is not null && string.Compare( name, 0, partial, 0, partial.Length, StringComparison.OrdinalIgnoreCase ) != 0 )
				continue;

			var value = Cvar.GetFloat( name );
			var line = value == (int)value ? $"{name,-15} : {(int)value,8}" : $"{name,-15} : {value,8:F3}";

			if ( (flags & CvarArchive) != 0 )
				line += ", a";

			if ( (flags & CvarServer) != 0 )
				line += ", sv";

			if ( (flags & CvarUserInfo) != 0 )
				line += ", i";

			line += "\n";

			Con.Printf( line );

			log.Append( line );

			count++;
		}

		if ( arguments.Length == 1 && !string.IsNullOrEmpty( partial ) )
			Con.Printf( $"--------------\n{count,3} CVars for [{partial}]\nCvarList ? for syntax\n" );
		else
			Con.Printf( $"--------------\n{count,3} Total CVars\nCvarList ? for syntax\n" );

		if ( logFile is null )
			return;

		FileSystem.Data.WriteAllText( logFile, log.ToString() );

		Con.Printf( $"cvarlist logged to {logFile}\n" );
	}
}
