using System;

namespace GoldSrc.GameUI;

public class Panel
{
	private const int DefaultWide = 64;
	private const int DefaultTall = 24;
	private const int RightAlignedX = 1 << 2;
	private const int CenterAlignedX = 1 << 3;
	private const int BottomAlignedY = 1 << 4;
	private const int CenterAlignedY = 1 << 5;

	private readonly List<Panel> _children = [];
	private readonly List<Panel> _actionSignalTargets = [];

	private bool _visible = true;
	private bool _enabled = true;
	private bool _needsLayout = true;
	private bool _needsSchemeUpdate = true;
	private bool _proportional;
	private bool _markedForDeletion;
	private bool _paintBorderEnabled = true;
	private bool _paintBackgroundEnabled = true;
	private bool _paintEnabled = true;
	private int _hdProportionalMode;
	private int _minimumWide;
	private int _minimumTall;
	private int _zpos;
	private int _positionFlags;
	private Border _border;
	private CursorCode _cursor = CursorCode.Arrow;
	private Color32 _fgColor;
	private Color32 _bgColor;

	public Vgui Vgui { get; }

	public Panel Parent { get; private set; }

	public IReadOnlyList<Panel> Children => _children;

	public string Name { get; set; }

	public int X { get; private set; }

	public int Y { get; private set; }

	public int Wide { get; private set; }

	public int Tall { get; private set; }

	public int AbsX { get; private set; }

	public int AbsY { get; private set; }

	public int ClipX0 { get; private set; }

	public int ClipY0 { get; private set; }

	public int ClipX1 { get; private set; }

	public int ClipY1 { get; private set; }

	public int InsetLeft { get; private set; }

	public int InsetTop { get; private set; }

	public int InsetRight { get; private set; }

	public int InsetBottom { get; private set; }

	public bool IsPopup { get; set; }

	public bool Render { get; set; }

	public bool KeyboardInputEnabled { get; set; } = true;

	public bool MouseInputEnabled { get; set; } = true;

	public bool Deleted { get; private set; }

	public int TabPosition { get; set; }

	public int PinCorner { get; private set; }

	public int AutoResize { get; private set; }

	public BuildGroup BuildGroup { get; set; }

	public Panel( Vgui vgui, Panel parent, string name )
	{
		Vgui = vgui;
		Name = name;
		Wide = DefaultWide;
		Tall = DefaultTall;

		SetParent( parent );
	}

	public bool Visible => _visible;

	public virtual void SetVisible( bool state ) => _visible = state;

	public bool Enabled => _enabled;

	public virtual void SetEnabled( bool state )
	{
		if ( state == _enabled )
			return;

		_enabled = state;

		InvalidateLayout();
	}

	public bool IsProportional => _proportional;

	public Proportional ProportionalMode
	{
		get
		{
			for ( var panel = this; panel is not null; panel = panel.Parent )
			{
				if ( panel._hdProportionalMode == 2 )
					return Proportional.Hd;

				if ( panel._hdProportionalMode != 0 )
					break;
			}

			return _proportional ? Proportional.Standard : Proportional.None;
		}
	}

	public void SetProportional( bool state )
	{
		if ( state != _proportional )
		{
			_proportional = state;

			foreach ( var child in _children )
				child.SetProportional( _proportional );
		}

		InvalidateLayout();
	}

	public void SetHdProportional( bool state )
	{
		_hdProportionalMode = state ? 2 : 0;

		if ( state && !_proportional )
			SetProportional( true );
	}

	public int Scaled( int value )
	{
		return ProportionalMode switch
		{
			Proportional.Hd => Vgui.Scheme.GetHdProportionalScaledValue( value ),
			Proportional.Standard => Vgui.Scheme.GetProportionalScaledValue( value ),
			_ => value
		};
	}

	public float ProportionalScale => _proportional ? Vgui.Scheme.GetProportionalScale( ProportionalMode ) : 1f;

	public SchemeFont GetFont( string name ) => Vgui.Scheme.GetFont( name, ProportionalMode );

