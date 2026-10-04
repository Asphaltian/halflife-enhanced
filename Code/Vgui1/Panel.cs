namespace GoldSrc.Vgui1;

public class Panel
{
	private const int DefaultSize = 64;
	private const int BuildOverlayThickness = 2;

	protected int _x;
	protected int _y;
	protected int _wide;
	protected int _tall;
	protected int _locX;
	protected int _locY;
	protected int _minimumWide;
	protected int _minimumTall;
	protected int _preferredWide;
	protected int _preferredTall;
	protected readonly List<Panel> _childDar = [];
	protected Panel _parent;
	protected SurfaceBase _surfaceBase;
	protected readonly List<InputSignal> _inputSignalDar = [];
	protected readonly List<RepaintSignal> _repaintSignalDar = [];
	protected int _clipX0;
	protected int _clipY0;
	protected int _clipX1;
	protected int _clipY1;
	protected Cursor _cursor;
	protected SchemeCursor _schemeCursor;
	protected bool _visible;
	protected Layout _layout;
	protected bool _needsLayout;
	protected FocusNavGroup _focusNavGroup;
	protected readonly List<FocusChangeSignal> _focusChangeSignalDar = [];
	protected bool _autoFocusNavEnabled;
	protected Border _border;

	private bool _needsRepaint;
	private bool _enabled;
	private BuildGroup _buildGroup;
	private Color _fgColor;
	private Color _bgColor;
	private LayoutInfo _layoutInfo;
	private bool _paintBorderEnabled;
	private bool _paintBackgroundEnabled;
	private bool _paintEnabled;

	public Panel()
	{
		Init( 0, 0, DefaultSize, DefaultSize );
	}

	public Panel( int x, int y, int wide, int tall )
	{
		Init( x, y, wide, tall );
	}

	private void Init( int x, int y, int wide, int tall )
	{
		_x = x;
		_y = y;
		_wide = wide;
		_tall = tall;
		_locX = 0;
		_locY = 0;
		_needsRepaint = false;
		_parent = null;
		_surfaceBase = null;
		_visible = true;
		_minimumWide = 0;
		_minimumTall = 0;
		_cursor = null;
		_schemeCursor = SchemeCursor.Arrow;
		_layout = null;
		_needsLayout = true;
		_focusNavGroup = null;
		_border = null;
		_buildGroup = null;
		_enabled = true;
		_layoutInfo = null;
		_paintBorderEnabled = true;
		_paintBackgroundEnabled = true;
		_paintEnabled = true;

		_fgColor.SetColor( SchemeColor.Black );
		_bgColor.SetColor( SchemeColor.Secondary3 );
		_autoFocusNavEnabled = true;
	}

	public virtual void SetPos( int x, int y )
	{
		_x = x;
		_y = y;
	}

	public virtual void GetPos( out int x, out int y )
	{
		x = _x;
		y = _y;
	}

	public virtual void SetSize( int wide, int tall ) => PanelSetSize( wide, tall );

	private protected void PanelSetSize( int wide, int tall )
	{
		_wide = wide < _minimumWide ? _minimumWide : wide;
		_tall = tall < _minimumTall ? _minimumTall : tall;
	}

	public virtual void GetSize( out int wide, out int tall )
	{
		wide = _wide;
		tall = _tall;
	}

	public virtual void SetBounds( int x, int y, int wide, int tall )
	{
		SetPos( x, y );
		SetSize( wide, tall );
	}

	public virtual void GetBounds( out int x, out int y, out int wide, out int tall )
	{
		x = _x;
		y = _y;
		wide = _wide;
		tall = _tall;
	}

	public virtual int GetWide() => _wide;

	public virtual int GetTall() => _tall;

	public virtual Panel GetParent() => _parent;

	public virtual void SetVisible( bool state )
	{
		_visible = state;
	}

	public virtual bool IsVisible() => _visible;

	public virtual bool IsVisibleUp()
	{
		for ( var panel = this; panel is not null; panel = panel.GetParent() )
		{
			if ( !panel._visible )
				return false;
		}

		return true;
	}

	public virtual void Repaint() => PanelRepaint();

