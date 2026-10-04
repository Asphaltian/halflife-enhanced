using System;

namespace GoldSrc;

public static class Keys
{
	[SkipHotload]
	private static readonly (string Action, string Binding)[] Bindings =
	[
		("forward", "+forward"),
		("back", "+back"),
		("left", "+left"),
		("right", "+right"),
		("moveleft", "+moveleft"),
		("moveright", "+moveright"),
		("jump", "+jump"),
		("duck", "+duck"),
		("moveup", "+moveup"),
		("movedown", "+movedown"),
		("lookup", "+lookup"),
		("lookdown", "+lookdown"),
		("force_centerview", "force_centerview"),
		("strafe", "+strafe"),
		("mlook", "+mlook"),
		("klook", "+klook"),
		("use", "+use"),
		("voicerecord", "+voicerecord"),
		("messagemode", "messagemode"),
		("messagemode2", "messagemode2"),
		("attack", "+attack"),
		("attack2", "+attack2"),
		("reload", "+reload"),
		("speed", "+speed"),
		("impulse_100", "impulse 100"),
		("impulse_201", "impulse 201"),
		("slot1", "slot1"),
		("slot2", "slot2"),
		("slot3", "slot3"),
		("slot4", "slot4"),
		("slot5", "slot5"),
		("invprev", "invprev"),
		("invnext", "invnext"),
		("lastinv", "lastinv"),
		("showscores", "+showscores"),
		("snapshot", "snapshot"),
		("save_quick", "save quick"),
		("load_quick", "load quick"),
		("pause", "pause"),
		("quit", "quit")
	];

	public static IReadOnlyList<(string Action, string Binding)> ActionBindings => Bindings;

	[SkipHotload]
	private static readonly (string Name, string Display)[] KeyNames =
	[
		("", ""),
		("0", "0"),
		("1", "1"),
		("2", "2"),
		("3", "3"),
		("4", "4"),
		("5", "5"),
		("6", "6"),
		("7", "7"),
		("8", "8"),
		("9", "9"),
		("a", "A"),
		("b", "B"),
		("c", "C"),
		("d", "D"),
		("e", "E"),
		("f", "F"),
		("g", "G"),
		("h", "H"),
		("i", "I"),
		("j", "J"),
		("k", "K"),
		("l", "L"),
		("m", "M"),
		("n", "N"),
		("o", "O"),
		("p", "P"),
		("q", "Q"),
		("r", "R"),
		("s", "S"),
		("t", "T"),
		("u", "U"),
		("v", "V"),
		("w", "W"),
		("x", "X"),
		("y", "Y"),
		("z", "Z"),
		("kp_0", "Keypad 0"),
		("kp_1", "Keypad 1"),
		("kp_2", "Keypad 2"),
		("kp_3", "Keypad 3"),
		("kp_4", "Keypad 4"),
		("kp_5", "Keypad 5"),
		("kp_6", "Keypad 6"),
		("kp_7", "Keypad 7"),
		("kp_8", "Keypad 8"),
		("kp_9", "Keypad 9"),
		("kp_divide", "Keypad /"),
		("kp_multiply", "Keypad *"),
		("kp_minus", "Keypad -"),
		("kp_plus", "Keypad +"),
		("kp_enter", "Keypad Enter"),
		("kp_del", "Keypad ."),
		("[", "["),
		("]", "]"),
		("semicolon", ";"),
		("'", "'"),
		("`", "`"),
		(",", ","),
		(".", "."),
		("/", "/"),
		("\\", "\\"),
		("-", "-"),
		("=", "="),
		("enter", "Return"),
		("space", "Space"),
		("backspace", "Backspace"),
		("tab", "Tab"),
		("capslock", "CapsLock"),
		("numlock", ""),
		("escape", "Escape"),
		("scrolllock", "ScrollLock"),
		("ins", "Insert"),
		("del", "Delete"),
		("home", "Home"),
		("end", "End"),
		("pgup", "PageUp"),
		("pgdn", "PageDown"),
		("pause", "Pause"),
		("shift", "Left Shift"),
		("rshift", "Right Shift"),
		("alt", "Left Alt"),
		("ralt", "Right Alt"),
		("ctrl", "Left Ctrl"),
		("rctrl", "Right Ctrl"),
		("lwin", "Left GUI"),
		("rwin", "Right GUI"),
		("app", "Application"),
		("uparrow", "Up"),
		("leftarrow", "Left"),
		("downarrow", "Down"),
		("rightarrow", "Right"),
		("f1", "F1"),
		("f2", "F2"),
		("f3", "F3"),
		("f4", "F4"),
		("f5", "F5"),
		("f6", "F6"),
		("f7", "F7"),
		("f8", "F8"),
		("f9", "F9"),
		("f10", "F10"),
		("f11", "F11"),
		("f12", "F12")
	];