	public Color32 GetSchemeColor( string name ) => Vgui.Scheme.GetColor( name, default );

	public Color32 GetSchemeColor( string name, Color32 defaultColor ) => Vgui.Scheme.GetColor( name, defaultColor );

	public virtual void SetParent( Panel newParent )
	{
		SetParentInternal( newParent );

		if ( Parent is null || IsPopup )
			return;

		SetProportional( Parent.IsProportional );

		if ( Parent.KeyboardInputEnabled != KeyboardInputEnabled )
			SetKeyBoardInputEnabled( Parent.KeyboardInputEnabled );

		if ( Parent.MouseInputEnabled != MouseInputEnabled )
			MouseInputEnabled = Parent.MouseInputEnabled;
	}

	public void SetParentInternal( Panel newParent )
	{
		if ( newParent == this || newParent == Parent )
			return;

		Parent?._children.Remove( this );
		Parent = newParent;

		if ( newParent is null )
			return;

		if ( !newParent._children.Contains( this ) )
			newParent._children.Add( this );

		SetZPos( _zpos );
		newParent.OnChildAdded( this );
	}

	public void SetKeyBoardInputEnabled( bool state )
	{
		KeyboardInputEnabled = state;

		foreach ( var child in _children )
			child.SetKeyBoardInputEnabled( state );
	}

	public void SetZPos( int zpos )
	{
		_zpos = zpos;

		if ( Parent is null )
			return;

		var siblings = Parent._children;
		var index = siblings.IndexOf( this );

		if ( index < 0 )
			return;

		while ( true )
		{
			if ( index > 0 && siblings[index - 1]._zpos > _zpos )
			{
				(siblings[index - 1], siblings[index]) = (siblings[index], siblings[index - 1]);
				index--;
			}
			else if ( index < siblings.Count - 1 && _zpos > siblings[index + 1]._zpos )
			{
				(siblings[index + 1], siblings[index]) = (siblings[index], siblings[index + 1]);
				index++;
			}
			else
			{
				return;
			}
		}
	}

	public void MoveToFrontInternal()
	{
		Vgui.Surface.MovePopupToFront( this );

		if ( Parent is null )
			return;

		var siblings = Parent._children;

		if ( siblings[^1] != this && siblings.Remove( this ) )
			siblings.Add( this );

		for ( var i = siblings.Count - 2; i >= 0; i-- )
		{
			if ( siblings[i]._zpos <= _zpos )
				return;

			(siblings[i], siblings[i + 1]) = (siblings[i + 1], siblings[i]);
		}
	}

	public void MoveToFront()
	{
		if ( IsPopup )
			Vgui.Surface.BringToFront( this );
		else
			MoveToFrontInternal();
	}

	public void MoveToBack()
	{
		if ( Parent is null )
			return;

		var siblings = Parent._children;

		siblings.Remove( this );
		siblings.Insert( 0, this );

		for ( var i = 1; i < siblings.Count; i++ )
		{
			if ( _zpos <= siblings[i]._zpos )
				return;

			(siblings[i - 1], siblings[i]) = (siblings[i], siblings[i - 1]);
		}
	}

	public void MakeReadyForUse()
	{
		SchemeTraverse( true );
		ThinkTraverse();
		SolveTraverse();
	}

	public void MakePopup()
	{
		Vgui.Surface.CreatePopup( this, true, true );
	}

	public bool HasParent( Panel potentialParent )
	{
		for ( var panel = this; panel is not null; panel = panel.Parent )
		{
			if ( panel == potentialParent )
				return true;
		}

		return false;
	}

	public Panel FindChildByName( string name )
	{
		return _children.Find( x => string.Equals( x.Name, name, StringComparison.OrdinalIgnoreCase ) );
	}

	public void SetPos( int x, int y )
	{
		X = x;
		Y = y;
	}

	public void SetSize( int wide, int tall )
	{
		wide = Math.Max( wide, _minimumWide );
		tall = Math.Max( tall, _minimumTall );

		if ( wide == Wide && tall == Tall )
			return;

		Wide = wide;
		Tall = tall;

		OnSizeChanged( wide, tall );
	}

