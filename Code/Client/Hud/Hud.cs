using System;
using GoldSrc;

namespace HalfLife;

public sealed class ExtraPlayerInfo
{
	public short Frags;
	public short Deaths;
	public short PlayerClass;
	public short TeamNumber;
	public string TeamName = "";
}

public sealed class TeamInfo
{
	public string Name = "";
	public short Frags;
	public short Deaths;
	public short Ping;
	public short PacketLoss;
	public short OwnTeam;
	public short Players;
	public bool AlreadyDrawn;
	public bool ScoresOverriden;
	public int TeamNumber;

	public void Clear()
	{
		Name = "";
		Frags = Deaths = Ping = PacketLoss = OwnTeam = Players = 0;
		AlreadyDrawn = ScoresOverriden = false;
		TeamNumber = 0;
	}
}

public sealed class HalfLifeVoiceStatusHelper( Hud hud ) : IVoiceStatusHelper
{
	public void GetPlayerTextColor( int entIndex, int[] color )
	{
		color[0] = color[1] = color[2] = 255;

		if ( entIndex >= 0 && entIndex < Hud.MaxPlayers + 1 )
		{
			var team = (int)hud.GetExtraPlayerInfo( entIndex ).TeamNumber;

			if ( team < 0 )
				team = 0;

			team %= VguiUtil.NumberOfTeamColors;

			color[0] = VguiUtil.TeamColors[team, 0];
			color[1] = VguiUtil.TeamColors[team, 1];
			color[2] = VguiUtil.TeamColors[team, 2];
		}
	}

	public void UpdateCursorState() => hud.ViewPort.UpdateCursorState();

	public int GetAckIconHeight() => hud.ScreenHeight - (hud.FontHeight * 3) - 6;

	public bool CanShowSpeakerLabels()
	{
		if ( hud.ViewPort is { ScoreBoard: { } scoreBoard } )
			return !scoreBoard.IsVisible();

		return false;
	}
}

public sealed class Hud( HalfLifeClientDll dll )
{
	public const int RgbYellowish = 0x00FFA000;
	public const int RgbRedish = 0x00FF1010;
	public const int RgbGreenish = 0x0000A000;

	public const int DrawZero = 1;
	public const int Draw2Digits = 2;
	public const int Draw3Digits = 4;
	public const int MinAlpha = 100;
	public const int FadeTime = 100;

	public const int HideWeapons = 1 << 0;
	public const int HideFlashlight = 1 << 1;
	public const int HideAll = 1 << 2;
	public const int HideHealth = 1 << 3;

	public const int WeaponSuit = 31;

	public const int MaxPlayers = 64;
	public const int MaxPlayerNameLength = 32;
	public const int MaxTeamName = 16;
	public const int MaxTeams = 64;

	private const int MaxSpriteNameLength = 24;
	private const int MaxLogoFrames = 56;

	private static readonly int[] LogoFrame =
	[
		1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 13, 13, 13, 13, 13, 12, 11, 10, 9, 8, 14, 15,
		16, 17, 18, 19, 20, 20, 20, 20, 20, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
		29, 29, 29, 29, 29, 28, 27, 26, 25, 24, 30, 31
	];

	private readonly List<HudBase> _hudList = [];
	private readonly ExtraPlayerInfo[] _playerExtraInfo = new ExtraPlayerInfo[MaxPlayers + 1];

	public readonly HudPlayerInfo[] PlayerInfoList = new HudPlayerInfo[MaxPlayers + 1];
	public readonly TeamInfo[] TeamInfo = new TeamInfo[MaxTeams + 1];
	public readonly int[] IsSpectator = new int[MaxPlayers + 1];

	[ConVar( "hud_classautokill" )]
	public static float ClassAutoKill { get; set; } = 1f;

	[ConVar( "hud_takesshots" )]
	private static float TakeScreenshots { get; set; } = 0f;

	[ConVar( "zoom_sensitivity_ratio" )]
	private static float ZoomSensitivityRatio { get; set; } = 1.2f;

	[ConVar( "cl_autowepswitch" )]
	private static float AutoWeaponSwitch { get; set; } = 1f;

