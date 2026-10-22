using System;
using System.Text;

namespace GoldSrc;

public static class Cmd
{
	private const int MaxLineLength = 1023;
	private const int MaxArgs = 80;
	private const int MaxArgLength = 515;

	private static readonly string[] PrivilegedCommands =
	[
		"connect", "retry", "rcon", "exec", "alias", "quit", "_restart", "_setrenderer", "_setvideomode", "_setgamedir",
		"_sethdmodels", "_setaddons_folder", "kill", "motd", "motd_write", "writecfg", "bind", "unbind", "unbindall"
	];

	private static readonly string[] DispatchedCommands = ["+voicerecord", "-voicerecord", "exec", "version", "status", "kick", "disconnect", "connect", "reconnect", "quit", "soundlist"];
	private static readonly string[] FilteredCommands = ["removedemo", "cd", "say", "setinfo", "timerefresh"];
	private static readonly string[] GameOwnedCvars = ["sv_password", "password"];
	private static readonly string[] PrivilegedCvars = ["sv_logsecret", "rcon_address", "rcon_port", "cl_filterstuffcmd", "rcon_password"];
	private static readonly string[] FilteredCvars = ["sys_ticrate", "fps_max", "developer", "rate", "ex_interp", "volume", "MP3Volume", "speak_enabled", "voice_enable"];
	private static readonly string[] FilteredPrefixes = ["cl_", "gl_", "m_", "r_", "hud_"];

	private static StringBuilder _text = new();
	private static StringBuilder _filteredText = new();
	private static List<string> _arguments = [];
	private static HashSet<string> _members;
	private static HashSet<string> _commands;
	private static HashSet<string> _hudCommands;

	private const string HudCommandNamespace = "HalfLife";
	private static readonly string[] SilentConfigs = ["autoexec.cfg", "userconfig.cfg", "hw/opengl.cfg", "joystick.cfg", "game.cfg"];

	private static List<(string Name, string Value)> _aliases = [];

	private static bool _wait;

	public static bool FromClient { get; private set; }

	public static bool Privileged { get; private set; } = true;

	public static int Argc => _arguments.Count;
	public static string Args { get; private set; }

	public static void Hotloaded()
	{
		_members = null;
		_commands = null;
		_hudCommands = null;
	}

	public static string Argv( int index ) => index < _arguments.Count ? _arguments[index] : "";

	public static void AddText( string text ) => _text.Append( text );

	public static void AddFilteredText( string text ) => _filteredText.Append( text );

	public static void InsertText( string text ) => (Privileged ? _text : _filteredText).Insert( 0, text );

	public static void Execute()
	{
		Execute( _text, true, -1 );
		Execute( _filteredText, false, 1 );
	}

	private static void Execute( StringBuilder buffer, bool privileged, int maxCommands )
	{
		var count = 0;

		while ( buffer.Length > 0 )
		{
			var quoted = false;
			int end;

			for ( end = 0; end < buffer.Length; end++ )
			{
				var c = buffer[end];

				if ( c == '"' )
					quoted = !quoted;

				if ( (!quoted && c == ';') || c == '\n' )
					break;
			}

			var line = buffer.ToString( 0, Math.Min( end, MaxLineLength ) );

			buffer.Remove( 0, end == buffer.Length ? end : end + 1 );

			ExecuteString( line, privileged );

			count++;

			var stop = _wait || (maxCommands > 0 && count >= maxCommands);

			_wait = false;

			if ( stop )
				return;
		}
	}

