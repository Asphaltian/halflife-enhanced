using System;
using System.Globalization;

namespace GoldSrc;

public static class CdAudio
{
	private const int MaxTracks = 200;

	private static List<string> _tracks =
	[
		"",
		"",
		"media/Half-Life01.mp3",
		"media/Prospero01.mp3",
		"media/Half-Life12.mp3",
		"media/Half-Life07.mp3",
		"media/Half-Life10.mp3",
		"media/Suspense01.mp3",
		"media/Suspense03.mp3",
		"media/Half-Life09.mp3",
		"media/Half-Life02.mp3",
		"media/Half-Life13.mp3",
		"media/Half-Life04.mp3",
		"media/Half-Life15.mp3",
		"media/Half-Life14.mp3",
		"media/Half-Life16.mp3",
		"media/Suspense02.mp3",
		"media/Half-Life03.mp3",
		"media/Half-Life08.mp3",
		"media/Prospero02.mp3",
		"media/Half-Life05.mp3",
		"media/Prospero04.mp3",
		"media/Half-Life11.mp3",
		"media/Half-Life06.mp3",
		"media/Prospero03.mp3",
		"media/Half-Life17.mp3",
		"media/Prospero05.mp3",
		"media/Suspense05.mp3",
		"media/Suspense07.mp3"
	];

	[ConVar( "MP3Volume", ConVarFlags.Saved )]
	private static float Mp3Volume { get; set; } = 0.8f;

	[ConVar( "MP3FadeTime", ConVarFlags.Saved )]
	private static float Mp3FadeTime { get; set; } = 2f;

	[ConVar( "bgmvolume", ConVarFlags.Saved )]
	private static float BgmVolume { get; set; } = 1f;

	private static int[] _remap = new int[100];

	private static SoundFile _streamFile;
	private static SoundHandle _stream;
	private static bool _streamPaused;
	private static bool _initialized;
	private static bool _enabled;
	private static bool _playing;
	private static bool _wasPlaying;
	private static bool _looping;
	private static bool _primed;
	private static int _playTrack;
	private static string _pendingPlayFileName = string.Empty;
	private static float _volume = 1f;
	private static float _mp3Volume = 1f;
	private static double _fadeOutTime;
	private static bool _mp3Active;
	private static int _mp3Track;
	private static bool _mp3Looping;
	private static string _mp3File = string.Empty;
	private static float _mp3StreamVolume;

	public static void Init()
	{
		if ( _initialized )
			return;

		_mp3Active = false;
		_mp3Track = 0;
		_mp3Looping = false;
		_mp3StreamVolume = 100f;

		SetVolume( Mp3Volume );

		_initialized = true;
		_enabled = true;

	}

	public static void Shutdown()
	{
		CloseStream();

		_primed = false;

		if ( _initialized )
			Stop();
	}

