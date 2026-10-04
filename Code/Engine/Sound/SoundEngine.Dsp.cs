using System;
using Sandbox.Audio;

namespace GoldSrc;

partial class SoundEngine
{
	[ConVar( "room_delay" )]
	private static float RoomDelay { get; set; } = 0f;

	[ConVar( "room_feedback" )]
	private static float RoomFeedback { get; set; } = 0.2f;

	[ConVar( "room_dlylp" )]
	private static float RoomDelayLowpass { get; set; } = 1f;

	[ConVar( "room_size" )]
	private static float RoomSize { get; set; } = 0f;

	[ConVar( "room_refl" )]
	private static float RoomReflect { get; set; } = 0.7f;

	[ConVar( "room_rvblp" )]
	private static float RoomReverbLowpass { get; set; } = 1f;

	[ConVar( "room_left" )]
	private static float RoomLeft { get; set; } = 0f;

	[ConVar( "room_lp" )]
	private static float RoomLowpass { get; set; } = 0f;

	[ConVar( "room_mod" )]
	private static float RoomMod { get; set; } = 0f;

	[ConVar( "waterroom_type" )]
	private static float WaterRoomType { get; set; } = 14f;

	[ConVar( "room_off", ConVarFlags.Saved )]
	private static float RoomOff { get; set; } = 0f;

	private static readonly float[,] RoomPresets =
	{
		{ 0f, 0f, 0f, 0f, 1f, 0f, 0f, 2f, 0f },
		{ 0f, 0f, 0f, 0f, 1f, 0.065f, 0.1f, 0f, 0.01f },
		{ 0f, 0f, 0f, 0f, 1f, 0.02f, 0.75f, 0f, 0.01f },
		{ 0f, 0f, 0f, 0f, 1f, 0.03f, 0.78f, 0f, 0.02f },
		{ 0f, 0f, 0f, 0f, 1f, 0.06f, 0.77f, 0f, 0.03f },
		{ 0f, 0f, 0.05f, 0.85f, 1f, 0.008f, 0.96f, 2f, 0.01f },
		{ 0f, 0f, 0.05f, 0.88f, 1f, 0.01f, 0.98f, 2f, 0.02f },
		{ 0f, 0f, 0.05f, 0.92f, 1f, 0.015f, 0.995f, 2f, 0.04f },
		{ 0f, 0f, 0.05f, 0.84f, 1f, 0f, 0f, 2f, 0.012f },
		{ 0f, 0f, 0.05f, 0.9f, 1f, 0f, 0f, 2f, 0.008f },
		{ 0f, 0f, 0.05f, 0.95f, 1f, 0f, 0f, 2f, 0.004f },
		{ 0f, 0f, 0.05f, 0.7f, 0f, 0f, 0f, 2f, 0.012f },
		{ 0f, 0f, 0.055f, 0.78f, 0f, 0f, 0f, 2f, 0.008f },
		{ 0f, 0f, 0.05f, 0.86f, 0f, 0f, 0f, 2f, 0.002f },
		{ 1f, 0f, 0f, 0f, 1f, 0f, 0f, 2f, 0.01f },
		{ 1f, 0f, 0f, 0f, 1f, 0.06f, 0.85f, 2f, 0.02f },
		{ 1f, 0f, 0f, 0f, 1f, 0.2f, 0.6f, 2f, 0.05f },
		{ 0f, 0f, 0.05f, 0.8f, 1f, 0f, 0.48f, 2f, 0.016f },
		{ 0f, 0f, 0.06f, 0.9f, 1f, 0f, 0.52f, 2f, 0.01f },
		{ 0f, 0f, 0.07f, 0.94f, 1f, 0.3f, 0.6f, 2f, 0.008f },
		{ 0f, 0f, 0f, 0f, 1f, 0.3f, 0.42f, 2f, 0f },
		{ 0f, 0f, 0f, 0f, 1f, 0.35f, 0.48f, 2f, 0f },
		{ 0f, 0f, 0f, 0f, 1f, 0.38f, 0.6f, 2f, 0f },
		{ 0f, 0f, 0.05f, 0.9f, 1f, 0.2f, 0.28f, 0f, 0f },
		{ 0f, 0f, 0.07f, 0.9f, 1f, 0.3f, 0.4f, 0f, 0f },
		{ 0f, 0f, 0.09f, 0.9f, 1f, 0.35f, 0.5f, 0f, 0f },
		{ 0f, 1f, 0.01f, 0.9f, 0f, 0f, 0f, 2f, 0.05f },
		{ 0f, 0f, 0f, 0f, 1f, 0.009f, 0.999f, 2f, 0.04f },
		{ 0f, 0f, 0.001f, 0.999f, 0f, 0.2f, 0.8f, 2f, 0.05f }
	};

	private static readonly string[] RoomPresetCvars = ["room_lp", "room_mod", "room_size", "room_refl", "room_rvblp", "room_delay", "room_feedback", "room_dlylp", "room_left"];

	[SkipHotload]
	private static readonly string[] RoomEffectNames =
	[
		null,
		null,
		"metallic.small",
		"metallic.medium",
		"metallic.large",
		"tunnel.small",
		"tunnel.medium",
		"tunnel.large",
		"chamber.small",
		"chamber.medium",
		"chamber.large",
		"brite.small",
		"brite.medium",
		"brite.large",
		"water.small",
		"water.medium",
		"water.large",
		"concrete.small",
		"concrete.medium",
		"concrete.large",
		"outside.small",
		"outside.medium",
		"outside.large",
		"cavern.small",
		"cavern.medium",
		"cavern.large",
		"weird.1",
		"weird.2",
		"weird.3"
	];

	private float _roomTypePrevious = -1f;
	private string _roomEffect;
	private DspProcessor _roomProcessor;

	private void UpdateRoom()
	{
		string effect = null;

		if ( RoomOff == 0f )
		{
			var roomType = Cvar.GetFloat( "room_type" );

			if ( Client.Current is { WaterLevel: > 2 } )
				roomType = WaterRoomType;

			var preset = (uint)roomType;

			if ( roomType != _roomTypePrevious )
			{
				_roomTypePrevious = roomType;

				if ( preset < RoomPresets.GetLength( 0 ) )
				{
					for ( var i = 0; i < RoomPresetCvars.Length; i++ )
						Cvar.SetValue( RoomPresetCvars[i], RoomPresets[preset, i] );
				}
			}

			if ( preset < RoomEffectNames.Length )
				effect = RoomEffectNames[preset];
		}

		if ( effect == _roomEffect )
			return;

		_roomEffect = effect;

		if ( _roomProcessor is not null )
		{
			_mixer.RemoveProcessor( _roomProcessor );
			_roomProcessor = null;
		}

		if ( effect is null )
			return;

		_roomProcessor = new DspProcessor( effect );
		_mixer.AddProcessor( _roomProcessor );
	}
}