	public void SetBounds( int x, int y, int wide, int tall )
	{
		SetPos( x, y );
		SetSize( wide, tall );
	}

	public void SetMinimumSize( int wide, int tall )
	{
		_minimumWide = wide;
		_minimumTall = tall;

		var newWide = Math.Max( Wide, wide );
		var newTall = Math.Max( Tall, tall );

		if ( newWide != Wide || newTall != Tall )
			SetSize( newWide, newTall );
	}

	public (int Wide, int Tall) MinimumSize => (_minimumWide, _minimumTall);

	public (int Wide, int Tall) PaintSize => (Wide - (InsetLeft + InsetRight), Tall - (InsetTop + InsetBottom));

	public (int X, int Y) LocalToScreen( int x, int y ) => (x + AbsX, y + AbsY);

	public (int X, int Y) ScreenToLocal( int x, int y ) => (x - AbsX, y - AbsY);

	public bool IsWithin( int x, int y ) => x >= ClipX0 && y >= ClipY0 && x < ClipX1 && y < ClipY1;

	public bool IsCursorOver()
	{
		var (x, y) = Vgui.Input.CursorPos;

		return IsWithin( x, y );
	}

	public Panel IsWithinTraverse( int x, int y, bool traversePopups )
	{
		if ( !Visible || !MouseInputEnabled )
			return null;

		if ( !traversePopups )
		{
			if ( !IsWithin( x, y ) )
				return null;

			for ( var i = _children.Count - 1; i >= 0; i-- )
			{
				if ( _children[i].IsPopup )
					continue;

				if ( _children[i].IsWithinTraverse( x, y, false ) is { } panel )
					return panel;
			}

			return this;
		}

		for ( var i = _children.Count - 1; i >= 0; i-- )
		{
			if ( _children[i].IsPopup && _children[i].IsWithinTraverse( x, y, true ) is { } panel )
				return panel;
		}

		for ( var i = _children.Count - 1; i >= 0; i-- )
		{
			if ( !_children[i].IsPopup && _children[i].IsWithinTraverse( x, y, true ) is { } panel )
				return panel;
		}

		return IsWithin( x, y ) ? this : null;
	}

	private void Solve()
	{
		AbsX = X;
		AbsY = Y;

		var parent = IsPopup ? Vgui.Surface.EmbeddedPanel : Parent;

		if ( parent is not null )
		{
			AbsX += parent.AbsX + parent.InsetLeft;
			AbsY += parent.AbsY + parent.InsetTop;
		}

		ClipX0 = AbsX;
		ClipY0 = AbsY;
		ClipX1 = AbsX + Wide;
		ClipY1 = AbsY + Tall;

		if ( parent is null || IsPopup )
			return;

		ClipX0 = Math.Max( ClipX0, parent.ClipX0 );
		ClipY0 = Math.Max( ClipY0, parent.ClipY0 );

		if ( parent.ClipX1 < ClipX1 )
			ClipX1 = parent.ClipX1 - parent.InsetRight;

		if ( parent.ClipY1 < ClipY1 )
			ClipY1 = parent.ClipY1 - parent.InsetBottom;
	}

	public void SchemeTraverse( bool forceApplySchemeSettings = false )
	{
		for ( var i = 0; i < _children.Count; i++ )
		{
			if ( forceApplySchemeSettings || _children[i].Visible )
				_children[i].SchemeTraverse( forceApplySchemeSettings );
		}

		PerformApplySchemeSettings();
	}

	public void ThinkTraverse()
	{
		Think();

		for ( var i = 0; i < _children.Count; i++ )
		{
			if ( _children[i].Visible )
				_children[i].ThinkTraverse();
		}
	}

	public void SolveTraverse()
	{
		Solve();

		for ( var i = 0; i < _children.Count; i++ )
		{
			if ( _children[i].Visible )
				_children[i].SolveTraverse();
		}
	}

	private void Think()
	{
		OnThink();

		if ( !Visible || !_needsLayout )
			return;

		_needsLayout = false;

		PerformLayout();
	}

