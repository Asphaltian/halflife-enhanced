using System;

namespace GoldSrc.Vgui1;

public abstract class App
{
	private const int DefaultMinimumTickMillisInterval = 50;
	private const string RegistryCookie = "vgui.registry.";

	private static System.Diagnostics.Stopwatch _tickCount = System.Diagnostics.Stopwatch.StartNew();

	public static string ClipboardText { get; set; }

	protected static App _instance;

	protected bool _running;
	protected bool _externalMain;
	protected readonly List<SurfaceBase> _surfaceBaseDar = [];
	protected Panel _keyFocus;
	protected Panel _oldMouseFocus;
	protected Panel _mouseFocus;
	protected Panel _mouseCapture;
	protected Panel _wantedKeyFocus;
	protected readonly bool[] _mousePressed = new bool[(int)MouseCode.Last];
	protected readonly bool[] _mouseDoublePressed = new bool[(int)MouseCode.Last];
	protected readonly bool[] _mouseDown = new bool[(int)MouseCode.Last];
	protected readonly bool[] _mouseReleased = new bool[(int)MouseCode.Last];
	protected readonly bool[] _keyPressed = new bool[(int)KeyCode.Last];
	protected readonly bool[] _keyTyped = new bool[(int)KeyCode.Last];
	protected readonly bool[] _keyDown = new bool[(int)KeyCode.Last];
	protected readonly bool[] _keyReleased = new bool[(int)KeyCode.Last];
	protected readonly List<TickSignal> _tickSignalDar = [];
	protected Scheme _scheme;
	protected bool _buildMode;
	protected bool _wantedBuildMode;
	protected Panel _mouseArenaPanel;
	protected Cursor _cursorOveride;

	private long _nextTickMillis;
	private long _minimumTickMillisInterval;
	private bool _mouseArenaEnabled;
	private int _mouseArenaX0;
	private int _mouseArenaY0;
	private int _mouseArenaX1;
	private int _mouseArenaY1;

	protected App()
	{
		Init();
	}

	protected App( bool externalMain )
	{
		Init();

		_externalMain = externalMain;
	}

	public static App GetInstance() => _instance;

	public virtual void Start()
	{
		Main();

		if ( _externalMain )
			return;

		Run();

		foreach ( var surfaceBase in _surfaceBaseDar )
			surfaceBase.SetWindowedMode();
	}

	public virtual void Stop()
	{
		_running = false;
	}

	public virtual void ExternalTick()
	{
		InternalTick();
	}

	public virtual bool WasMousePressed( MouseCode code, Panel panel ) => _mousePressed[(int)code];

	public virtual bool WasMouseDoublePressed( MouseCode code, Panel panel ) => _mouseDoublePressed[(int)code];

	public virtual bool IsMouseDown( MouseCode code, Panel panel ) => _mouseDown[(int)code];

	public virtual bool WasMouseReleased( MouseCode code, Panel panel ) => _mouseReleased[(int)code];

	public virtual bool WasKeyPressed( KeyCode code, Panel panel )
	{
		if ( panel is not null && panel != _keyFocus )
			return false;

		return _keyPressed[(int)code];
	}

	public virtual bool IsKeyDown( KeyCode code, Panel panel )
	{
		if ( panel is not null && panel != _keyFocus )
			return false;

		return _keyDown[(int)code];
	}

	public virtual bool WasKeyTyped( KeyCode code, Panel panel )
	{
		if ( panel is not null && panel != _keyFocus )
			return false;

		return _keyTyped[(int)code];
	}

	public virtual bool WasKeyReleased( KeyCode code, Panel panel )
	{
		if ( panel is not null && panel != _keyFocus )
			return false;

		return _keyReleased[(int)code];
	}

	public virtual void AddTickSignal( TickSignal s )
	{
		if ( !_tickSignalDar.Contains( s ) )
			_tickSignalDar.Add( s );
	}

	public virtual void SetCursorPos( int x, int y )
	{
	}

	public virtual void GetCursorPos( out int x, out int y )
	{
		_surfaceBaseDar[0].GetMousePos( out x, out y );
	}

