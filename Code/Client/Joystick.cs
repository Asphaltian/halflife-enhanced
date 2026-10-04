using System;
using GoldSrc;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const int JoyAbsoluteAxis = 0x00000000;
	private const int JoyRelativeAxis = 0x00000010;
	private const int JoyMaxAxes = 6;
	private const int JoyAxisX = 0;
	private const int JoyAxisY = 1;
	private const int JoyAxisZ = 2;
	private const int JoyAxisR = 3;
	private const int JoyAxisU = 4;
	private const int JoyAxisV = 5;

	private const int AxisNada = 0;
	private const int AxisForward = 1;
	private const int AxisLook = 2;
	private const int AxisSide = 3;
	private const int AxisTurn = 4;

	private const string DefaultJoyName = "joystick";

	[ConVar( "joystick", ConVarFlags.Saved )]
	public static float InJoystick { get; set; }

	[ConVar( "joyname" )]
	public static string JoyName { get; set; } = DefaultJoyName;

	[ConVar( "joyadvanced" )]
	public static float JoyAdvanced { get; set; }

	[ConVar( "joyadvaxisx" )]
	public static float JoyAdvAxisX { get; set; }

	[ConVar( "joyadvaxisy" )]
	public static float JoyAdvAxisY { get; set; }

	[ConVar( "joyadvaxisz" )]
	public static float JoyAdvAxisZ { get; set; }

	[ConVar( "joyadvaxisr" )]
	public static float JoyAdvAxisR { get; set; }

	[ConVar( "joyadvaxisu" )]
	public static float JoyAdvAxisU { get; set; }

	[ConVar( "joyadvaxisv" )]
	public static float JoyAdvAxisV { get; set; }

	[ConVar( "joysupported" )]
	public static float JoySupported { get; set; } = 1f;

	[ConVar( "joyforwardthreshold" )]
	public static float JoyForwardThreshold { get; set; } = 0.15f;

	[ConVar( "joysidethreshold" )]
	public static float JoySideThreshold { get; set; } = 0.15f;

	[ConVar( "joypitchthreshold" )]
	public static float JoyPitchThreshold { get; set; } = 0.15f;

	[ConVar( "joyyawthreshold" )]
	public static float JoyYawThreshold { get; set; } = 0.15f;

	[ConVar( "joyforwardsensitivity" )]
	public static float JoyForwardSensitivity { get; set; } = -1f;

	[ConVar( "joysidesensitivity" )]
	public static float JoySideSensitivity { get; set; } = -1f;

	[ConVar( "joypitchsensitivity" )]
	public static float JoyPitchSensitivity { get; set; } = 1f;

	[ConVar( "joyyawsensitivity" )]
	public static float JoyYawSensitivity { get; set; } = -1f;

	[ConVar( "joywwhack1" )]
	public static float JoyWingManHack1 { get; set; }

	[ConVar( "joywwhack2" )]
	public static float JoyWingManHack2 { get; set; }

	private readonly int[] _joyAxisMap = new int[JoyMaxAxes];
	private readonly int[] _joyControlMap = new int[JoyMaxAxes];
	private readonly int[] _joyRawValue = new int[JoyMaxAxes];

	private bool _joyOpen;
	private bool _joyAvailable;
	private bool _joyAdvancedInit;
	private float _joyLastCheck;

	[ConCmd( "joyadvancedupdate" )]
	public static void JoyAdvancedUpdateCommand()
	{
		(Loaded as HalfLifeClientDll)?.JoyAdvancedUpdate();
	}

	private void StartupJoystick()
	{
		var time = (float)Host.RealTime;

		if ( _joyLastCheck > 0f && time - _joyLastCheck < 1f )
			return;

		_joyLastCheck = time;

		if ( Input.ControllerCount > 0 )
		{
			if ( _joyOpen )
				return;

			_joyOpen = true;

			Log.Info( "joystick found \n\n" );

			_joyAvailable = true;
			_joyAdvancedInit = false;
		}
		else
		{
			_joyOpen = false;

			if ( _joyAvailable )
			{
				_joyAvailable = false;

				Log.Trace( "joystick not found -- driver not present\n\n" );
			}
		}
	}

	private static int RawAxisValue( InputAnalog analog )
	{
		return (int)MathF.Round( ((Input.GetAnalog( analog ) + 1f) * 32767.5f) - 32768f );
	}

	private static int RawValuePointer( int axis )
	{
		return axis switch
		{
			JoyAxisY => RawAxisValue( InputAnalog.LeftStickY ),
			JoyAxisZ => RawAxisValue( InputAnalog.RightStickX ),
			JoyAxisR => RawAxisValue( InputAnalog.RightStickY ),
			_ => RawAxisValue( InputAnalog.LeftStickX )
		};
	}

	private void JoyAdvancedUpdate()
	{
		for ( var i = 0; i < JoyMaxAxes; i++ )
		{
			_joyAxisMap[i] = AxisNada;
			_joyControlMap[i] = JoyAbsoluteAxis;
			_joyRawValue[i] = RawValuePointer( i );
		}

		if ( JoyAdvanced == 0f )
		{
			_joyAxisMap[JoyAxisX] = AxisTurn;
			_joyAxisMap[JoyAxisY] = AxisForward;
			return;
		}

		if ( JoyName != DefaultJoyName )
			Log.Info( $"\n{JoyName} configured\n\n" );

		SetJoyAxis( JoyAxisX, JoyAdvAxisX );
		SetJoyAxis( JoyAxisY, JoyAdvAxisY );
		SetJoyAxis( JoyAxisZ, JoyAdvAxisZ );
		SetJoyAxis( JoyAxisR, JoyAdvAxisR );
		SetJoyAxis( JoyAxisU, JoyAdvAxisU );
		SetJoyAxis( JoyAxisV, JoyAdvAxisV );
	}

	private void SetJoyAxis( int axis, float value )
	{
		var bits = (int)(uint)value;

		_joyAxisMap[axis] = bits & 0x0000000f;
		_joyControlMap[axis] = bits & JoyRelativeAxis;
	}

	private void JoyCommands()
	{
		if ( !_joyAvailable )
			return;

		for ( var i = 0; i < JoyMaxAxes; i++ )
			_joyRawValue[i] = RawValuePointer( i );
	}

	private void JoyMove( float frameTime, ref UserCmd cmd, ref Vector3 viewAngles )
	{
		if ( !_joyAdvancedInit )
		{
			JoyAdvancedUpdate();
			_joyAdvancedInit = true;
		}

		StartupJoystick();

		if ( !_joyAvailable || InJoystick == 0f )
			return;

		var speed = (_speed.State & 1) != 0 ? MoveSpeedKey : 1f;
		var angleSpeed = speed * frameTime;

		for ( var i = 0; i < JoyMaxAxes; i++ )
		{
			var axisValue = (float)_joyRawValue[i];

			if ( JoyWingManHack2 != 0f && _joyAxisMap[i] == AxisTurn )
			{
				var spin = (float)(300.0 * Math.Pow( Math.Abs( (int)axisValue ) / 800.0, 1.3 ));

				if ( spin > 14000f )
					spin = 14000f;

				axisValue = axisValue > 0f ? spin : -spin;
			}

			axisValue = (float)(axisValue / 32768.0);

			switch ( _joyAxisMap[i] )
			{
				case AxisForward:
					if ( JoyAdvanced == 0f && (_joyLook.State & 1) != 0 )
					{
						if ( MathF.Abs( axisValue ) > JoyPitchThreshold )
						{
							if ( MousePitch < 0f )
								viewAngles.x -= axisValue * JoyPitchSensitivity * angleSpeed * PitchSpeed;
							else
								viewAngles.x += axisValue * JoyPitchSensitivity * angleSpeed * PitchSpeed;

							StopPitchDrift();
						}
						else if ( LookSpring == 0f )
						{
							StopPitchDrift();
						}
					}
					else if ( MathF.Abs( axisValue ) > JoyForwardThreshold )
					{
						cmd.ForwardMove += axisValue * JoyForwardSensitivity * speed * ForwardSpeed;
					}

					break;

				case AxisSide:
					if ( MathF.Abs( axisValue ) > JoySideThreshold )
						cmd.SideMove += axisValue * JoySideSensitivity * speed * SideSpeed;

					break;

				case AxisTurn:
					if ( (_strafe.State & 1) != 0 || (LookStrafe != 0f && (_joyLook.State & 1) != 0) )
					{
						if ( MathF.Abs( axisValue ) > JoySideThreshold )
							cmd.SideMove -= axisValue * JoySideSensitivity * speed * SideSpeed;
					}
					else if ( MathF.Abs( axisValue ) > JoyYawThreshold )
					{
						if ( _joyControlMap[i] == JoyAbsoluteAxis )
							viewAngles.y += axisValue * JoyYawSensitivity * angleSpeed * YawSpeed;
						else
							viewAngles.y += axisValue * JoyYawSensitivity * speed * 180f;
					}

					break;

				case AxisLook:
					if ( (_joyLook.State & 1) != 0 )
					{
						if ( MathF.Abs( axisValue ) > JoyPitchThreshold )
						{
							if ( _joyControlMap[i] == JoyAbsoluteAxis )
								viewAngles.x += axisValue * JoyPitchSensitivity * angleSpeed * PitchSpeed;
							else
								viewAngles.x += axisValue * JoyPitchSensitivity * speed * 180f;

							StopPitchDrift();
						}
						else if ( LookSpring == 0f )
						{
							StopPitchDrift();
						}
					}

					break;
			}
		}

		if ( viewAngles.x > PitchDown )
			viewAngles.x = PitchDown;

		if ( viewAngles.x < -PitchUp )
			viewAngles.x = -PitchUp;
	}
}