	private void PerformApplySchemeSettings()
	{
		if ( _needsSchemeUpdate )
			ApplySchemeSettings();
	}

	public void InvalidateLayout( bool layoutNow = false, bool reloadScheme = false )
	{
		_needsLayout = true;

		if ( reloadScheme )
		{
			_needsSchemeUpdate = true;

			for ( var i = 0; i < _children.Count; i++ )
				_children[i].InvalidateLayout( layoutNow, true );

			PerformApplySchemeSettings();
		}

		if ( !layoutNow )
			return;

		_needsLayout = false;

		PerformLayout();
	}

	public void PaintTraverse()
	{
		if ( !Visible )
			return;

		var surface = Vgui.Surface;
		var visible = ClipX0 < ClipX1 && ClipY0 < ClipY1;

		if ( visible )
		{
			if ( _paintBackgroundEnabled )
			{
				surface.PushMakeCurrent( this, false );
				PaintBackground();
			}

			if ( _paintEnabled )
			{
				surface.PushMakeCurrent( this, true );
				Paint();
			}
		}

		for ( var i = 0; i < _children.Count; i++ )
		{
			if ( surface.ShouldPaintChildPanel( _children[i] ) )
				_children[i].PaintTraverse();
		}

		if ( visible && _paintBorderEnabled && _border is not null )
		{
			surface.PushMakeCurrent( this, false );
			PaintBorder();
		}
	}

	public Border Border => _border;

	public virtual void SetBorder( Border border )
	{
		_border = border;

		InsetLeft = border?.InsetLeft ?? 0;
		InsetTop = border?.InsetTop ?? 0;
		InsetRight = border?.InsetRight ?? 0;
		InsetBottom = border?.InsetBottom ?? 0;
	}

	public void SetPaintBorderEnabled( bool state ) => _paintBorderEnabled = state;

	public void SetPaintBackgroundEnabled( bool state ) => _paintBackgroundEnabled = state;

	public void SetPaintEnabled( bool state ) => _paintEnabled = state;

	public virtual Color32 GetBgColor() => _bgColor;

	public virtual void SetBgColor( Color32 color ) => _bgColor = color;

	public virtual Color32 GetFgColor() => _fgColor;

	public virtual void SetFgColor( Color32 color ) => _fgColor = color;

	public bool HasFocus => Vgui.Input.Focus == this;

	public virtual void RequestFocus( int direction = 0 ) => OnRequestFocus( this, null );

	public virtual bool RequestFocusPrev( Panel panel = null ) => Parent is not null && Parent.RequestFocusPrev( this );

	public virtual bool RequestFocusNext( Panel panel = null ) => Parent is not null && Parent.RequestFocusNext( this );

	public virtual Panel GetCurrentKeyFocus() => null;

	public virtual Panel HasHotkey( char key ) => null;

	public virtual bool CanBeDefaultButton => false;

	public void AddActionSignalTarget( Panel target )
	{
		if ( target is not null && !_actionSignalTargets.Contains( target ) )
			_actionSignalTargets.Add( target );
	}

	public void PostActionSignal( Message message )
	{
		message.Panel = this;

		for ( var i = _actionSignalTargets.Count - 1; i >= 0; i-- )
			Vgui.PostMessage( _actionSignalTargets[i], message, this );
	}

	public void PostMessage( Panel target, Message message ) => Vgui.PostMessage( target, message, this );

	protected void CallParentFunction( Message message ) => Parent?.OnMessage( message, this );

	public void MarkForDeletion()
	{
		if ( _markedForDeletion )
			return;

		_markedForDeletion = true;

		Vgui.PostMessage( this, new Message( "Delete" ), this );
	}

	public void Delete()
	{
		if ( Deleted )
			return;

		Deleted = true;

		while ( _children.Count > 0 )
			_children[^1].Delete();

		Vgui.Input.PanelDeleted( this );
		Vgui.Surface.ReleasePanel( this );
		BuildGroup?.PanelRemoved( this );

		Parent?._children.Remove( this );
		Parent = null;
	}

	public virtual void OnTick()
	{
	}