	private protected void PanelRepaint()
	{
		_needsRepaint = true;

		if ( _surfaceBase is not null )
		{
			_surfaceBase._needsSwap = true;
			_surfaceBase.Invalidate( this );
		}

		for ( var i = 0; i < _repaintSignalDar.Count; i++ )
			_repaintSignalDar[i].PanelRepainted( this );
	}

	public virtual void RepaintAll()
	{
		_surfaceBase.GetPanel().Repaint();
	}

	public virtual void GetAbsExtents( out int x0, out int y0, out int x1, out int y1 )
	{
		x0 = _locX;
		y0 = _locY;
		x1 = _wide + x0;
		y1 = _tall + y0;
	}

	public virtual void GetClipRect( out int x0, out int y0, out int x1, out int y1 )
	{
		x0 = _clipX0;
		y0 = _clipY0;
		x1 = _clipX1;
		y1 = _clipY1;
	}

	public virtual void SetParent( Panel newParent )
	{
		_parent?.RemoveChild( this );

		if ( newParent is null )
			return;

		_parent = newParent;

		if ( !newParent._childDar.Contains( this ) )
			newParent._childDar.Add( this );

		SetSurfaceBaseTraverse( _parent._surfaceBase );
	}

	public virtual void AddChild( Panel child )
	{
		child._parent?.RemoveChild( child );
		child._parent = this;

		if ( !_childDar.Contains( child ) )
			_childDar.Add( child );

		child.SetSurfaceBaseTraverse( _surfaceBase );
	}

	public virtual void InsertChildAt( Panel child, int index )
	{
		if ( _childDar.Contains( child ) )
			return;

		child._parent?.RemoveChild( child );
		child._parent = this;

		if ( index >= 0 && index <= _childDar.Count )
			_childDar.Insert( index, child );

		child.SetSurfaceBaseTraverse( _surfaceBase );
	}

	public virtual void RemoveChild( Panel child )
	{
		child._parent = null;
		child._surfaceBase = null;

		_childDar.Remove( child );
	}

	public virtual bool WasMousePressed( MouseCode code ) => GetApp().WasMousePressed( code, this );

	public virtual bool WasMouseDoublePressed( MouseCode code ) => GetApp().WasMouseDoublePressed( code, this );

	public virtual bool IsMouseDown( MouseCode code ) => GetApp().IsMouseDown( code, this );

	public virtual bool WasMouseReleased( MouseCode code ) => GetApp().WasMouseReleased( code, this );

	public virtual bool WasKeyPressed( KeyCode code ) => GetApp().WasKeyPressed( code, this );

	public virtual bool IsKeyDown( KeyCode code ) => GetApp().IsKeyDown( code, this );

	public virtual bool WasKeyTyped( KeyCode code ) => GetApp().WasKeyTyped( code, this );

	public virtual bool WasKeyReleased( KeyCode code ) => GetApp().WasKeyReleased( code, this );

	public virtual void AddInputSignal( InputSignal s )
	{
		if ( !_inputSignalDar.Contains( s ) )
			_inputSignalDar.Add( s );
	}

	public virtual void RemoveInputSignal( InputSignal s )
	{
		_inputSignalDar.Remove( s );
	}

	public virtual void AddRepaintSignal( RepaintSignal s )
	{
		if ( !_repaintSignalDar.Contains( s ) )
			_repaintSignalDar.Add( s );
	}

	public virtual void RemoveRepaintSignal( RepaintSignal s )
	{
		_repaintSignalDar.Remove( s );
	}

	public virtual bool IsWithin( int x, int y )
	{
		ScreenToLocal( ref x, ref y );

		return x >= 0 && y >= 0 && x < _wide && y < _tall;
	}

	public virtual Panel IsWithinTraverse( int x, int y )
	{
		if ( !_visible )
			return null;

		if ( !IsWithin( x, y ) )
			return null;

		for ( var i = _childDar.Count - 1; i >= 0; i-- )
		{
			if ( _childDar[i].IsWithinTraverse( x, y ) is { } panel )
				return panel;
		}

		return this;
	}

	public virtual void LocalToScreen( ref int x, ref int y )
	{
		x += _locX;
		y += _locY;
	}

	public virtual void ScreenToLocal( ref int x, ref int y )
	{
		x -= _locX;
		y -= _locY;
	}

