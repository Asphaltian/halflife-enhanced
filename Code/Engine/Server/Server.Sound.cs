using System;

namespace GoldSrc;

public enum SoundChannel
{
	Auto,
	Weapon,
	Voice,
	Item,
	Body,
	Stream,
	Static,
	NetworkVoiceBase
}

partial class Server
{
	public const int SoundStop = 1 << 5;
	public const int SoundChangeVolume = 1 << 6;
	public const int SoundChangePitch = 1 << 7;
	public const int SoundSpawning = 1 << 8;

	public const float AttenuationNone = 0f;
	public const float VolumeNormal = 1f;
	public const float AttenuationNormal = 0.8f;
	public const float AttenuationIdle = 2f;
	public const float AttenuationStatic = 1.25f;

	public const int PitchNormal = 100;

	public const int SoundMessage = 6;
	public const int SpawnStaticSoundMessage = 29;

	public const int SoundVolume = 1 << 0;
	public const int SoundAttenuation = 1 << 1;
	public const int SoundLargeIndex = 1 << 2;
	public const int SoundPitch = 1 << 3;
	public const int SoundSentence = 1 << 4;

	public const int MaxSentenceNumber = 2048;

	private const int MaxSounds = 512;

	private readonly List<string> _soundPrecache = [];

	public int PrecacheSound( string name )
	{
		if ( name is null )
			throw HostError( "PF_precache_sound_I: NULL pointer" );

		if ( name.Length == 0 || name[0] <= ' ' )
			throw HostError( $"PF_precache_sound_I: Bad string '{name}'" );

		if ( name[0] == '!' )
			throw HostError( $"PF_precache_sound_I: '{name}' do not precache sentence names!" );

		var index = Common.IndexOfName( _soundPrecache, name );

		if ( index >= 0 )
			return index;

		if ( !_loading )
			throw HostError( $"PF_precache_sound_I: '{name}' Precache can only be done in spawn functions" );

		if ( _soundPrecache.Count >= MaxSounds )
			throw HostError( $"PF_precache_sound_I: Sound '{name}' failed to precache because the item count is over the {MaxSounds} limit.\nReduce the number of brush models and/or regular models in the map to correct this." );

		_soundPrecache.Add( name );

		return _soundPrecache.Count - 1;
	}

	private void ClearSounds()
	{
		_soundPrecache.Clear();
		_soundPrecache.Add( string.Empty );
	}

	public void EmitSound( Edict entity, SoundChannel channel, string sample, float volume, float attenuation, int flags = 0, int pitch = PitchNormal )
	{
		if ( volume < 0f || volume > 255f )
			throw new InvalidOperationException( $"EMIT_SOUND: volume = {(int)volume}" );

		if ( attenuation < 0f || attenuation > 4f )
			throw new InvalidOperationException( $"EMIT_SOUND: attenuation = {attenuation}" );

		if ( channel < 0 || (int)channel > 7 )
			throw new InvalidOperationException( $"EMIT_SOUND: channel = {(int)channel}" );

		if ( pitch < 0 || pitch > 255 )
			throw new InvalidOperationException( $"EMIT_SOUND: pitch = {pitch}" );

		StartSound( false, entity, (int)channel, sample, (int)(volume * 255f), attenuation, flags, pitch );
	}

	public void StopSound( Edict entity, SoundChannel channel, string sample )
	{
		EmitSound( entity, channel, sample, 0f, 0f, SoundStop );
	}

	private void StartSound( bool skipHost, Edict entity, int channel, string sample, int volume, float attenuation, int flags, int pitch )
	{
		var origin = new Vector3(
			(float)(((entity.Mins.x + entity.Maxs.x) * 0.5) + entity.Origin.x),
			(float)(((entity.Mins.y + entity.Maxs.y) * 0.5) + entity.Origin.y),
			(float)(((entity.Mins.z + entity.Maxs.z) * 0.5) + entity.Origin.z) );

		if ( !BuildSoundMessage( entity, channel, sample, volume, attenuation, flags, pitch, origin, _multicast ) )
			return;

		if ( channel != (int)SoundChannel.Static && (flags & SoundStop) == 0 )
			Multicast( entity, origin, MulticastPas | (skipHost ? MulticastSkipHost : 0), false );
		else
			Multicast( entity, origin, MulticastBroadcast | (skipHost ? MulticastSkipHost : 0), MaxClients != 1 );
	}

