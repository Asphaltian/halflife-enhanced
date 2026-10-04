using System;
using GoldSrc;

namespace HalfLife;

public sealed partial class HalfLifeClientDll : ClientDll
{
	[ConVar( "lookstrafe", ConVarFlags.Saved )]
	public static float LookStrafe { get; set; }

	[ConVar( "lookspring", ConVarFlags.Saved )]
	public static float LookSpring { get; set; }

	[ConVar( "cl_anglespeedkey" )]
	public static float AngleSpeedKey { get; set; } = 0.67f;

	[ConVar( "cl_yawspeed" )]
	public static float YawSpeed { get; set; } = 210f;

	[ConVar( "cl_pitchspeed" )]
	public static float PitchSpeed { get; set; } = 225f;

	[ConVar( "cl_upspeed" )]
	public static float UpSpeed { get; set; } = 320f;

	[ConVar( "cl_forwardspeed", ConVarFlags.Saved )]
	public static float ForwardSpeed { get; set; } = 400f;

	[ConVar( "cl_backspeed", ConVarFlags.Saved )]
	public static float BackSpeed { get; set; } = 400f;

	[ConVar( "cl_sidespeed" )]
	public static float SideSpeed { get; set; } = 400f;

	[ConVar( "cl_movespeedkey" )]
	public static float MoveSpeedKey { get; set; } = 0.3f;

	[ConVar( "cl_pitchup" )]
	public static float PitchUp { get; set; } = 89f;

	[ConVar( "cl_pitchdown" )]
	public static float PitchDown { get; set; } = 89f;

	[ConVar( "m_pitch", ConVarFlags.Saved )]
	public static float MousePitch { get; set; } = 0.022f;

	[ConVar( "m_yaw", ConVarFlags.Saved )]
	public static float MouseYaw { get; set; } = 0.022f;

	[ConVar( "m_forward", ConVarFlags.Saved )]
	public static float MouseForward { get; set; } = 1f;

	[ConVar( "m_side", ConVarFlags.Saved )]
	public static float MouseSide { get; set; } = 0.8f;

	[ConVar( "m_filter", ConVarFlags.Saved )]
	public static float MouseFilter { get; set; }

	[ConVar( "m_customaccel", ConVarFlags.Saved )]
	public static float MouseCustomAccel { get; set; }

	[ConVar( "m_customaccel_scale", ConVarFlags.Saved )]
	public static float MouseCustomAccelScale { get; set; } = 0.04f;

	[ConVar( "m_customaccel_max", ConVarFlags.Saved )]
	public static float MouseCustomAccelMax { get; set; }

	[ConVar( "m_customaccel_exponent", ConVarFlags.Saved )]
	public static float MouseCustomAccelExponent { get; set; } = 1f;

	private const float MouseDegrees = 0.022f;
	private const float MaxSensitivity = 10000f;
	private const float MinSensitivity = 0.01f;
	private const float MinAcceleratedSensitivityMax = 0.0001f;
	private const int MaxBindingLength = 63;

	private static int _impulse;

	[ConCmd( "impulse" )]
	public static void Impulse( int impulse )
	{
		_impulse = impulse;
	}

	[ConCmd( "centerview" )]
	public static void CenterView()
	{
		(Loaded as HalfLifeClientDll)?.StartPitchDrift();
	}

	[ConCmd( "force_centerview" )]
	public static void ForceCenterView()
	{
		if ( Loaded is HalfLifeClientDll { MouseInUse: false } client )
			client.Engine.ViewAngles.x = 0f;
	}

	public override void Init()
	{
		HookEvents();

		_hud = new Hud( this );
		_hud.Init();

		StartupJoystick();

		Cmd.AddText( "+mlook\n" );
		Cmd.AddText( "+jlook\n" );
		Cmd.AddText( "exec controller.cfg\n" );
	}

	private Hud _hud;

	public Hud Hud => _hud;

	private sealed class KeyButton
	{
		public int Down0;
		public int Down1;
		public int State;
	}

