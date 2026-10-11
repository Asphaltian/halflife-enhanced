using System;

namespace GoldSrc;

public static class ControllerConfig
{
	private const string ManifestFile = "controller_configs/hl1_manifest.vdf";
	private const string ConfigDirectory = "controller_configs/";
	private const string ControllerType = "controller_xboxone";
	private const string GameSet = "HL_GameDefault";
	private const string MenuSet = "HL_MenuDefault";
	private const string MultiplayerLayer = "HL_LayerMultiplayer";
	private const string FullPress = "Full_Press";

	[SkipHotload]
	private static readonly (string Action, string Source, string Input)[] Buttons =
	[
		("button_a", "button_diamond", "button_a"),
		("button_b", "button_diamond", "button_b"),
		("button_x", "button_diamond", "button_x"),
		("button_y", "button_diamond", "button_y"),
		("dpad_north", "dpad", "dpad_north"),
		("dpad_south", "dpad", "dpad_south"),
		("dpad_east", "dpad", "dpad_east"),
		("dpad_west", "dpad", "dpad_west"),
		("button_menu", "switch", "button_menu"),
		("left_bumper", "switch", "left_bumper"),
		("right_bumper", "switch", "right_bumper"),
		("left_trigger", "left_trigger", "edge"),
		("right_trigger", "right_trigger", "edge"),
		("joystick_click", "joystick", "click"),
		("right_joystick_click", "right_joystick", "click")
	];

	[SkipHotload]
	private static readonly (string Name, string Key, string Button)[] KeyNames =
	[
		("SPACE", "space", "space"),
		("LEFT_CONTROL", "ctrl", "lcontrol"),
		("LEFT_SHIFT", "shift", "lshift"),
		("RETURN", "enter", "enter"),
		("TAB", "tab", "tab"),
		("ESCAPE", "escape", "escape"),
		("UP_ARROW", "uparrow", "up"),
		("DOWN_ARROW", "downarrow", "down"),
		("LEFT_ARROW", "leftarrow", "left"),
		("RIGHT_ARROW", "rightarrow", "right"),
		("LEFT_BRACKET", "[", "lbracket"),
		("RIGHT_BRACKET", "]", "rbracket"),
		("F4", "f4", "f4"),
		("F6", "f6", "f6"),
		("F7", "f7", "f7")
	];

	private enum BindingType
	{
		Key,
		MouseButton,
		MouseWheel
	}

	private readonly record struct Binding( BindingType Type, string Name );

	private static KeyValues _mappings;
	private static bool _loaded;
	private static bool _menu;
	private static bool _multiplayer;
	private static List<Binding>[] _held = new List<Binding>[Buttons.Length];
	private static HashSet<Binding> _toggled = [];

	[ConCmd( "inputset_menu" )]
	public static void InputSetMenu() => SetActionSet( true );

	[ConCmd( "inputset_game" )]
	public static void InputSetGame() => SetActionSet( false );

	private static void SetActionSet( bool menu )
	{
		var maxClients = Server.Current is { Active: true } server ? server.MaxClients : Client.Current?.MaxClients ?? 0;

		_menu = menu;
		_multiplayer = maxClients > 1 && !menu;
	}

	private static void Load()
	{
		_loaded = true;

		var path = KeyValues.LoadFromFile( ManifestFile )?.FindPath( $"configurations/{ControllerType}/0" )?.GetString( "path" );

		if ( !string.IsNullOrEmpty( path ) )
			_mappings = KeyValues.LoadFromFile( ConfigDirectory + path );
	}

	private static KeyValues FindKey( KeyValues parent, string name, string field, string value )
	{
		foreach ( var key in parent.Keys )
		{
			if ( key.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) && key.GetString( field ) == value )
				return key;
		}