	public virtual void SetCursor( Cursor cursor )
	{
		_schemeCursor = SchemeCursor.User;
		_cursor = cursor;
	}

	public virtual void SetCursor( SchemeCursor scu )
	{
		_cursor = null;
		_schemeCursor = scu;
	}

	public virtual Cursor GetCursor()
	{
		if ( GetApp().GetCursorOveride() is not null )
			return GetApp().GetCursorOveride();

		if ( _schemeCursor != SchemeCursor.User )
			return GetApp().GetScheme().GetCursor( _schemeCursor );

		return _cursor;
	}

	public virtual void SetMinimumSize( int wide, int tall )
	{
		_minimumWide = wide;
		_minimumTall = tall;
	}

	public virtual void GetMinimumSize( out int wide, out int tall )
	{
		wide = _minimumWide;
		tall = _minimumTall;
	}

	public virtual void RequestFocus()
	{
		GetApp().RequestFocus( this );
	}

	public virtual bool HasFocus() => GetApp().GetFocus() == this;

	public virtual int GetChildCount() => _childDar.Count;

	public virtual Panel GetChild( int index ) => index >= 0 && index < _childDar.Count ? _childDar[index] : null;

	public virtual void SetLayout( Layout layout )
	{
		_layout = layout;

		InvalidateLayout( false );
	}

	public virtual void InvalidateLayout( bool layoutNow )
	{
		_needsLayout = true;

		if ( layoutNow )
			InternalPerformLayout();
	}

	public virtual void SetFocusNavGroup( FocusNavGroup focusNavGroup )
	{
		_focusNavGroup = focusNavGroup;

		focusNavGroup?.AddPanel( this );
	}

	public virtual void RequestFocusPrev()
	{
		_focusNavGroup?.RequestFocusPrev();
	}

	public virtual void RequestFocusNext()
	{
		_focusNavGroup?.RequestFocusNext();
	}

	public virtual void AddFocusChangeSignal( FocusChangeSignal s )
	{
		if ( !_focusChangeSignalDar.Contains( s ) )
			_focusChangeSignalDar.Add( s );
	}

	public virtual bool IsAutoFocusNavEnabled() => _autoFocusNavEnabled;

	public virtual void SetAutoFocusNavEnabled( bool state )
	{
		_autoFocusNavEnabled = state;
	}

	public virtual void SetBorder( Border border )
	{
		_border = border;
	}

	public virtual void SetPaintBorderEnabled( bool state )
	{
		_paintBorderEnabled = state;
	}

	public virtual void SetPaintBackgroundEnabled( bool state )
	{
		_paintBackgroundEnabled = state;
	}

	public virtual void SetPaintEnabled( bool state )
	{
		_paintEnabled = state;
	}

	public virtual void GetInset( out int left, out int top, out int right, out int bottom )
	{
		if ( _border is null )
		{
			left = 0;
			top = 0;
			right = 0;
			bottom = 0;
			return;
		}

		_border.GetInset( out left, out top, out right, out bottom );
	}

	public virtual void GetPaintSize( out int wide, out int tall ) => PanelGetPaintSize( out wide, out tall );

	private protected void PanelGetPaintSize( out int wide, out int tall )
	{
		if ( _border is null )
		{
			wide = _wide;
			tall = _tall;
			return;
		}

		_border.GetInset( out var left, out var top, out var right, out var bottom );

		wide = _wide - (left + right);
		tall = _tall - (top + bottom);
	}

	public virtual void SetPreferredSize( int wide, int tall ) => PanelSetPreferredSize( wide, tall );

	private protected void PanelSetPreferredSize( int wide, int tall )
	{
		_preferredWide = wide;
		_preferredTall = tall;
	}

	public virtual void GetPreferredSize( out int wide, out int tall )
	{
		wide = _preferredWide;
		tall = _preferredTall;
	}

	public virtual SurfaceBase GetSurfaceBase() => _surfaceBase;

	public virtual bool IsEnabled() => _enabled;

	public virtual void SetEnabled( bool state )
	{
		_enabled = state;
	}

	public virtual void SetBuildGroup( BuildGroup buildGroup, string panelPersistanceName )
	{
		_buildGroup = buildGroup;

		buildGroup.PanelAdded( this, panelPersistanceName );
	}

