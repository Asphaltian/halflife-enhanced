using System;
using GoldSrc;

namespace HalfLife;

public struct OverviewInfo
{
	public string Map;
	public Vector3 Origin;
	public float Zoom;
	public int Layers;
	public float LayerHeight;
	public string LayerImage;
	public bool Rotated;
	public int InsetWindowX;
	public int InsetWindowY;
	public int InsetWindowHeight;
	public int InsetWindowWidth;
}

public struct OverviewEntity
{
	public int Sprite;
	public ClientEntity Entity;
	public double KillTime;
}

public struct CameraWayPoint
{
	public float Time;
	public Vector3 Position;
	public Vector3 Angle;
	public float Fov;
	public int Flags;
}

public sealed class HudSpectator( Hud hud ) : HudBase( hud )
{
	public const int InsetOff = 0;
	public const int InsetChaseFree = 1;
	public const int InsetInEye = 2;
	public const int InsetMapFree = 3;
	public const int InsetMapChase = 4;

	public const int ObsNone = 0;
	public const int ObsChaseLocked = 1;
	public const int ObsChaseFree = 2;
	public const int ObsRoaming = 3;
	public const int ObsInEye = 4;
	public const int ObsMapFree = 5;
	public const int ObsMapChase = 6;

	private const int MaxSpecHudMessages = 8;
	private const int MaxOverviewEntities = 128;
	private const int MaxCamWaypoints = 32;
	private const int MaxHudMessageText = 127;
	private const int MaxOverviewLayers = 1;
	private const int RgbYellowish = 0x00FFA000;
	private const float OverviewEntityScale = 4f;
	private const float OverviewRange = 4096f;
	private const double ObserverInputDelay = 0.2;

	[ConVar( "spec_drawnames" )]
	private static float DrawNames { get; set; } = 1f;

	[ConVar( "spec_drawcone" )]
	private static float DrawCone { get; set; } = 1f;

	[ConVar( "spec_drawstatus" )]
	public static float DrawStatus { get; set; } = 1f;

	[ConVar( "spec_autodirector" )]
	public static float AutoDirector { get; set; } = 1f;

	[ConVar( "spec_pip" )]
	public static float Pip { get; set; } = 1f;

	public int DrawCycle;

	private readonly ClientTextMessage[] _hudMessages = new ClientTextMessage[MaxSpecHudMessages];
	private int _lastHudMessage;

	public OverviewInfo OverviewData;

	private readonly OverviewEntity[] _overviewEntities = new OverviewEntity[MaxOverviewEntities];

	public int ObserverFlags;
	public int SpectatorNumber;
	public float MapZoom;
	public Vector3 MapOrigin;

	private bool _chatEnabled;
	private bool _isInterpolating;
	private int _chaseEntity;
	private int _wayPoint;
	private int _numWayPoints;
	public Vector3 CameraOrigin;
	public Vector3 CameraAngles;
	private readonly Interpolation _wayInterpolation = new();

	private readonly Vector3[] _playerPos = new Vector3[Hud.MaxPlayers];
	private int _spritePlayerBlue;
	private int _spritePlayerRed;
	private int _spritePlayer;
	private int _spriteCamera;
	private int _spritePlayerDead;
	private int _spriteUnknownMap;
	private int _spriteBeam;
	private int _crosshair;
	private SpriteRect _crosshairRect;
	private SpriteModel _mapSprite;
	private float _nextObserverInput;
	private float _fov;
	private float _zoomDelta;
	private float _moveDelta;
	private int _lastPrimaryObject;
	private int _lastSecondaryObject;
	private readonly CameraWayPoint[] _camPath = new CameraWayPoint[MaxCamWaypoints + 1];

	private HalfLifeClientDll Dll => Hud.Dll;

	private TeamFortressViewport ViewPort => Hud.ViewPort;

	private static HudSpectator Current => (ClientDll.Loaded as HalfLifeClientDll)?.Hud?.Spectator;

	[ConCmd( "spec_mode" )]
	private static void SpectatorMode( params string[] arguments )
	{
		if ( Current is not { } spectator )
			return;

		if ( arguments.Length == 0 )
		{
			spectator.Engine.ConsolePrint( "usage:  spec_mode <Main Mode> [<Inset Mode>]\n" );
			return;
		}

		if ( arguments.Length == 1 )
			spectator.SetModes( Common.Atoi( arguments[0] ), -1 );
		else if ( arguments.Length == 2 )
			spectator.SetModes( Common.Atoi( arguments[0] ), Common.Atoi( arguments[1] ) );
	}

	[ConCmd( "spec_decal" )]
	private static void SpectatorSpray()
	{
		if ( Current is not { } spectator || !spectator.Engine.IsSpectateOnly )
			return;

		var dll = spectator.Dll;

		MathLib.AngleVectors( dll.LastViewAngles, out var forward, out _, out _ );

		forward = (forward * 128f) + dll.LastViewOrigin;

		var trace = spectator.Engine.TraceLine( dll.LastViewOrigin, forward, PlayerMove.TraceLinePhysEntsOnly, 2, -1 );

		if ( trace.Fraction != 1f )
			spectator.Engine.ServerCmd( $"drc_spray {Format2( trace.EndPos.x )} {Format2( trace.EndPos.y )} {Format2( trace.EndPos.z )} {trace.Ent}" );
	}

	private static string Format2( float value ) => value.ToString( "F2", System.Globalization.CultureInfo.InvariantCulture );

	[ConCmd( "spec_help" )]
	private static void SpectatorHelp()
	{
		if ( Current is not { } spectator )
			return;

		if ( spectator.ViewPort is not null )
		{
			spectator.ViewPort.ShowVGUIMenu( TeamFortressViewport.MenuSpecHelp );
			return;
		}

		var text = spectator.Hud.TextMessage.BufferedLocaliseTextString( "#Spec_Help_Text" );

		if ( text is not null )
			spectator.Engine.ConsolePrint( text.Replace( "\r", "" ) );
	}

	[ConCmd( "spec_menu" )]
	private static void SpectatorMenu( params string[] arguments )
	{
		if ( Current is not { } spectator )
			return;

		if ( arguments.Length == 0 )
		{
			spectator.Engine.ConsolePrint( "usage:  spec_menu <0|1>\n" );
			return;
		}

		spectator.ViewPort.SpectatorPanel.ShowMenu( Common.Atoi( arguments[0] ) != 0 );
	}