	private readonly KeyButton _mouseLook = new();
	private readonly KeyButton _keyLook = new();
	private readonly KeyButton _joyLook = new();
	private readonly KeyButton _left = new();
	private readonly KeyButton _right = new();
	private readonly KeyButton _forward = new();
	private readonly KeyButton _back = new();
	private readonly KeyButton _lookUp = new();
	private readonly KeyButton _lookDown = new();
	private readonly KeyButton _moveLeft = new();
	private readonly KeyButton _moveRight = new();
	private readonly KeyButton _strafe = new();
	private readonly KeyButton _speed = new();
	private readonly KeyButton _use = new();
	private readonly KeyButton _jump = new();
	private readonly KeyButton _attack = new();
	private readonly KeyButton _attack2 = new();
	private readonly KeyButton _up = new();
	private readonly KeyButton _down = new();
	private readonly KeyButton _duck = new();
	private readonly KeyButton _reload = new();
	private readonly KeyButton _alt1 = new();
	private readonly KeyButton _score = new();
	private readonly KeyButton _break = new();
	private readonly KeyButton _graph = new();

	private Vector3 _oldAngles;
	private float _mouseSideMove;
	private float _oldMouseX;
	private float _oldMouseY;
	private float _mouseForwardMove;
	private float _mouseUpMove;

	public override bool KeyCommand( string name, bool down )
	{
		var key = name switch
		{
			"moveup" => _up,
			"movedown" => _down,
			"left" => _left,
			"right" => _right,
			"forward" => _forward,
			"back" => _back,
			"lookup" => _lookUp,
			"lookdown" => _lookDown,
			"strafe" => _strafe,
			"moveleft" => _moveLeft,
			"moveright" => _moveRight,
			"speed" => _speed,
			"attack" => _attack,
			"attack2" => _attack2,
			"use" => _use,
			"jump" => _jump,
			"klook" => _keyLook,
			"mlook" => _mouseLook,
			"jlook" => _joyLook,
			"duck" => _duck,
			"reload" => _reload,
			"alt1" => _alt1,
			"score" => _score,
			"showscores" => _score,
			"graph" => _graph,
			"break" => _break,
			"campitchup" => _camPitchUp,
			"campitchdown" => _camPitchDown,
			"camyawleft" => _camYawLeft,
			"camyawright" => _camYawRight,
			"camin" => _camIn,
			"camout" => _camOut,
			_ => null
		};

		if ( key is null )
			return CamKeyCommand( name, down ) || HudKeyCommand( name, down );

		if ( down )
		{
			KeyDown( key );

			var button = name switch
			{
				"forward" => InButtons.Forward,
				"back" => InButtons.Back,
				"moveleft" => InButtons.MoveLeft,
				"moveright" => InButtons.MoveRight,
				"attack2" => InButtons.Attack2,
				"use" => InButtons.Use,
				"jump" => InButtons.Jump,
				"duck" => InButtons.Duck,
				"attack" => InButtons.Attack,
				_ => default
			};

			if ( button != default )
				_hud.Spectator.HandleButtonsDown( (int)button );

			if ( key == _score )
				_hud.ViewPort?.ShowScoreBoard();

			return true;
		}

		KeyUp( key );

		switch ( name )
		{
			case "forward":
				_hud.Spectator.HandleButtonsUp( (int)InButtons.Forward );
				break;

			case "back":
				_hud.Spectator.HandleButtonsUp( (int)InButtons.Back );
				break;

			case "moveleft":
				_hud.Spectator.HandleButtonsUp( (int)InButtons.MoveLeft );
				break;

			case "moveright":
				_hud.Spectator.HandleButtonsUp( (int)InButtons.MoveRight );
				break;
		}

		if ( key == _score )
			_hud.ViewPort?.HideScoreBoard();

		if ( key == _mouseLook && (_mouseLook.State & 1) == 0 && LookSpring != 0f )
			StartPitchDrift();

		return true;
	}

	private bool HudKeyCommand( string name, bool down )
	{
		if ( name != "commandmenu" )
			return false;

		if ( down )
			_hud.ViewPort?.ShowCommandMenu( _hud.ViewPort.StandardMenu );
		else
			_hud.ViewPort?.InputSignalHideCommandMenu();

		return true;
	}

	[ConCmd( "ForceCloseCommandMenu" )]
	private static void ForceCloseCommandMenuCommand() => (ClientDll.Loaded as HalfLifeClientDll)?._hud.ViewPort?.HideCommandMenu();

	[ConCmd( "special" )]
	private static void InputPlayerSpecialCommand() => (ClientDll.Loaded as HalfLifeClientDll)?._hud.ViewPort?.InputPlayerSpecial();

	[ConCmd( "togglebrowser" )]
	private static void ToggleServerBrowserCommand() => (ClientDll.Loaded as HalfLifeClientDll)?._hud.ViewPort?.ToggleServerBrowser();

