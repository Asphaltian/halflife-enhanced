using System;

namespace GoldSrc;

partial class SoundEngine
{
	private const int ChannelStatic = 6;
	private const string TestSentence = "xxtestxx";

	[ConVar( "suitvolume", ConVarFlags.Saved )]
	private static float SuitVolumeCvar { get; set; } = 0.25f;

	[ConVar( "s_show" )]
	private static float SoundShowCvar { get; set; } = 0f;

	[ConVar( "ambient_level" )]
	private static float AmbientLevelCvar { get; set; } = 0.3f;

	[ConVar( "ambient_fade" )]
	private static float AmbientFadeCvar { get; set; } = 100f;

	[ConVar( "snd_noextraupdate" )]
	private static float NoExtraUpdateCvar { get; set; } = 1f;

	[ConVar( "speak_enabled" )]
	private static float SpeakEnabledCvar { get; set; } = 1f;

	private static SoundEngine _current;
	private static int _playHash = 345;
	private static int _playVolumeHash = 543;

	[ConCmd( "play" )]
	private static void PlayCommand( params string[] arguments )
	{
		Cmd.SetArguments( "play", arguments );

		_current?.Play();
	}

	[ConCmd( "playvol" )]
	private static void PlayVolCommand( params string[] arguments )
	{
		Cmd.SetArguments( "playvol", arguments );

		_current?.PlayVol();
	}

	[ConCmd( "speak" )]
	private static void SayCommand( params string[] arguments )
	{
		Cmd.SetArguments( "speak", arguments );

		_current?.Say();
	}

	[ConCmd( "spk" )]
	private static void SayReliableCommand( params string[] arguments )
	{
		Cmd.SetArguments( "spk", arguments );

		_current?.SayReliable();
	}

	[ConCmd( "stopsound" )]
	private static void StopSoundCommand() => _current?.StopAllSounds();

	public static void SoundListCommand() => _current?.SoundList();

	[ConCmd( "soundinfo" )]
	private static void SoundInfoCommand() => _current?.SoundInfo();

	private static string PlayName( int argument )
	{
		var name = Cmd.Argv( argument );

		if ( name.Length > 251 )
			name = name[..251];

		return Cmd.Argv( argument ).Contains( '.' ) ? name : $"{name}.wav";
	}

	private void Play()
	{
		if ( SpeakEnabledCvar == 0f )
			return;

		for ( var i = 1; i < Cmd.Argc; i++ )
		{
			var sfx = PrecacheSound( PlayName( i ) );

			StartDynamicSound( _playHash++, 0, sfx, _listenerOrigin, 1f, 1f, 0, 100 );
		}
	}

	private void PlayVol()
	{
		if ( SpeakEnabledCvar == 0f )
			return;

		for ( var i = 1; i < Cmd.Argc; i += 2 )
		{
			var sfx = PrecacheSound( PlayName( i ) );
			var volume = Common.Atof( Cmd.Argv( i + 1 ) );

			StartDynamicSound( _playVolumeHash++, 0, sfx, _listenerOrigin, volume, 1f, 0, 100 );
		}
	}

	private Sfx SentenceSound( string caller )
	{
		var sound = Cmd.Argv( 1 );

		if ( sound.Length > 255 )
			sound = sound[..255];

		if ( sound.Length != 0 && sound[0] == '!' )
		{
			var sentence = FindName( sound );

			if ( sentence is null )
				Log.Info( $"{caller}: can't find sentence name {sound}" );

			return sentence;
		}

		Log.Info( $"{caller}: can't cache {TestSentence}" );

		return null;
	}

	private void Say()
	{
		if ( NoSound != 0f || Cmd.Argc < 2 || SpeakEnabledCvar == 0f )
			return;

		if ( SentenceSound( "S_Say" ) is not { } sfx )
			return;

		StartDynamicSound( ViewEntity, -1, sfx, Vector3.Zero, 1f, 1f, 0, 100 );
	}

	private void SayReliable()
	{
		if ( NoSound != 0f || Cmd.Argc < 2 || SpeakEnabledCvar == 0f )
			return;

		if ( SentenceSound( "S_Say_Reliable" ) is not { } sfx )
			return;

		StartStaticSound( ViewEntity, ChannelStatic, sfx, Vector3.Zero, 1f, 1f, 0, 100 );
	}

	private void SoundList()
	{
		var total = 0;

		foreach ( var sfx in _knownSfx )
		{
			if ( sfx.File is not { IsValid: true } )
				continue;

			var size = (int)(sfx.Duration * sfx.Rate) * (sfx.Bits / 8);

			total += size;

			Log.Info( $"{(sfx.Loop ? "L" : " ")}({sfx.Bits,2}b) {size,6} : {sfx.Name}" );
		}

		Log.Info( $"Total resident: {total}" );
	}

	private void SoundInfo()
	{
		if ( _mixer is null )
		{
			Log.Info( "sound system not started" );
			return;
		}

		Log.Info( $"{1,5} stereo" );
		Log.Info( $"{16,5} samplebits" );
		Log.Info( $"{_totalChannels,5} total_channels" );
	}
}