	[ConCmd( "togglescores" )]
	private static void ToggleScores()
	{
		if ( Current?.ViewPort is not { } viewPort )
			return;

		if ( viewPort.IsScoreBoardVisible() )
			viewPort.HideScoreBoard();
		else
			viewPort.ShowScoreBoard();
	}

	public override void Init()
	{
		Hud.AddHudElem( this );

		Flags |= Active;

		_nextObserverInput = 0f;
		_zoomDelta = 0f;
		_moveDelta = 0f;
		_fov = 90f;
		_chatEnabled = (HudSayText.SayText != 0f);

		Dll.JumpSpectator = false;

		OverviewData = default;

		Array.Clear( _overviewEntities );

		_lastPrimaryObject = _lastSecondaryObject = 0;
	}

	private static Vector3 StringToVector( string text )
	{
		if ( text.Length > 127 )
			text = text[..127];

		var vector = default( Vector3 );
		var front = 0;
		var position = 0;
		int j;

		for ( j = 0; j < 3; j++ )
		{
			vector[j] = Common.Atof( text[front..] );

			while ( position < text.Length && text[position] != ' ' )
				position++;

			if ( position >= text.Length )
				break;

			position++;
			front = position;
		}

		if ( j < 2 )
		{
			for ( j++; j < 3; j++ )
				vector[j] = 0f;
		}

		return vector;
	}

	private bool FindEntityInMap( string name, ref Vector3 origin, ref Vector3 angle )
	{
		var world = Engine.GetEntityByIndex( 0 );

		if ( world is null )
			return false;

		if ( Engine.WorldModel is not { } model )
			return false;

		var data = model.File.Entities;
		var position = 0;

		while ( position >= 0 )
		{
			var token = ParseToken( data, ref position );

			if ( token.Length == 0 || token[0] == '}' )
				break;

			if ( position < 0 )
			{
				Con.DPrintf( "UTIL_FindEntityInMap: EOF without closing brace\n" );
				return false;
			}

			if ( token[0] != '{' )
			{
				Con.DPrintf( "UTIL_FindEntityInMap: expected {\n" );
				return false;
			}

			var found = false;

			while ( true )
			{
				token = ParseToken( data, ref position );

				if ( token.Length != 0 && token[0] == '}' )
					break;

				if ( position < 0 )
				{
					Con.DPrintf( "UTIL_FindEntityInMap: EOF without closing brace\n" );
					return false;
				}

				var keyName = token.TrimEnd( ' ' );

				token = ParseToken( data, ref position );

				if ( position < 0 )
				{
					Con.DPrintf( "UTIL_FindEntityInMap: EOF without closing brace\n" );
					return false;
				}

				if ( token.Length != 0 && token[0] == '}' )
				{
					Con.DPrintf( "UTIL_FindEntityInMap: closing brace without data" );
					return false;
				}

				if ( keyName == "classname" && token == name )
					found = true;

				if ( keyName == "angle" )
				{
					var y = Common.Atof( token );

					if ( y >= 0f )
					{
						angle.x = 0f;
						angle.y = y;
					}
					else if ( (int)y == -1 )
					{
						angle.x = -90f;
						angle.y = 0f;
					}
					else
					{
						angle.x = 90f;
						angle.y = 0f;
					}

					angle.z = 0f;
				}

				if ( keyName == "angles" )
					angle = StringToVector( token );

				if ( keyName == "origin" )
					origin = StringToVector( token );
			}

			if ( found )
				return true;
		}

		return false;
	}

	private static string ParseToken( string data, ref int position )
	{
		var token = Common.Parse( data, ref position );

		if ( token is null )
		{
			position = -1;
			return "";
		}

		return token;
	}

	public void SetSpectatorStartPosition()
	{
		if ( FindEntityInMap( "trigger_camera", ref CameraOrigin, ref CameraAngles ) )
		{
			Dll.JumpSpectator = true;
		}
		else if ( FindEntityInMap( "info_player_start", ref CameraOrigin, ref CameraAngles ) )
		{
			Dll.JumpSpectator = true;
		}
		else if ( FindEntityInMap( "info_player_deathmatch", ref CameraOrigin, ref CameraAngles ) )
		{
			Dll.JumpSpectator = true;
		}
		else if ( FindEntityInMap( "info_player_coop", ref CameraOrigin, ref CameraAngles ) )
		{
			Dll.JumpSpectator = true;
		}
		else
		{
			CameraOrigin = Vector3.Zero;
			CameraAngles = Vector3.Zero;
		}

		Dll.JumpOrigin = CameraOrigin;
		Dll.JumpAngles = CameraAngles;
		Dll.JumpSpectator = true;
	}

	public void SetCameraView( Vector3 position, Vector3 angle, float fov )
	{
		_fov = fov;

		Dll.JumpOrigin = position;
		Dll.JumpAngles = angle;

		Engine.ViewAngles = Dll.JumpAngles;

		Dll.JumpSpectator = true;
	}

	public void AddWaypoint( float time, Vector3 position, Vector3 angle, float fov, int flags )
	{
		if ( flags != 0 && time == 0f )
		{
			SetCameraView( position, angle, fov );
			return;
		}

		if ( _numWayPoints >= MaxCamWaypoints )
		{
			Engine.ConsolePrint( "Too many camera waypoints!\n" );
			return;
		}

		_camPath[_numWayPoints].Angle = angle;
		_camPath[_numWayPoints].Position = position;
		_camPath[_numWayPoints].Flags = flags;
		_camPath[_numWayPoints].Fov = fov;
		_camPath[_numWayPoints].Time = time;

		Con.DPrintf( $"Added waypoint {_numWayPoints}\n" );

		_numWayPoints++;
	}