	public override bool KeyEvent( bool down, int keyNum, string currentBinding )
	{
		if ( _hud.ViewPort is { } viewPort )
			return viewPort.KeyInput( down, keyNum, currentBinding ) != 0;

		return true;
	}

	public override bool MouseEvent() => !MouseInUse && !VisibleMouse;

	public override int? FindKey( string name )
	{
		return name switch
		{
			"in_graph" => _graph.State,
			"in_mlook" => _mouseLook.State,
			"in_jlook" => _joyLook.State,
			_ => null
		};
	}

	public override void DirectorMessage( byte[] data ) => _hud.Spectator.DirectorMessage( data );

	public override void ChatInputPosition( ref int x, ref int y )
	{
		if ( User1 == 0 && !Engine.IsSpectateOnly )
			return;

		if ( HudSpectator.Pip == HudSpectator.InsetOff )
			y = (int)VguiUtil.YResHd( SpectatorPanel.PanelHeight );
		else
			y = (int)VguiUtil.YResHd( _hud.Spectator.OverviewData.InsetWindowHeight + 5 );
	}

	private void VGuiStartup()
	{
		var root = Engine.VGuiGetPanel();

		root.SetBgColor( 128, 128, 0, 0 );
		root.SetLayout( new GoldSrc.Vgui1.BorderLayout( 0 ) );

		if ( _hud.ViewPort is { } viewPort )
		{
			if ( viewPort.GetWide() == root.GetWide() && viewPort.GetTall() == root.GetTall() )
			{
				viewPort.Initialize();
				return;
			}

			var playerClass = PlayerClass;
			var teamNumber = TeamNumber;

			viewPort.SetParent( null );

			var resized = new TeamFortressViewport( _hud, 0, 0, root.GetWide(), root.GetTall() );

			resized.SetParent( root );
			resized.RestoreFrom( viewPort );

			PlayerClass = playerClass;
			TeamNumber = teamNumber;

			return;
		}

		viewPort = new TeamFortressViewport( _hud, 0, 0, root.GetWide(), root.GetTall() );
		viewPort.SetParent( root );
	}

	private static void KeyDown( KeyButton button )
	{
		var argument = Cmd.Argv( 1 );
		var key = argument.Length > 0 ? Common.Atoi( argument ) : -1;

		if ( key == button.Down0 || key == button.Down1 )
			return;

		if ( button.Down0 == 0 )
		{
			button.Down0 = key;
		}
		else if ( button.Down1 == 0 )
		{
			button.Down1 = key;
		}
		else
		{
			Log.Trace( $"Three keys down for a button '{(char)button.Down0}' '{(char)button.Down1}' '{argument}'!" );
			return;
		}

		if ( (button.State & 1) != 0 )
			return;

		button.State |= 1 + 2;
	}

	private static void KeyUp( KeyButton button )
	{
		var argument = Cmd.Argv( 1 );

		if ( argument.Length == 0 )
		{
			button.Down0 = button.Down1 = 0;
			button.State = 4;
			return;
		}

		var key = Common.Atoi( argument );

		if ( button.Down0 == key )
			button.Down0 = 0;
		else if ( button.Down1 == key )
			button.Down1 = 0;
		else
			return;

		if ( button.Down0 != 0 || button.Down1 != 0 )
			return;

		if ( (button.State & 1) == 0 )
			return;

		button.State &= ~1;
		button.State |= 4;
	}

	private readonly PlayerMovement _movement = new();

	public override void PlayerMoveInit( PlayerMove pmove )
	{
		_movement.Init( pmove );
	}

	public override void PlayerMove( PlayerMove pmove, bool server )
	{
		_movement.Move( pmove, server );
	}

	public static float GetMouseSensitivity()
	{
		if ( Client.Sensitivity > MaxSensitivity )
			Cvar.SetValue( "sensitivity", MaxSensitivity );
		else if ( Client.Sensitivity < MinSensitivity )
			Cvar.SetValue( "sensitivity", MinSensitivity );

		return Client.Sensitivity;
	}