	public virtual void SetMouseCapture( Panel panel )
	{
		if ( panel is null )
		{
			if ( _mouseCapture is not null )
			{
				_mouseCapture.GetSurfaceBase().EnableMouseCapture( false );
				_mouseCapture = null;
				return;
			}
		}
		else
		{
			panel.GetSurfaceBase().EnableMouseCapture( true );
		}

		_mouseCapture = panel;
	}

	public virtual void SetMouseArena( int x0, int y0, int x1, int y1, bool enabled )
	{
		SetMouseArena( null );
		InternalSetMouseArena( x0, y0, x1, y1, enabled );
	}

	public virtual void SetMouseArena( Panel panel )
	{
		_mouseArenaPanel = panel;
	}

	public virtual void RequestFocus( Panel panel )
	{
		_wantedKeyFocus = panel;
	}

	public virtual Panel GetFocus() => _keyFocus;

	public virtual void RepaintAll()
	{
		foreach ( var surfaceBase in _surfaceBaseDar )
		{
			surfaceBase.GetPanel().RepaintAll();
			surfaceBase.Invalidate( surfaceBase.GetPanel() );
		}
	}

	public virtual void SetScheme( Scheme scheme )
	{
		if ( scheme is null )
			return;

		_scheme = scheme;

		RepaintAll();
	}

	public virtual Scheme GetScheme() => _scheme;

	public virtual void EnableBuildMode()
	{
		_wantedBuildMode = true;
	}

	public virtual long GetTimeMillis() => _tickCount.ElapsedMilliseconds;

	public virtual char GetKeyCodeChar( KeyCode code, bool shifted ) => KeyCodeInfo.GetChar( code, shifted );

	public virtual string GetKeyCodeText( KeyCode code ) => KeyCodeInfo.GetText( code );

	public virtual int GetClipboardTextCount() => ClipboardText is null ? 0 : ClipboardText.Length + 1;

	public virtual void SetClipboardText( string text, int textLen )
	{
		if ( text is null || textLen <= 0 )
			return;

		ClipboardText = text.Length > textLen ? text[..textLen] : text;

		Sandbox.UI.Clipboard.SetText( ClipboardText );
	}

	public virtual string GetClipboardText( int offset, int bufLen, out int count )
	{
		count = 0;

		if ( bufLen < 1 || ClipboardText is null )
			return "";

		count = ClipboardText.Length + 1 - offset;

		if ( count < 1 )
		{
			count = 0;
			return "";
		}

		if ( bufLen < count )
			count = bufLen;

		return ClipboardText.Substring( offset, Math.Min( count, ClipboardText.Length - offset ) );
	}

	public virtual void Reset()
	{
		_keyFocus = null;
		_mouseFocus = null;
		_mouseCapture = null;
		_wantedKeyFocus = null;
		_buildMode = false;
		_wantedBuildMode = false;
		_mouseArenaPanel = null;
		_tickSignalDar.Clear();

		SetScheme( new Scheme() );
	}

	public virtual void InternalSetMouseArena( int x0, int y0, int x1, int y1, bool enabled )
	{
		_mouseArenaEnabled = enabled;
		_mouseArenaX0 = x0;
		_mouseArenaY0 = y0;
		_mouseArenaX1 = x1;
		_mouseArenaY1 = y1;

		ClipCursor();
	}

	private void ClipCursor()
	{
		if ( !_mouseArenaEnabled )
			return;

		var position = Sandbox.Mouse.Position;
		var clipped = new Vector2( Math.Clamp( position.x, _mouseArenaX0, Math.Max( _mouseArenaX1 - 1, _mouseArenaX0 ) ), Math.Clamp( position.y, _mouseArenaY0, Math.Max( _mouseArenaY1 - 1, _mouseArenaY0 ) ) );

		if ( clipped != position )
			Sandbox.Mouse.Position = clipped;
	}

	private static bool SplitRegistryKey( string key ) => !string.IsNullOrEmpty( key ) && !key.EndsWith( '\\' );

	public virtual bool SetRegistryString( string key, string value )
	{
		if ( !SplitRegistryKey( key ) )
			return false;

		Game.Cookies.Set( RegistryCookie + key, value );

		return true;
	}