	private void SetWayInterpolation( int previous, int start, int end, int next )
	{
		ref var from = ref _camPath[start];
		ref var to = ref _camPath[end];

		_wayInterpolation.SetViewAngles( from.Angle, to.Angle );
		_wayInterpolation.SetFovs( from.Fov, to.Fov );
		_wayInterpolation.SetSmoothing( (from.Flags & Hltv.FlagSlowStart) != 0, (from.Flags & Hltv.FlagSlowEnd) != 0 );
		_wayInterpolation.SetWaypoints( previous >= 0 ? _camPath[previous].Position : null, from.Position, to.Position, next >= 0 ? _camPath[next].Position : null );
	}

	public bool GetDirectorCamera( ref Vector3 position, ref Vector3 angle )
	{
		var now = Hud.Time;
		var fov = 90f;

		if ( _chaseEntity != 0 )
		{
			var entity = Engine.GetEntityByIndex( _chaseEntity );

			if ( entity is null )
				return false;

			var target = entity.CurState.Origin;

			if ( _chaseEntity <= Engine.MaxClients )
			{
				if ( entity.CurState.Solid == (int)Solid.Not )
					target.z += -8f;
				else if ( entity.CurState.UseHull == 1 )
					target.z += 12f;
				else
					target.z += 28f;
			}

			target -= position;

			angle = MathLib.VectorAngles( target );
			angle.x = -angle.x;

			return true;
		}

		if ( !_isInterpolating )
			return false;

		if ( _wayPoint < 0 || _wayPoint >= _numWayPoints - 1 )
			return false;

		var first = _wayPoint;
		var second = _wayPoint + 1;

		if ( now < _camPath[first].Time )
			return false;

		while ( now > _camPath[second].Time )
		{
			_wayPoint++;

			if ( _wayPoint >= _numWayPoints - 1 )
			{
				_isInterpolating = false;
				return false;
			}

			first = second;
			second = _wayPoint + 1;

			if ( _wayPoint > 0 )
				SetWayInterpolation( _wayPoint - 1, first, second, _wayPoint < _numWayPoints - 1 ? _wayPoint + 2 : -1 );
			else if ( _wayPoint < _numWayPoints - 1 )
				SetWayInterpolation( -1, first, second, _wayPoint + 2 );
			else
				SetWayInterpolation( -1, first, second, -1 );
		}

		if ( _camPath[second].Time <= _camPath[first].Time )
			return false;

		var fraction = Math.Clamp( (now - _camPath[first].Time) / (_camPath[second].Time - _camPath[first].Time), 0f, 1f );

		_wayInterpolation.Interpolate( fraction, out position, out angle, out fov );

		SetCameraView( position, angle, fov );

		return true;
	}

	public override void VidInit()
	{
		_spritePlayer = Engine.SpriteLoad( "sprites/iplayer.spr" );
		_spritePlayerBlue = Engine.SpriteLoad( "sprites/iplayerblue.spr" );
		_spritePlayerRed = Engine.SpriteLoad( "sprites/iplayerred.spr" );
		_spritePlayerDead = Engine.SpriteLoad( "sprites/iplayerdead.spr" );
		_spriteUnknownMap = Engine.SpriteLoad( "sprites/tile.spr" );
		_spriteBeam = Engine.SpriteLoad( "sprites/laserbeam.spr" );
		_spriteCamera = Engine.SpriteLoad( "sprites/camera.spr" );
		_crosshair = Engine.SpriteLoad( "sprites/crosshairs.spr" );

		_lastPrimaryObject = _lastSecondaryObject = 0;
		_nextObserverInput = 0f;
		_lastHudMessage = 0;

		SpectatorNumber = 0;

		Dll.JumpSpectator = false;
		Dll.User1 = Dll.User2 = 0;
	}

	public float GetFov() => _fov;

	public override void Draw( float time )
	{
		if ( Dll.User1 == 0 )
			return;

		if ( _zoomDelta != 0f && Dll.User1 == ObsMapFree )
			MapZoom = Math.Clamp( MapZoom + _zoomDelta, 0.5f, 3f );

		if ( _moveDelta != 0f && Dll.User1 != ObsRoaming )
		{
			MathLib.AngleVectors( Dll.LastViewAngles, out _, out var right, out _ );

			MathLib.VectorNormalize( ref right );

			MapOrigin += right * _moveDelta;
		}

		if ( Dll.User1 < ObsMapFree )
			return;

		if ( DrawNames == 0f )
			return;

		ViewPort.GetAllPlayersInfo();

		for ( var i = 0; i < Hud.MaxPlayers; i++ )
		{
			if ( _playerPos[i].z < 0f )
				continue;

			if ( Pip != InsetOff )
			{
				if ( _playerPos[i].x > VguiUtil.XResHd( OverviewData.InsetWindowX ) &&
					_playerPos[i].y > VguiUtil.YResHd( OverviewData.InsetWindowY ) &&
					_playerPos[i].x < VguiUtil.XResHd( OverviewData.InsetWindowX + OverviewData.InsetWindowWidth ) &&
					_playerPos[i].y < VguiUtil.YResHd( OverviewData.InsetWindowY + OverviewData.InsetWindowHeight ) )
					continue;
			}

			var color = Hud.GetClientColor( i + 1 );
			var name = Hud.PlayerInfoList[i + 1].Name ?? "(null)";
			var lx = name.Length * 3;

			Engine.DrawSetTextColor( color[0], color[1], color[2] );
			Engine.DrawConsoleString( (int)(_playerPos[i].x - lx), (int)_playerPos[i].y, name );
		}
	}