	private static float DefaultFov => Preferences.FieldOfView;

	public float MouseSensitivity { get; private set; }

	[ConVar( "hud_capturemouse" )]
	public static float CaptureMouse { get; set; } = 1f;

	[ConVar( "hud_draw" )]
	public static float DrawHud { get; set; } = 1f;
	private ClientSprite[] _spriteList;
	private int[] _sprites;
	private SpriteRect[] _spriteRects;
	private string[] _spriteNames;
	private int _spriteLogo;
	private int _logo;
	private static bool _benchStarted;
	private static float _shotTime;
	private int _concussionEffect;
	private float _lastFov;

	internal float LastFov
	{
		get => _lastFov;
		set => _lastFov = value;
	}

	public HalfLifeClientDll Dll { get; } = dll;

	public Client Engine => Dll.Engine;

	public float Time { get; private set; }

	public float OldTime { get; private set; }

	public double TimeDelta { get; private set; }

	public Vector3 Origin { get; private set; }

	public Vector3 Angles { get; private set; }

	public InButtons KeyBits { get; set; }

	public int HideHudDisplay { get; set; }

	public int Fov { get; set; }

	public int Teamplay { get; private set; }

	public int Res { get; private set; }

	public int FontHeight { get; private set; }

	public int WeaponBits { get; private set; }

	public bool IntermissionActive { get; private set; }

	public int NumberZero { get; private set; }

	public int ScreenWidth => Engine.ScreenWidth;

	public int ScreenHeight => Engine.ScreenHeight;

	public bool PlayerDead { get; set; }

	public HudAmmo Ammo { get; private set; }

	public HudHealth Health { get; private set; }

	public HudGeiger Geiger { get; private set; }

	public HudTrain Train { get; private set; }

	public HudBattery Battery { get; private set; }

	public HudFlashlight Flash { get; private set; }

	public HudMessage Message { get; private set; }

	public HudAmmoSecondary AmmoSecondary { get; private set; }

	public HudSayText SayText { get; private set; }

	public HudTextMessage TextMessage { get; private set; }

	public HudStatusBar StatusBar { get; private set; }

	public HudDeathNotice DeathNotice { get; private set; }

	public HudStatusIcons StatusIcons { get; private set; }

	public HudMenu Menu { get; private set; }

	public VoiceStatus Voice { get; private set; }

	public HudSpectator Spectator { get; private set; }

	public HudServers Servers { get; private set; }

	public TeamFortressViewport ViewPort { get; set; }

	public ExtraPlayerInfo GetExtraPlayerInfo( int index ) => _playerExtraInfo[index];

	public void ClearPlayerExtraInfo()
	{
		foreach ( var info in _playerExtraInfo )
		{
			info.Frags = info.Deaths = info.PlayerClass = info.TeamNumber = 0;
			info.TeamName = "";
		}
	}

	public void ClearTeamInfo()
	{
		foreach ( var team in TeamInfo )
			team.Clear();
	}

