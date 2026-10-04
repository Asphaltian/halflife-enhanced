using System;
using GoldSrc.GameUI;

namespace GoldSrc;

partial class Client
{
	private GameUi _gameUi;
	private bool _gameUiActivated = true;
	private bool _doubleClickPending;
	private int _doubleClickTime;
	private int _doubleClickX;
	private int _doubleClickY;

	[ConVar( "sdl_double_click_size" )]
	private static float DoubleClickSize { get; set; } = 2f;

	[ConVar( "sdl_double_click_time" )]
	private static float DoubleClickTime { get; set; } = 400f;

	public bool GameUiActive { get; private set; }

	public string GameUiCursor
	{
		get
		{
			return _gameUi?.Cursor switch
			{
				CursorCode.SizeNwse => "sizenwse",
				CursorCode.SizeNesw => "sizenesw",
				CursorCode.SizeWe => "sizewe",
				CursorCode.SizeNs => "sizens",
				CursorCode.IBeam => "text",
				CursorCode.None => "none",
				_ => "arrow"
			};
		}
	}

	private void GameUiFrame( bool inGame )
	{
		if ( _gameUi is not null && (_gameUi.ScreenWide != ScreenWidth || _gameUi.ScreenTall != ScreenHeight) )
		{
			var previous = _gameUi;

			ServerBrowserDialog.Instance = null;

			_gameUi = new GameUi( ScreenWidth, ScreenHeight );

			if ( _gameUiActivated )
				_gameUi.ActivateGameUI();
			else
				_gameUi.HideGameUI();

			_gameUi.RestoreFrom( previous );
		}

		if ( _gameUi is null )
		{
			_gameUi = new GameUi( ScreenWidth, ScreenHeight );

			if ( _gameUiActivated )
				_gameUi.ActivateGameUI();
		}

		_hudCanvas ??= HudCanvas.Create( this );

		_gameUi.Frame( ScreenWidth, ScreenHeight, inGame, inGame && MaxClients > 1, Mouse.Position );

		if ( !inGame )
			_hudDraws.Clear();

		_hudDraws.AddRange( _gameUi.Draws );
	}

	private void GameUiEscape( bool inLevel )
	{
		if ( MessageMode )
		{
			KeyMessage( "escape" );
			return;
		}

		_gameUi?.KeyCodePressed( KeyCode.Escape );
		_gameUi?.KeyCodeTyped( KeyCode.Escape );
		_gameUi?.KeyCodeReleased( KeyCode.Escape );

		if ( _gameUiActivated && inLevel )
			HideGameUi();
		else
			ActivateGameUi();
	}

	[ConVar( "ui_cmd_on_connect" )]
	private static string UiCmdOnConnect { get; set; } = "";

	private void GameUiConnectToServer()
	{
		if ( _gameUi is null )
			return;

		GameUI.GameServerItem server = null;

		if ( Networking.IsClient && Connection.Host is { } host )
			server = new GameUI.GameServerItem { SteamId = host.SteamId, OwnerId = host.SteamId, ServerName = Networking.ServerName ?? "" };

		_gameUi.ConnectToServer( Server.GameDirectory, server );
	}

	public void ActivateGameUi()
	{
		if ( _gameUiActivated || _state == ClientState.Dedicated )
			return;

		_gameUiActivated = true;

		_gameUi?.ActivateGameUI();

		ClientCmd( "inputset_menu" );
		ClientCmd( "setpause" );
	}

	public void HideGameUi()
	{
		if ( LevelName.Length == 0 )
			return;

		_gameUiActivated = false;

		_gameUi?.HideGameUI();

		ClientCmd( "inputset_game" );
		ClientCmd( "unpause" );
	}

	private static MouseCode? GameUiMouseCode( string button )
	{
		return button switch
		{
			"mouseleft" => MouseCode.Left,
			"mouseright" => MouseCode.Right,
			"mousemiddle" => MouseCode.Middle,
			"mouseback" => MouseCode.Mouse4,
			"mouseforward" => MouseCode.Mouse5,
			_ => null
		};
	}