	public virtual void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "Tick":
				OnTick();
				break;

			case "CursorMoved":
				var (x, y) = ScreenToLocal( message.Int, message.Int2 );
				OnCursorMoved( x, y );
				break;

			case "CursorEntered":
				OnCursorEntered();
				break;

			case "CursorExited":
				OnCursorExited();
				break;

			case "MousePressed":
				OnMousePressed( (MouseCode)message.Int );
				break;

			case "MouseDoublePressed":
				OnMouseDoublePressed( (MouseCode)message.Int );
				break;

			case "MouseReleased":
				OnMouseReleased( (MouseCode)message.Int );
				break;

			case "MouseWheeled":
				OnMouseWheeled( message.Int );
				break;

			case "MouseFocusTicked":
				InternalMouseFocusTicked();
				break;

			case "OnMouseFocusTicked":
				OnMouseFocusTicked();
				break;

			case "MouseCaptureLost":
				OnMouseCaptureLost();
				break;

			case "KeyFocusTicked":
				OnKeyFocusTicked();
				break;

			case "KeyCodePressed":
				OnKeyCodePressed( (KeyCode)message.Int );
				break;

			case "KeyCodeTyped":
				OnKeyCodeTyped( (KeyCode)message.Int );
				break;

			case "KeyTyped":
				OnKeyTyped( (char)message.Int );
				break;

			case "KeyCodeReleased":
				OnKeyCodeReleased( (KeyCode)message.Int );
				break;

			case "SetFocus":
				OnSetFocus();
				break;

			case "KillFocus":
				OnKillFocus();
				break;

			case "Command":
				OnCommand( message.Text );
				break;