	public virtual bool IsBuildGroupEnabled() => _buildGroup is not null && _buildGroup.IsEnabled();

	public virtual void RemoveAllChildren()
	{
		_childDar.Clear();
	}

	public virtual void RepaintParent()
	{
		_parent?.Repaint();
	}

	public virtual Panel CreatePropertyPanel()
	{
		var parentFolder = new TreeFolder( "Properties" );
		var panelFolder = new TreeFolder( "Panel" );

		panelFolder.AddChild( new Label( "setPos" ) );
		panelFolder.AddChild( new Label( "setSize" ) );
		panelFolder.AddChild( new Label( "setBorder" ) );
		panelFolder.AddChild( new Label( "setLayout" ) );

		parentFolder.AddChild( panelFolder );

		return parentFolder;
	}

	public virtual string GetPersistanceText()
	{
		GetBounds( out var x, out var y, out var wide, out var tall );

		return $"->setBounds({x},{y},{wide},{tall});\n";
	}

	public virtual void ApplyPersistanceText( string text )
	{
	}

	public virtual void SetFgColor( SchemeColor sc ) => _fgColor.SetColor( sc );

	public virtual void SetBgColor( SchemeColor sc ) => _bgColor.SetColor( sc );

	public virtual void SetFgColor( int r, int g, int b, int a ) => _fgColor.SetColor( r, g, b, a );

	public virtual void SetBgColor( int r, int g, int b, int a ) => _bgColor.SetColor( r, g, b, a );

	public virtual void GetFgColor( out int r, out int g, out int b, out int a ) => _fgColor.GetColor( out r, out g, out b, out a );

	public virtual void GetBgColor( out int r, out int g, out int b, out int a ) => _bgColor.GetColor( out r, out g, out b, out a );

	public virtual void SetBgColor( Color color )
	{
		_bgColor = color;
	}

	public virtual void SetFgColor( Color color )
	{
		_fgColor = color;
	}

	public virtual void GetBgColor( out Color color )
	{
		color = _bgColor;
	}

	public virtual void GetFgColor( out Color color )
	{
		color = _fgColor;
	}

	public virtual void SetAsMouseCapture( bool state )
	{
		GetApp().SetMouseCapture( state ? this : null );
	}

	public virtual void SetAsMouseArena( bool state )
	{
		GetApp().SetMouseArena( this );
	}

	public virtual App GetApp() => App.GetInstance();

	public virtual void GetVirtualSize( out int wide, out int tall ) => GetSize( out wide, out tall );

	public virtual void SetLayoutInfo( LayoutInfo layoutInfo )
	{
		_layoutInfo = layoutInfo;
	}

	public virtual LayoutInfo GetLayoutInfo() => _layoutInfo;

	public virtual bool IsCursorNone()
	{
		var cursor = GetCursor();

		return cursor is null || cursor.GetDefaultCursor() == DefaultCursor.None;
	}

	public virtual void SolveTraverse()
	{
		if ( !_visible )
			return;

		Solve();

		if ( _needsLayout )
			InternalPerformLayout();

		Solve();

		for ( var i = 0; i < _childDar.Count; i++ )
			_childDar[i].SolveTraverse();
	}

	public virtual void PaintTraverse()
	{
		PaintTraverse( _needsRepaint );
	}

	public virtual void SetSurfaceBaseTraverse( SurfaceBase surfaceBase )
	{
		_surfaceBase = surfaceBase;

		for ( var i = 0; i < _childDar.Count; i++ )
			_childDar[i].SetSurfaceBaseTraverse( surfaceBase );
	}

	public virtual void PerformLayout()
	{
	}

	public virtual void InternalPerformLayout()
	{
		_needsLayout = false;

		if ( _layout is null )
			PerformLayout();
		else
			_layout.PerformLayout( this );

		Repaint();
	}

	public virtual void DrawSetColor( SchemeColor sc )
	{
		GetApp().GetScheme().GetColor( sc, out var r, out var g, out var b, out var a );

		DrawSetColor( r, g, b, a );
	}

	public virtual void DrawSetColor( int r, int g, int b, int a ) => _surfaceBase.DrawSetColor( r, g, b, a );

	public virtual void DrawFilledRect( int x0, int y0, int x1, int y1 ) => _surfaceBase.DrawFilledRect( x0, y0, x1, y1 );

