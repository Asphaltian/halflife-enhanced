using System;
using Sandbox.Audio;

namespace GoldSrc;

public sealed class Sfx
{
	public string Name { get; set; }

	internal SoundFile File;
	internal bool Loop;
	internal float Duration;
	internal int Bits;
	internal int Rate;
	internal int ServerCount;
}

internal sealed class SfxChannel
{
	public readonly int Index;

	public Sfx Sfx;
	public SoundHandle Handle;
	public int LeftVol;
	public int RightVol;
	public double End;
	public float Length;
	public int EntNum;
	public int EntChannel;
	public Vector3 Origin;
	public float DistMult;
	public int MasterVol;
	public int ISentence = -1;
	public int Pitch;
	public bool PitchShift;
	public bool Loop;

	public SfxChannel( int index )
	{
		Index = index;
	}

	public void Clear()
	{
		Sfx = null;
		Handle = null;
		LeftVol = 0;
		RightVol = 0;
		End = 0.0;
		Length = 0f;
		EntNum = 0;
		EntChannel = 0;
		Origin = Vector3.Zero;
		DistMult = 0f;
		MasterVol = 0;
		ISentence = 0;
		Pitch = 0;
		PitchShift = false;
		Loop = false;
	}
}

public sealed partial class SoundEngine
{
	public const int MaxChannels = 128;
	public const int NumAmbients = 4;
	public const int MaxDynamicChannels = 32;
	public const int MixSpeed = 11025;

	private const int ChannelVoice = 2;
	private const int ChannelStream = 5;
	private const int MaxSfx = 1024;
	private const float NominalClipDistance = 1000f;
	private const string MixerName = "GoldSrc";
	private const float MouthScale = 34.56f;

	private const int SoundStop = 1 << 5;
	private const int SoundChangeVolume = 1 << 6;
	private const int SoundChangePitch = 1 << 7;

	[ConVar( "nosound" )]
	private static float NoSoundCvar { get; set; } = 0f;

	[ConVar( "hisound", ConVarFlags.Saved )]
	private static float HiSoundCvar { get; set; } = 1f;

	[ConVar( "loadas8bit" )]
	private static float LoadAs8BitCvar { get; set; } = 0f;

	[ConVar( "_snd_mixahead", ConVarFlags.Saved )]
	private static float MixAheadCvar { get; set; } = 0.1f;

	[ConVar( "snd_show" )]
	public static float ShowCvar { get; set; } = 0f;

	[ConVar( "snd_acoustics", ConVarFlags.Saved )]
	public static float Acoustics { get; set; }

	public static float Volume { get; set; } = 0.7f;

	private readonly SfxChannel[] _channels = new SfxChannel[MaxChannels];
	private readonly List<Sfx> _knownSfx = [];

	[SkipHotload]
	private static readonly Curve LinearFalloff = new( new Curve.Frame( 0f, 1f ) { Mode = Curve.HandleMode.Linear }, new Curve.Frame( 1f, 0f ) { Mode = Curve.HandleMode.Linear } );

	private Mixer _mixer;
	private int _totalChannels;
	private double _time;

	private Vector3 _listenerOrigin;
	private Vector3 _listenerForward;
	private Vector3 _listenerRight;
	private Vector3 _listenerUp;

	public SoundEngine()
	{
		_current = this;

		for ( var i = 0; i < MaxChannels; i++ )
			_channels[i] = new SfxChannel( i );

		ReadSentenceFile();
		StopAllSounds();
	}

	[ConVar( "sys_timescale" )]
	private static float SysTimeScale { get; set; } = 1f;

	public int ServerCount { get; set; }

	public bool Paused { get; set; }

	public Sfx PrecacheSound( string name )
	{
		if ( NoSoundCvar != 0f || name.Length >= 64 )
			return null;

		var sfx = FindName( name );

		if ( name[0] != '*' && name[0] != '!' )
			LoadSound( sfx );

		return sfx;
	}

	private Sfx FindName( string name )
	{
		if ( name is null )
			throw new InvalidOperationException( "S_FindName: NULL\n" );

		if ( name.Length > 63 )
			throw new InvalidOperationException( $"Sound name too long: {name}" );

		Sfx free = null;

		foreach ( var known in _knownSfx )
		{
			if ( known.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) )
			{
				if ( known.ServerCount > 0 )
					known.ServerCount = ServerCount;

				return known;
			}

			if ( free is null && known.ServerCount > 0 && known.ServerCount != ServerCount )
				free = known;
		}

		if ( free is not null )
		{
			free.File = null;
		}
		else
		{
			if ( _knownSfx.Count == MaxSfx )
				throw new InvalidOperationException( "S_FindName: out of sfx_t" );

			free = new Sfx();

			_knownSfx.Add( free );
		}

