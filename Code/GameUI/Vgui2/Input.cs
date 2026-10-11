using System;

namespace GoldSrc.GameUI;

public sealed class Input( Vgui vgui )
{
	private const int MouseCodeCount = 5;

	private readonly bool[] _mouseDown = new bool[MouseCodeCount];
	private readonly bool[] _mousePressed = new bool[MouseCodeCount];
	private readonly bool[] _mouseReleased = new bool[MouseCodeCount];
	private readonly HashSet<KeyCode> _keysDown = [];

	private Panel _keyFocus;
	private Panel _mouseFocus;
	private Panel _mouseOver;
	private Panel _mouseCapture;
	private Panel _appModalPanel;
	private int _cursorX;
	private int _cursorY;

	public Panel Focus => _keyFocus;

	public Panel MouseOver => _mouseOver;

	public Panel AppModalSurface => _appModalPanel;

	public (int X, int Y) CursorPos => (_cursorX, _cursorY);

	public void SetCursorPos( int x, int y ) => Mouse.Position = new Vector2( x, y );

	public bool IsMouseDown( MouseCode code ) => _mouseDown[(int)code];

	public bool WasMousePressed( MouseCode code ) => _mousePressed[(int)code];

	public bool WasMouseReleased( MouseCode code ) => _mouseReleased[(int)code];

	public bool IsKeyDown( KeyCode code ) => _keysDown.Contains( code );

	public bool IsShiftDown => IsKeyDown( KeyCode.LShift ) || IsKeyDown( KeyCode.RShift );

	public bool IsAltDown => IsKeyDown( KeyCode.LAlt ) || IsKeyDown( KeyCode.RAlt );

	public bool IsControlDown => IsKeyDown( KeyCode.LControl ) || IsKeyDown( KeyCode.RControl );

	public void SetAppModalSurface( Panel panel ) => _appModalPanel = panel;

	public void ReleaseAppModalSurface() => _appModalPanel = null;

	public bool IsChildOfModalPanel( Panel panel )
	{
		return panel is null || _appModalPanel is null || panel.HasParent( _appModalPanel );
	}

	public void SetMouseFocus( Panel newMouseFocus )
	{
		if ( !IsChildOfModalPanel( newMouseFocus ) )
			return;

		for ( var panel = newMouseFocus; panel is not null; )
		{
			var parent = panel.Parent;

			if ( !panel.MouseInputEnabled )
				return;

			if ( panel.IsPopup || parent is null )
				break;

			panel = parent;
		}

		if ( _mouseOver == newMouseFocus && (_mouseCapture is not null || _mouseFocus == newMouseFocus) )
			return;

		var oldMouseOver = _mouseOver;

		_mouseOver = newMouseFocus;

		if ( oldMouseOver is not null && (_mouseCapture is null || oldMouseOver.HasParent( _mouseCapture )) )
			vgui.PostMessage( oldMouseOver, new Message( "CursorExited" ), null );

		if ( _mouseOver is not null && (_mouseCapture is null || _mouseOver.HasParent( _mouseCapture )) )
			vgui.PostMessage( _mouseOver, new Message( "CursorEntered" ), null );

		_mouseFocus = _mouseCapture ?? _mouseOver;
	}

	public void SetMouseCapture( Panel panel )
	{
		if ( !IsChildOfModalPanel( panel ) )
			return;

		if ( _mouseCapture is not null && panel != _mouseCapture )
			vgui.PostMessage( _mouseCapture, new Message( "MouseCaptureLost" ), null );

		_mouseCapture = panel;
	}

	public void PanelDeleted( Panel panel )
	{
		if ( _keyFocus == panel )
			_keyFocus = null;

		if ( _mouseOver == panel )
			_mouseOver = null;

		if ( _mouseFocus == panel )
			_mouseFocus = null;

		if ( _mouseCapture == panel )
		{
			SetMouseCapture( null );
			_mouseCapture = null;
		}

		if ( _appModalPanel == panel )
			ReleaseAppModalSurface();
	}

	public void RunFrame()
	{
		if ( _keyFocus is not null && IsChildOfModalPanel( _keyFocus ) )
			vgui.PostMessage( _keyFocus, new Message( "KeyFocusTicked" ), null );

		if ( _mouseFocus is not null && IsChildOfModalPanel( _mouseFocus ) )
			vgui.PostMessage( _mouseFocus, new Message( "MouseFocusTicked" ), null );

		Array.Clear( _mousePressed );
		Array.Clear( _mouseReleased );

		var wantedKeyFocus = CalculateNewKeyFocus();

		if ( _keyFocus == wantedKeyFocus )
			return;

		if ( _keyFocus is not null )
			vgui.PostMessage( _keyFocus, new Message( "KillFocus" ), null );

		if ( wantedKeyFocus is not null )
			vgui.PostMessage( wantedKeyFocus, new Message( "SetFocus" ), null );

		_keyFocus = wantedKeyFocus;
		_keyFocus?.MoveToFrontInternal();
	}

	private Panel CalculateNewKeyFocus()
	{
		Panel wantedKeyFocus = null;
		var popups = vgui.Surface.Popups;

		for ( var i = popups.Count - 1; i >= 0; i-- )
		{
			var popup = popups[i];

			if ( !popup.IsPopup || !popup.Visible || !popup.KeyboardInputEnabled || !AncestorsVisible( popup ) )
				continue;

			wantedKeyFocus = popup.GetCurrentKeyFocus() ?? popup;
			break;
		}

		return IsChildOfModalPanel( wantedKeyFocus ) ? wantedKeyFocus : null;
	}