	public virtual void DrawOutlinedRect( int x0, int y0, int x1, int y1 ) => _surfaceBase.DrawOutlinedRect( x0, y0, x1, y1 );

	public virtual void DrawSetTextFont( SchemeFont sf ) => DrawSetTextFont( GetApp().GetScheme().GetFont( sf ) );

	public virtual void DrawSetTextFont( Font font ) => _surfaceBase.DrawSetTextFont( font );

	public virtual void DrawSetTextColor( SchemeColor sc )
	{
		GetApp().GetScheme().GetColor( sc, out var r, out var g, out var b, out var a );

		DrawSetTextColor( r, g, b, a );
	}

	public virtual void DrawSetTextColor( int r, int g, int b, int a ) => _surfaceBase.DrawSetTextColor( r, g, b, a );

	public virtual void DrawSetTextPos( int x, int y ) => _surfaceBase.DrawSetTextPos( x, y );

	public virtual void DrawPrintText( string text, int textLen ) => _surfaceBase.DrawPrintText( text, textLen );

	public virtual void DrawPrintText( int x, int y, string text, int textLen )
	{
		_surfaceBase.DrawSetTextPos( x, y );
		_surfaceBase.DrawPrintText( text, textLen );
	}

	public virtual void DrawPrintChar( char ch ) => _surfaceBase.DrawPrintText( ch.ToString(), 1 );

	public virtual void DrawPrintChar( int x, int y, char ch )
	{
		_surfaceBase.DrawSetTextPos( x, y );
		_surfaceBase.DrawPrintText( ch.ToString(), 1 );
	}

	public virtual void DrawSetTextureRgba( int id, byte[] rgba, int wide, int tall ) => _surfaceBase?.DrawSetTextureRgba( id, rgba, wide, tall );

	public virtual void DrawSetTexture( int id ) => _surfaceBase.DrawSetTexture( id );

	public virtual void DrawTexturedRect( int x0, int y0, int x1, int y1 ) => _surfaceBase.DrawTexturedRect( x0, y0, x1, y1 );

	public virtual void Solve()
	{
		_locX = _x;
		_locY = _y;

		if ( _parent is not null )
		{
			_parent.GetInset( out var left, out var top, out _, out _ );

			_locX += _parent._locX + left;
			_locY += _parent._locY + top;
		}

		_clipX0 = _locX;
		_clipY0 = _locY;
		_clipX1 = _wide + _locX;
		_clipY1 = _tall + _locY;

		if ( _parent is null )
			return;

		if ( _clipX0 < _parent._clipX0 )
			_clipX0 = _parent._clipX0;

		if ( _clipY0 < _parent._clipY0 )
			_clipY0 = _parent._clipY0;

		if ( _parent._clipX1 < _clipX1 )
			_clipX1 = _parent._clipX1;

		if ( _parent._clipY1 < _clipY1 )
			_clipY1 = _parent._clipY1;
	}

	public virtual void PaintTraverse( bool repaint )
	{
		if ( !_visible )
			return;

		if ( _needsRepaint )
			repaint = true;

		_needsRepaint = false;

		if ( _clipX0 < _clipX1 && _clipY0 < _clipY1 )
		{
			if ( repaint && (_paintBorderEnabled || _paintBackgroundEnabled || _paintEnabled) )
			{
				_surfaceBase.PushMakeCurrent( this, true );

				if ( _border is not null && _paintBorderEnabled )
					_border.DoPaint( this );

				if ( _paintBackgroundEnabled )
					PaintBackground();

				if ( _paintEnabled )
					Paint();

				_surfaceBase.PopMakeCurrent( this );
			}
		}
		else
		{
			repaint = false;
		}

		for ( var i = 0; i < _childDar.Count; i++ )
			_childDar[i].PaintTraverse( repaint );

		if ( repaint && IsBuildGroupEnabled() && HasFocus() )
		{
			_surfaceBase.PushMakeCurrent( this, false );
			PaintBuildOverlay();
			_surfaceBase.PopMakeCurrent( this );
		}

		if ( _surfaceBase._needsSwap && _surfaceBase.GetPanel() == this )
		{
			_surfaceBase._needsSwap = false;
			_surfaceBase.SwapBuffers();
		}
	}