	[SkipHotload]
	private static readonly string[] GameUiKeyNames =
	[
		"", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a",
		"b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m",
		"n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y",
		"z", "pad_0", "pad_1", "pad_2", "pad_3", "pad_4", "pad_5", "pad_6", "pad_7", "pad_8", "pad_9", "pad_divide",
		"pad_multiply", "pad_minus", "pad_plus", "pad_enter", "pad_decimal", "lbracket", "rbracket", "semicolon", "apostrophe", "backquote", "comma", "period",
		"slash", "backslash", "minus", "equal", "enter", "space", "backspace", "tab", "capslock", "numlock", "escape", "scrolllock",
		"insert", "delete", "home", "end", "pageup", "pagedown", "break", "lshift", "rshift", "lalt", "ralt", "lcontrol",
		"rcontrol", "lwin", "rwin", "app", "up", "left", "down", "right", "f1", "f2", "f3", "f4",
		"f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12"
	];

	private static KeyCode GameUiKeyCode( string button )
	{
		var code = (KeyCode)Array.IndexOf( GameUiKeyNames, button );

		return code is > KeyCode.None and not KeyCode.Escape ? code : KeyCode.None;
	}

	private void GameUiModifiers( KeyboardModifiers modifiers )
	{
		GameUiModifier( (modifiers & KeyboardModifiers.Shift) != 0, KeyCode.LShift, KeyCode.RShift );
		GameUiModifier( (modifiers & KeyboardModifiers.Alt) != 0, KeyCode.LAlt, KeyCode.RAlt );
		GameUiModifier( (modifiers & KeyboardModifiers.Ctrl) != 0, KeyCode.LControl, KeyCode.RControl );
	}

	private void GameUiModifier( bool down, KeyCode left, KeyCode right )
	{
		var input = _gameUi.Vgui.Input;

		if ( down == (input.IsKeyDown( left ) || input.IsKeyDown( right )) )
			return;

		if ( down )
		{
			_gameUi.KeyCodePressed( left );
			return;
		}

		if ( input.IsKeyDown( left ) )
			_gameUi.KeyCodeReleased( left );

		if ( input.IsKeyDown( right ) )
			_gameUi.KeyCodeReleased( right );
	}

	public void GameUiMousePressed( string button )
	{
		if ( !GameUiActive || GameUiMouseCode( button ) is not { } code )
			return;

		var time = (int)(RealTime.Now * 1000f);
		var x = (int)Mouse.Position.x;
		var y = (int)Mouse.Position.y;

		if ( _doubleClickPending && time - _doubleClickTime <= DoubleClickTime && Math.Abs( x - _doubleClickX ) <= DoubleClickSize && Math.Abs( y - _doubleClickY ) <= DoubleClickSize )
		{
			_doubleClickPending = false;

			_gameUi?.MouseDoublePressed( code );
			return;
		}

		_doubleClickTime = time;
		_doubleClickX = x;
		_doubleClickY = y;
		_doubleClickPending = true;

		_gameUi?.MousePressed( code );
	}

	public void GameUiMouseReleased( string button )
	{
		if ( GameUiActive && GameUiMouseCode( button ) is { } code )
			_gameUi?.MouseReleased( code );
	}

	public void GameUiMouseWheel( int delta )
	{
		if ( GameUiActive )
			_gameUi?.MouseWheeled( delta );
	}

	public void GameUiKeyDown( string button, KeyboardModifiers modifiers )
	{
		var code = GameUiKeyCode( button );

		if ( !GameUiActive || _gameUi is null || code == KeyCode.None )
			return;

		if ( code is < KeyCode.LShift or > KeyCode.RControl )
			GameUiModifiers( modifiers );

		_gameUi.KeyCodePressed( code );
		_gameUi.KeyCodeTyped( code );
	}

	public void GameUiKeyUp( string button )
	{
		var code = GameUiKeyCode( button );

		if ( GameUiActive && code != KeyCode.None )
			_gameUi?.KeyCodeReleased( code );
	}

	public void GameUiPaste( string text )
	{
		if ( !GameUiActive || _gameUi is null )
			return;

		_gameUi.Vgui.ClipboardText = text;

		GameUiModifiers( KeyboardModifiers.Ctrl );

		_gameUi.KeyCodeTyped( KeyCode.V );
	}

	public void GameUiCopy( bool cut )
	{
		if ( !GameUiActive || _gameUi is null )
			return;

		GameUiModifiers( KeyboardModifiers.Ctrl );

		_gameUi.KeyCodeTyped( cut ? KeyCode.X : KeyCode.C );
	}

	public void GameUiCharTyped( char character )
	{
		if ( GameUiActive )
			_gameUi?.KeyTyped( character );
	}
}
