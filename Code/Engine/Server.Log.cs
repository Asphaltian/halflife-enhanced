using System;
using System.IO;
using System.Text;

namespace GoldSrc;

partial class Server
{
	private const int MaxLogFiles = 1000;
	private const int MaxLogLine = 0x400;

	private static bool _logActive;
	private static string _logFile;

	[ConVar( "mp_logfile", ConVarFlags.Replicated )]
	private static float MpLogFile { get; set => Server.SetServerCvar( "mp_logfile", ref field, value ); } = 1f;

	[ConVar( "mp_logecho" )]
	private static float MpLogEcho { get; set; } = 1f;

	[ConVar( "sv_log_onefile" )]
	private static float SvLogOneFile { get; set; }

	[ConVar( "sv_log_singleplayer" )]
	private static float SvLogSinglePlayer { get; set; }

	[ConVar( "logsdir" )]
	private static string LogsDirectory { get; set; } = "logs";

	[ConCmd( "log" )]
	public static void LogCommand( params string[] arguments )
	{
		if ( arguments.Length != 1 )
		{
			Log.Info( "usage:  log < on | off >\n" );
			Log.Info( _logActive ? "currently logging\n" : "not currently logging\n" );
			return;
		}

		if ( arguments[0].Equals( "off", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( !_logActive )
				return;

			if ( _logFile is not null )
				LogClose();

			Log.Info( "Server logging disabled.\n" );

			_logActive = false;
			return;
		}

		if ( arguments[0].Equals( "on", StringComparison.OrdinalIgnoreCase ) )
		{
			_logActive = true;

			LogOpen();
			return;
		}

		Log.Info( $"log:  unknown parameter {arguments[0]}, 'on' and 'off' are valid\n" );
	}

	private static void LogClose()
	{
		if ( _logFile is not null )
			LogPrintf( "Log file closed\n" );

		_logFile = null;
	}

	private static void LogOpen()
	{
		if ( !_logActive || (SvLogOneFile != 0f && _logFile is not null) )
			return;

		if ( MpLogFile == 0f )
		{
			Log.Info( "Server logging data to console.\n" );
			return;
		}

		LogClose();

		var now = DateTime.Now;
		var directory = LogsDirectory;

		var baseName = directory.Length < 1 || directory.Contains( ':' ) || directory.Contains( ".." )
			? $"logs/L{now.Month:00}{now.Day:00}"
			: $"{directory}/L{now.Month:00}{now.Day:00}";

		string fileName = null;

		for ( var i = 0; i < MaxLogFiles; i++ )
		{
			var candidate = $"{baseName}{i:000}.log";

			if ( FileSystem.Data.FileExists( candidate ) )
				continue;

			WriteFile( candidate, [] );

			if ( !FileSystem.Data.FileExists( candidate ) )
				break;

			Log.Info( $"Server logging data to file {candidate}\n" );

			fileName = candidate;
			break;
		}

		if ( fileName is null )
		{
			Log.Info( $"Unable to open logfiles under {baseName}\nLogging disabled\n" );

			_logActive = false;
			return;
		}

		_logFile = fileName;

		LogPrintf( $"Log file started (file \"{fileName}\") (game \"{GameDirectory}\") (version \"{Host.ProtocolVersion}/{Host.VersionString()}/{Host.BuildNumber()}\")\n" );
	}

	public static void LogPrintf( string message )
	{
		if ( !_logActive )
			return;

		var now = DateTime.Now;
		var line = $"L {now.Month:00}/{now.Day:00}/{now.Year:0000} - {now.Hour:00}:{now.Minute:00}:{now.Second:00}: {message}";

		if ( line.Length >= MaxLogLine )
			line = line[..(MaxLogLine - 1)];

		if ( (Current?.MaxClients ?? 1) <= 1 && SvLogSinglePlayer == 0f )
			return;

		if ( MpLogEcho != 0f )
			Log.Info( line );

		if ( _logFile is null || MpLogFile == 0f )
			return;

		using var stream = FileSystem.Data.OpenWrite( _logFile, FileMode.Append );

		stream.Write( Encoding.Latin1.GetBytes( line.Replace( "\n", "\r\n" ) ) );
	}
}