	public void DirectorMessage( byte[] data )
	{
		var message = new MessageBuffer( data );
		var command = message.ReadByte();

		switch ( command )
		{
			case Hltv.CmdStart:
				Dll.PlayerClass = 0;
				Dll.TeamNumber = 0;

				Hud.MsgFuncInitHud( null, [] );
				Hud.MsgFuncResetHud( null, [] );

				break;

			case Hltv.CmdEvent:
				_lastPrimaryObject = message.ReadShort() & 0xffff;
				_lastSecondaryObject = message.ReadShort() & 0xffff;
				ObserverFlags = message.ReadLong();

				if ( AutoDirector != 0f )
				{
					if ( Dll.User2 != _lastPrimaryObject || Dll.User3 != _lastSecondaryObject )
						Dll.ResetChaseCam();

					Dll.User2 = _lastPrimaryObject;
					Dll.User3 = _lastSecondaryObject;

					_isInterpolating = false;
					_chaseEntity = 0;
				}

				break;

			case Hltv.CmdMode:
				if ( AutoDirector != 0f )
					SetModes( message.ReadByte(), -1 );

				break;

			case Hltv.CmdCamera:
				{
					var position = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
					var angle = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
					var fov = (float)message.ReadByte();
					var target = message.ReadShort() & 0xffff;

					if ( AutoDirector != 0f )
					{
						SetModes( ObsRoaming, -1 );
						SetCameraView( position, angle, fov );

						_chaseEntity = target;
					}

					break;
				}

			case Hltv.CmdMessage:
				{
					var text = _hudMessages[_lastHudMessage] ??= new ClientTextMessage();

					text.Effect = message.ReadByte();

					var color = message.ReadLong();

					text.R1 = text.R2 = (byte)((color >> 16) & 0xff);
					text.G1 = text.G2 = (byte)((color >> 8) & 0xff);
					text.B1 = text.B2 = (byte)(color & 0xff);
					text.A2 = text.A1 = 0xff;

					text.X = message.ReadFloat();
					text.Y = message.ReadFloat();
					text.FadeIn = message.ReadFloat();
					text.FadeOut = message.ReadFloat();
					text.HoldTime = message.ReadFloat();
					text.FxTime = message.ReadFloat();

					var content = message.ReadString();

					text.Message = content.Length > MaxHudMessageText ? content[..MaxHudMessageText] : content;
					text.Name = "HUD_MESSAGE";

					Hud.Message.MessageAdd( text );

					_lastHudMessage++;
					_lastHudMessage %= MaxSpecHudMessages;

					break;
				}

			case Hltv.CmdSound:
				{
					var sample = message.ReadString();
					var volume = message.ReadFloat();

					Engine.PlaySound( 0, Dll.LastViewOrigin, SoundChannel.Body, sample, volume, Server.AttenuationNormal, 0, Server.PitchNormal );

					break;
				}

			case Hltv.CmdTimeScale:
				message.ReadFloat();
				break;

			case Hltv.CmdStatus:
				message.ReadLong();
				SpectatorNumber = message.ReadLong();
				message.ReadShort();

				ViewPort.UpdateSpectatorPanel();

				break;

			case Hltv.CmdBanner:
				ViewPort.SpectatorPanel.TopBanner.LoadImage( message.ReadString() );
				ViewPort.UpdateSpectatorPanel();

				break;

			case Hltv.CmdStuffText:
				Engine.FilteredClientCmd( message.ReadString() );
				break;

			case Hltv.CmdCamPath:
				{
					var position = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
					var angle = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
					var fov = (float)message.ReadByte();

					message.ReadByte();

					if ( AutoDirector != 0f )
					{
						SetModes( ObsRoaming, -1 );
						SetCameraView( position, angle, fov );
					}

					break;
				}

			case Hltv.CmdWaypoints:
				{
					var count = message.ReadByte();

					_numWayPoints = 0;
					_wayPoint = 0;

					for ( var i = 0; i < count; i++ )
					{
						var time = Hud.Time + ((float)(short)message.ReadShort() / 100f);
						var position = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
						var angle = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );
						var fov = (float)message.ReadByte();
						var flags = message.ReadByte();

						AddWaypoint( time, position, angle, fov, flags );
					}

					if ( AutoDirector == 0f )
					{
						_numWayPoints = 0;
						break;
					}

					SetModes( ObsRoaming, -1 );

					_isInterpolating = true;

					SetWayInterpolation( -1, 0, 1, _numWayPoints > 2 ? 2 : -1 );

					break;
				}

			default:
				Con.DPrintf( $"CHudSpectator::DirectorMessage: unknown command {command}.\n" );
				break;
		}
	}

	public void FindNextPlayer( bool reverse )
	{
		if ( !Engine.IsSpectateOnly )
		{
			Engine.ServerCmd( $"follownext {(reverse ? 1 : 0)}" );
			return;
		}

		var start = Dll.User2 != 0 ? Dll.User2 : 1;

		Dll.User2 = 0;

		var current = start;
		var direction = reverse ? -1 : 1;
		ClientEntity entity;

		ViewPort.GetAllPlayersInfo();

		do
		{
			current += direction;

			if ( current > Hud.MaxPlayers )
				current = 1;

			if ( current < 1 )
				current = Hud.MaxPlayers;

			entity = Engine.GetEntityByIndex( current );

			if ( !IsActivePlayer( entity ) )
				continue;

			Dll.User2 = current;

			break;
		}
		while ( current != start );

		if ( Dll.User2 == 0 )
		{
			Con.DPrintf( "No observer targets.\n" );

			Dll.JumpOrigin = CameraOrigin;
			Dll.JumpAngles = CameraAngles;
		}
		else
		{
			entity = Engine.GetEntityByIndex( current );

			Dll.JumpOrigin = entity.Origin;
			Dll.JumpAngles = entity.Angles;
		}

		Dll.JumpSpectator = true;

		ViewPort.MsgFuncResetFade( null, [] );
	}

	public void FindPlayer( string name )
	{
		if ( !Engine.IsSpectateOnly )
		{
			Engine.ServerCmd( $"follow {name}" );
			return;
		}

		Dll.User2 = 0;

		ViewPort.GetAllPlayersInfo();

		ClientEntity entity = null;

		for ( var i = 1; i < Hud.MaxPlayers; i++ )
		{
			entity = Engine.GetEntityByIndex( i );

			if ( !IsActivePlayer( entity ) )
				continue;

			if ( string.Equals( Hud.PlayerInfoList[entity.Index].Name, name, StringComparison.OrdinalIgnoreCase ) )
			{
				Dll.User2 = i;
				break;
			}
		}

		if ( Dll.User2 == 0 )
		{
			Con.DPrintf( "No observer targets.\n" );

			Dll.JumpOrigin = CameraOrigin;
			Dll.JumpAngles = CameraAngles;
		}
		else
		{
			Dll.JumpOrigin = entity.Origin;
			Dll.JumpAngles = entity.Angles;
		}

		Dll.JumpSpectator = true;

		ViewPort.MsgFuncResetFade( null, [] );
	}

	public void HandleButtonsDown( int buttonPressed )
	{
		var time = Engine.ClientTime;
		var newMainMode = Dll.User1;
		var newInsetMode = (int)Pip;

		if ( ViewPort is null )
			return;

		if ( Hud.IntermissionActive )
			return;

		if ( Dll.User1 == 0 )
			return;

		if ( Engine.DemoPlayback && !Engine.IsSpectateOnly )
			return;

		if ( _nextObserverInput > time )
			return;

		if ( (buttonPressed & (int)InButtons.Duck) != 0 )
			ViewPort.SpectatorPanel.ShowMenu( !ViewPort.SpectatorPanel.MenuVisible );

		if ( (buttonPressed & (int)InButtons.Use) != 0 )
			newInsetMode = ToggleInset( true );

		if ( Engine.IsSpectateOnly )
		{
			if ( (buttonPressed & (int)InButtons.Jump) != 0 )
			{
				newMainMode = Dll.User1 switch
				{
					ObsChaseLocked => ObsChaseFree,
					ObsChaseFree => ObsInEye,
					ObsInEye => ObsRoaming,
					ObsRoaming => ObsMapFree,
					ObsMapFree => ObsMapChase,
					_ => ObsChaseFree
				};
			}

			if ( (buttonPressed & (int)(InButtons.Attack | InButtons.Attack2)) != 0 )
			{
				FindNextPlayer( (buttonPressed & (int)InButtons.Attack2) != 0 );

				if ( Dll.User1 == ObsRoaming )
				{
					Engine.ViewAngles = Dll.JumpAngles;

					Dll.JumpSpectator = true;
				}

				AutoDirector = 0f;
			}
		}

		SetModes( newMainMode, newInsetMode );

		if ( Dll.User1 == ObsMapFree )
		{
			if ( (buttonPressed & (int)InButtons.Forward) != 0 )
				_zoomDelta = 0.01f;

			if ( (buttonPressed & (int)InButtons.Back) != 0 )
				_zoomDelta = -0.01f;

			if ( (buttonPressed & (int)InButtons.MoveLeft) != 0 )
				_moveDelta = -12f;

			if ( (buttonPressed & (int)InButtons.MoveRight) != 0 )
				_moveDelta = 12f;
		}

		_nextObserverInput = (float)(time + ObserverInputDelay);
	}

	public void HandleButtonsUp( int buttonPressed )
	{
		if ( ViewPort is null )
			return;

		if ( !ViewPort.SpectatorPanel.IsVisible() )
			return;

		if ( (buttonPressed & (int)(InButtons.Forward | InButtons.Back)) != 0 )
			_zoomDelta = 0f;

		if ( (buttonPressed & (int)(InButtons.MoveLeft | InButtons.MoveRight)) != 0 )
			_moveDelta = 0f;
	}

	private void SetObserverCrosshair()
	{
		if ( Dll.User1 is ObsInEye or ObsRoaming )
		{
			_crosshairRect = new SpriteRect( 24, 48, 0, 24 );

			Engine.SetCrosshair( _crosshair, _crosshairRect, 255, 255, 255 );
		}
		else
		{
			_crosshairRect = default;

			Engine.SetCrosshair( 0, _crosshairRect, 0, 0, 0 );
		}
	}

	public void SetModes( int newMainMode, int newInsetMode )
	{
		if ( newMainMode == -1 )
			newMainMode = Dll.User1;

		if ( newInsetMode == -1 )
			newInsetMode = (int)Pip;

		Pip = newInsetMode;

		if ( newMainMode < ObsChaseLocked || newMainMode > ObsMapChase )
		{
			Engine.ConsolePrint( "Invalid spectator mode.\n" );
			return;
		}

		_isInterpolating = false;
		_chaseEntity = 0;

		if ( newMainMode != Dll.User1 )
		{
			if ( !Engine.IsSpectateOnly )
			{
				Engine.ServerCmd( $"specmode {newMainMode}" );
				return;
			}

			if ( Dll.User2 == 0 && newMainMode != ObsRoaming )
			{
				if ( IsActivePlayer( Engine.GetEntityByIndex( _lastPrimaryObject ) ) )
				{
					Dll.User2 = _lastPrimaryObject;
					Dll.User3 = _lastSecondaryObject;
				}
				else
				{
					FindNextPlayer( false );
				}
			}

			switch ( newMainMode )
			{
				case ObsChaseLocked:
					Dll.User1 = ObsChaseLocked;
					break;

				case ObsChaseFree:
					Dll.User1 = ObsChaseFree;
					break;

				case ObsRoaming:
					Dll.User1 = ObsRoaming;

					if ( Dll.User2 != 0 )
					{
						Dll.GetChasePos( Dll.User2, Dll.LastClientAngles, ref Dll.JumpOrigin, ref Dll.JumpAngles );

						Engine.ViewAngles = Dll.JumpAngles;

						Dll.JumpSpectator = true;
					}

					break;

				case ObsInEye:
					Dll.User1 = ObsInEye;
					break;

				case ObsMapFree:
					Dll.User1 = ObsMapFree;

					MapZoom = OverviewData.Zoom;
					MapOrigin = OverviewData.Origin;

					break;

				case ObsMapChase:
					Dll.User1 = ObsMapChase;

					MapZoom = OverviewData.Zoom;
					MapOrigin = OverviewData.Origin;

					break;
			}

			SetObserverCrosshair();

			ViewPort.MsgFuncResetFade( null, [] );

			Hud.TextMessage.TextMsg( HudTextMessage.HudPrintCenter, Hud.TextMessage.BufferedLocaliseTextString( $"#Spec_Mode{Dll.User1}" ) );
		}

		ViewPort.UpdateSpectatorPanel();
	}

	public bool IsActivePlayer( ClientEntity entity )
	{
		return entity is not null &&
			entity.Player &&
			entity.CurState.Solid != (int)Solid.Not &&
			entity != Engine.GetLocalPlayer() &&
			Hud.PlayerInfoList[entity.Index].Name is not null;
	}

	public bool ParseOverviewFile()
	{
		OverviewData = new OverviewInfo
		{
			InsetWindowX = 4,
			InsetWindowY = 4,
			InsetWindowHeight = 180,
			InsetWindowWidth = 240,
			Zoom = 1f,
			Map = Engine.LevelName
		};

		if ( OverviewData.Map.Length == 0 )
			return false;

		var levelName = OverviewData.Map[5..];

		levelName = levelName[..^4];

		var fileName = $"overviews/{levelName}.txt";
		var file = Server.LoadMountText( fileName );

		if ( file is null )
		{
			Con.DPrintf( $"Couldn't open file {fileName}. Using default values for overiew mode.\n" );
			return false;
		}

		var position = 0;

		while ( true )
		{
			var token = ParseToken( file, ref position );

			if ( position < 0 )
				break;

			if ( token.Equals( "global", StringComparison.OrdinalIgnoreCase ) )
			{
				token = ParseToken( file, ref position );

				if ( token != "{" )
				{
					Engine.ConsolePrint( $"Error parsing overview file {fileName}. (expected {{ )\n" );
					return false;
				}

				token = ParseToken( file, ref position );

				while ( token != "}" )
				{
					if ( token.Equals( "zoom", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.Zoom = Common.Atof( ParseToken( file, ref position ) );
					}
					else if ( token.Equals( "origin", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.Origin.x = Common.Atof( ParseToken( file, ref position ) );
						OverviewData.Origin.y = Common.Atof( ParseToken( file, ref position ) );
						OverviewData.Origin.z = Common.Atof( ParseToken( file, ref position ) );
					}
					else if ( token.Equals( "rotated", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.Rotated = Common.Atoi( ParseToken( file, ref position ) ) != 0;
					}
					else if ( token.Equals( "inset", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.InsetWindowX = (int)Common.Atof( ParseToken( file, ref position ) );
						OverviewData.InsetWindowY = (int)Common.Atof( ParseToken( file, ref position ) );
						OverviewData.InsetWindowWidth = (int)Common.Atof( ParseToken( file, ref position ) );
						OverviewData.InsetWindowHeight = (int)Common.Atof( ParseToken( file, ref position ) );
					}
					else
					{
						Engine.ConsolePrint( $"Error parsing overview file {fileName}. ({token} unkown)\n" );
						return false;
					}

					token = ParseToken( file, ref position );
				}
			}
			else if ( token.Equals( "layer", StringComparison.OrdinalIgnoreCase ) )
			{
				if ( OverviewData.Layers == MaxOverviewLayers )
				{
					Engine.ConsolePrint( $"Error parsing overview file {fileName}. ( too many layers )\n" );
					return false;
				}

				token = ParseToken( file, ref position );

				if ( token != "{" )
				{
					Engine.ConsolePrint( $"Error parsing overview file {fileName}. (expected {{ )\n" );
					return false;
				}

				token = ParseToken( file, ref position );

				while ( token != "}" )
				{
					if ( token.Equals( "image", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.LayerImage = ParseToken( file, ref position );
					}
					else if ( token.Equals( "height", StringComparison.OrdinalIgnoreCase ) )
					{
						OverviewData.LayerHeight = Common.Atof( ParseToken( file, ref position ) );
					}
					else
					{
						Engine.ConsolePrint( $"Error parsing overview file {fileName}. ({token} unkown)\n" );
						return false;
					}

					token = ParseToken( file, ref position );
				}

				OverviewData.Layers++;
			}
		}

		MapZoom = OverviewData.Zoom;
		MapOrigin = OverviewData.Origin;

		return true;
	}

	public void LoadMapSprites()
	{
		_mapSprite = OverviewData.Layers > 0 ? Engine.LoadMapSprite( OverviewData.LayerImage ) : null;
	}

	private void DrawOverviewLayer()
	{
		var hasMapImage = _mapSprite is not null;
		var dummySprite = Engine.GetSpritePointer( _spriteUnknownMap );
		int xTiles;
		int yTiles;

		if ( hasMapImage )
		{
			var i = (int)MathF.Sqrt( _mapSprite.Frames.Length / (4 * 3) );

			xTiles = i * 4;
			yTiles = i * 3;
		}
		else
		{
			xTiles = 8;
			yTiles = 6;
		}

		const float screenAspect = 4f / 3f;

		var xs = OverviewData.Origin.x;
		var ys = OverviewData.Origin.y;
		var z = (90f - Dll.LastViewAngles.x) / 90f;

		z *= OverviewData.LayerHeight;

		Engine.TriRenderMode( RenderMode.TransTexture );
		Engine.TriCullFace( TriCullStyle.None );
		Engine.TriColor4f( 1f, 1f, 1f, 1f );

		var frame = 0;

		if ( OverviewData.Rotated )
		{
			var xStep = (2 * OverviewRange / OverviewData.Zoom) / xTiles;
			var yStep = -(2 * OverviewRange / (OverviewData.Zoom * screenAspect)) / yTiles;
			var y = ys + (OverviewRange / (OverviewData.Zoom * screenAspect));

			for ( var iy = 0; iy < yTiles; iy++ )
			{
				var x = xs - (OverviewRange / OverviewData.Zoom);

				for ( var ix = 0; ix < xTiles; ix++ )
				{
					if ( hasMapImage )
						Engine.TriSpriteTexture( _mapSprite, frame );
					else
						Engine.TriSpriteTexture( dummySprite, 0 );

					Engine.TriBegin( TriPrimitive.Quads );
					Engine.TriTexCoord2f( 0f, 0f );
					Engine.TriVertex3f( x, y, z );
					Engine.TriTexCoord2f( 1f, 0f );
					Engine.TriVertex3f( x + xStep, y, z );
					Engine.TriTexCoord2f( 1f, 1f );
					Engine.TriVertex3f( x + xStep, y + yStep, z );
					Engine.TriTexCoord2f( 0f, 1f );
					Engine.TriVertex3f( x, y + yStep, z );
					Engine.TriEnd();

					frame++;
					x += xStep;
				}

				y += yStep;
			}
		}
		else
		{
			var xStep = -(2 * OverviewRange / OverviewData.Zoom) / xTiles;
			var yStep = -(2 * OverviewRange / (OverviewData.Zoom * screenAspect)) / yTiles;
			var x = xs + (OverviewRange / (OverviewData.Zoom * screenAspect));

			for ( var ix = 0; ix < yTiles; ix++ )
			{
				var y = ys + (OverviewRange / OverviewData.Zoom);

				for ( var iy = 0; iy < xTiles; iy++ )
				{
					if ( hasMapImage )
						Engine.TriSpriteTexture( _mapSprite, frame );
					else
						Engine.TriSpriteTexture( dummySprite, 0 );

					Engine.TriBegin( TriPrimitive.Quads );
					Engine.TriTexCoord2f( 0f, 0f );
					Engine.TriVertex3f( x, y, z );
					Engine.TriTexCoord2f( 0f, 1f );
					Engine.TriVertex3f( x + xStep, y, z );
					Engine.TriTexCoord2f( 1f, 1f );
					Engine.TriVertex3f( x + xStep, y + yStep, z );
					Engine.TriTexCoord2f( 1f, 0f );
					Engine.TriVertex3f( x, y + yStep, z );
					Engine.TriEnd();

					frame++;
					y += yStep;
				}

				x += xStep;
			}
		}
	}

	private void DrawOverviewEntities()
	{
		var zScale = (90f - Dll.LastViewAngles.x) / 90f;
		var z = OverviewData.LayerHeight * zScale;
		var r = ((RgbYellowish >> 16) & 0xff) / 255f;
		var g = ((RgbYellowish >> 8) & 0xff) / 255f;
		var b = (RgbYellowish & 0xff) / 255f;

		Engine.TriCullFace( TriCullStyle.None );

		for ( var i = 0; i < Hud.MaxPlayers; i++ )
			_playerPos[i].z = -1f;

		var origin = default( Vector3 );

		for ( var i = 0; i < MaxOverviewEntities; i++ )
		{
			if ( _overviewEntities[i].Sprite == 0 )
				continue;

			var entity = _overviewEntities[i].Entity;

			Engine.TriSpriteTexture( Engine.GetSpritePointer( _overviewEntities[i].Sprite ), 0 );
			Engine.TriRenderMode( RenderMode.TransTexture );

			MathLib.AngleVectors( entity.Angles, out var right, out var up, out _ );

			origin = entity.Origin;

			Engine.TriBegin( TriPrimitive.Quads );
			Engine.TriColor4f( 1f, 1f, 1f, 1f );

			Engine.TriTexCoord2f( 1f, 0f );
			OverviewVertex( origin + (up * (16f * OverviewEntityScale)) + (right * (16f * OverviewEntityScale)), zScale );

			Engine.TriTexCoord2f( 0f, 0f );
			OverviewVertex( origin + (up * (16f * OverviewEntityScale)) + (right * (-16f * OverviewEntityScale)), zScale );

			Engine.TriTexCoord2f( 0f, 1f );
			OverviewVertex( origin + (up * (-16f * OverviewEntityScale)) + (right * (-16f * OverviewEntityScale)), zScale );

			Engine.TriTexCoord2f( 1f, 1f );
			OverviewVertex( origin + (up * (-16f * OverviewEntityScale)) + (right * (16f * OverviewEntityScale)), zScale );

			Engine.TriEnd();

			if ( !entity.Player )
				continue;

			origin.z *= zScale;

			Engine.TriRenderMode( RenderMode.TransAdd );
			Engine.TriSpriteTexture( Engine.GetSpritePointer( _spriteBeam ), 0 );
			Engine.TriColor4f( r, g, b, 0.3f );

			Engine.TriBegin( TriPrimitive.Quads );
			Engine.TriTexCoord2f( 1f, 0f );
			Engine.TriVertex3f( origin.x + 4f, origin.y + 4f, origin.z - zScale );
			Engine.TriTexCoord2f( 0f, 0f );
			Engine.TriVertex3f( origin.x - 4f, origin.y - 4f, origin.z - zScale );
			Engine.TriTexCoord2f( 0f, 1f );
			Engine.TriVertex3f( origin.x - 4f, origin.y - 4f, z );
			Engine.TriTexCoord2f( 1f, 1f );
			Engine.TriVertex3f( origin.x + 4f, origin.y + 4f, z );
			Engine.TriEnd();

			Engine.TriBegin( TriPrimitive.Quads );
			Engine.TriTexCoord2f( 1f, 0f );
			Engine.TriVertex3f( origin.x - 4f, origin.y + 4f, origin.z - zScale );
			Engine.TriTexCoord2f( 0f, 0f );
			Engine.TriVertex3f( origin.x + 4f, origin.y - 4f, origin.z - zScale );
			Engine.TriTexCoord2f( 0f, 1f );
			Engine.TriVertex3f( origin.x + 4f, origin.y - 4f, z );
			Engine.TriTexCoord2f( 1f, 1f );
			Engine.TriVertex3f( origin.x - 4f, origin.y + 4f, z );
			Engine.TriEnd();

			if ( Engine.TriWorldToScreen( origin, out var screen ) )
				continue;

			screen.x = XProject( screen.x );
			screen.y = YProject( screen.y );
			screen.z = 0f;

			origin.x += 32f;
			origin.y += 32f;

			Engine.TriWorldToScreen( origin, out var offset );

			offset.x = XProject( offset.x );
			offset.y = YProject( offset.y );
			offset.z = 0f;

			offset -= screen;

			var playerNum = entity.Index - 1;

			_playerPos[playerNum].x = screen.x;
			_playerPos[playerNum].y = screen.y + MathLib.Length( offset );
			_playerPos[playerNum].z = 1f;
		}

		if ( Pip == 0f || DrawCone == 0f )
			return;

		var angles = default( Vector3 );

		if ( Pip == InsetInEye || Dll.User1 == ObsInEye )
		{
			Dll.GetInEyePos( Dll.User2, ref origin, ref angles );
		}
		else if ( Pip == InsetChaseFree || Dll.User1 == ObsChaseFree )
		{
			Dll.GetChasePos( Dll.User2, Dll.LastClientAngles, ref origin, ref angles );
		}
		else if ( Dll.User1 == ObsRoaming )
		{
			origin = Dll.LastSimOrigin;
			angles = Dll.LastClientAngles;
		}
		else
		{
			Dll.GetChasePos( Dll.User2, null, ref origin, ref angles );
		}

		angles.x = 0f;

		Engine.TriRenderMode( RenderMode.TransAdd );
		Engine.TriSpriteTexture( Engine.GetSpritePointer( _spriteCamera ), 0 );
		Engine.TriColor4f( r, g, b, 1f );

		MathLib.AngleVectors( angles, out var forward, out _, out _ );

		forward *= 512f;

		var matrix = new float[3, 4];

		MathLib.AngleMatrix( new Vector3( 0f, 45f, 0f ), matrix );

		var coneRight = MathLib.VectorTransform( forward, matrix );

		MathLib.AngleMatrix( new Vector3( 0f, -45f, 0f ), matrix );

		var coneLeft = MathLib.VectorTransform( forward, matrix );

		Engine.TriBegin( TriPrimitive.Triangles );
		Engine.TriTexCoord2f( 0f, 0f );
		Engine.TriVertex3f( origin.x + coneRight.x, origin.y + coneRight.y, (origin.z + coneRight.z) * zScale );
		Engine.TriTexCoord2f( 0f, 1f );
		Engine.TriVertex3f( origin.x, origin.y, origin.z * zScale );
		Engine.TriTexCoord2f( 1f, 1f );
		Engine.TriVertex3f( origin.x + coneLeft.x, origin.y + coneLeft.y, (origin.z + coneLeft.z) * zScale );
		Engine.TriEnd();
	}

	private void OverviewVertex( Vector3 point, float zScale )
	{
		point.z *= zScale;

		Engine.TriVertex3fv( point );
	}

	private float XProject( float x ) => (1f + x) * Hud.ScreenWidth * 0.5f;

	private float YProject( float y ) => (1f - y) * Hud.ScreenHeight * 0.5f;

	public void DrawOverview()
	{
		if ( Dll.User1 == 0 )
			return;

		if ( DrawCycle == 0 && Dll.User1 != ObsMapFree && Dll.User1 != ObsMapChase )
			return;

		if ( DrawCycle == 1 && Pip < InsetMapFree )
			return;

		DrawOverviewLayer();
		DrawOverviewEntities();
		CheckOverviewEntities();
	}

	public void CheckOverviewEntities()
	{
		var time = Engine.ClientTime;

		for ( var i = 0; i < MaxOverviewEntities; i++ )
		{
			if ( _overviewEntities[i].KillTime < time )
				_overviewEntities[i] = default;
		}
	}

	public bool AddOverviewEntity( int type, ClientEntity entity, string modelName )
	{
		const double duration = -1.0;

		if ( entity is null )
			return false;

		if ( type != ClientDll.EntityTypePlayer )
			return false;

		if ( entity.CurState.Solid == (int)Solid.Not )
			return false;

		var sprite = Hud.GetExtraPlayerInfo( entity.Index ).TeamNumber switch
		{
			1 => _spritePlayerBlue,
			2 => _spritePlayerRed,
			_ => _spritePlayer
		};

		return AddOverviewEntityToList( sprite, entity, Engine.ClientTime + duration );
	}

	public void DeathMessage( int victim )
	{
		if ( Engine.GetEntityByIndex( victim ) is { Player: true } player )
			AddOverviewEntityToList( _spritePlayerDead, player, Engine.ClientTime + 2f );
	}

	public bool AddOverviewEntityToList( int sprite, ClientEntity entity, double killTime )
	{
		for ( var i = 0; i < MaxOverviewEntities; i++ )
		{
			if ( _overviewEntities[i].Entity is not null )
				continue;

			_overviewEntities[i].Entity = entity;
			_overviewEntities[i].Sprite = sprite;
			_overviewEntities[i].KillTime = killTime;

			return true;
		}

		return false;
	}

	public void CheckSettings()
	{
		Pip = (int)Pip;

		if ( Dll.User1 < ObsMapFree && (Pip == InsetChaseFree || Pip == InsetInEye) )
			Pip = InsetMapFree;

		if ( Dll.User1 >= ObsMapFree && Pip >= InsetMapFree )
			Pip = InsetChaseFree;

		if ( Hud.IntermissionActive )
			Pip = InsetOff;

		if ( _chatEnabled != (HudSayText.SayText != 0f) )
		{
			_chatEnabled = (HudSayText.SayText != 0f);

			if ( Engine.IsSpectateOnly )
				Engine.ServerCmd( $"ignoremsg {(_chatEnabled ? 0 : 1)}" );
		}

		SetObserverCrosshair();

		if ( Dll.TeamNumber is 1 or 2 && Dll.User1 == ObsInEye )
			Pip = InsetOff;

		ViewPort.SpectatorPanel.EnableInsetView( Pip != InsetOff );
	}

	public int ToggleInset( bool allowOff )
	{
		var newInsetMode = (int)Pip + 1;

		if ( Dll.User1 < ObsMapFree )
		{
			if ( newInsetMode > InsetMapChase )
				newInsetMode = allowOff ? InsetOff : InsetMapFree;

			if ( newInsetMode == InsetChaseFree )
				newInsetMode = InsetMapFree;
		}
		else if ( newInsetMode > InsetInEye )
		{
			newInsetMode = allowOff ? InsetOff : InsetChaseFree;
		}

		return newInsetMode;
	}

	public override void Reset()
	{
		if ( OverviewData.Map != Engine.LevelName )
		{
			ParseOverviewFile();
			LoadMapSprites();
		}

		Array.Clear( _overviewEntities );

		_fov = 90f;
		_isInterpolating = false;
		_chaseEntity = 0;

		SetSpectatorStartPosition();
	}

	public override void InitHudData()
	{
		_lastPrimaryObject = _lastSecondaryObject = 0;
		_nextObserverInput = 0f;
		_lastHudMessage = 0;

		SpectatorNumber = 0;

		Dll.JumpSpectator = false;
		Dll.User1 = Dll.User2 = 0;

		OverviewData = default;

		Array.Clear( _overviewEntities );

		AutoDirector = Engine.IsSpectateOnly || Engine.DemoPlayback ? 1f : 0f;

		Reset();

		SetModes( ObsChaseLocked, InsetOff );

		Dll.User2 = 0;

		Hud.Fov = (int)Cvar.GetFloat( "default_fov" );
	}
}