	[SkipHotload]
	private static readonly string[] MouseNames = ["mouse1", "mouse2", "mouse3", "mouse4", "mouse5"];

	public static string NameForKey( int keyCode ) => keyCode > 0 && keyCode < KeyNames.Length ? KeyNames[keyCode].Name : null;

	public static string NameForMouse( int mouseCode ) => MouseNames[mouseCode >= 0 && mouseCode < MouseNames.Length ? mouseCode : 0];

	public static string DisplayName( string key )
	{
		if ( key.Equals( "mwheelup", StringComparison.OrdinalIgnoreCase ) )
			return "Mouse Wheel Up";

		if ( key.Equals( "mwheeldown", StringComparison.OrdinalIgnoreCase ) )
			return "Mouse Wheel Down";

		foreach ( var (name, display) in KeyNames )
		{
			if ( name.Equals( key, StringComparison.OrdinalIgnoreCase ) )
				return display;
		}

		return key.ToUpperInvariant();
	}

	private const string UnboundCookie = "unbound.";
	private const string AlternateCookie = "altbind.";

	private static string[] _alternates = new string[Bindings.Length];

	[SkipHotload]
	private static readonly (string Name, int Number)[] KeyNumbers =
	[
		("tab", 9), ("enter", 13), ("escape", 27), ("space", 32), ("semicolon", 59), ("backspace", 127),
		("uparrow", 128), ("downarrow", 129), ("leftarrow", 130), ("rightarrow", 131), ("alt", 132), ("ctrl", 133), ("shift", 134),
		("f1", 135), ("f2", 136), ("f3", 137), ("f4", 138), ("f5", 139), ("f6", 140), ("f7", 141), ("f8", 142), ("f9", 143), ("f10", 144), ("f11", 145), ("f12", 146),
		("ralt", 132), ("rctrl", 133), ("rshift", 134),
		("ins", 147), ("del", 148), ("pgdn", 149), ("pgup", 150), ("home", 151), ("end", 152),
		("kp_7", 160), ("kp_8", 161), ("kp_9", 162), ("kp_4", 163), ("kp_5", 164), ("kp_6", 165), ("kp_1", 166), ("kp_2", 167), ("kp_3", 168),
		("kp_enter", 169), ("kp_0", 170), ("kp_del", 171), ("kp_divide", 172), ("kp_minus", 173), ("kp_plus", 174), ("capslock", 175), ("kp_multiply", 176),
		("lwin", 177), ("rwin", 177),
		("mwheeldown", 239), ("mwheelup", 240), ("mouse1", 241), ("mouse2", 242), ("mouse3", 243), ("mouse4", 244), ("mouse5", 245), ("pause", 255)
	];

	private const int FirstReservedKey = 135;
	private const int LastReservedKey = 146;
	private const int EscapeKey = 27;
	private const int FirstMouseKey = 239;
	private const int LastMouseKey = 245;

	public static int KeyNumber( string key )
	{
		if ( key.Length == 1 )
			return char.ToLowerInvariant( key[0] );

		foreach ( var (name, number) in KeyNumbers )
		{
			if ( name.Equals( key, StringComparison.OrdinalIgnoreCase ) )
				return number;
		}

		return 0;
	}

	private static bool IsReservedKey( string key ) => KeyNumber( key ) is EscapeKey or (>= FirstReservedKey and <= LastReservedKey);

	public static List<(string Key, string Binding)> DefaultBindings()
	{
		var defaults = new List<(string Key, string Binding)>();

		if ( Server.LoadMountText( "gfx/shell/kb_def.lst" ) is not { } data )
			return defaults;

		var position = 0;

		while ( Common.Parse( data, ref position ) is { Length: > 0 } key )
		{
			if ( Common.Parse( data, ref position ) is not { Length: > 0 } binding )
				break;

			if ( key == ";" )
				key = "semicolon";

			if ( !IsReservedKey( key ) )
				defaults.Add( (key.ToLowerInvariant(), binding) );
		}

		return defaults;
	}