		return null;
	}

	private static KeyValues FindInput( string preset, string source, string input )
	{
		if ( FindKey( _mappings, "preset", "name", preset )?.FindKey( "group_source_bindings" ) is not { } sources )
			return null;

		foreach ( var binding in sources.Keys )
		{
			if ( binding.Value != $"{source} active" )
				continue;

			return FindKey( _mappings, "group", "id", binding.Name )?.FindKey( "inputs" )?.FindKey( input );
		}

		return null;
	}

	private static bool HasBindings( KeyValues input )
	{
		if ( input?.FindKey( "activators" ) is not { } activators )
			return false;

		foreach ( var activator in activators.Keys )
		{
			if ( activator.FindKey( "bindings" ) is { Keys.Count: > 0 } )
				return true;
		}

		return false;
	}

	private static KeyValues ActiveInput( string source, string input )
	{
		if ( _multiplayer && FindInput( MultiplayerLayer, source, input ) is { } layer && HasBindings( layer ) )
			return layer;

		return FindInput( _menu ? MenuSet : GameSet, source, input );
	}

	private static Binding? ParseBinding( string text )
	{
		var comma = text.IndexOf( ',' );
		var tokens = (comma < 0 ? text : text[..comma]).Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		if ( tokens.Length != 2 )
			return null;

		return tokens[0] switch
		{
			"key_press" => new Binding( BindingType.Key, tokens[1] ),
			"mouse_button" => new Binding( BindingType.MouseButton, tokens[1] ),
			"mouse_wheel" => new Binding( BindingType.MouseWheel, tokens[1] ),
			_ => null
		};
	}

	private static void Emit( Binding binding, bool down )
	{
		var client = Client.Current;
		var gameUi = client is { GameUiActive: true };

		switch ( binding.Type )
		{
			case BindingType.MouseButton:
				var left = binding.Name == "LEFT";

				if ( !gameUi )
					Keys.KeyEvent( left ? "mouse1" : "mouse2", down );
				else if ( down )
					client.GameUiMousePressed( left ? "mouseleft" : "mouseright" );
				else
					client.GameUiMouseReleased( left ? "mouseleft" : "mouseright" );

				break;

			case BindingType.MouseWheel:
				var up = binding.Name == "SCROLL_UP";

				if ( gameUi )
					client.GameUiMouseWheel( up ? 1 : -1 );
				else
					Keys.KeyEvent( up ? "mwheelup" : "mwheeldown", down );

				break;

			default:
				var key = binding.Name.Length == 1 ? binding.Name.ToLowerInvariant() : null;
				var button = key;

				foreach ( var (name, bindKey, uiButton) in KeyNames )
				{
					if ( name == binding.Name )
					{
						key = bindKey;
						button = uiButton;
					}
				}

				if ( key is null )
					break;

				if ( !gameUi )
					Keys.KeyEvent( key, down );
				else if ( down )
					client.GameUiKeyDown( button, default );
				else
					client.GameUiKeyUp( button );

				break;
		}
	}

	private static void Press( int index )
	{
		var (_, source, input) = Buttons[index];

		if ( ActiveInput( source, input )?.FindKey( "activators" ) is not { } activators )
			return;

		var held = _held[index] ??= [];

		foreach ( var activator in activators.Keys )
		{
			if ( activator.Name != FullPress || activator.FindKey( "bindings" ) is not { } bindings )
				continue;

			var toggle = activator.FindKey( "settings" )?.GetInt( "toggle" ) == 1;

			foreach ( var entry in bindings.Keys )
			{
				if ( ParseBinding( entry.Value ) is not { } binding )
					continue;

				if ( binding.Type == BindingType.MouseWheel )
				{
					Emit( binding, true );
					Emit( binding, false );
				}
				else if ( !toggle )
				{
					Emit( binding, true );
					held.Add( binding );
				}
				else if ( _toggled.Remove( binding ) )
				{
					Emit( binding, false );
				}
				else
				{
					_toggled.Add( binding );
					Emit( binding, true );
				}
			}
		}
	}

	private static void Release( int index )
	{
		if ( _held[index] is not { } held )
			return;

		foreach ( var binding in held )
			Emit( binding, false );

		held.Clear();
	}

	public static void Frame()
	{
		if ( !_loaded )
			Load();

		if ( _mappings is null )
			return;

		for ( var i = 0; i < Buttons.Length; i++ )
		{
			if ( Sandbox.Input.Pressed( Buttons[i].Action ) )
				Press( i );

			if ( Sandbox.Input.Released( Buttons[i].Action ) )
				Release( i );
		}
	}
}
