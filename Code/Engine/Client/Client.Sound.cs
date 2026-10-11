using System;

namespace GoldSrc;

partial class Client
{
	private const int SoundSentenceFlag = 1 << 4;

	public SoundEngine Sound { get; private set; }

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
			Log.Trace( $"invalid sound {name}" );
			return;
		}

		Sound.StartDynamicSound( _viewEntity, (int)SoundChannel.Item, sfx, _viewOrigin, volume, 1f, 0, 100 );
	}

	public void StopSound( int entityIndex, SoundChannel channel, string sample )
	{
		PlaySound( entityIndex, Vector3.Zero, channel, sample, 0f, 0f, Server.SoundStop, Server.PitchNormal );
	}
}