	public static void ReadArchive()
	{
		var defaults = DefaultBindings();

		_alternates = new string[Bindings.Length];

		for ( var i = 0; i < Bindings.Length; i++ )
		{
			var (action, binding) = Bindings[i];

			if ( Game.Cookies.Get( UnboundCookie + action, false ) )
				IGameInstance.Current?.SetBind( action, "" );

			if ( Game.Cookies.TryGet<string>( AlternateCookie + action, out var alternate ) )
			{
				_alternates[i] = alternate;
				continue;
			}

			var primary = GetBind( action );

			foreach ( var (key, command) in defaults )
			{
				if ( command.Equals( binding, StringComparison.OrdinalIgnoreCase ) && !key.Equals( primary, StringComparison.OrdinalIgnoreCase ) )
					_alternates[i] = key;
			}
		}
	}

	private static int ActionIndex( string action )
	{
		for ( var i = 0; i < Bindings.Length; i++ )
		{
			if ( Bindings[i].Action.Equals( action, StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	private static Dictionary<string, string> _unboundCookies = [];

	private static string UnboundCookieName( string action )
	{
		if ( !_unboundCookies.TryGetValue( action, out var name ) )
			_unboundCookies[action] = name = UnboundCookie + action;

		return name;
	}

	public static string GetBind( string action )
	{
		return Game.Cookies.Get( UnboundCookieName( action ), false ) ? "" : IGameInstance.Current?.GetBind( action, out _, out _ );
	}

	public static string GetAlternateBind( string action ) => ActionIndex( action ) is >= 0 and var index ? _alternates[index] : "";

	private static bool IsMouseKey( string key ) => KeyNumber( key ) is >= FirstMouseKey and <= LastMouseKey;

	public static void SetBinds( string action, string key, string alternate )
	{
		if ( ActionIndex( action ) is not (>= 0 and var index) )
			return;

		if ( IsMouseKey( alternate ) && !IsMouseKey( key ) )
			(key, alternate) = (alternate, key);

		IGameInstance.Current?.SetBind( action, key );

		_alternates[index] = alternate;

		Game.Cookies.Set( UnboundCookie + action, string.IsNullOrEmpty( key ) );
		Game.Cookies.Set( AlternateCookie + action, alternate );
	}

	public static void SaveBinds() => IGameInstance.Current?.SaveBinds();

	public static string LookupBinding( string binding )
	{
		foreach ( var (action, command) in Bindings )
		{
			var name = command[0] == '+' ? command[1..] : command;

			if ( !name.StartsWith( binding, StringComparison.Ordinal ) )
				continue;

			var key = Input.GetButtonOrigin( action );

			if ( !string.IsNullOrEmpty( key ) )
				return key.Length > 1 ? key.ToUpperInvariant() : key;
		}

		return null;
	}

	private static void KeyDown( string binding, int keyNumber )
	{
		Cmd.AddText( binding[0] == '+' ? $"{binding} {keyNumber}\n" : $"{binding}\n" );
	}

	private static void KeyUp( string binding, int keyNumber )
	{
		if ( binding[0] == '+' )
			Cmd.AddText( $"-{binding[1..]} {keyNumber}\n" );
	}

	private static bool IsBoundTo( string key, string bind ) => key.Equals( bind, StringComparison.OrdinalIgnoreCase );

	public static void KeyEvent( string key, bool down )
	{
		for ( var i = 0; i < Bindings.Length; i++ )
		{
			var (action, binding) = Bindings[i];
			var keyNumber = IsBoundTo( key, GetBind( action ) ?? "" ) ? i + 1 : IsBoundTo( key, _alternates[i] ?? "" ) ? Bindings.Length + i + 1 : 0;

			if ( keyNumber == 0 )
				continue;

			if ( down )
				KeyDown( binding, keyNumber );
			else
				KeyUp( binding, keyNumber );
		}
	}

	private static HashSet<string> _gameKeysDown = new( StringComparer.OrdinalIgnoreCase );
	private static HashSet<string> _blockedKeys = new( StringComparer.OrdinalIgnoreCase );

	private static string KeyBinding( string key )
	{
		for ( var i = 0; i < Bindings.Length; i++ )
		{
			if ( IsBoundTo( key, GetBind( Bindings[i].Action ) ?? "" ) || IsBoundTo( key, _alternates[i] ?? "" ) )
				return Bindings[i].Binding;
		}

		return null;
	}

	private static void ClientKeyEvent( string key, bool down )
	{
		if ( !down && !_gameKeysDown.Contains( key ) )
			return;

		if ( ClientDll.Loaded is not { } client || client.KeyEvent( down, KeyNumber( key ), KeyBinding( key ) ) )
		{
			if ( !down )
				_gameKeysDown.Remove( key );

			return;
		}

		_blockedKeys.Add( key );

		if ( down )
			_gameKeysDown.Add( key );
		else
			_gameKeysDown.Remove( key );
	}

	private static bool[] _mouseOldButtonState = new bool[MouseNames.Length];
	private static bool[] _mousePressed = new bool[MouseNames.Length];
	private static bool[] _mouseReleased = new bool[MouseNames.Length];

	private static void MouseEvent()
	{
		Array.Clear( _mousePressed );
		Array.Clear( _mouseReleased );

		if ( ClientDll.Loaded is { } client && !client.MouseEvent() )
			return;

		for ( var i = 0; i < MouseNames.Length; i++ )
		{
			var down = Input.Keyboard.Down( MouseNames[i] );

			_mousePressed[i] = down && !_mouseOldButtonState[i];
			_mouseReleased[i] = !down && _mouseOldButtonState[i];
			_mouseOldButtonState[i] = down;
		}
	}

	private static int MouseIndex( string key )
	{
		for ( var i = 0; i < MouseNames.Length; i++ )
		{
			if ( IsBoundTo( MouseNames[i], key ) )
				return i;
		}

		return -1;
	}

	private static bool KeyPressed( string key, string action )
	{
		var mouse = MouseIndex( key );

		if ( mouse >= 0 )
			return _mousePressed[mouse];

		return action is null ? Input.Keyboard.Pressed( key ) : Input.Pressed( action );
	}

	private static bool KeyReleased( string key, string action )
	{
		var mouse = MouseIndex( key );

		if ( mouse >= 0 )
			return _mouseReleased[mouse];

		return action is null ? Input.Keyboard.Released( key ) : Input.Released( action );
	}

	private static void ClientKeyEvents( string wheelKey )
	{
		_blockedKeys.Clear();

		MouseEvent();

		if ( ClientDll.Loaded?.Engine is not { GameUiActive: false, MessageMode: false } )
			return;

		foreach ( var (name, _) in KeyNames )
		{
			if ( name.Length == 0 )
				continue;

			if ( Input.Keyboard.Pressed( name ) )
				ClientKeyEvent( name, true );

			if ( Input.Keyboard.Released( name ) )
				ClientKeyEvent( name, false );
		}

		for ( var i = 0; i < MouseNames.Length; i++ )
		{
			if ( _mousePressed[i] )
				ClientKeyEvent( MouseNames[i], true );

			if ( _mouseReleased[i] )
				ClientKeyEvent( MouseNames[i], false );
		}

		if ( wheelKey is not null )
		{
			ClientKeyEvent( wheelKey, true );
			ClientKeyEvent( wheelKey, false );
		}
	}

	public static void Frame()
	{
		var wheel = Input.MouseWheel.y;
		var wheelKey = wheel > 0f ? "mwheelup" : wheel < 0f ? "mwheeldown" : null;

		ClientKeyEvents( wheelKey );

		for ( var i = 0; i < Bindings.Length; i++ )
		{
			var (action, binding) = Bindings[i];
			var primary = GetBind( action ) ?? "";
			var alternate = _alternates[i] ?? "";
			var alternateKey = Bindings.Length + i + 1;

			if ( !_blockedKeys.Contains( primary ) )
			{
				if ( wheelKey is not null && IsBoundTo( wheelKey, primary ) )
				{
					KeyDown( binding, i + 1 );
					KeyUp( binding, i + 1 );
					continue;
				}

				if ( KeyPressed( primary, action ) )
					KeyDown( binding, i + 1 );

				if ( KeyReleased( primary, action ) )
					KeyUp( binding, i + 1 );
			}

			if ( alternate.Length == 0 || _blockedKeys.Contains( alternate ) )
				continue;

			if ( wheelKey is not null && IsBoundTo( wheelKey, alternate ) )
			{
				KeyDown( binding, alternateKey );
				KeyUp( binding, alternateKey );
				continue;
			}

			if ( KeyPressed( alternate, null ) )
				KeyDown( binding, alternateKey );

			if ( KeyReleased( alternate, null ) )
				KeyUp( binding, alternateKey );
		}

		ControllerConfig.Frame();
	}
}