	private static bool AncestorsVisible( Panel panel )
	{
		for ( var parent = panel.Parent; parent is not null; parent = parent.Parent )
		{
			if ( !parent.Visible )
				return false;
		}

		return true;
	}

	public void UpdateMouseFocus( int x, int y )
	{
		Panel focus = null;
		var surface = vgui.Surface;

		for ( var i = surface.Popups.Count - 1; i >= 0 && focus is null; i-- )
		{
			var popup = surface.Popups[i];

			if ( popup.Visible && AncestorsVisible( popup ) )
				focus = popup.IsWithinTraverse( x, y, false );
		}

		focus ??= surface.EmbeddedPanel?.IsWithinTraverse( x, y, false );

		if ( !IsChildOfModalPanel( focus ) )
			focus = null;

		SetMouseFocus( focus );
	}

	public void InternalCursorMoved( int x, int y )
	{
		if ( _cursorX == x && _cursorY == y )
			return;

		_cursorX = x;
		_cursorY = y;

		UpdateMouseFocus( x, y );

		if ( _mouseCapture is null )
		{
			if ( _mouseFocus is not null )
				vgui.PostMessage( _mouseFocus, new Message( "CursorMoved" ) { Int = x, Int2 = y }, null );

			return;
		}

		if ( !IsChildOfModalPanel( _mouseCapture ) )
			return;

		if ( _mouseOver is not null && _mouseOver != _mouseCapture && _mouseOver.HasParent( _mouseCapture ) )
			vgui.PostMessage( _mouseOver, new Message( "CursorMoved" ) { Int = x, Int2 = y }, null );

		vgui.PostMessage( _mouseCapture, new Message( "CursorMoved" ) { Int = x, Int2 = y }, null );
	}

	public void InternalMousePressed( MouseCode code )
	{
		_mouseDown[(int)code] = true;
		_mousePressed[(int)code] = true;

		if ( _mouseCapture is not null && IsChildOfModalPanel( _mouseCapture ) )
		{
			var captureLost = true;

			if ( _mouseOver is not null && _mouseOver != _mouseCapture && _mouseOver.HasParent( _mouseCapture ) )
			{
				vgui.PostMessage( _mouseOver, new Message( "MousePressed" ) { Int = (int)code }, null );
				captureLost = false;
			}

			vgui.PostMessage( _mouseCapture, new Message( "MousePressed" ) { Int = (int)code }, null );

			if ( captureLost )
				SetMouseCapture( null );
		}
		else if ( _mouseFocus is not null && IsChildOfModalPanel( _mouseFocus ) )
		{
			vgui.PostMessage( _mouseFocus, new Message( "MousePressed" ) { Int = (int)code }, null );
		}

		if ( IsChildOfModalPanel( _mouseOver ) )
			vgui.Surface.SetTopLevelFocus( _mouseOver );
	}

	public void InternalMouseDoublePressed( MouseCode code )
	{
		PostMouseMessage( new Message( "MouseDoublePressed" ) { Int = (int)code } );

		if ( IsChildOfModalPanel( _mouseOver ) )
			vgui.Surface.SetTopLevelFocus( _mouseOver );
	}

	public void InternalMouseReleased( MouseCode code )
	{
		_mouseDown[(int)code] = false;
		_mouseReleased[(int)code] = true;

		PostMouseMessage( new Message( "MouseReleased" ) { Int = (int)code } );
	}

	private void PostMouseMessage( Message message )
	{
		if ( _mouseCapture is not null && IsChildOfModalPanel( _mouseCapture ) )
		{
			if ( _mouseOver is not null && _mouseOver != _mouseCapture && _mouseOver.HasParent( _mouseCapture ) )
				vgui.PostMessage( _mouseOver, new Message( message.Name ) { Int = message.Int }, null );

			vgui.PostMessage( _mouseCapture, message, null );
		}
		else if ( _mouseFocus is not null && IsChildOfModalPanel( _mouseFocus ) )
		{
			vgui.PostMessage( _mouseFocus, message, null );
		}
	}

	public void InternalMouseWheeled( int delta )
	{
		if ( _mouseFocus is not null && IsChildOfModalPanel( _mouseFocus ) )
			vgui.PostMessage( _mouseFocus, new Message( "MouseWheeled" ) { Int = delta }, null );
	}

	public void InternalKeyCodePressed( KeyCode code )
	{
		_keysDown.Add( code );

		PostKeyMessage( new Message( "KeyCodePressed" ) { Int = (int)code } );
	}

	public void InternalKeyCodeTyped( KeyCode code ) => PostKeyMessage( new Message( "KeyCodeTyped" ) { Int = (int)code } );

	public void InternalKeyTyped( char character ) => PostKeyMessage( new Message( "KeyTyped" ) { Int = character } );

	public void InternalKeyCodeReleased( KeyCode code )
	{
		_keysDown.Remove( code );

		PostKeyMessage( new Message( "KeyCodeReleased" ) { Int = (int)code } );
	}

	private void PostKeyMessage( Message message )
	{
		if ( _keyFocus is not null && IsChildOfModalPanel( _keyFocus ) )
			vgui.PostMessage( _keyFocus, message, null );
	}
}