	private void ScaleMouse( ref float x, ref float y )
	{
		var mouseSensitivity = _hud.MouseSensitivity != 0f ? _hud.MouseSensitivity : GetMouseSensitivity();

		if ( MouseCustomAccel == 0f )
		{
			x *= mouseSensitivity;
			y *= mouseSensitivity;
			return;
		}

		var rawMouseMovementDistance = MathF.Sqrt( (x * x) + (y * y) );
		var acceleratedSensitivity = (MathF.Pow( rawMouseMovementDistance, MouseCustomAccelExponent ) * MouseCustomAccelScale) + mouseSensitivity;

		if ( MouseCustomAccelMax > MinAcceleratedSensitivityMax && acceleratedSensitivity > MouseCustomAccelMax )
			acceleratedSensitivity = MouseCustomAccelMax;

		x *= acceleratedSensitivity;
		y *= acceleratedSensitivity;

		if ( MouseCustomAccel == 2f )
		{
			x *= MouseYaw;
			y *= MousePitch;
		}
	}

	public override void MouseMove( ref Vector3 viewAngles )
	{
		_mouseSideMove = 0f;
		_mouseForwardMove = 0f;
		_mouseUpMove = 0f;

		if ( MouseInUse )
			return;

		if ( (_mouseLook.State & 1) != 0 )
			StopPitchDrift();

		if ( _hud.IntermissionActive || VisibleMouse )
			return;

		var look = Input.UsingController ? default : Input.AnalogLook;
		var mx = -look.yaw / (MouseDegrees * Client.DefaultSensitivity);
		var my = look.pitch / (MouseDegrees * Client.DefaultSensitivity);
		var mouseX = mx;
		var mouseY = my;

		if ( MouseFilter != 0f )
		{
			mouseX = (mx + _oldMouseX) * 0.5f;
			mouseY = (my + _oldMouseY) * 0.5f;
		}

		_oldMouseX = mx;
		_oldMouseY = my;

		ScaleMouse( ref mouseX, ref mouseY );

		if ( (_strafe.State & 1) != 0 || (LookStrafe != 0f && (_mouseLook.State & 1) != 0) )
			_mouseSideMove = MouseSide * mouseX;
		else
			viewAngles.y -= MouseYaw * mouseX;

		if ( (_mouseLook.State & 1) != 0 && (_strafe.State & 1) == 0 )
		{
			viewAngles.x += MousePitch * mouseY;

			if ( viewAngles.x > PitchDown )
				viewAngles.x = PitchDown;

			if ( viewAngles.x < -PitchUp )
				viewAngles.x = -PitchUp;
		}
		else if ( (_strafe.State & 1) != 0 && Engine.IsNoClipping )
		{
			_mouseUpMove = -MouseForward * mouseY;
		}
		else
		{
			_mouseForwardMove = -MouseForward * mouseY;
		}
	}

	private const int DeadNo = 0;

	private bool _runFuncs;
	private bool _alive = true;

	public int PlayerClass { get; set; }

	public int TeamNumber { get; set; }

	public int User1 { get; set; }

	public int User2 { get; set; }

	public int User3 { get; set; }

	public bool VisibleMouse { get; set; }

	public static int TfcIsAllyTeam( int team1, int team2 ) => 0;

	public override bool AddEntity( int type, ClientEntity entity, int index, string modelName )
	{
		if ( User1 != 0 )
		{
			_hud.Spectator.AddOverviewEntity( type, entity, modelName );

			if ( (User1 == HudSpectator.ObsInEye || HudSpectator.Pip == HudSpectator.InsetInEye) && index == User2 )
				return false;
		}

		return true;
	}

	public override void PostRunCmd( LocalState from, LocalState to, ref UserCmd cmd, bool runFuncs, double time, uint randomSeed )
	{
		_runFuncs = runFuncs;

		if ( Cvar.GetFloat( "cl_lw" ) != 0f )
			WeaponsPostThink( from, to, ref cmd, time, randomSeed );
		else
			to.Client.Fov = _hud.LastFov;

		if ( RunningGaussPrediction )
		{
			MathLib.AngleVectors( LastViewAngles, out var forward, out _, out _ );

			to.Client.Velocity -= forward * ApplyVelocity * 5f;

			RunningGaussPrediction = false;
		}

		_hud.LastFov = to.Client.Fov;
	}

	public override void TxferLocalOverrides( ref EntityState state, in LocalClientData client )
	{
		state.Origin = client.Origin;

		state.IUser1 = client.IUser1;
		state.IUser2 = client.IUser2;
		state.IUser3 = client.IUser3;
		state.IUser4 = client.IUser4;
	}