	public virtual bool GetRegistryString( string key, out string value )
	{
		value = null;

		return SplitRegistryKey( key ) && Game.Cookies.TryGet( RegistryCookie + key, out value );
	}

	public virtual bool SetRegistryInteger( string key, int value )
	{
		if ( !SplitRegistryKey( key ) )
			return false;

		Game.Cookies.Set( RegistryCookie + key, value );

		return true;
	}

	public virtual bool GetRegistryInteger( string key, out int value )
	{
		value = 0;

		return SplitRegistryKey( key ) && Game.Cookies.TryGet( RegistryCookie + key, out value );
	}

	public virtual void SetCursorOveride( Cursor cursor )
	{
		_cursorOveride = cursor;
	}

	public virtual Cursor GetCursorOveride() => _cursorOveride;

	public virtual void SetMinimumTickMillisInterval( int interval )
	{
		_minimumTickMillisInterval = interval;
	}

	public abstract void Main();

	public virtual void Run()
	{
		_running = true;

		do
		{
			InternalTick();
		}
		while ( _running );

		SetMouseArena( 0, 0, 0, 0, false );
	}

	public virtual void InternalCursorMoved( int x, int y, SurfaceBase surfaceBase )
	{
		surfaceBase.GetPanel().LocalToScreen( ref x, ref y );

		if ( _buildMode )
			return;

		UpdateMouseFocus( x, y, surfaceBase );

		_mouseFocus?.InternalCursorMoved( x, y );
	}

	public virtual void InternalMousePressed( MouseCode code, SurfaceBase surfaceBase )
	{
		_mousePressed[(int)code] = true;
		_mouseDown[(int)code] = true;

		if ( !_buildMode )
			_mouseFocus?.InternalMousePressed( code );
	}

	public virtual void InternalMouseDoublePressed( MouseCode code, SurfaceBase surfaceBase )
	{
		_mouseDoublePressed[(int)code] = true;

		if ( !_buildMode )
			_mouseFocus?.InternalMouseDoublePressed( code );
	}

	public virtual void InternalMouseReleased( MouseCode code, SurfaceBase surfaceBase )
	{
		_mouseReleased[(int)code] = true;
		_mouseDown[(int)code] = false;

		if ( !_buildMode )
			_mouseFocus?.InternalMouseReleased( code );
	}

	public virtual void InternalMouseWheeled( int delta, SurfaceBase surfaceBase )
	{
		if ( !_buildMode )
			_keyFocus?.InternalMouseWheeled( delta );
	}

	public virtual void InternalKeyPressed( KeyCode code, SurfaceBase surfaceBase )
	{
		if ( code < 0 || code >= KeyCode.Last )
			return;

		_keyPressed[(int)code] = true;
		_keyDown[(int)code] = true;

		if ( !_buildMode )
			_keyFocus?.InternalKeyPressed( code );
	}

	public virtual void InternalKeyTyped( KeyCode code, SurfaceBase surfaceBase )
	{
		if ( code < 0 || code >= KeyCode.Last )
			return;

		_keyTyped[(int)code] = true;

		if ( !_buildMode )
			_keyFocus?.InternalKeyTyped( code );
	}

	public virtual void InternalKeyReleased( KeyCode code, SurfaceBase surfaceBase )
	{
		if ( code < 0 || code >= KeyCode.Last )
			return;

		_keyReleased[(int)code] = true;
		_keyDown[(int)code] = false;

		if ( !_buildMode )
			_keyFocus?.InternalKeyReleased( code );
	}

	private void Init()
	{
		_instance = this;
		_externalMain = false;
		_running = false;
		_keyFocus = null;
		_oldMouseFocus = null;
		_mouseFocus = null;
		_mouseCapture = null;
		_wantedKeyFocus = null;
		_scheme = new Scheme();
		_buildMode = false;
		_wantedBuildMode = false;
		_mouseArenaPanel = null;
		_cursorOveride = null;
		_nextTickMillis = GetTimeMillis();
		_minimumTickMillisInterval = DefaultMinimumTickMillisInterval;

		Array.Clear( _mousePressed );
		Array.Clear( _mouseDoublePressed );
		Array.Clear( _mouseDown );
		Array.Clear( _mouseReleased );
		Array.Clear( _keyPressed );
		Array.Clear( _keyTyped );
		Array.Clear( _keyDown );
		Array.Clear( _keyReleased );
	}