	private static void LoadConsoleMembers()
	{
		_members = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		_commands = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var attribute in Game.TypeLibrary.GetMemberAttributes<ConVarAttribute>( true ) )
		{
			if ( attribute.Name is null )
				continue;

			_members.Add( attribute.Name );

			if ( attribute is ConCmdAttribute )
				_commands.Add( attribute.Name );
		}
	}

	public static bool IsPrivilegedCvar( string name ) => Contains( PrivilegedCvars, name );

	public static List<string> CommandNames()
	{
		if ( _members is null )
			LoadConsoleMembers();

		var names = new List<string>( _commands );

		foreach ( var name in DispatchedCommands )
		{
			if ( !_commands.Contains( name ) )
				names.Add( name );
		}

		names.Sort( StringComparer.OrdinalIgnoreCase );

		return names;
	}

	private static bool IsConsoleMember( string name )
	{
		if ( _members is null )
			LoadConsoleMembers();

		return _members.Contains( name );
	}

	private static bool Contains( string[] names, string name )
	{
		foreach ( var entry in names )
		{
			if ( entry.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	private static bool HasFilteredPrefix( string name )
	{
		foreach ( var prefix in FilteredPrefixes )
		{
			if ( name.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}

	private static bool RefusedUnprivileged( string name, string text )
	{
		var filter = Cvar.GetFloat( "cl_filterstuffcmd" ) > 0f;

		var prefixed = IsConsoleMember( name ) && filter && HasFilteredPrefix( name );
		var command = _commands.Contains( name );

		if ( Contains( PrivilegedCommands, name ) || (filter && Contains( FilteredCommands, name )) || (prefixed && command) )
		{
			Con.Printf( $"Could not execute privileged command {text}\n" );

			return true;
		}

		if ( Contains( PrivilegedCvars, name ) || (filter && Contains( FilteredCvars, name )) || (prefixed && !command) )
		{
			Con.Printf( $"{name} is a privileged variable\n" );

			return true;
		}

		return false;
	}

	public static void ExecuteString( string text, bool privileged = true )
	{
		TokenizeString( text );

		if ( Argc == 0 )
			return;

		if ( !privileged && RefusedUnprivileged( Argv( 0 ), text ) )
			return;

		Privileged = privileged;

		Dispatch();

		Privileged = true;
	}

	private static void Dispatch()
	{
		if ( Argv( 0 )[0] is '+' or '-' && ClientDll.Loaded is { } client && client.KeyCommand( Argv( 0 )[1..], Argv( 0 )[0] == '+' ) )
		{
			Client.Current?.RecordHudCommand( Argv( 0 ) );
			return;
		}

		if ( Argv( 0 ).Equals( "+voicerecord", StringComparison.OrdinalIgnoreCase ) || Argv( 0 ).Equals( "-voicerecord", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.Current?.VoiceRecordCommand( Argv( 0 )[0] == '+' );
			return;
		}

		if ( Argv( 0 ).Equals( "exec", StringComparison.OrdinalIgnoreCase ) )
		{
			Exec();
			return;
		}

		if ( ExecuteHostCommand() )
			return;

		if ( Argv( 0 ).Equals( "disconnect", StringComparison.OrdinalIgnoreCase ) )
		{
			Host.Disconnect();
			return;
		}

		if ( Argv( 0 ).Equals( "connect", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( Argc == 2 )
				Networking.Connect( Argv( 1 ) );

			return;
		}

		if ( Argv( 0 ).Equals( "reconnect", StringComparison.OrdinalIgnoreCase ) )
		{
			Client.Current?.Reconnect();
			return;
		}

		if ( Argv( 0 ).Equals( "quit", StringComparison.OrdinalIgnoreCase ) )
		{
			Game.Close();
			return;
		}

		if ( Argv( 0 ).Equals( "voice_loopback", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( Argc == 1 )
				Con.Printf( $"\"voice_loopback\" is \"{Cvar.GetString( "voice_loopback" )}\"\n" );
			else
				Cvar.Set( "voice_loopback", Argv( 1 ) );

			return;
		}

		if ( Argv( 0 ).Equals( "hostname", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( Argc == 1 )
				Con.Printf( $"\"hostname\" is \"{Cvar.GetString( "hostname" )}\"\n" );
			else
				Cvar.Set( "hostname", Argv( 1 ) );

			return;
		}

		foreach ( var name in GameOwnedCvars )
		{
			if ( !Argv( 0 ).Equals( name, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( Argc == 1 )
				Con.Printf( $"\"{name}\" is \"{Cvar.GetString( name )}\"\n" );
			else
				Cvar.Set( name, Argv( 1 ) );

			return;
		}

		if ( Argv( 0 ).Equals( "soundlist", StringComparison.OrdinalIgnoreCase ) )
		{
			SoundEngine.SoundListCommand();
			return;
		}

		if ( !IsConsoleMember( Argv( 0 ) ) )
		{
			foreach ( var (name, value) in _aliases )
			{
				if ( !name.Equals( Argv( 0 ), StringComparison.OrdinalIgnoreCase ) )
					continue;

				InsertText( value );
				return;
			}

			ForwardToServer();
			return;
		}

		var arguments = new object[Argc - 1];

		for ( var i = 1; i < Argc; i++ )
			arguments[i - 1] = Argv( i );

		var command = Argv( 0 );

		ConsoleSystem.Run( command, arguments );

		if ( IsHudCommand( command ) )
			Client.Current?.RecordHudCommand( command );
	}

	private static bool IsHudCommand( string name )
	{
		if ( _hudCommands is null )
		{
			_hudCommands = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

			foreach ( var (method, attribute) in Game.TypeLibrary.GetMethodsWithAttribute<ConCmdAttribute>() )
			{
				if ( attribute.Name is not null && method.TypeDescription.Namespace == HudCommandNamespace && method.TypeDescription.BaseType?.TargetType != typeof( GameDll ) )
					_hudCommands.Add( attribute.Name );
			}
		}

		return _hudCommands.Contains( name );
	}

	private static bool ExecuteHostCommand()
	{
		if ( Argv( 0 ).Equals( "version", StringComparison.OrdinalIgnoreCase ) )
		{
			Host.Version();
			return true;
		}

		if ( Argv( 0 ).Equals( "status", StringComparison.OrdinalIgnoreCase ) )
		{
			Host.Status( [.. _arguments.Skip( 1 )] );
			return true;
		}

		if ( Argv( 0 ).Equals( "kick", StringComparison.OrdinalIgnoreCase ) )
		{
			Host.Kick();
			return true;
		}

		return false;
	}

	public static bool ExecuteClientString( string text )
	{
		TokenizeString( text );

		if ( Argc == 0 )
			return false;

		if ( Argv( 0 ).Equals( "status", StringComparison.OrdinalIgnoreCase ) || Argv( 0 ).Equals( "kick", StringComparison.OrdinalIgnoreCase ) )
		{
			FromClient = true;

			try
			{
				ExecuteHostCommand();
			}
			finally
			{
				FromClient = false;
			}

			return true;
		}

		if ( !IsConsoleMember( Argv( 0 ) ) )
			return false;

		var arguments = new object[Argc - 1];

		for ( var i = 1; i < Argc; i++ )
			arguments[i - 1] = Argv( i );

		FromClient = true;

		try
		{
			ConsoleSystem.Run( Argv( 0 ), arguments );
		}
		finally
		{
			FromClient = false;
		}

		return true;
	}

	private static string Join( string name, string[] arguments )
	{
		var builder = new StringBuilder( name );

		foreach ( var argument in arguments )
		{
			builder.Append( ' ' );

			if ( argument.Length == 0 || argument.Contains( ' ' ) )
				builder.Append( '"' ).Append( argument ).Append( '"' );
			else
				builder.Append( argument );
		}

		return builder.ToString();
	}

	public static void SetArguments( string name, string[] arguments ) => TokenizeString( Join( name, arguments ) );

	public static void ForwardToServer( string name, string[] arguments )
	{
		SetArguments( name, arguments );
		ForwardToServer();
	}

	public static void ForwardToServer()
	{
		if ( Argv( 0 ).Equals( "cmd", StringComparison.OrdinalIgnoreCase ) && Argv( 1 ).Equals( "dlfile", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( Client.Current is not { CanForwardToServer: true } client )
		{
			if ( !Argv( 0 ).Equals( "setinfo", StringComparison.OrdinalIgnoreCase ) )
				Con.Printf( $"Can't \"{Argv( 0 )}\", not connected\n" );

			return;
		}

		client.ForwardToServer();
	}

	[ConCmd( "cmd" )]
	private static void ForwardCommand( params string[] arguments )
	{
		SetArguments( "cmd", arguments );
		ForwardToServer();
	}

	public static void TokenizeString( string text )
	{
		_arguments.Clear();
		Args = null;

		var position = 0;

		while ( true )
		{
			while ( position < text.Length && text[position] <= ' ' && text[position] != '\n' )
				position++;

			if ( position >= text.Length || text[position] == '\n' )
				return;

			if ( Argc == 1 )
				Args = text[position..];

			var token = Common.Parse( text, ref position );

			if ( token is null || token.Length + 1 > MaxArgLength )
				return;

			if ( Argc < MaxArgs )
				_arguments.Add( token );
		}
	}

	[ConCmd( "alias" )]
	private static void Alias( params string[] arguments )
	{
		if ( arguments.Length == 0 )
		{
			Con.Printf( "Current alias commands:\n" );

			foreach ( var (name, value) in _aliases )
				Con.Printf( $"{name} : {value}\n" );

			return;
		}

		var alias = arguments[0];

		if ( alias.Length > 31 )
		{
			Con.Printf( "Alias name is too long\n" );
			return;
		}

		if ( ConsoleSystem.GetValue( alias ) is not null || alias.Equals( "_special", StringComparison.OrdinalIgnoreCase ) || alias.Equals( "special", StringComparison.OrdinalIgnoreCase ) )
		{
			Con.Printf( "Alias name is invalid\n" );
			return;
		}

		var builder = new StringBuilder();

		foreach ( var argument in arguments.AsSpan( 1 ) )
			builder.Append( argument ).Append( ' ' );

		builder.Append( '\n' );

		var command = builder.ToString();
		var index = _aliases.FindIndex( entry => entry.Name == alias );

		if ( index >= 0 )
			_aliases[index] = (alias, command);
		else
			_aliases.Insert( 0, (alias, command) );
	}

	[ConCmd( "wait" )]
	private static void Wait() => _wait = true;

	private static void Exec()
	{
		if ( Argc != 2 )
		{
			Con.Printf( "exec <filename> : execute a script file\n" );
			return;
		}

		var fileName = Argv( 1 );

		if ( fileName.Contains( '\\' ) || fileName.Contains( ':' ) || fileName.Contains( '~' ) || fileName.Contains( ".." ) || fileName.StartsWith( '/' ) )
		{
			Con.Printf( $"exec {fileName}: invalid path.\n" );
			return;
		}

		if ( fileName.IndexOf( '.' ) != fileName.LastIndexOf( '.' ) )
		{
			Con.Printf( $"exec {fileName}: invalid filename.\n" );
			return;
		}

		var extension = System.IO.Path.GetExtension( fileName );

		if ( extension != ".cfg" && extension != ".rc" )
		{
			Con.Printf( $"exec {fileName}: not a .cfg or .rc file\n" );
			return;
		}

		if ( Server.LoadMountText( fileName ) is not { } file )
		{
			if ( !Array.Exists( SilentConfigs, fileName.Contains ) )
				Con.Printf( $"couldn't exec {fileName}\n" );

			return;
		}

		Con.DPrintf( $"execing {fileName}\n" );

		InsertText( $"\n{file}\n" );
	}
}
