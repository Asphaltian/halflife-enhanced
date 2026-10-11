using System;

namespace GoldSrc.GameUI;

public enum MouseCode
{
	Left,
	Right,
	Middle,
	Mouse4,
	Mouse5
}

public enum CursorCode
{
	User,
	None,
	Arrow,
	IBeam = 3,
	SizeNwse = 8,
	SizeNesw = 9,
	SizeWe = 10,
	SizeNs = 11
}

public enum KeyCode
{
	None,
	Key0,
	Key1,
	Key2,
	Key3,
	Key4,
	Key5,
	Key6,
	Key7,
	Key8,
	Key9,
	A,
	B,
	C,
	D,
	E,
	F,
	G,
	H,
	I,
	J,
	K,
	L,
	M,
	N,
	O,
	P,
	Q,
	R,
	S,
	T,
	U,
	V,
	W,
	X,
	Y,
	Z,
	Pad0,
	Pad1,
	Pad2,
	Pad3,
	Pad4,
	Pad5,
	Pad6,
	Pad7,
	Pad8,
	Pad9,
	PadDivide,
	PadMultiply,
	PadMinus,
	PadPlus,
	PadEnter,
	PadDecimal,
	LBracket,
	RBracket,
	Semicolon,
	Apostrophe,
	Backquote,
	Comma,
	Period,
	Slash,
	Backslash,
	Minus,
	Equal,
	Enter,
	Space,
	Backspace,
	Tab,
	CapsLock,
	NumLock,
	Escape,
	ScrollLock,
	Insert,
	Delete,
	Home,
	End,
	PageUp,
	PageDown,
	Break,
	LShift,
	RShift,
	LAlt,
	RAlt,
	LControl,
	RControl,
	LWin,
	RWin,
	App,
	Up,
	Left,
	Down,
	Right,
	F1,
	F2,
	F3,
	F4,
	F5,
	F6,
	F7,
	F8,
	F9,
	F10,
	F11,
	F12
}

public sealed class Message( string name )
{
	public string Name { get; } = name;

	public int Int { get; init; }

	public int Int2 { get; init; }

	public string Text { get; init; }

	public Panel Panel { get; set; }
}

public sealed class Vgui
{
	public const string SchemeFile = "resource/TrackerScheme.res";
	private const int MaxTokenNameLength = 128;
	private const int MaxTokenValueLength = 2048;

	private readonly Queue<(Panel Target, Message Message, Panel From)> _messages = [];
	private readonly Queue<(Panel Target, Message Message, Panel From)> _secondaryMessages = [];

	private sealed class Tick
	{
		public Panel Panel;
		public int Interval;
		public int NextTick;
	}

	private readonly List<Tick> _tickSignals = [];
	private readonly List<(int Arrival, Panel Target, Message Message, Panel From)> _delayedMessages = [];

	private bool _dispatching;
	private readonly Dictionary<string, string> _tokens = new( StringComparer.OrdinalIgnoreCase );

	public Scheme Scheme { get; } = new( SchemeFile );

	public Surface Surface { get; }

	public Input Input { get; }

	public string ClipboardText { get; set; } = "";

	public static int TimeMillis => (int)(RealTime.Now * 1000f);

	public void SetClipboardText( string text )
	{
		ClipboardText = text;

		Sandbox.UI.Clipboard.SetText( text );
	}

	public Vgui()
	{
		Surface = new Surface( this );
		Input = new Input( this );

		foreach ( var file in new[] { "resource/valve_english.txt", "resource/gameui_english.txt", "resource/vgui_english.txt" } )
			AddLocalizationFile( file );
	}

	private static string ReadUnicodeToken( string text, ref int position, int maxLength, out bool quoted )
	{
		while ( position < text.Length && char.IsWhiteSpace( text[position] ) )
			position++;

		quoted = false;

		if ( position >= text.Length )
			return "";

		var token = new System.Text.StringBuilder();

		if ( text[position] != '"' )
		{
			while ( position < text.Length && !char.IsWhiteSpace( text[position] ) && token.Length < maxLength )
				token.Append( text[position++] );

			return token.ToString();
		}

		quoted = true;
		position++;

		while ( position < text.Length && text[position] != '"' && token.Length < maxLength )
		{
			var character = text[position];

			if ( character == '\\' && position + 1 < text.Length && text[position + 1] is 'n' or '"' )
			{
				position++;
				character = text[position] == 'n' ? '\n' : '"';
			}

			position++;

			token.Append( character );
		}

		if ( position < text.Length && text[position] == '"' )
			position++;

		return token.ToString();
	}