	public override void ProcessPlayerState( ref EntityState destination, in EntityState source )
	{
		destination.Origin = source.Origin;
		destination.Angles = source.Angles;

		destination.Velocity = source.Velocity;

		destination.Frame = source.Frame;
		destination.ModelIndex = source.ModelIndex;
		destination.Skin = source.Skin;
		destination.Effects = source.Effects;
		destination.WeaponModel = source.WeaponModel;
		destination.MoveType = source.MoveType;
		destination.Sequence = source.Sequence;
		destination.AnimTime = source.AnimTime;

		destination.Solid = source.Solid;

		destination.RenderMode = source.RenderMode;
		destination.RenderAmt = source.RenderAmt;
		destination.RenderColorR = source.RenderColorR;
		destination.RenderColorG = source.RenderColorG;
		destination.RenderColorB = source.RenderColorB;
		destination.RenderFx = source.RenderFx;

		destination.FrameRate = source.FrameRate;
		destination.Body = source.Body;

		destination.Controller0 = source.Controller0;
		destination.Controller1 = source.Controller1;
		destination.Controller2 = source.Controller2;
		destination.Controller3 = source.Controller3;
		destination.Blending0 = source.Blending0;
		destination.Blending1 = source.Blending1;

		destination.BaseVelocity = source.BaseVelocity;

		destination.Friction = source.Friction;
		destination.Gravity = source.Gravity;
		destination.GaitSequence = source.GaitSequence;
		destination.Spectator = source.Spectator;
		destination.UseHull = source.UseHull;
		destination.PlayerClass = source.PlayerClass;
		destination.Team = source.Team;
		destination.Colormap = source.Colormap;

		if ( destination.Number == Engine.PlayerNum + 1 )
		{
			PlayerClass = destination.PlayerClass;
			TeamNumber = destination.Team;

			User1 = source.IUser1;
			User2 = source.IUser2;
			User3 = source.IUser3;
		}
	}

	public override void TxferPredictionData( ref EntityState playerState, in EntityState predictedPlayerState, ref LocalClientData clientData, in LocalClientData predictedClientData, WeaponData[] weaponData, WeaponData[] predictedWeaponData )
	{
		playerState.OldButtons = predictedPlayerState.OldButtons;
		playerState.FallVelocity = predictedPlayerState.FallVelocity;
		playerState.StepLeft = predictedPlayerState.StepLeft;
		playerState.PlayerClass = predictedPlayerState.PlayerClass;

		clientData.ViewModel = predictedClientData.ViewModel;
		clientData.Id = predictedClientData.Id;
		clientData.AmmoShells = predictedClientData.AmmoShells;
		clientData.AmmoNails = predictedClientData.AmmoNails;
		clientData.AmmoCells = predictedClientData.AmmoCells;
		clientData.AmmoRockets = predictedClientData.AmmoRockets;
		clientData.NextAttack = predictedClientData.NextAttack;
		clientData.Fov = predictedClientData.Fov;
		clientData.WeaponAnim = predictedClientData.WeaponAnim;
		clientData.TfState = predictedClientData.TfState;
		clientData.MaxSpeed = predictedClientData.MaxSpeed;

		clientData.DeadFlag = predictedClientData.DeadFlag;

		_alive = predictedClientData.IUser1 != 0 || clientData.DeadFlag == DeadNo;

		clientData.IUser1 = predictedClientData.IUser1;
		clientData.IUser2 = predictedClientData.IUser2;

		clientData.IUser3 = predictedClientData.IUser3;

		if ( Engine.IsSpectateOnly )
		{
			clientData.IUser1 = User1;
			clientData.IUser2 = User2;
			clientData.IUser3 = User3;
		}

		clientData.IUser4 = predictedClientData.IUser4;

		clientData.FUser2 = predictedClientData.FUser2;
		clientData.FUser3 = predictedClientData.FUser3;

		clientData.VUser1 = predictedClientData.VUser1;
		clientData.VUser2 = predictedClientData.VUser2;
		clientData.VUser3 = predictedClientData.VUser3;
		clientData.VUser4 = predictedClientData.VUser4;

		Array.Copy( predictedWeaponData, weaponData, BasePlayerItem.MaxWeapons );
	}

