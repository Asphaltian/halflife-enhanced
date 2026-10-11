using System;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const float CamDistDelta = 1f;
	private const float CamAngleDelta = 2.5f;
	private const float CamMinDist = 30f;
	private const float CamAngleMove = 0.5f;

	private const int CamCommandToThirdPerson = 1;
	private const int CamCommandToFirstPerson = 2;

	[ConVar( "cam_command" )]
	private static float CamCommand { get; set; }

	[ConVar( "cam_snapto" )]
	private static float CamSnapTo { get; set; }

	[ConVar( "cam_idealyaw" )]
	private static float CamIdealYaw { get; set; } = 90f;

	[ConVar( "cam_idealpitch" )]
	private static float CamIdealPitch { get; set; }

	[ConVar( "cam_idealdist" )]
	private static float CamIdealDist { get; set; } = 64f;

	[ConVar( "cam_contain" )]
	private static float CamContain { get; set; }

	[ConVar( "c_maxpitch" )]
	private static float CamMaxPitch { get; set; } = 90f;

	[ConVar( "c_minpitch" )]
	private static float CamMinPitch { get; set; }

	[ConVar( "c_maxyaw" )]
	private static float CamMaxYaw { get; set; } = 135f;

	[ConVar( "c_minyaw" )]
	private static float CamMinYaw { get; set; } = -135f;

	[ConVar( "c_maxdistance" )]
	private static float CamMaxDistance { get; set; } = 200f;

	[ConVar( "c_mindistance" )]
	private static float CamMinDistance { get; set; } = 30f;

	private readonly KeyButton _camPitchUp = new();
	private readonly KeyButton _camPitchDown = new();
	private readonly KeyButton _camYawLeft = new();
	private readonly KeyButton _camYawRight = new();
	private readonly KeyButton _camIn = new();
	private readonly KeyButton _camOut = new();

	private Vector3 _camOfs;
	private bool _camMouseMove;
	private bool _camDistanceMove;
	private int _camMouseX;
	private int _camMouseY;

	public bool MouseInUse { get; private set; }

	public bool CamThirdPerson { get; private set; }

	private static float MoveToward( float cur, float goal )
	{
		if ( cur != goal )
		{
			if ( MathF.Abs( cur - goal ) > 180f )
			{
				if ( cur < goal )
					cur += 360f;
				else
					cur -= 360f;
			}

			if ( cur < goal )
			{
				if ( cur < goal - 1f )
					cur += (goal - cur) / 4f;
				else
					cur = goal;
			}
			else
			{
				if ( cur > goal + 1f )
					cur -= (cur - goal) / 4f;
				else
					cur = goal;
			}
		}

		if ( cur < 0f )
			cur += 360f;
		else if ( cur >= 360f )
			cur -= 360f;

		return cur;
	}

	public override void CamThink()
	{
		switch ( (int)CamCommand )
		{
			case CamCommandToThirdPerson:
				CamToThirdPerson();
				break;

			case CamCommandToFirstPerson:
				CamToFirstPerson();
				break;
		}

		if ( !CamThirdPerson )
			return;

		var pitch = CamIdealPitch;
		var yaw = CamIdealYaw;
		var dist = CamIdealDist;

		var centerX = Engine.WindowCenterX;
		var centerY = Engine.WindowCenterY;

		if ( _camMouseMove )
		{
			Engine.GetMousePosition( out _camMouseX, out _camMouseY );

			if ( !_camDistanceMove )
			{
				if ( _camMouseX > centerX )
				{
					if ( yaw < CamMaxYaw )
						yaw += CamAngleMove * ((_camMouseX - centerX) / 2);

					if ( yaw > CamMaxYaw )
						yaw = CamMaxYaw;
				}
				else if ( _camMouseX < centerX )
				{
					if ( yaw > CamMinYaw )
						yaw -= CamAngleMove * ((centerX - _camMouseX) / 2);

					if ( yaw < CamMinYaw )
						yaw = CamMinYaw;
				}

				if ( _camMouseY > centerY )
				{
					if ( pitch < CamMaxPitch )
						pitch += CamAngleMove * ((_camMouseY - centerY) / 2);

					if ( pitch > CamMaxPitch )
						pitch = CamMaxPitch;
				}
				else if ( _camMouseY < centerY )
				{
					if ( pitch > CamMinPitch )
						pitch -= CamAngleMove * ((centerY - _camMouseY) / 2);

					if ( pitch < CamMinPitch )
						pitch = CamMinPitch;
				}
			}
		}

		if ( KeyState( _camPitchUp ) != 0f )
			pitch += CamAngleDelta;
		else if ( KeyState( _camPitchDown ) != 0f )
			pitch -= CamAngleDelta;

		if ( KeyState( _camYawLeft ) != 0f )
			yaw -= CamAngleDelta;
		else if ( KeyState( _camYawRight ) != 0f )
			yaw += CamAngleDelta;

		if ( KeyState( _camIn ) != 0f )
		{
			dist -= CamDistDelta;

			if ( dist < CamMinDist )
			{
				pitch = 0f;
				yaw = 0f;
				dist = CamMinDist;
			}
		}
		else if ( KeyState( _camOut ) != 0f )
		{
			dist += CamDistDelta;
		}

		if ( _camDistanceMove )
		{
			if ( _camMouseY > centerY )
			{
				if ( dist < CamMaxDistance )
					dist += CamDistDelta * ((_camMouseY - centerY) / 2);

				if ( dist > CamMaxDistance )
					dist = CamMaxDistance;
			}
			else if ( _camMouseY < centerY )
			{
				if ( dist > CamMinDistance )
					dist -= CamDistDelta * ((centerY - _camMouseY) / 2);

				if ( dist < CamMinDistance )
					dist = CamMinDistance;
			}
		}

		CamIdealPitch = pitch;
		CamIdealYaw = yaw;
		CamIdealDist = dist;

		var camAngles = _camOfs;
		var viewAngles = Engine.ViewAngles;

		if ( CamSnapTo != 0f )
		{
			camAngles.y = CamIdealYaw + viewAngles.y;
			camAngles.x = CamIdealPitch + viewAngles.x;
		}
		else
		{
			if ( camAngles.y - viewAngles.y != CamIdealYaw )
				camAngles.y = MoveToward( camAngles.y, CamIdealYaw + viewAngles.y );

			if ( camAngles.x - viewAngles.x != CamIdealPitch )
				camAngles.x = MoveToward( camAngles.x, CamIdealPitch + viewAngles.x );
		}

		_camOfs = new Vector3( camAngles.x, camAngles.y, dist );
	}

	private bool CamKeyCommand( string name, bool down )
	{
		switch ( name )
		{
			case "cammousemove":
				if ( down )
					CamStartMouseMove();
				else
					CamEndMouseMove();

				return true;

			case "camdistance":
				if ( down )
					CamStartDistance();
				else
					CamEndDistance();

				return true;
		}

		return false;
	}

	private void CamToThirdPerson()
	{
		if ( Engine.MaxClients > 1 )
			return;

		var viewAngles = Engine.ViewAngles;

		if ( !CamThirdPerson )
		{
			CamThirdPerson = true;

			_camOfs.y = viewAngles.y;
			_camOfs.x = viewAngles.x;
			_camOfs.z = CamMinDist;
		}

		CamCommand = 0f;
	}

	public void CamToFirstPerson()
	{
		CamThirdPerson = false;

		CamCommand = 0f;
	}

	private void CamStartMouseMove()
	{
		if ( CamThirdPerson )
		{
			if ( !_camMouseMove )
			{
				_camMouseMove = true;
				MouseInUse = true;

				Engine.GetMousePosition( out _camMouseX, out _camMouseY );
			}
		}
		else
		{
			_camMouseMove = false;
			MouseInUse = false;
		}
	}

	private void CamEndMouseMove()
	{
		_camMouseMove = false;
		MouseInUse = false;
	}

	private void CamStartDistance()
	{
		if ( CamThirdPerson )
		{
			if ( !_camDistanceMove )
			{
				_camDistanceMove = true;
				_camMouseMove = true;
				MouseInUse = true;

				Engine.GetMousePosition( out _camMouseX, out _camMouseY );
			}
		}
		else
		{
			_camDistanceMove = false;
			_camMouseMove = false;
			MouseInUse = false;
		}
	}

	private void CamEndDistance()
	{
		_camDistanceMove = false;
		_camMouseMove = false;
		MouseInUse = false;
	}

	public override bool IsThirdPerson() => CamThirdPerson || (User1 != 0 && User2 == Engine.GetLocalPlayer()?.Index);

	[ConCmd( "thirdperson" )]
	private static void ThirdPersonCommand() => (Loaded as HalfLifeClientDll)?.CamToThirdPerson();

	[ConCmd( "firstperson" )]
	private static void FirstPersonCommand() => (Loaded as HalfLifeClientDll)?.CamToFirstPerson();

	[ConCmd( "snapto" )]
	private static void SnapToCommand() => CamSnapTo = CamSnapTo == 0f ? 1f : 0f;
}