	public void Init()
	{
		Dll.HookUserMsg( "Logo", MsgFuncLogo );
		Dll.HookUserMsg( "ResetHUD", MsgFuncResetHud );
		Dll.HookUserMsg( "GameMode", MsgFuncGameMode );
		Dll.HookUserMsg( "InitHUD", MsgFuncInitHud );
		Dll.HookUserMsg( "SetFOV", MsgFuncSetFov );
		Dll.HookUserMsg( "Concuss", MsgFuncConcuss );
		Dll.HookUserMsg( "ViewMode", MsgFuncViewMode );
		Dll.HookUserMsg( "ValClass", MsgFuncValClass );
		Dll.HookUserMsg( "TeamNames", MsgFuncTeamNames );
		Dll.HookUserMsg( "Feign", MsgFuncFeign );
		Dll.HookUserMsg( "Detpack", MsgFuncDetpack );
		Dll.HookUserMsg( "MOTD", MsgFuncMOTD );
		Dll.HookUserMsg( "BuildSt", MsgFuncBuildSt );
		Dll.HookUserMsg( "RandomPC", MsgFuncRandomPC );
		Dll.HookUserMsg( "ServerName", MsgFuncServerName );
		Dll.HookUserMsg( "ScoreInfo", MsgFuncScoreInfo );
		Dll.HookUserMsg( "TeamScore", MsgFuncTeamScore );
		Dll.HookUserMsg( "TeamInfo", MsgFuncTeamInfo );
		Dll.HookUserMsg( "Spectator", MsgFuncSpectator );
		Dll.HookUserMsg( "AllowSpec", MsgFuncAllowSpec );
		Dll.HookUserMsg( "SpecFade", MsgFuncSpecFade );
		Dll.HookUserMsg( "ResetFade", MsgFuncResetFade );
		Dll.HookUserMsg( "VGUIMenu", MsgFuncVGUIMenu );

		for ( var i = 0; i < _playerExtraInfo.Length; i++ )
		{
			_playerExtraInfo[i] = new ExtraPlayerInfo();
			PlayerInfoList[i] = new HudPlayerInfo();
		}

		for ( var i = 0; i < TeamInfo.Length; i++ )
			TeamInfo[i] = new TeamInfo();

		_logo = 0;
		Fov = 0;

		_spriteList = null;
		_hudList.Clear();

		Time = 1f;

		Ammo = new HudAmmo( this );
		Health = new HudHealth( this );
		SayText = new HudSayText( this );
		Spectator = new HudSpectator( this );
		Geiger = new HudGeiger( this );
		Train = new HudTrain( this );
		Battery = new HudBattery( this );
		Flash = new HudFlashlight( this );
		Message = new HudMessage( this );
		StatusBar = new HudStatusBar( this );
		DeathNotice = new HudDeathNotice( this );
		AmmoSecondary = new HudAmmoSecondary( this );
		TextMessage = new HudTextMessage( this );
		StatusIcons = new HudStatusIcons( this );
		Voice = new VoiceStatus( this );
		Menu = new HudMenu( this );

		Ammo.Init();
		Health.Init();
		SayText.Init();
		Spectator.Init();
		Geiger.Init();
		Train.Init();
		Battery.Init();
		Flash.Init();
		Message.Init();
		StatusBar.Init();
		DeathNotice.Init();
		AmmoSecondary.Init();
		TextMessage.Init();
		StatusIcons.Init();
		Voice.Init( new HalfLifeVoiceStatusHelper( this ) );
		Menu.Init();

		Servers = new HudServers( Engine );

		MsgFuncResetHud( null, [] );
	}

	public void AddHudElem( HudBase element ) => _hudList.Add( element );

	public int GetSprite( int index ) => index < 0 ? 0 : _sprites[index];

	public SpriteRect GetSpriteRect( int index ) => _spriteRects[index];

	public int GetSpriteIndex( string name )
	{
		if ( name.Length > MaxSpriteNameLength )
			name = name[..MaxSpriteNameLength];

		return _spriteNames is null ? -1 : Array.IndexOf( _spriteNames, name );
	}

	public int Resolution()
	{
		if ( ScreenWidth > 2560 && ScreenHeight > 1600 )
			return 2560;

		if ( ScreenWidth >= 1280 && ScreenHeight > 720 )
			return 1280;

		return ScreenWidth >= 640 ? 640 : 320;
	}

	public int LoadSprite( string format ) => Engine.SpriteLoad( format.Replace( "%d", Resolution().ToString() ) );

	public void VidInit()
	{
		_spriteLogo = 0;

		Res = Resolution();

		_spriteList ??= Engine.SpriteGetList( "sprites/hud.txt" );

		if ( _spriteList is not null )
		{
			var sprites = Array.FindAll( _spriteList, x => x.Res == Res );

			_sprites = new int[sprites.Length];
			_spriteRects = new SpriteRect[sprites.Length];
			_spriteNames = new string[sprites.Length];

			for ( var i = 0; i < sprites.Length; i++ )
			{
				var name = sprites[i].Name;

				_sprites[i] = Engine.SpriteLoad( $"sprites/{sprites[i].Sprite}.spr" );
				_spriteRects[i] = sprites[i].Rect;
				_spriteNames[i] = name.Length > MaxSpriteNameLength ? name[..MaxSpriteNameLength] : name;
			}
		}

		NumberZero = GetSpriteIndex( "number_0" );
		FontHeight = _spriteRects[NumberZero].Bottom - _spriteRects[NumberZero].Top;

		Ammo.VidInit();
		Health.VidInit();
		Spectator.VidInit();
		Geiger.VidInit();
		Train.VidInit();
		Battery.VidInit();
		Flash.VidInit();
		Message.VidInit();
		DeathNotice.VidInit();
		AmmoSecondary.VidInit();
		Voice.VidInit();
	}

