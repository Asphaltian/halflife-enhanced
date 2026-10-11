using System;

namespace GoldSrc;

partial class Client
{
	private const int SoundSentenceFlag = 1 << 4;

	private int _soundFadeStartPercent;
	private int _soundFadeOutTime;
	private int _soundFadeHoldTime;
	private int _soundFadeInTime;
	private double _soundFadeStartTime;

	public SoundEngine Sound { get; private set; }

	public int SoundFadePercent { get; private set; }

	private void SetSoundFade( int percent, int holdTime, int outTime, int inTime )
	{
		_soundFadeStartPercent = percent;
		_soundFadeHoldTime = holdTime;
		_soundFadeOutTime = outTime;
		_soundFadeInTime = inTime;
		_soundFadeStartTime = Host.RealTime;
	}

	private void UpdateSoundFade()
	{
		SoundFadePercent = 0;

		if ( Host.RealTime >= _soundFadeInTime + _soundFadeHoldTime + _soundFadeOutTime + _soundFadeStartTime )
			return;

		var elapsed = (float)(Host.RealTime - _soundFadeStartTime);

		if ( _soundFadeOutTime != 0 && elapsed < _soundFadeOutTime )
			SoundFadePercent = (int)(elapsed / _soundFadeOutTime * _soundFadeStartPercent);
		else if ( elapsed < _soundFadeHoldTime + _soundFadeOutTime )
			SoundFadePercent = _soundFadeStartPercent;
		else
			SoundFadePercent = (int)((1f - ((elapsed - (_soundFadeHoldTime + _soundFadeOutTime)) / _soundFadeOutTime)) * _soundFadeStartPercent);
	}

	[ConCmd( "soundfade" )]
	private static void SoundFadeCommand( params string[] arguments )
	{
		if ( arguments.Length is not (2 or 4) )
		{
			Con.Printf( "soundfade <percent> <hold> [<out> <int>]\n" );
			return;
		}

		var percent = Math.Clamp( Common.Atoi( arguments[0] ), 0, 100 );
		var holdTime = Math.Min( Common.Atoi( arguments[1] ), 255 );
		var outTime = 0;
		var inTime = 0;

		if ( arguments.Length == 4 )
		{
			outTime = Math.Min( Common.Atoi( arguments[2] ), 255 );
			inTime = Math.Min( Common.Atoi( arguments[3] ), 255 );
		}

		Current?.SetSoundFade( percent, holdTime, outTime, inTime );
	}

	public void PlaySound( int entityIndex, Vector3 origin, SoundChannel channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		var name = (flags & SoundSentenceFlag) != 0 ? $"!{Sound.SentenceName( Common.Atoi( sample[1..] ) )}" : sample;
		var sfx = name[0] == '!' ? new Sfx { Name = name } : Sound.PrecacheSound( name );

		if ( channel == SoundChannel.Static )
			Sound.StartStaticSound( entityIndex, (int)channel, sfx, origin, volume, attenuation, flags, pitch );
		else
			Sound.StartDynamicSound( entityIndex, (int)channel, sfx, origin, volume, attenuation, flags, pitch );
	}

	public void PlaySoundByName( string name, float volume )
	{
		volume = Math.Clamp( volume, 0f, 1f );

		if ( Sound.PrecacheSound( name ) is not { } sfx )
		{
			Con.DPrintf( $"invalid sound {name}\n" );
			return;
		}

		Sound.StartDynamicSound( _viewEntity, (int)SoundChannel.Item, sfx, _viewOrigin, volume, 1f, 0, 100 );
	}

	public void StopSound( int entityIndex, SoundChannel channel, string sample )
	{
		PlaySound( entityIndex, Vector3.Zero, channel, sample, 0f, 0f, Server.SoundStop, Server.PitchNormal );
	}
}