	private bool BuildSoundMessage( Edict entity, int channel, string sample, int volume, float attenuation, int flags, int pitch, Vector3 origin, SizeBuf buffer )
	{
		if ( volume < 0 || volume > 255 )
		{
			Con.Printf( $"SV_StartSound: volume = {volume}" );
			volume = volume < 0 ? 0 : 255;
		}

		if ( attenuation < 0f || attenuation > 4f )
		{
			Con.Printf( $"SV_StartSound: attenuation = {attenuation}" );
			attenuation = attenuation < 0f ? 0f : 4f;
		}

		if ( channel < 0 || channel > 7 )
		{
			Con.Printf( $"SV_StartSound: channel = {channel}" );
			channel = channel < 0 ? 0 : 7;
		}

		if ( pitch < 0 || pitch > 255 )
		{
			Con.Printf( $"SV_StartSound: pitch = {pitch}" );
			pitch = pitch < 0 ? 0 : 7;
		}

		int index;

		if ( sample is not null && sample.Length > 0 && sample[0] == '!' )
		{
			index = Common.Atoi( sample[1..] );

			if ( index >= MaxSentenceNumber )
			{
				Con.Printf( $"invalid sentence number: {sample[1..]}" );
				return false;
			}

			flags |= SoundSentence;
		}
		else if ( sample is not null && sample.Length > 0 && sample[0] == '#' )
		{
			index = Common.Atoi( sample[1..] ) + MaxSentenceNumber;
			flags |= SoundSentence;
		}
		else
		{
			index = LookupSoundIndex( sample );

			if ( index == 0 )
			{
				Con.Printf( $"SV_StartSound: {sample} not precached ({index})\n" );
				return false;
			}
		}

		if ( volume != 255 )
			flags |= SoundVolume;

		if ( attenuation != 1f )
			flags |= SoundAttenuation;

		if ( pitch != PitchNormal )
			flags |= SoundPitch;

		if ( index > 255 )
			flags |= SoundLargeIndex;

		buffer.WriteByte( SoundMessage );
		buffer.StartBitWriting();
		buffer.WriteBits( (uint)flags, 9 );

		if ( (flags & SoundVolume) != 0 )
			buffer.WriteBits( (uint)volume, 8 );

		if ( (flags & SoundAttenuation) != 0 )
			buffer.WriteBits( (uint)(int)(attenuation * 64.0), 8 );

		buffer.WriteBits( (uint)channel, 3 );
		buffer.WriteBits( (uint)entity.Index, 11 );
		buffer.WriteBits( (uint)index, index > 255 ? 16 : 8 );
		buffer.WriteBitVec3Coord( origin );

		if ( (flags & SoundPitch) != 0 )
			buffer.WriteBits( (uint)pitch, 8 );

		buffer.EndBitWriting();

		return true;
	}

	private int LookupSoundIndex( string sample )
	{
		if ( sample is null )
			return 0;

		for ( var i = 1; i < _soundPrecache.Count; i++ )
		{
			if ( _soundPrecache[i].Equals( sample, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return 0;
	}

	public void EmitAmbientSound( Edict entity, Vector3 origin, string sample, float volume, float attenuation, int flags, int pitch )
	{
		int index;

		if ( sample[0] == '!' )
		{
			flags |= SoundSentence;
			index = Common.Atoi( sample[1..] );

			if ( index >= MaxSentenceNumber )
			{
				Con.Printf( $"invalid sentence number: {sample[1..]}" );
				return;
			}
		}
		else
		{
			index = Common.IndexOfName( _soundPrecache, sample );

			if ( index < 0 )
			{
				Con.Printf( $"no precache: {sample}\n" );
				return;
			}
		}

		var buffer = (flags & SoundSpawning) != 0 ? _signon : _datagram;

		buffer.WriteByte( SpawnStaticSoundMessage );
		buffer.WriteCoord( origin.x );
		buffer.WriteCoord( origin.y );
		buffer.WriteCoord( origin.z );
		buffer.WriteShort( index );
		buffer.WriteByte( (int)(volume * 255f) );
		buffer.WriteByte( (int)(attenuation * 64f) );
		buffer.WriteShort( entity.Index );
		buffer.WriteByte( pitch );
		buffer.WriteByte( flags & 0xFF );
	}
}