	public override void CreateMove( float frameTime, ref UserCmd cmd, ref Vector3 viewAngles, bool active )
	{
		if ( active )
		{
			AdjustAngles( frameTime, ref viewAngles );

			if ( (_strafe.State & 1) != 0 )
			{
				cmd.SideMove += SideSpeed * KeyState( _right );
				cmd.SideMove -= SideSpeed * KeyState( _left );
			}

			cmd.SideMove += SideSpeed * KeyState( _moveRight );
			cmd.SideMove -= SideSpeed * KeyState( _moveLeft );

			cmd.UpMove += UpSpeed * KeyState( _up );
			cmd.UpMove -= UpSpeed * KeyState( _down );

			if ( (_keyLook.State & 1) == 0 )
			{
				cmd.ForwardMove += ForwardSpeed * KeyState( _forward );
				cmd.ForwardMove -= BackSpeed * KeyState( _back );
			}

			if ( (_speed.State & 1) != 0 )
			{
				cmd.ForwardMove *= MoveSpeedKey;
				cmd.SideMove *= MoveSpeedKey;
				cmd.UpMove *= MoveSpeedKey;
			}

			var maxSpeed = Engine.ClientMaxSpeed;

			if ( maxSpeed != 0f )
			{
				var move = MathF.Sqrt( (cmd.ForwardMove * cmd.ForwardMove) + (cmd.SideMove * cmd.SideMove) + (cmd.UpMove * cmd.UpMove) );

				if ( move > maxSpeed )
				{
					var ratio = maxSpeed / move;

					cmd.ForwardMove *= ratio;
					cmd.SideMove *= ratio;
					cmd.UpMove *= ratio;
				}
			}

			cmd.SideMove += _mouseSideMove;
			cmd.ForwardMove += _mouseForwardMove;
			cmd.UpMove += _mouseUpMove;

			JoyMove( frameTime, ref cmd, ref viewAngles );
		}

		cmd.Impulse = (byte)_impulse;
		_impulse = 0;

		cmd.WeaponSelect = (byte)_hud.Ammo.WeaponSelect;
		_hud.Ammo.WeaponSelect = 0;

		cmd.Buttons = (ushort)ButtonBits( true );

		if ( _hud.Voice.IsInSquelchMode() )
			cmd.Buttons &= unchecked( (ushort)~(int)InButtons.Attack );

		if ( InJoystick != 0f )
		{
			if ( cmd.ForwardMove > 0f )
				cmd.Buttons |= (ushort)InButtons.Forward;
			else if ( cmd.ForwardMove < 0f )
				cmd.Buttons |= (ushort)InButtons.Back;
		}

		if ( _alive )
		{
			cmd.ViewAngles = viewAngles;
			_oldAngles = viewAngles;
		}
		else
		{
			cmd.ViewAngles = _oldAngles;
		}

		_lastForwardMove = cmd.ForwardMove;
	}

	private void AdjustAngles( float frameTime, ref Vector3 viewAngles )
	{
		var speed = (_speed.State & 1) != 0 ? frameTime * AngleSpeedKey : frameTime;

		if ( (_strafe.State & 1) == 0 )
		{
			viewAngles.y -= speed * YawSpeed * KeyState( _right );
			viewAngles.y += speed * YawSpeed * KeyState( _left );
			viewAngles.y = MathLib.AngleMod( viewAngles.y );
		}

		if ( (_keyLook.State & 1) != 0 )
		{
			StopPitchDrift();

			viewAngles.x -= speed * PitchSpeed * KeyState( _forward );
			viewAngles.x += speed * PitchSpeed * KeyState( _back );
		}

		var up = KeyState( _lookUp );
		var down = KeyState( _lookDown );

		viewAngles.x -= speed * PitchSpeed * up;
		viewAngles.x += speed * PitchSpeed * down;

		if ( up != 0f || down != 0f )
			StopPitchDrift();

		if ( viewAngles.x > PitchDown )
			viewAngles.x = PitchDown;

		if ( viewAngles.x < -PitchUp )
			viewAngles.x = -PitchUp;

		if ( viewAngles.z > 50f )
			viewAngles.z = 50f;

		if ( viewAngles.z < -50f )
			viewAngles.z = -50f;
	}

	private static float KeyState( KeyButton key )
	{
		var impulseDown = (key.State & 2) != 0;
		var impulseUp = (key.State & 4) != 0;
		var down = (key.State & 1) != 0;

		var value = 0f;

		if ( impulseDown && !impulseUp )
			value = down ? 0.5f : 0f;

		if ( !impulseDown && !impulseUp )
			value = down ? 1f : 0f;

		if ( impulseDown && impulseUp )
			value = down ? 0.75f : 0.25f;

		key.State &= 1;

		return value;
	}

