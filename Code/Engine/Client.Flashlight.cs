using System;

namespace GoldSrc;

partial class Client
{
	private const string FlashlightCookie = "textures/lightcookies/lightcookies_01.vtex";
	private const float FlashlightFov = 45f;
	private const float FlashlightFar = 750f;
	private const float FlashlightOffsetUp = -20f;
	private const float FlashlightSwing = -12f;
	private const float FlashlightLinearAttenuation = 0.00725f;
	private const float FlashlightDistCutoff = 128f;
	private const float FlashlightDistDrag = 0.2f;
	private const float FlashlightDragRate = 100f;
	private const float FlashlightMaxCone = 60f;

	[ConVar( "r_flashlight", ConVarFlags.Saved )]
	public static float SourceFlashlight { get; set; }

	private SceneSpotLight _flashlight;
	private float _flashlightCone;

	private void FlashlightOff()
	{
		_flashlight?.Delete();
		_flashlight = null;
	}

	private PmTrace FlashlightTrace( Vector3 start, Vector3 end )
	{
		_pmove.UseHull = 2;

		return _pmove.PlayerTrace( start, end, TraceFlags.StudioBox, -1 );
	}

	private void UpdateFlashlight( Vector3 eye )
	{
		MathLib.AngleVectors( _viewAngles, out var forward, out _, out var up );

		var offsetUp = FlashlightOffsetUp;
		var swing = eye + (forward * FlashlightSwing);

		if ( swing.z > eye.z )
			offsetUp += swing.z - eye.z;

		var origin = eye + (up * offsetUp);

		if ( _pmove.MoveType == MoveType.Fly || FlashlightTrace( eye, origin ).Fraction != 1f )
			origin = eye;

		var target = eye + (forward * FlashlightFar);
		var direction = (target - origin).Normal;

		up = (up - (direction * Vector3.Dot( direction, up ))).Normal;

		var distance = (FlashlightTrace( origin, target ).EndPos - origin).Length;
		var cone = FlashlightFov * 0.5f;

		if ( distance < FlashlightDistCutoff )
			cone = MathF.Min( MathF.Atan2( FlashlightDistCutoff * MathF.Tan( cone * (MathF.PI / 180f) ), distance ) * (180f / MathF.PI), FlashlightMaxCone );

		if ( _flashlight is null )
		{
			_flashlightCone = cone;

			_flashlight = new SceneSpotLight( Scene.SceneWorld, origin, Color.White )
			{
				Radius = FlashlightFar,
				ConstantAttenuation = 0f,
				LinearAttenuation = FlashlightLinearAttenuation,
				QuadraticAttenuation = 0f,
				FallOff = 1f,
				ShadowsEnabled = true,
				LightCookie = Texture.Load( FlashlightCookie )
			};
		}

		_flashlightCone += (cone - _flashlightCone) * (1f - MathF.Pow( 1f - FlashlightDistDrag, RealTime.Delta * FlashlightDragRate ));

		_flashlight.ConeInner = _flashlightCone;
		_flashlight.ConeOuter = _flashlightCone;
		_flashlight.Position = origin;
		_flashlight.Rotation = Rotation.LookAt( direction, up );
	}
}