	public void AddLocalizationFile( string fileName )
	{
		if ( Server.LoadMountText( fileName ) is not { } text )
			return;

		var english = fileName.Contains( "_english.txt", StringComparison.Ordinal );
		var tokens = false;
		var position = 0;

		for ( var key = ReadUnicodeToken( text, ref position, MaxTokenNameLength, out _ ); key.Length > 0; key = ReadUnicodeToken( text, ref position, MaxTokenNameLength, out _ ) )
		{
			if ( key.StartsWith( "//", StringComparison.Ordinal ) )
			{
				while ( position < text.Length && text[position] != '\n' )
					position++;

				continue;
			}

			var value = ReadUnicodeToken( text, ref position, MaxTokenValueLength, out var quoted );

			if ( value.Length == 0 && !quoted )
				break;

			if ( tokens )
			{
				if ( key.Equals( "}", StringComparison.OrdinalIgnoreCase ) )
					tokens = false;
				else if ( english || !key.StartsWith( "[english]", StringComparison.OrdinalIgnoreCase ) )
					_tokens[key] = value;
			}
			else if ( key.Equals( "Tokens", StringComparison.OrdinalIgnoreCase ) )
			{
				tokens = true;
			}
			else if ( key.Equals( "}", StringComparison.OrdinalIgnoreCase ) )
			{
				break;
			}
		}
	}

	public string Localize( string text )
	{
		return text is { Length: > 0 } && text[0] == '#' && _tokens.TryGetValue( text[1..], out var value ) ? value : text ?? "";
	}

	public void PostMessage( Panel target, Message message, Panel from )
	{
		if ( target is not null )
			(_dispatching ? _secondaryMessages : _messages).Enqueue( (target, message, from) );
	}

	public void PostMessage( Panel target, Message message, Panel from, float delay )
	{
		if ( target is null )
			return;

		if ( delay <= 0f )
			PostMessage( target, message, from );
		else
			_delayedMessages.Add( (TimeMillis + (int)(delay * 1000f), target, message, from) );
	}

	public void AddTickSignal( Panel panel, int intervalMilliseconds = 0 )
	{
		foreach ( var tick in _tickSignals )
		{
			if ( tick.Panel == panel )
			{
				tick.Interval = intervalMilliseconds;
				tick.NextTick = TimeMillis + intervalMilliseconds;

				return;
			}
		}

		_tickSignals.Add( new Tick { Panel = panel, Interval = intervalMilliseconds, NextTick = TimeMillis + intervalMilliseconds } );
	}

	public void RemoveTickSignal( Panel panel ) => _tickSignals.RemoveAll( x => x.Panel == panel );

	public void DispatchMessages()
	{
		var time = TimeMillis;

		for ( var i = 0; i < _delayedMessages.Count; i++ )
		{
			var (arrival, delayedTarget, delayedMessage, delayedFrom) = _delayedMessages[i];

			if ( arrival > time )
				continue;

			_delayedMessages.RemoveAt( i-- );
			_messages.Enqueue( (delayedTarget, delayedMessage, delayedFrom) );
		}

		_dispatching = true;

		while ( _secondaryMessages.Count > 0 || _messages.Count > 0 )
		{
			var (target, message, from) = _secondaryMessages.Count > 0 ? _secondaryMessages.Dequeue() : _messages.Dequeue();

			if ( !target.Deleted )
				target.OnMessage( message, from );
		}

		_dispatching = false;
	}

	public void SetScreenSize( int screenWide, int screenTall )
	{
		Scheme.SetScreenSize( screenWide, screenTall );
		Surface.SetScreenSize( screenWide, screenTall );
	}

	public void RunFrame()
	{
		var (cursorX, cursorY) = Input.CursorPos;

		Input.UpdateMouseFocus( cursorX, cursorY );
		Input.RunFrame();
		DispatchMessages();

		var time = TimeMillis;

		for ( var i = _tickSignals.Count - 1; i >= 0; i-- )
		{
			var tick = _tickSignals[i];

			if ( tick.Panel.Deleted )
			{
				_tickSignals.RemoveAt( i );
				continue;
			}

			if ( tick.Interval != 0 )
			{
				if ( time < tick.NextTick )
					continue;

				tick.NextTick = tick.Interval + time;
			}

			PostMessage( tick.Panel, new Message( "Tick" ), null );
		}

		if ( Surface.EmbeddedPanel is not { Visible: true } panel )
			return;

		panel.SchemeTraverse();
		panel.ThinkTraverse();
		panel.SolveTraverse();
	}
}