	[ConCmd( "cd" )]
	private static void Command( params string[] arguments )
	{
		Cmd.SetArguments( "cd", arguments );

		if ( Cmd.Argc < 2 )
			return;

		var command = Cmd.Argv( 1 );

		if ( command.Equals( "on", StringComparison.OrdinalIgnoreCase ) )
		{
			_enabled = true;
			return;
		}

		if ( command.Equals( "off", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _playing )
				Stop();

			_enabled = false;
			return;
		}

		if ( command.Equals( "reset", StringComparison.OrdinalIgnoreCase ) )
		{
			_enabled = true;

			if ( _playing )
				Stop();

			for ( var i = 0; i < _remap.Length; i++ )
				_remap[i] = i;

			return;
		}

		if ( command.Equals( "remap", StringComparison.OrdinalIgnoreCase ) )
		{
			var count = Cmd.Argc - 2;

			for ( var i = 1; i <= count; i++ )
				_remap[i] = Common.Atoi( Cmd.Argv( i + 1 ) );

			return;
		}

		if ( command.Equals( "close", StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( command.Equals( "mp3info", StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Info( $"Current MP3 Title: {_mp3File}" );
			Log.Info( $"Current MP3 Track: {_mp3Track}" );
			Log.Info( $"Current MP3 Volume: {(int)BitConverter.DoubleToInt64Bits( _mp3StreamVolume )}" );
			return;
		}

		if ( Cmd.Argc > 2 && command.Equals( "mp3track", StringComparison.OrdinalIgnoreCase ) )
		{
			var track = Common.Atoi( Cmd.Argv( 2 ) );

			if ( !PlayMp3Track( track + 1, false ) )
				return;

			_playTrack = track + 1;
			_looping = false;
			_playing = true;
			return;
		}

		if ( command.Equals( "play", StringComparison.OrdinalIgnoreCase ) )
		{
			Play( Common.Atoi( Cmd.Argv( 2 ) ), false );
			return;
		}

		if ( command.Equals( "playfile", StringComparison.OrdinalIgnoreCase ) )
		{
			PlayFile( Cmd.Argv( 2 ), false );
			return;
		}

		if ( command.Equals( "loop", StringComparison.OrdinalIgnoreCase ) )
		{
			Play( Common.Atoi( Cmd.Argv( 2 ) ), true );
			return;
		}

		if ( command.Equals( "loopfile", StringComparison.OrdinalIgnoreCase ) )
		{
			PlayFile( Cmd.Argv( 2 ), true );
			return;
		}

		if ( command.Equals( "stop", StringComparison.OrdinalIgnoreCase ) )
		{
			Stop();
			return;
		}

		if ( command.Equals( "fadeout", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( !_playing )
				return;

			_fadeOutTime = Mp3FadeTime + RealTime.Now;
			Update();
			return;
		}

		if ( command.Equals( "pause", StringComparison.OrdinalIgnoreCase ) )
		{
			Pause();
			return;
		}

		if ( command.Equals( "resume", StringComparison.OrdinalIgnoreCase ) )
		{
			Resume();
			return;
		}

		if ( command.Equals( "eject", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( _playing )
				Stop();

			return;
		}

		if ( !command.Equals( "info", StringComparison.OrdinalIgnoreCase ) )
			return;

		Log.Info( $"{_tracks.Count - 1} tracks" );

		if ( _playing )
			Log.Info( $"Currently {(_looping ? "looping" : "playing")} track {_playTrack}" );
		else if ( _wasPlaying )
			Log.Info( $"Paused {(_looping ? "looping" : "playing")} track {_playTrack}" );

		Log.Info( $"Volume is {_volume.ToString( "F6", CultureInfo.InvariantCulture )}" );
	}

	public static void Play( int track, bool looping )
	{
		_fadeOutTime = 0.0;
		PlayTrack( track, looping );
	}

	public static void PlayFile( string fileName, bool looping )
	{
		_fadeOutTime = 0.0;
		_pendingPlayFileName = fileName;
		PlayTrack( 0, looping );
	}

	private static void PlayTrack( int track, bool looping )
	{
		if ( !_enabled )
			return;

		if ( track == 0 && _pendingPlayFileName.Length != 0 )
		{
			PlayMp3File( _pendingPlayFileName, looping );
			_pendingPlayFileName = string.Empty;
			return;
		}

		_pendingPlayFileName = string.Empty;

		PlayMp3Track( track, looping );
	}

	private static bool PlayMp3Track( int track, bool looping )
	{
		if ( track < 2 || track >= _tracks.Count )
			return false;

		if ( !_primed && !OpenStream( track ) )
			return false;

		_primed = false;
		StartStream( track, looping );

		return true;
	}

	private static bool PlayMp3File( string fileName, bool looping )
	{
		if ( !_primed && !OpenStream( AddTrack( fileName ) ) )
			return false;

		_primed = false;
		StartStream( 0, looping );

		return true;
	}

	private static int AddTrack( string fileName )
	{
		if ( fileName.Contains( ':' ) || fileName.Contains( ".." ) )
			return 0;

		var name = (fileName.Contains( ".mp3" ) ? fileName : $"{fileName}.mp3").Replace( '\\', '/' );
		var track = _tracks.IndexOf( name );

		if ( track >= 0 )
			return track;

		if ( _tracks.Count >= MaxTracks )
			return 0;

		_tracks.Add( name );

		return _tracks.Count - 1;
	}

	private static bool OpenStream( int track )
	{
		if ( track < 2 || track >= _tracks.Count )
			return false;

		_primed = false;

		Stop();
		_fadeOutTime = 0.0;

		_streamFile = SoundFile.Load( $"{Server.GetMountPath( _tracks[track] )}.vsnd" );

		if ( _streamFile is null )
			return false;

		_mp3File = _tracks[track];

		return true;
	}

	private static void StartStream( int track, bool looping )
	{
		StartStreamHandle();

		_mp3Active = true;
		_mp3Track = track;
		_mp3Looping = looping;
		_looping = looping;
		_playTrack = track;
		_playing = true;

		if ( _mp3Volume != 0f || !_enabled )
			return;

		PauseStream( true );

		_wasPlaying = true;
		_playing = false;
		_pendingPlayFileName = string.Empty;
	}

	private static void StartStreamHandle()
	{
		_stream = Sound.PlayFile( _streamFile, _mp3StreamVolume );

		if ( !_stream.IsValid() )
			return;

		_stream.ListenLocal = true;
		_stream.DistanceAttenuation = false;
		_stream.OcclusionEnabled = false;
		_stream.AirAbsorption = false;
		_stream.ReverbEnabled = false;
		_stream.Paused = _streamPaused;
	}

	private static void PauseStream( bool paused )
	{
		_streamPaused = paused;

		if ( _stream.IsValid() )
			_stream.Paused = paused;
	}

	private static void CloseStream()
	{
		if ( _stream.IsValid() )
			_stream.Stop();

		_stream = null;
		_streamFile = null;
		_streamPaused = false;
	}

	private static bool StreamPlaying()
	{
		if ( _streamFile is null || _streamPaused )
			return false;

		if ( _stream.IsValid() && !_stream.Finished )
			return true;

		if ( !_mp3Looping )
			return false;

		StartStreamHandle();

		return true;
	}

	public static void Stop()
	{
		if ( !_enabled || (!_playing && !_wasPlaying) )
			return;

		_playing = false;
		_wasPlaying = false;
		_pendingPlayFileName = string.Empty;
		_mp3File = string.Empty;

		CloseStream();

		_primed = false;
		_mp3Active = false;
		_mp3Track = 0;
		_mp3Looping = false;
		_mp3StreamVolume = _mp3Volume;
	}

	public static void Pause()
	{
		if ( !_initialized || !_enabled || !_playing )
			return;

		if ( _mp3Active )
			PauseStream( true );

		_wasPlaying = _playing;
		_playing = false;
		_pendingPlayFileName = string.Empty;
	}

	public static void Resume()
	{
		if ( !_initialized || !_enabled || !_wasPlaying )
			return;

		if ( _mp3Active )
			PauseStream( false );

		_playing = true;
	}

	private static float SetVolume( float volume )
	{
		volume = volume >= 0f ? (volume <= 1f ? volume : 1f) : 0f;

		if ( _mp3StreamVolume == volume )
			return _mp3StreamVolume;

		_mp3StreamVolume = volume;

		if ( _stream.IsValid() )
			_stream.Volume = volume;

		return _mp3StreamVolume;
	}

	public static void Frame()
	{
		if ( !_enabled )
			return;

		if ( _volume != BgmVolume )
		{
			if ( _volume != 0f )
			{
				_volume = 0f;
				Pause();
			}
			else
			{
				_volume = 1f;
				Resume();
			}

			Cvar.Set( "bgmvolume", _volume.ToString( "F6", CultureInfo.InvariantCulture ) );
		}

		if ( _fadeOutTime == 0.0 && _mp3Volume != Mp3Volume )
			_mp3Volume = SetVolume( Mp3Volume );

		Update();
	}

	private static void Update()
	{
		if ( !_playing )
			return;

		if ( _fadeOutTime != 0.0 )
		{
			double volume = Mp3Volume;
			var now = RealTime.Now;

			if ( now < _fadeOutTime )
			{
				volume = (_fadeOutTime - now) / Mp3FadeTime * Mp3Volume;
			}
			else
			{
				Stop();
				_fadeOutTime = 0.0;
			}

			SetVolume( (float)volume );
		}

		if ( !_mp3Active )
			return;

		_playing = StreamPlaying();

		if ( !_playing )
			_pendingPlayFileName = string.Empty;
	}
}