	protected virtual void UpdateMouseFocus( int x, int y, SurfaceBase surfaceBase )
	{
		var focus = _mouseCapture;

		if ( focus is null )
		{
			if ( !surfaceBase.IsWithin( x, y ) )
				return;

			focus = surfaceBase.GetPanel().IsWithinTraverse( x, y );

			if ( focus is null )
				return;
		}

		SetMouseFocus( focus );
	}

	protected virtual void SetMouseFocus( Panel newMouseFocus )
	{
		var oldMouseFocus = _mouseFocus;

		if ( oldMouseFocus == newMouseFocus )
			return;

		_mouseFocus = newMouseFocus;
		_oldMouseFocus = oldMouseFocus;

		oldMouseFocus?.InternalCursorExited();
		_mouseFocus?.InternalCursorEntered();
	}

	public virtual void SurfaceBaseCreated( SurfaceBase surfaceBase )
	{
		if ( !_surfaceBaseDar.Contains( surfaceBase ) )
			_surfaceBaseDar.Add( surfaceBase );
	}

	public virtual void SurfaceBaseDeleted( SurfaceBase surfaceBase )
	{
		_surfaceBaseDar.Remove( surfaceBase );

		_mouseFocus = null;
		_mouseCapture = null;
		_keyFocus = null;
	}

	protected virtual void PlatTick()
	{
		ClipCursor();
	}

	protected virtual void InternalTick()
	{
		if ( GetTimeMillis() < _nextTickMillis )
			return;

		PlatTick();

		GetCursorPos( out var cursorX, out var cursorY );

		var mouseOverAnySurface = false;

		foreach ( var surfaceBase in _surfaceBaseDar )
		{
			UpdateMouseFocus( cursorX, cursorY, surfaceBase );

			if ( surfaceBase.IsWithin( cursorX, cursorY ) )
				mouseOverAnySurface = true;

			surfaceBase.SetEmulatedCursorPos( cursorX, cursorY );
		}

		if ( !mouseOverAnySurface )
			SetMouseFocus( null );

		_mouseFocus?.InternalSetCursor();

		for ( var i = 0; i < _tickSignalDar.Count; i++ )
			_tickSignalDar[i].Ticked();

		_keyFocus?.InternalKeyFocusTicked();

		Array.Clear( _mousePressed );
		Array.Clear( _mouseDoublePressed );
		Array.Clear( _mouseReleased );
		Array.Clear( _keyPressed );
		Array.Clear( _keyTyped );
		Array.Clear( _keyReleased );

		var focused = 0;

		for ( ; focused < _surfaceBaseDar.Count; focused++ )
		{
			if ( _surfaceBaseDar[focused].HasFocus() )
				break;
		}

		if ( focused == _surfaceBaseDar.Count )
			_wantedKeyFocus = null;

		if ( _keyFocus != _wantedKeyFocus )
		{
			if ( _keyFocus is not null )
			{
				_keyFocus.InternalFocusChanged( true );
				_keyFocus.Repaint();
			}

			if ( _wantedKeyFocus is not null )
			{
				_wantedKeyFocus.InternalFocusChanged( false );
				_wantedKeyFocus.Repaint();
			}
		}

		_keyFocus = _wantedKeyFocus;
		_buildMode = _wantedBuildMode;

		foreach ( var surfaceBase in _surfaceBaseDar )
		{
			surfaceBase.GetPanel().SolveTraverse();
			surfaceBase.ApplyChanges();
		}

		if ( _mouseArenaPanel?.GetSurfaceBase() is { } arenaSurface )
		{
			arenaSurface.GetPanel().GetPos( out var x, out var y );
			_mouseArenaPanel.GetAbsExtents( out var x0, out var y0, out var x1, out var y1 );

			InternalSetMouseArena( x + x0, y + y0, x + x1, y + y1, true );
		}

		_nextTickMillis = GetTimeMillis() + _minimumTickMillisInterval;
	}
}