	public string ConvertString( string text )
	{
		var builder = new System.Text.StringBuilder();
		var position = 0;

		while ( position < text.Length )
		{
			if ( text[position] != '+' )
			{
				builder.Append( text[position++] );
				continue;
			}

			var start = position;

			while ( position < text.Length && (char.IsAsciiLetterOrDigit( text[position] ) || position == start) && position - start < MaxBindingLength )
				position++;

			var binding = text[start..position];
			var key = binding.Length > 1 ? Keys.LookupBinding( binding[1..] ) : null;

			builder.Append( key is null ? binding : $"[{key}]" );
		}

		return builder.ToString();
	}

	public InButtons ButtonBits( bool resetState )
	{
		InButtons bits = 0;

		AddButton( ref bits, _attack, InButtons.Attack );
		AddButton( ref bits, _duck, InButtons.Duck );
		AddButton( ref bits, _jump, InButtons.Jump );
		AddButton( ref bits, _forward, InButtons.Forward );
		AddButton( ref bits, _back, InButtons.Back );
		AddButton( ref bits, _use, InButtons.Use );
		AddButton( ref bits, _left, InButtons.Left );
		AddButton( ref bits, _right, InButtons.Right );
		AddButton( ref bits, _moveLeft, InButtons.MoveLeft );
		AddButton( ref bits, _moveRight, InButtons.MoveRight );
		AddButton( ref bits, _attack2, InButtons.Attack2 );
		AddButton( ref bits, _reload, InButtons.Reload );
		AddButton( ref bits, _alt1, InButtons.Alt1 );
		AddButton( ref bits, _score, InButtons.Score );

		if ( _hud.Health.Health <= 0 || _hud.IntermissionActive )
			bits |= InButtons.Score;

		if ( resetState )
		{
			_attack.State &= ~2;
			_duck.State &= ~2;
			_jump.State &= ~2;
			_forward.State &= ~2;
			_back.State &= ~2;
			_use.State &= ~2;
			_left.State &= ~2;
			_right.State &= ~2;
			_moveLeft.State &= ~2;
			_moveRight.State &= ~2;
			_attack2.State &= ~2;
			_reload.State &= ~2;
			_alt1.State &= ~2;
			_score.State &= ~2;
		}

		return bits;
	}

	private static void AddButton( ref InButtons bits, KeyButton key, InButtons button )
	{
		if ( (key.State & 3) != 0 )
			bits |= button;
	}

	public void ResetButtonBits( InButtons bits )
	{
		var changed = ButtonBits( false ) ^ bits;

		if ( (changed & InButtons.Attack) != InButtons.Attack )
			return;

		if ( (bits & InButtons.Attack) == InButtons.Attack )
			KeyDown( _attack );
		else
			_attack.State &= ~7;
	}

	public override void CreateEntities()
	{
		GameAddObjects();

		_hud.Voice.CreateEntities();
	}

	public override void Frame( double time )
	{
		_hud.Servers.Think( time );
		_hud.Voice.Frame( time );
	}

	public override void DemoReadBuffer( int size, byte[] buffer ) => _hud.DemoReadBuffer( size, buffer );

	public override void VoiceStatus( int entityIndex, bool talking ) => _hud.Voice.UpdateSpeakerStatus( entityIndex, talking );

	public override void Shutdown() => _hud.Voice.Shutdown();

	private sealed class ClientExports( HalfLifeClientDll dll ) : GameClientExports
	{
		public override string GetServerHostName() => "";

		public override bool IsPlayerGameVoiceMuted( int playerIndex ) => dll._hud.Voice.IsPlayerBlocked( playerIndex );

		public override void MutePlayerGameVoice( int playerIndex ) => dll._hud.Voice.SetPlayerBlockedState( playerIndex, true );

		public override void UnmutePlayerGameVoice( int playerIndex ) => dll._hud.Voice.SetPlayerBlockedState( playerIndex, false );
	}

	private ClientExports _clientExports;

	public override GameClientExports GameClientExports => _clientExports ??= new ClientExports( this );

	public override void VidInit()
	{
		_hud.VidInit();

		VGuiStartup();
	}

	public override bool UpdateClientData( ref ClientData data, float time )
	{
		JoyCommands();

		return _hud.UpdateClientData( ref data, time );
	}

	public override void Redraw( float time, bool intermission ) => _hud.Redraw( time, intermission );
}