	public virtual void PaintBackground()
	{
		GetPaintSize( out var wide, out var tall );
		GetBgColor( out var r, out var g, out var b, out var a );

		DrawSetColor( r, g, b, a );
		DrawFilledRect( 0, 0, wide, tall );
	}

	public virtual void Paint()
	{
	}

	public virtual void PaintBuildOverlay()
	{
		GetSize( out var wide, out var tall );

		DrawSetColor( SchemeColor.Black );
		DrawFilledRect( 0, 0, wide, BuildOverlayThickness );
		DrawFilledRect( 0, tall - BuildOverlayThickness, wide, tall );
		DrawFilledRect( 0, BuildOverlayThickness, BuildOverlayThickness, tall - BuildOverlayThickness );
		DrawFilledRect( wide - BuildOverlayThickness, BuildOverlayThickness, wide, tall - BuildOverlayThickness );
	}

	public virtual void InternalCursorMoved( int x, int y )
	{
		if ( IsCursorNone() )
			return;

		if ( IsBuildGroupEnabled() )
		{
			_buildGroup.CursorMoved( x, y, this );
			return;
		}

		ScreenToLocal( ref x, ref y );

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].CursorMoved( x, y, this );
	}

	public virtual void InternalCursorEntered()
	{
		if ( IsCursorNone() || IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].CursorEntered( this );
	}

	public virtual void InternalCursorExited()
	{
		if ( IsCursorNone() || IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].CursorExited( this );
	}

	public virtual void InternalMousePressed( MouseCode code )
	{
		if ( IsCursorNone() )
			return;

		if ( IsBuildGroupEnabled() )
		{
			_buildGroup.MousePressed( code, this );
			return;
		}

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].MousePressed( code, this );
	}

	public virtual void InternalMouseDoublePressed( MouseCode code )
	{
		if ( IsCursorNone() )
			return;

		if ( IsBuildGroupEnabled() )
		{
			_buildGroup.MouseDoublePressed( code, this );
			return;
		}

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].MouseDoublePressed( code, this );
	}

	public virtual void InternalMouseReleased( MouseCode code )
	{
		if ( IsCursorNone() )
			return;

		if ( IsBuildGroupEnabled() )
		{
			_buildGroup.MouseReleased( code, this );
			return;
		}

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].MouseReleased( code, this );
	}

	public virtual void InternalMouseWheeled( int delta )
	{
		if ( IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].MouseWheeled( delta, this );
	}

	public virtual void InternalKeyPressed( KeyCode code )
	{
		if ( IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].KeyPressed( code, this );
	}

	public virtual void InternalKeyTyped( KeyCode code )
	{
		if ( IsBuildGroupEnabled() )
		{
			_buildGroup.KeyTyped( code, this );
			return;
		}

		if ( IsAutoFocusNavEnabled() )
		{
			if ( code == KeyCode.Tab )
			{
				if ( IsKeyDown( KeyCode.LShift ) || IsKeyDown( KeyCode.RShift ) || IsKeyDown( KeyCode.LControl ) )
					RequestFocusPrev();
				else
					RequestFocusNext();
			}
			else if ( code == KeyCode.Up )
			{
				RequestFocusPrev();
			}
			else if ( code == KeyCode.Down )
			{
				RequestFocusNext();
			}
		}

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].KeyTyped( code, this );
	}

	public virtual void InternalKeyReleased( KeyCode code )
	{
		if ( IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].KeyReleased( code, this );
	}

	public virtual void InternalKeyFocusTicked()
	{
		if ( IsBuildGroupEnabled() )
			return;

		for ( var i = 0; i < _inputSignalDar.Count; i++ )
			_inputSignalDar[i].KeyFocusTicked( this );
	}

	public virtual void InternalFocusChanged( bool lost )
	{
		if ( !lost )
			_focusNavGroup?.SetCurrentPanel( this );

		for ( var i = 0; i < _focusChangeSignalDar.Count; i++ )
			_focusChangeSignalDar[i].FocusChanged( lost, this );
	}

	public virtual void InternalSetCursor()
	{
		if ( IsBuildGroupEnabled() )
			_buildGroup.GetCursor( this );

		_surfaceBase?.SetCursor( GetCursor() );
	}
}