	private bool MsgFuncLogo( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_logo = message.ReadByte();

		return true;
	}

	public bool MsgFuncResetHud( string name, byte[] data )
	{
		foreach ( var element in _hudList )
			element.Reset();

		_concussionEffect = 0;

		return true;
	}

	public bool MsgFuncInitHud( string name, byte[] data )
	{
		foreach ( var element in _hudList )
			element.InitHudData();

		Dll.ClearEgonBeams();

		return true;
	}

	private bool MsgFuncGameMode( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		Teamplay = message.ReadByte();

		return true;
	}

	private bool MsgFuncConcuss( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_concussionEffect = message.ReadByte();

		if ( _concussionEffect != 0 )
			StatusIcons.EnableIcon( "dmg_concuss", 255, 160, 0 );
		else
			StatusIcons.DisableIcon( "dmg_concuss" );

		return true;
	}

	private bool MsgFuncValClass( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncValClass( name, data );

	private bool MsgFuncTeamNames( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncTeamNames( name, data );

	private bool MsgFuncFeign( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncFeign( name, data );

	private bool MsgFuncDetpack( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncDetpack( name, data );

	private bool MsgFuncMOTD( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncMOTD( name, data );

	private bool MsgFuncBuildSt( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncBuildSt( name, data );

	private bool MsgFuncRandomPC( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncRandomPC( name, data );

	private bool MsgFuncServerName( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncServerName( name, data );

	private bool MsgFuncScoreInfo( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncScoreInfo( name, data );

	private bool MsgFuncTeamScore( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncTeamScore( name, data );

	private bool MsgFuncTeamInfo( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncTeamInfo( name, data );

	private bool MsgFuncSpectator( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncSpectator( name, data );

	private bool MsgFuncAllowSpec( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncAllowSpec( name, data );

	private bool MsgFuncViewMode( string name, byte[] data )
	{
		Dll.CamToFirstPerson();

		return true;
	}

	private bool MsgFuncSpecFade( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncSpecFade( name, data );

	private bool MsgFuncResetFade( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncResetFade( name, data );

	private bool MsgFuncVGUIMenu( string name, byte[] data ) => ViewPort is not null && ViewPort.MsgFuncVGUIMenu( name, data );

	public float[] GetClientColor( int clientIndex )
	{
		if ( clientIndex < 0 || clientIndex > MaxPlayers )
			return HudDeathNotice.ColorGrey;

		return _playerExtraInfo[clientIndex].TeamNumber switch
		{
			1 => HudDeathNotice.ColorBlue,
			2 => HudDeathNotice.ColorRed,
			3 => HudDeathNotice.ColorYellow,
			4 => HudDeathNotice.ColorGreen,
			0 => HudDeathNotice.ColorYellow,
			_ => HudDeathNotice.ColorGrey
		};
	}

	private bool MsgFuncSetFov( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var newFov = message.ReadByte();
		var defaultFov = (int)DefaultFov;

		if ( Cvar.GetFloat( "cl_lw" ) != 0f )
			return true;

		_lastFov = newFov;

		Fov = newFov == 0 ? defaultFov : newFov;

		if ( Fov == defaultFov )
			MouseSensitivity = 0f;
		else
			MouseSensitivity = HalfLifeClientDll.GetMouseSensitivity() * ((float)newFov / defaultFov) * ZoomSensitivityRatio;

		return true;
	}

	private const int DemoTypeSniperDot = 0;
	private const int DemoTypeZoom = 1;

	private readonly byte[] _demoBuffer = new byte[8];

	private int _demoSniper;
	private int _demoSniperDamage;
	private Vector3 _demoSniperAngles;
	private Vector3 _demoSniperOrigin;
	private float _demoZoom;

	public void DemoReadBuffer( int size, byte[] buffer )
	{
		var type = BitConverter.ToInt32( buffer, 0 );

		switch ( type )
		{
			case DemoTypeSniperDot:
				_demoSniper = BitConverter.ToInt32( buffer, 4 );

				if ( _demoSniper != 0 )
				{
					_demoSniperDamage = BitConverter.ToInt32( buffer, 8 );
					_demoSniperAngles = new Vector3( BitConverter.ToSingle( buffer, 12 ), BitConverter.ToSingle( buffer, 16 ), BitConverter.ToSingle( buffer, 20 ) );
					_demoSniperOrigin = new Vector3( BitConverter.ToSingle( buffer, 24 ), BitConverter.ToSingle( buffer, 28 ), BitConverter.ToSingle( buffer, 32 ) );
				}

				break;

			case DemoTypeZoom:
				_demoZoom = BitConverter.ToSingle( buffer, 4 );
				break;

			default:
				Log.Trace( "Unknown demo buffer type, skipping." );
				break;
		}
	}

	private float GetFov()
	{
		if ( Engine.DemoRecording )
		{
			BitConverter.TryWriteBytes( _demoBuffer.AsSpan( 0 ), DemoTypeZoom );
			BitConverter.TryWriteBytes( _demoBuffer.AsSpan( 4 ), _lastFov );

			Engine.WriteDemoUserBuffer( _demoBuffer.Length, _demoBuffer );
		}

		if ( Engine.DemoPlayback )
			_lastFov = _demoZoom;

		return _lastFov;
	}

	public void Think()
	{
		foreach ( var element in _hudList )
		{
			if ( (element.Flags & HudBase.Active) != 0 )
				element.Think();
		}

		var newFov = (int)GetFov();

		Fov = newFov == 0 ? (int)DefaultFov : newFov;

		if ( Fov == (int)DefaultFov )
			MouseSensitivity = 0f;
		else
			MouseSensitivity = HalfLifeClientDll.GetMouseSensitivity() * ((float)newFov / Math.Max( (int)DefaultFov, 90 )) * ZoomSensitivityRatio;

		if ( Fov == 0 )
			Fov = Math.Max( (int)DefaultFov, 90 );

		if ( Engine.IsSpectateOnly )
			Fov = (int)Spectator.GetFov();

		BenchCheckStart();
	}

	private void BenchCheckStart()
	{
		if ( _benchStarted )
			return;

		if ( !Engine.LevelName.Equals( "maps/ppdemo.bsp", StringComparison.OrdinalIgnoreCase ) )
			return;

		_benchStarted = true;

		Client.ClientCmd( "ppdemostart" );
	}

	public bool UpdateClientData( ref ClientData data, float time )
	{
		Origin = data.Origin;
		Angles = data.ViewAngles;

		KeyBits = Dll.ButtonBits( false );
		WeaponBits = data.WeaponBits;

		Think();

		data.Fov = Fov;

		Dll.IdleScale = _concussionEffect;

		Dll.ResetButtonBits( KeyBits );

		return true;
	}

	public void Redraw( float time, bool intermission )
	{
		OldTime = Time;
		Time = time;
		TimeDelta = (double)Time - OldTime;

		if ( TimeDelta < 0 )
			TimeDelta = 0;

		if ( ViewPort is not null )
		{
			if ( IntermissionActive && !intermission )
			{
				IntermissionActive = intermission;

				ViewPort.HideCommandMenu();
				ViewPort.HideScoreBoard();
				ViewPort.UpdateSpectatorPanel();
			}
			else if ( !IntermissionActive && intermission )
			{
				IntermissionActive = intermission;

				ViewPort.HideCommandMenu();
				ViewPort.HideVGUIMenu();
				ViewPort.ShowScoreBoard();
				ViewPort.UpdateSpectatorPanel();

				if ( TakeScreenshots != 0f )
					_shotTime = time + 1f;
			}
		}

		if ( _shotTime != 0f && _shotTime < time )
		{
			Client.ClientCmd( "snapshot" );

			_shotTime = 0f;
		}

		IntermissionActive = intermission;

		if ( DrawHud != 0f )
		{
			foreach ( var element in _hudList )
			{
				if ( !intermission )
				{
					if ( (element.Flags & HudBase.Active) != 0 && (HideHudDisplay & HideAll) == 0 )
						element.Draw( time );
				}
				else if ( (element.Flags & HudBase.Intermission) != 0 )
				{
					element.Draw( time );
				}
			}
		}

		if ( _logo == 0 )
			return;

		if ( _spriteLogo == 0 )
			_spriteLogo = LoadSprite( "sprites/%d_logo.spr" );

		Engine.SpriteSet( _spriteLogo, 250, 250, 250 );

		var x = ScreenWidth - Engine.SpriteWidth( _spriteLogo, 0 );
		var y = Engine.SpriteHeight( _spriteLogo, 0 ) / 2;
		var frame = (int)(time * 20) % MaxLogoFrames;

		Engine.SpriteDrawAdditive( LogoFrame[frame] - 1, x, y, null );
	}

	public static int Rand() => Game.Random.Next( 0, 0x8000 );

	public static void UnpackRgb( out int r, out int g, out int b, int rgb )
	{
		r = (rgb & 0xFF0000) >> 16;
		g = (rgb & 0xFF00) >> 8;
		b = rgb & 0xFF;
	}

	public static void ScaleColors( ref int r, ref int g, ref int b, int a )
	{
		var x = (float)a / 255;

		r = (int)(r * x);
		g = (int)(g * x);
		b = (int)(b * x);
	}

	public int DrawHudString( int x, int y, int maxX, string text, int r, int g, int b )
	{
		return x + Engine.DrawString( x, y, text, r, g, b );
	}

	public int DrawHudNumberString( int x, int y, int minX, int number, int r, int g, int b )
	{
		return DrawHudStringReverse( x, y, minX, number.ToString(), r, g, b );
	}

	public int DrawHudStringReverse( int x, int y, int minX, string text, int r, int g, int b )
	{
		return x - Engine.DrawStringReverse( x, y, text, r, g, b );
	}

	public int DrawHudNumber( int x, int y, int flags, int number, int r, int g, int b )
	{
		var width = GetSpriteRect( NumberZero ).Right - GetSpriteRect( NumberZero ).Left;

		if ( number > 0 )
		{
			int k;

			if ( number >= 100 )
			{
				k = number / 100;
				Engine.SpriteSet( GetSprite( NumberZero + k ), r, g, b );
				Engine.SpriteDrawAdditive( 0, x, y, GetSpriteRect( NumberZero + k ) );
				x += width;
			}
			else if ( (flags & Draw3Digits) != 0 )
			{
				x += width;
			}

			if ( number >= 10 )
			{
				k = number % 100 / 10;
				Engine.SpriteSet( GetSprite( NumberZero + k ), r, g, b );
				Engine.SpriteDrawAdditive( 0, x, y, GetSpriteRect( NumberZero + k ) );
				x += width;
			}
			else if ( (flags & (Draw3Digits | Draw2Digits)) != 0 )
			{
				x += width;
			}

			k = number % 10;
			Engine.SpriteSet( GetSprite( NumberZero + k ), r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y, GetSpriteRect( NumberZero + k ) );
			x += width;
		}
		else if ( (flags & DrawZero) != 0 )
		{
			Engine.SpriteSet( GetSprite( NumberZero ), r, g, b );

			if ( (flags & Draw3Digits) != 0 )
				x += width;

			if ( (flags & (Draw3Digits | Draw2Digits)) != 0 )
				x += width;

			Engine.SpriteDrawAdditive( 0, x, y, GetSpriteRect( NumberZero ) );
			x += width;
		}

		return x;
	}

	public static int GetNumWidth( int number, int flags )
	{
		if ( (flags & Draw3Digits) != 0 )
			return 3;

		if ( (flags & Draw2Digits) != 0 )
			return 2;

		if ( number <= 0 )
			return (flags & DrawZero) != 0 ? 1 : 0;

		if ( number < 10 )
			return 1;

		return number < 100 ? 2 : 3;
	}
}