		free.Name = name;
		free.ServerCount = ServerCount;

		return free;
	}

	public void StopAllSounds()
	{
		_totalChannels = MaxDynamicChannels + NumAmbients;

		for ( var i = 0; i < MaxChannels; i++ )
		{
			if ( _channels[i].Sfx is not null )
				FreeChannel( _channels[i] );

			_channels[i].Handle?.Stop();
			_channels[i].Clear();
			_channels[i].ISentence = -1;
		}

		Array.Clear( _sentenceUsed );
	}

	private bool PrepareStart( string caller, Sfx sfx, int entNum, ref int entChannel, float volume, ref int pitch, int flags, out int vol )
	{
		vol = 0;

		if ( sfx.Name[0] == '*' )
			entChannel = ChannelStream;

		if ( entChannel == ChannelStream && pitch != 100 )
		{
			Log.Info( $"Warning: pitch shift ignored on stream sound {sfx.Name}" );
			pitch = 100;
		}

		vol = (int)(volume * 255f);

		if ( vol > 255 )
		{
			Log.Info( $"{caller}: {sfx.Name} volume > 255" );
			vol = 255;
		}

		if ( (flags & (SoundStop | SoundChangeVolume | SoundChangePitch)) != 0 && (AlterChannel( entNum, entChannel, sfx, vol, pitch, flags ) || (flags & SoundStop) != 0) )
			return false;

		if ( pitch == 0 )
		{
			Log.Info( $"Warning: {caller} Ignored, called with pitch 0" );
			return false;
		}

		pitch = (int)((SysTimeScale + 1f) * 0.5f * pitch);

		if ( caller == StaticCaller ? sfx.Name[0] != '*' : entChannel != ChannelStream )
			return true;

		for ( var i = NumAmbients; i < _totalChannels; i++ )
		{
			if ( _channels[i].Sfx == sfx )
				return false;
		}

		return true;
	}

	private const string DynamicCaller = "S_StartDynamicSound";
	private const string StaticCaller = "S_StartStaticSound";

	public void StartDynamicSound( int entNum, int entChannel, Sfx sfx, Vector3 origin, float volume, float attenuation, int flags, int pitch )
	{
		if ( sfx is null || NoSoundCvar != 0f )
			return;

		if ( !PrepareStart( DynamicCaller, sfx, entNum, ref entChannel, volume, ref pitch, flags, out var vol ) )
			return;

		var viewEntity = ViewEntity;
		var firstToDie = -1;
		var lifeLeft = double.MaxValue;

		for ( var i = NumAmbients; i < NumAmbients + MaxDynamicChannels; i++ )
		{
			var check = _channels[i];

			if ( check.EntChannel == ChannelStream && check.Sfx is not null )
			{
				if ( entChannel == ChannelVoice )
					return;

				continue;
			}

			if ( entChannel != 0 && check.EntNum == entNum && (check.EntChannel == entChannel || entChannel == -1) )
			{
				firstToDie = i;
				break;
			}

			if ( check.EntNum == viewEntity && entNum != viewEntity && check.Sfx is not null )
				continue;

			if ( check.End - _time < lifeLeft )
			{
				lifeLeft = check.End - _time;
				firstToDie = i;
			}
		}

		if ( firstToDie == -1 )
			return;

		var channel = _channels[firstToDie];

		if ( channel.Sfx is not null )
			FreeChannel( channel );

		var sentence = sfx.Name[0] is '!' or '#';

		channel.Clear();
		channel.Origin = origin;
		channel.MasterVol = vol;
		channel.EntNum = entNum;
		channel.EntChannel = entChannel;
		channel.Pitch = pitch;
		channel.ISentence = -1;
		channel.DistMult = attenuation / NominalClipDistance;

		Spatialize( channel );

		if ( channel.LeftVol == 0 && channel.RightVol == 0 && entChannel != ChannelStream )
		{
			channel.Sfx = null;
			return;
		}

		if ( !StartChannel( channel, sfx, sentence, pitch ) )
			return;

		InitMouth( entNum, entChannel );

		if ( sentence || channel.Handle is null )
			return;

		for ( var i = NumAmbients; i < NumAmbients + MaxDynamicChannels; i++ )
		{
			var check = _channels[i];

			if ( check == channel || check.Sfx != sfx || check.Handle is null || check.Handle.Time != 0f )
				continue;

			var length = (int)(sfx.Duration * MixSpeed);
			var skip = Server.RandomLong( 0, (int)(MixSpeed * 0.1) );

			if ( length <= skip )
				skip = length - 1;

			channel.Handle.Time = (float)skip / MixSpeed;
			channel.End -= (double)skip / MixSpeed;
			return;
		}
	}

	public void StartStaticSound( int entNum, int entChannel, Sfx sfx, Vector3 origin, float volume, float attenuation, int flags, int pitch )
	{
		if ( sfx is null )
			return;

		if ( !PrepareStart( StaticCaller, sfx, entNum, ref entChannel, volume, ref pitch, flags, out var vol ) )
			return;

		var index = NumAmbients + MaxDynamicChannels;

		while ( index < _totalChannels && _channels[index].Sfx is not null )
			index++;

		if ( index == _totalChannels )
		{
			if ( _totalChannels == MaxChannels )
			{
				Log.Info( "total_channels == MAX_CHANNELS" );
				return;
			}

			_totalChannels++;
		}

		var channel = _channels[index];
		var sentence = sfx.Name[0] is '!' or '#';

		channel.Handle?.Stop();
		channel.Clear();
		channel.ISentence = -1;
		channel.Origin = origin;
		channel.MasterVol = vol;
		channel.DistMult = attenuation / NominalClipDistance;
		channel.EntNum = entNum;
		channel.Pitch = pitch;
		channel.EntChannel = entChannel;

		if ( !StartChannel( channel, sfx, sentence, pitch ) )
			return;

		Spatialize( channel );
	}

	private bool StartChannel( SfxChannel channel, Sfx sfx, bool sentence, int pitch )
	{
		if ( sentence )
		{
			if ( !VoxLoadSound( channel, sfx.Name.Length > 64 ? sfx.Name[1..64] : sfx.Name[1..] ) )
			{
				channel.Sfx = null;
				return false;
			}

			return channel.Sfx is not null;
		}

		if ( !LoadSound( sfx, channel.EntChannel == ChannelStream ) )
		{
			channel.Sfx = null;
			return false;
		}

		channel.Sfx = sfx;
		channel.Loop = sfx.Loop;

		if ( pitch != 100 )
			VoxMakeSingleWordSentence( channel, pitch );

		channel.PitchShift = channel.ISentence >= 0 && sfx.Bits == 8;

		PlayChannel( channel, sfx.File, sfx.Duration );

		return true;
	}

	private void PlayChannel( SfxChannel channel, SoundFile file, float length )
	{
		_mixer ??= FindMixer();

		var handle = Sound.PlayFile( file );

		if ( !handle.IsValid() )
		{
			FreeChannel( channel );
			return;
		}

		handle.TargetMixer = _mixer;
		handle.Falloff = LinearFalloff;

		channel.Handle = handle;
		channel.Length = length;
		channel.End = _time + (length / PitchScale( channel ));

		ApplyChannel( channel );
	}

	private static float PitchScale( SfxChannel channel )
	{
		return channel.PitchShift ? channel.Pitch / 100f : 1f;
	}

	private static Mixer FindMixer()
	{
		var mixer = Mixer.FindMixerByName( MixerName ) ?? Mixer.Master.AddChild();

		mixer.Name = MixerName;
		mixer.MaxVoices = MaxChannels;
		mixer.BlockingTags.Add( ScenePhysics.WorldTag );

		return mixer;
	}

	public void StopSound( int entNum, int entChannel )
	{
		for ( var i = NumAmbients; i < _totalChannels; i++ )
		{
			if ( _channels[i].EntNum == entNum && _channels[i].EntChannel == entChannel )
				FreeChannel( _channels[i] );
		}
	}

	private bool AlterChannel( int entNum, int entChannel, Sfx sfx, int volume, int pitch, int flags )
	{
		for ( var i = NumAmbients; i < _totalChannels; i++ )
		{
			var channel = _channels[i];

			if ( channel.EntNum != entNum || channel.EntChannel != entChannel )
				continue;

			if ( sfx.Name[0] == '!' ? channel.Sfx is null || channel.ISentence < 0 : channel.Sfx != sfx )
				continue;

			if ( (flags & SoundChangePitch) != 0 )
				channel.Pitch = pitch;

			if ( (flags & SoundChangeVolume) != 0 )
				channel.MasterVol = volume;

			if ( (flags & SoundStop) != 0 )
				FreeChannel( channel );

			return true;
		}

		return false;
	}

	private void FreeChannel( SfxChannel channel )
	{
		channel.Handle?.Stop();
		channel.Handle = null;

		if ( channel.ISentence >= 0 )
			_sentenceUsed[channel.ISentence] = false;

		var entNum = channel.EntNum;

		channel.ISentence = -1;
		channel.Sfx = null;

		if ( entNum > 0 && channel.EntChannel is ChannelVoice or ChannelStream && Client.Current.GetEntity( entNum ) is { } entity )
			entity.MouthOpen = 0;
	}

	private void InitMouth( int entNum, int entChannel )
	{
		if ( entChannel is not (ChannelVoice or ChannelStream) || entNum <= 0 || Client.Current.GetEntity( entNum ) is not { } entity )
			return;

		entity.MouthOpen = 0;
		entity.MouthSoundAverage = 0;
		entity.MouthSoundCount = 0;
	}

	private static int ViewEntity => Client.Current.ViewEntityIndex;

	private void Spatialize( SfxChannel channel )
	{
		if ( channel.EntNum == ViewEntity )
		{
			channel.LeftVol = channel.MasterVol;
			channel.RightVol = channel.MasterVol;
			return;
		}

		if ( channel.EntNum > 0 && Client.Current.GetCurrentEntity( channel.EntNum ) is { } entity && Client.Current.GetModelByIndex( entity.ModelIndex ) is { } model )
		{
			channel.Origin = entity.Origin;

			if ( model.Type == ModelType.Brush )
				channel.Origin += (model.Mins + model.Maxs) * 0.5f;
		}

		var source = channel.Origin - _listenerOrigin;
		var distance = MathLib.VectorNormalize( ref source );
		var dot = (source.y * _listenerRight.y) + (source.x * _listenerRight.x) + (source.z * _listenerRight.z);

		var rightScale = 1f + dot;
		var leftScale = 1f - dot;
		var scale = 1.0 - (float)(distance * channel.DistMult);

		channel.RightVol = (int)((float)(rightScale * scale) * channel.MasterVol);
		channel.LeftVol = (int)((float)(leftScale * scale) * channel.MasterVol);

		if ( channel.RightVol < 0 )
			channel.RightVol = 0;

		if ( channel.LeftVol < 0 )
			channel.LeftVol = 0;
	}

	private void ApplyChannel( SfxChannel channel )
	{
		var handle = channel.Handle;
		var local = channel.EntNum == ViewEntity;

		handle.Paused = Paused;
		handle.Volume = channel.MasterVol / 255f;
		handle.Pitch = PitchScale( channel );
		handle.SpacialBlend = local ? 0f : 1f;
		handle.DistanceAttenuation = !local && channel.DistMult > 0f;
		handle.OcclusionEnabled = Acoustics != 0f && handle.DistanceAttenuation && channel.Index < NumAmbients + MaxDynamicChannels;
		handle.AirAbsorption = handle.OcclusionEnabled;
		handle.Position = local ? _listenerOrigin : channel.Origin;

		if ( channel.DistMult > 0f )
			handle.Distance = 1f / channel.DistMult;
	}

	public void Update( Vector3 origin, Vector3 forward, Vector3 right, Vector3 up )
	{
		_listenerOrigin = origin;
		_listenerForward = forward;
		_listenerRight = right;
		_listenerUp = up;

		if ( !Paused )
			_time += RealTime.Delta;

		var simulation = Acoustics != 0f ? 1f : 0f;

		_mixer ??= FindMixer();
		_mixer.Volume = Math.Clamp( Volume, 0f, 1f );
		_mixer.Occlusion = simulation;
		_mixer.AirAbsorption = simulation;
		_mixer.Reverb = simulation;

		UpdateRoom();

		for ( var i = NumAmbients; i < _totalChannels; i++ )
		{
			var channel = _channels[i];

			if ( channel.Sfx is null )
				continue;

			Spatialize( channel );

			var handle = channel.Handle;

			if ( handle is null || !handle.IsValid() || handle.Finished )
			{
				FreeChannel( channel );
				continue;
			}

			if ( channel.LeftVol == 0 && channel.RightVol == 0 && !channel.Loop )
			{
				FreeChannel( channel );
				continue;
			}

			ApplyChannel( channel );

			if ( !Paused )
			{
				channel.End = _time + ((channel.Length - handle.Time) / Math.Max( handle.Pitch, 0.01f ));

				MoveMouth( channel );
			}
		}

		if ( ShowCvar == 0f )
			return;

		var total = 0;

		for ( var i = 0; i < _totalChannels; i++ )
		{
			var channel = _channels[i];

			if ( channel.Sfx is null || (channel.LeftVol == 0 && channel.RightVol == 0) )
				continue;

			Log.Info( $"{channel.LeftVol,3} {channel.RightVol,3} {channel.Sfx.Name}" );
			total++;
		}

		Log.Info( $"----({total})----" );
	}

	private void MoveMouth( SfxChannel channel )
	{
		if ( channel.EntNum <= 0 || channel.EntChannel is not (ChannelVoice or ChannelStream) || channel.Sfx.Bits != 8 )
			return;

		if ( Client.Current.GetEntity( channel.EntNum ) is not { } entity )
			return;

		entity.MouthOpen = (byte)Math.Clamp( channel.Handle.Amplitude * MouthScale, 0f, 255f );
	}
}
