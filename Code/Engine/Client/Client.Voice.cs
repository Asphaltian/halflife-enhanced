using System;

namespace GoldSrc;

public sealed class VoiceTransmitter : Voice
{
	[Sync( SyncFlags.FromHost )]
	public int ClientSlot { get; set; } = -1;

	[Sync( SyncFlags.FromHost )]
	public uint ListenerMask { get; set; }

	protected override bool ShouldHearVoice( Connection connection ) => Client.Current is { } client && client.ShouldHearVoice( this );
}

public enum VoiceTweakControl
{
	MicrophoneVolume,
	OtherSpeakerScale,
	MicBoost
}

partial class Client
{
	private const string VoiceAction = "voicerecord";
	private const float VoiceChannelStartDelay = 0.2f;
	private const float VoiceAckTime = 0.2f;
	private const int VoiceMaxSpeakingVolume = 32768;
	private const int VoiceSpeakingVolumeMask = 0xfe00;

	[ConVar( "voice_enable", ConVarFlags.Saved )]
	private static float VoiceEnable { get; set; } = 1f;

	public static float VoiceLoopback;

	[ConVar( "voice_scale", ConVarFlags.Saved )]
	private static float VoiceScale { get; set; } = 1f;

	[ConVar( "voice_avggain" )]
	private static float VoiceAvgGain { get; set; } = 0.5f;

	[ConVar( "voice_maxgain" )]
	private static float VoiceMaxGain { get; set; } = 5f;

	[ConVar( "voice_fadeouttime" )]
	private static float VoiceFadeOutTime { get; set; } = 0.1f;

	[ConVar( "voice_profile" )]
	private static float VoiceProfile { get; set; }

	[ConVar( "voice_showchannels" )]
	private static float VoiceShowChannels { get; set; }

	[ConVar( "voice_showincoming" )]
	private static float VoiceShowIncoming { get; set; }

	[ConVar( "voice_forcemicrecord", ConVarFlags.Saved )]
	private static float VoiceForceMicRecord { get; set; } = 1f;

	[ConVar( "voice_overdrive" )]
	private static float VoiceOverdrive { get; set; } = 2f;

	[ConVar( "voice_overdrivefadetime" )]
	private static float VoiceOverdriveFadeTime { get; set; } = 0.4f;

	[ConVar( "voice_dsound" )]
	private static float VoiceDSound { get; set; } = 1f;

	[ConVar( "voice_inputfromfile" )]
	private static float VoiceInputFromFile { get; set; }

	[ConVar( "voice_recordtofile" )]
	private static float VoiceRecordToFile { get; set; }

	private readonly float[] _voiceChannelDelay = new float[Server.MaxClientsLimit];
	private readonly bool[] _voiceChannelActive = new bool[Server.MaxClientsLimit];
	private readonly bool[] _voiceChannelHeard = new bool[Server.MaxClientsLimit];

	private VoiceTransmitter _voiceTweakTransmitter;
	private bool _voiceRecording;
	private bool _voiceTweakMode;
	private bool _voiceLocalTalkingAck;

	public bool ShouldHearVoice( VoiceTransmitter transmitter )
	{
		if ( VoiceEnable == 0f )
			return false;

		if ( !transmitter.IsProxy )
			return true;

		return !_voiceTweakMode && (transmitter.ListenerMask & (1u << (_playerNum & 31))) != 0;
	}

	private void VoiceStatus( int entityIndex, bool talking ) => _clientDll?.VoiceStatus( entityIndex, talking );

	public bool VoiceIsRecording => _voiceRecording && !_voiceTweakMode;

	public void VoiceRecordStart()
	{
		VoiceRecordStop();

		_voiceRecording = VoiceEnable != 0f;

		if ( _voiceRecording )
			VoiceStatus( -1, true );
	}

	public void VoiceRecordStop()
	{
		if ( _voiceRecording )
			VoiceStatus( -1, false );

		_voiceRecording = false;
	}

	public void VoiceRecordCommand( bool down )
	{
		if ( _state != ClientState.Active )
			return;

		if ( down )
			VoiceRecordStart();
		else if ( VoiceIsRecording )
			VoiceRecordStop();
	}

	private void VoiceEndChannel( int slot )
	{
		if ( _voiceChannelActive[slot] && _voiceChannelDelay[slot] <= 0f )
			VoiceStatus( slot + 1, false );

		_voiceChannelActive[slot] = false;
	}

	private void VoiceEndAllChannels()
	{
		for ( var i = 0; i < _voiceChannelActive.Length; i++ )
			VoiceEndChannel( i );
	}