			case "Delete":
				Delete();
				break;
		}
	}

	public virtual void ApplySchemeSettings()
	{
		SetFgColor( GetSchemeColor( "FgColor" ) );
		SetBgColor( GetSchemeColor( "BgColor" ) );

		_needsSchemeUpdate = false;
	}

	public virtual void ApplySettings( KeyValues settings )
	{
		_positionFlags = 0;

		var surface = Vgui.Surface;
		var x = X;
		var y = Y;

		if ( settings.FindKey( "xpos" )?.Value is { } xpos )
		{
			if ( xpos.StartsWith( 'r' ) || xpos.StartsWith( 'R' ) )
			{
				_positionFlags |= RightAlignedX;
				xpos = xpos[1..];
			}
			else if ( xpos.StartsWith( 'c' ) || xpos.StartsWith( 'C' ) )
			{
				_positionFlags |= CenterAlignedX;
				xpos = xpos[1..];
			}

			x = Scaled( Common.Atoi( xpos ) );

			if ( (_positionFlags & RightAlignedX) != 0 )
				x = surface.ScreenWide - x;
			else if ( (_positionFlags & CenterAlignedX) != 0 )
				x += surface.ScreenWide / 2;
		}

		if ( settings.FindKey( "ypos" )?.Value is { } ypos )
		{
			if ( ypos.StartsWith( 'r' ) || ypos.StartsWith( 'R' ) )
			{
				_positionFlags |= BottomAlignedY;
				ypos = ypos[1..];
			}
			else if ( ypos.StartsWith( 'c' ) || ypos.StartsWith( 'C' ) )
			{
				_positionFlags |= CenterAlignedY;
				ypos = ypos[1..];
			}

			y = Scaled( Common.Atoi( ypos ) );

			if ( (_positionFlags & BottomAlignedY) != 0 )
				y = surface.ScreenTall - y;
			else if ( (_positionFlags & CenterAlignedY) != 0 )
				y += surface.ScreenTall / 2;
		}

		SetPos( x, y );

		if ( settings.FindKey( "zpos" ) is not null )
			SetZPos( settings.GetInt( "zpos" ) );

		SetSize( Scaled( settings.GetInt( "wide", Wide ) ), Scaled( settings.GetInt( "tall", Tall ) ) );

		AutoResize = settings.GetInt( "AutoResize" );
		PinCorner = settings.GetInt( "PinCorner" );

		if ( settings.GetInt( "IgnoreScheme" ) != 0 )
			PerformApplySchemeSettings();

		var visible = settings.GetInt( "visible", 1 );

		if ( visible is 0 or 1 )
			SetVisible( visible == 1 );

		SetEnabled( settings.GetInt( "enabled", 1 ) != 0 );

		TabPosition = settings.GetInt( "tabPosition" );

		var paintBackground = settings.GetInt( "paintbackground", -1 );

		if ( paintBackground >= 0 )
			SetPaintBackgroundEnabled( paintBackground != 0 );

		var paintBorder = settings.GetInt( "paintborder", -1 );

		if ( paintBorder >= 0 )
			SetPaintBorderEnabled( paintBorder != 0 );

		if ( settings.FindKey( "fieldName" )?.Value is { } fieldName )
			Name = fieldName;
	}

	public virtual void PerformLayout()
	{
	}

	public virtual void OnThink()
	{
	}

	public virtual void OnChildAdded( Panel child )
	{
	}

	public virtual void OnSizeChanged( int wide, int tall ) => InvalidateLayout();

	public virtual void OnCommand( string command )
	{
	}

	public virtual void OnCursorMoved( int x, int y )
	{
	}

	public virtual void OnCursorEntered()
	{
	}

	public virtual void OnCursorExited()
	{
	}

	public virtual void OnMousePressed( MouseCode code )
	{
	}

	public virtual void OnMouseDoublePressed( MouseCode code )
	{
	}

	public virtual void OnMouseReleased( MouseCode code )
	{
	}

	public virtual void OnMouseWheeled( int delta ) => CallParentFunction( new Message( "MouseWheeled" ) { Int = delta } );

	public void SetCursor( CursorCode cursor ) => _cursor = cursor;

	private void InternalMouseFocusTicked()
	{
		var visible = Visible;

		for ( var parent = Parent; parent is not null; parent = parent.Parent )
			visible &= parent.Visible;

		if ( visible )
			Vgui.Surface.SetCursor( _cursor );

		OnMouseFocusTicked();
	}

	public virtual void OnMouseFocusTicked() => CallParentFunction( new Message( "OnMouseFocusTicked" ) );

	public virtual void OnKeyFocusTicked() => CallParentFunction( new Message( "KeyFocusTicked" ) );

	public virtual void OnMouseCaptureLost()
	{
	}

	public virtual void OnSetFocus()
	{
	}

	public virtual void OnKillFocus()
	{
	}

	public virtual void OnKeyCodePressed( KeyCode code ) => CallParentFunction( new Message( "KeyCodePressed" ) { Int = (int)code } );

	public virtual void OnKeyCodeTyped( KeyCode code )
	{
		var input = Vgui.Input;

		switch ( code )
		{
			case KeyCode.Tab when !input.IsShiftDown && !input.IsKeyDown( KeyCode.LControl ):
			case KeyCode.Down:
			case KeyCode.Right:
				RequestFocusNext();
				break;

			case KeyCode.Tab:
			case KeyCode.Up:
			case KeyCode.Left:
				RequestFocusPrev();
				break;

			default:
				CallParentFunction( new Message( "KeyCodeTyped" ) { Int = (int)code } );
				break;
		}
	}

	public virtual void OnKeyTyped( char character ) => CallParentFunction( new Message( "KeyTyped" ) { Int = character } );

	public virtual void OnKeyCodeReleased( KeyCode code ) => CallParentFunction( new Message( "KeyCodeReleased" ) { Int = (int)code } );

	public virtual void OnRequestFocus( Panel subFocus, Panel defaultPanel )
	{
		Parent?.OnRequestFocus( subFocus, defaultPanel );
	}

	public virtual void PaintBackground()
	{
		Vgui.Surface.DrawSetColor( GetBgColor() );
		Vgui.Surface.DrawFilledRect( 0, 0, Wide, Tall );
	}

	public virtual void Paint()
	{
	}

	public virtual void PaintBorder() => _border.Paint( Vgui.Surface, 0, 0, Wide, Tall );
}