	public bool StartVoiceTweakMode()
	{
		if ( _voiceTweakMode || VoiceEnable == 0f )
			return false;

		VoiceEndAllChannels();
		VoiceRecordStop();

		_voiceRecording = true;

		VoiceStatus( -1, true );

		_voiceTweakMode = true;

		return true;
	}

	public void EndVoiceTweakMode()
	{
		if ( !_voiceTweakMode )
			return;

		_voiceTweakMode = false;

		VoiceRecordStop();
	}

	public void SetVoiceTweakControl( VoiceTweakControl control, float value )
	{
		if ( control == VoiceTweakControl.OtherSpeakerScale )
			VoiceScale = value;
	}

	public float GetVoiceTweakControl( VoiceTweakControl control ) => control == VoiceTweakControl.OtherSpeakerScale ? VoiceScale : 1f;

	public int VoiceTweakSpeakingVolume { get; private set; }

	private VoiceTransmitter VoiceLocalTransmitter()
	{
		foreach ( var transmitter in Scene.GetAll<VoiceTransmitter>() )
		{
			if ( !transmitter.IsProxy && transmitter != _voiceTweakTransmitter )
				return transmitter;
		}

		if ( !_voiceTweakMode )
		{
			if ( _voiceTweakTransmitter.IsValid() )
				_voiceTweakTransmitter.GameObject.Destroy();

			_voiceTweakTransmitter = null;

			return null;
		}

		if ( !_voiceTweakTransmitter.IsValid() )
		{
			var gameObject = new GameObject( true, "voice tweak" );

			gameObject.NetworkMode = NetworkMode.Never;

			_voiceTweakTransmitter = gameObject.AddComponent<VoiceTransmitter>();
		}

		return _voiceTweakTransmitter;
	}

	private void VoiceFrame( float frameTime )
	{
		var local = VoiceLocalTransmitter();

		if ( VoiceEnable == 0f )
		{
			VoiceEndAllChannels();
			VoiceRecordStop();

			_voiceTweakMode = false;
			_voiceLocalTalkingAck = false;

			if ( local.IsValid() )
				local.IsListening = false;

			Input.SetAction( VoiceAction, false );

			return;
		}

		var listening = _voiceRecording && (_voiceTweakMode || _state == ClientState.Active);

		Input.SetAction( VoiceAction, listening );

		if ( local.IsValid() )
		{
			local.Mode = Voice.ActivateMode.Manual;
			local.PushToTalkInput = VoiceAction;
			local.IsListening = listening;
			local.WorldspacePlayback = false;
			local.LipSync = false;
			local.Loopback = _voiceTweakMode || VoiceLoopback != 0f;
			local.Volume = VoiceScale;
		}

		var heard = local.IsValid() && local.LastPlayed < VoiceAckTime;

		if ( _voiceTweakMode )
		{
			VoiceTweakSpeakingVolume = heard ? Math.Min( (int)(local.Amplitude * VoiceMaxSpeakingVolume), VoiceMaxSpeakingVolume ) & VoiceSpeakingVolumeMask : 0;
		}
		else if ( heard != _voiceLocalTalkingAck )
		{
			_voiceLocalTalkingAck = heard;

			VoiceStatus( -2, heard );
		}

		Array.Clear( _voiceChannelHeard );

		foreach ( var transmitter in Scene.GetAll<VoiceTransmitter>() )
		{
			if ( !transmitter.IsProxy )
				continue;

			transmitter.WorldspacePlayback = false;
			transmitter.LipSync = false;
			transmitter.Volume = VoiceScale;

			var slot = transmitter.ClientSlot;

			if ( slot < 0 || slot >= _voiceChannelActive.Length || _voiceTweakMode )
				continue;

			if ( transmitter.LastPlayed < VoiceChannelStartDelay + VoiceFadeOutTime )
				_voiceChannelHeard[slot] = true;
		}

		for ( var slot = 0; slot < _voiceChannelActive.Length; slot++ )
		{
			if ( !_voiceChannelHeard[slot] )
			{
				VoiceEndChannel( slot );
				continue;
			}

			if ( !_voiceChannelActive[slot] )
			{
				_voiceChannelActive[slot] = true;
				_voiceChannelDelay[slot] = VoiceChannelStartDelay;
			}

			var delay = _voiceChannelDelay[slot];

			_voiceChannelDelay[slot] = delay - frameTime;

			if ( delay > 0f && _voiceChannelDelay[slot] <= 0f )
				VoiceStatus( slot + 1, true );
		}
	}
}
