namespace GoldSrc.GameUI;

public class MenuItem : Button
{
	private readonly Menu _menu;

	public MenuItem( Menu menu, string name, string text ) : base( menu.Vgui, menu, name, text )
	{
		_menu = menu;

		SetButtonActivationType( ActivationType.OnReleased );
		SetButtonBorderEnabled( false );
		SetContentAlignment( Alignment.West );
		SetUseCaptureMouse( false );
	}

	public KeyValues UserData { get; set; }

	public override bool CanBeDefaultButton => false;

	protected override Border GetBorder( bool depressed, bool armed, bool selected, bool keyFocus ) => null;

	protected virtual void OnArmed()
	{
	}

	protected virtual void OnDisarmed()
	{
	}

	public void ArmItem()
	{
		OnArmed();
		SetArmed( true );

		_menu.ForceCalculateWidth();
	}

	public void DisarmItem()
	{
		OnDisarmed();

		base.OnCursorExited();

		_menu.ForceCalculateWidth();
	}

	protected override void FireActionSignal()
	{
		PostMessage( _menu, new Message( "MenuItemSelected" ) { Panel = this } );

		base.FireActionSignal();
	}

	public override void OnCursorEntered() => PostMessage( _menu, new Message( "CursorEnteredMenuItem" ) { Panel = this } );

	public override void OnCursorExited() => PostMessage( _menu, new Message( "CursorExitedMenuItem" ) { Panel = this } );

	public override void OnCursorMoved( int x, int y )
	{
		if ( _menu.IsKeyboardInputMode )
			OnCursorEntered();

		_menu.OnCursorMoved( x, y );
	}

	public override void OnKillFocus() => _menu.OnKillFocus();

	public override void OnKeyCodeReleased( KeyCode code )
	{
		if ( code is KeyCode.Escape or KeyCode.F4 )
			_menu.OnKeyCodeTyped( code );
		else
			base.OnKeyCodeReleased( code );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetDefaultColor( GetSchemeColor( "Menu/FgColor", GetFgColor() ), GetSchemeColor( "Menu/BgColor", GetBgColor() ) );
		SetArmedColor( GetSchemeColor( "Menu/ArmedFgColor", GetFgColor() ), GetSchemeColor( "Menu/ArmedBgColor", GetBgColor() ) );
		SetDepressedColor( GetSchemeColor( "Menu/ArmedFgColor", GetFgColor() ), GetSchemeColor( "Menu/ArmedBgColor", GetBgColor() ) );

		SetTextInset( Common.Atoi( Vgui.Scheme.GetResourceString( "Menu/TextInset" ) ), 0 );

		_menu.ForceCalculateWidth();
	}
}

public class Menu : Panel
{
	private const int DefaultMenuItemHeight = 22;
	private const int ContentWidthPadding = 8;
	private const int DefaultVisibleLines = 99;
	private const float ScrollBarSize = 18f;

	private readonly List<MenuItem> _items = [];
	private readonly ScrollBar _scroller;
	private readonly int _scrollerSize;

	private int _numVisibleLines = DefaultVisibleLines;
	private bool _sizedForScrollBar;
	private Color32 _borderDark;

	private int _menuItemHeight = DefaultMenuItemHeight;
	private int _itemWidePadding;
	private int _fixedWidth;
	private int _menuWide;
	private int _currentlyHighlightedItem = -1;
	private int _activatedItem;
	private bool _recalculateWidth;
	private bool _keyboardInputMode;

	public Menu( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_scroller = new ScrollBar( vgui, this, "MenuScrollBar", true );
		_scroller.SetVisible( false );
		_scroller.AddActionSignalTarget( this );

		SetZPos( 1 );

		base.SetVisible( false );

		MakePopup();

		_scrollerSize = IsProportional ? (int)((float)vgui.Surface.ScreenWide / vgui.Scheme.GetProportionalBaseWide( ProportionalMode ) * ScrollBarSize) : (int)ScrollBarSize;
	}

	public int ItemCount => _items.Count;

	public int ActiveItem => _activatedItem;

	public bool IsKeyboardInputMode => _keyboardInputMode;

	public MenuItem GetMenuItem( int itemID ) => itemID >= 0 && itemID < _items.Count ? _items[itemID] : null;

	public int GetMenuID( int row ) => row >= 0 && row < _items.Count ? row : -1;

	public int GetMenuID( MenuItem item ) => _items.IndexOf( item );

	public void SetMenuItemHeight( int itemHeight ) => _menuItemHeight = itemHeight;

	public void SetNumberOfVisibleItems( int numItems )
	{
		_numVisibleLines = numItems;

		InvalidateLayout();
	}

	public void SetItemWidePadding( int padding ) => _itemWidePadding = padding;

	public void SetFixedWidth( int width )
	{
		_fixedWidth = width;

		InvalidateLayout();
	}

	public int AddMenuItem( string itemName, string itemText, Message message, Panel target )
	{
		var item = new MenuItem( this, itemName, itemText );

		item.SetCommand( message );
		item.AddActionSignalTarget( target );

		return AddMenuItem( item );
	}

	public int AddMenuItem( string itemName, string itemText, Message message, Panel target, KeyValues userData )
	{
		var item = new MenuItem( this, itemName, itemText ) { UserData = userData };

		item.SetCommand( message );
		item.AddActionSignalTarget( target );

		return AddMenuItem( item );
	}

	public bool IsValidMenuID( int itemID ) => itemID >= 0 && itemID < _items.Count;

	public KeyValues GetItemUserData( int itemID ) => GetMenuItem( itemID ) is { Deleted: false } item ? item.UserData : null;

	public void DeleteAllItems()
	{
		foreach ( var item in _items )
			item.MarkForDeletion();

		_items.Clear();

		InvalidateLayout();
	}

	public int AddMenuItem( string itemName, string itemText, string command, Panel target )
	{
		return AddMenuItem( itemName, itemText, new Message( "Command" ) { Text = command }, target );
	}

	public void SetItemEnabled( string itemName, bool state )
	{
		foreach ( var item in _items )
		{
			if ( string.Equals( itemName, item.Name, System.StringComparison.OrdinalIgnoreCase ) )
				item.SetEnabled( state );
		}
	}

	public int AddMenuItem( MenuItem item )
	{
		item.SetParent( this );

		_items.Add( item );
		_recalculateWidth = true;

		InvalidateLayout();

		return _items.Count - 1;
	}

	public override void SetVisible( bool state )
	{
		if ( state == Visible )
			return;

		if ( state )
		{
			MoveToFront();
			RequestFocus();
		}
		else
		{
			PostActionSignal( new Message( "MenuClose" ) );
			SetCurrentlyHighlightedItemInternal( -1 );
		}

		base.SetVisible( state );

		_sizedForScrollBar = false;
	}

	public void ForceCalculateWidth()
	{
		_recalculateWidth = true;

		ComputeWidth();
		PerformLayout();
	}

	private void ComputeWidth()
	{
		if ( !_recalculateWidth )
			return;

		_menuWide = 0;

		if ( _fixedWidth == 0 )
		{
			foreach ( var item in _items )
			{
				var wide = item.GetContentSize().Wide;

				if ( _menuWide - ContentWidthPadding < wide )
					_menuWide = wide + ContentWidthPadding;
			}
		}

		_recalculateWidth = false;
	}

	public void ClearCurrentlyHighlightedItem() => SetCurrentlyHighlightedItemInternal( -1 );

	private void SetCurrentlyHighlightedItemInternal( int itemID )
	{
		if ( itemID == _currentlyHighlightedItem )
			return;

		GetMenuItem( _currentlyHighlightedItem )?.DisarmItem();

		PostActionSignal( new Message( "MenuItemHighlight" ) { Int = itemID } );

		_currentlyHighlightedItem = itemID;
	}

	private void ScrollToItem( int row )
	{
		if ( _scroller.Visible && (_scroller.Value + _numVisibleLines - 1 < row || row < _scroller.Value) )
			_scroller.SetValue( row );
	}

	public void SetCurrentlyHighlightedItem( int itemID )
	{
		SetCurrentlyHighlightedItemInternal( itemID );
		ScrollToItem( itemID );

		if ( GetMenuItem( _currentlyHighlightedItem ) is { IsArmed: false } item )
			item.ArmItem();
	}

	public void ActivateItem( int itemID )
	{
		if ( GetMenuItem( itemID ) is not { Enabled: true } item )
			return;

		item.DoClick();

		_activatedItem = itemID;
	}

	private void MoveAlongMenuItemList( int direction, int loopCount )
	{
		var row = _currentlyHighlightedItem + direction;

		if ( row > _items.Count - 1 )
			row = _scroller.Visible ? _items.Count - 1 : 0;
		else if ( row < 0 )
			row = _scroller.Visible ? _scroller.Value : _items.Count - 1;

		if ( _scroller.Visible )
		{
			if ( _scroller.Value + _numVisibleLines - 1 < row || row < _scroller.Value )
			{
				_scroller.SetValue( _scroller.Value + direction );

				InvalidateLayout();
			}

			ScrollToItem( row );
		}

		SetCurrentlyHighlightedItemInternal( row );

		if ( loopCount >= _items.Count )
			return;

		if ( _items[row].Text.Length != 0 && _items[row].Visible )
			return;

		MoveAlongMenuItemList( direction, loopCount + 1 );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "MenuItemSelected":
				OnMenuItemSelected( message.Panel );
				break;

			case "CursorEnteredMenuItem":
				OnCursorEnteredMenuItem( message.Panel as MenuItem );
				break;

			case "CursorExitedMenuItem":
				if ( !_keyboardInputMode )
					(message.Panel as MenuItem)?.DisarmItem();

				break;

			case "KeyModeSet":
				_keyboardInputMode = true;
				break;

			case "ScrollBarSliderMoved":
				InvalidateLayout();
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	private void OnMenuItemSelected( Panel panel )
	{
		SetVisible( false );

		var itemID = _items.IndexOf( panel as MenuItem );

		if ( itemID >= 0 )
			_activatedItem = itemID;

		if ( Parent is not null )
			PostMessage( Parent, new Message( "MenuItemSelected" ) { Panel = panel } );
	}

	private void OnCursorEnteredMenuItem( MenuItem item )
	{
		if ( _keyboardInputMode || item is null )
			return;

		item.ArmItem();

		SetCurrentlyHighlightedItemInternal( _items.IndexOf( item ) );
	}

	public override void OnCommand( string command )
	{
		PostActionSignal( new Message( "Command" ) { Text = command } );

		base.OnCommand( command );
	}

	public override void OnCursorMoved( int x, int y )
	{
		_keyboardInputMode = false;

		RequestFocus();
		InvalidateLayout();
	}

	public override void OnKeyCodePressed( KeyCode code )
	{
		_keyboardInputMode = true;

		if ( Parent is not null )
			PostMessage( Parent, new Message( "KeyModeSet" ) );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		var input = Vgui.Input;

		if ( input.IsAltDown )
		{
			base.OnKeyCodeTyped( code );

			PostActionSignal( new Message( "MenuClose" ) );
		}

		switch ( code )
		{
			case KeyCode.Enter when GetMenuItem( _currentlyHighlightedItem ) is not null:
				ActivateItem( _currentlyHighlightedItem );
				break;

			case KeyCode.Escape:
			case KeyCode.F4 when input.IsControlDown:
				SetVisible( false );
				break;

			case KeyCode.Up:
			case KeyCode.Down:
				MoveAlongMenuItemList( code == KeyCode.Up ? -1 : 1, 0 );
				GetMenuItem( _currentlyHighlightedItem )?.ArmItem();
				break;

			case KeyCode.Enter:
			case KeyCode.Left:
			case KeyCode.Right:
				base.OnKeyCodeTyped( code );
				break;
		}
	}

	public override void OnKeyTyped( char character )
	{
		if ( _items.Count == 0 )
			return;

		var current = System.Math.Max( _currentlyHighlightedItem, 0 );
		var itemID = current + 1 < _items.Count ? current + 1 : 0;

		while ( itemID != current )
		{
			var text = _items[itemID].Text;

			if ( text.Length > 0 && char.ToLowerInvariant( text[0] ) == char.ToLowerInvariant( character ) )
				break;

			itemID = itemID + 1 < _items.Count ? itemID + 1 : 0;
		}

		SetCurrentlyHighlightedItem( itemID );
		InvalidateLayout();
	}

	public override void OnKillFocus()
	{
		var focus = Vgui.Input.Focus;

		if ( focus is not null && focus.HasParent( this ) )
			return;

		if ( !KeyboardInputEnabled && focus is null )
			return;

		PostActionSignal( new Message( "MenuClose" ) );
		SetVisible( false );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "Menu/FgColor" ) );
		SetBgColor( GetSchemeColor( "Menu/BgColor" ) );

		_borderDark = Vgui.Scheme.GetColor( "BorderDark", new Color32( 255, 255, 255, 0 ) );

		_recalculateWidth = true;

		ComputeWidth();
		InvalidateLayout();
	}

	public override void OnMouseWheeled( int delta )
	{
		if ( !_scroller.Visible )
			return;

		_scroller.SetValue( _scroller.Value - delta );

		InvalidateLayout();
	}

	private void MakeItemsVisibleInScrollRange()
	{
		var shown = 0;
		var start = _scroller.Value;

		while ( start < _items.Count )
		{
			for ( var i = 0; i < start; i++ )
				_items[i].SetVisible( false );

			var row = start;

			for ( ; shown < _numVisibleLines && row < _items.Count; row++, shown++ )
				_items[row].SetVisible( true );

			for ( ; row < _items.Count; row++ )
				_items[row].SetVisible( false );

			if ( shown < _numVisibleLines )
			{
				start--;
				shown = 0;
			}

			if ( _numVisibleLines - 1 <= shown )
				return;
		}
	}

	public override void Paint()
	{
		if ( !_scroller.Visible )
			return;

		Vgui.Surface.DrawSetColor( _borderDark );
		Vgui.Surface.DrawFilledRect( Wide - _scrollerSize, -1, Wide - _scrollerSize + 1, Tall );
	}

	protected virtual void LayoutMenuBorder()
	{
		if ( Vgui.Scheme.GetBorder( "MenuBorder" ) is { } border )
			SetBorder( border );
	}

	public override void PerformLayout()
	{
		if ( _numVisibleLines < _items.Count )
		{
			_scroller.SetVisible( true );
			_sizedForScrollBar = true;

			MakeItemsVisibleInScrollRange();
		}
		else
		{
			_scroller.SetVisible( false );
			_sizedForScrollBar = false;
		}

		LayoutMenuBorder();

		var itemHeight = IsProportional ? Scaled( _menuItemHeight ) : _menuItemHeight;
		var y = 0;

		for ( var row = 0; row < _items.Count; row++ )
		{
			if ( !_items[row].Visible )
				continue;

			_items[row].SetPos( 0, y );

			y += itemHeight;
		}

		if ( _fixedWidth == 0 )
			ComputeWidth();
		else
			_menuWide = _sizedForScrollBar ? _fixedWidth - _scrollerSize : _fixedWidth;

		var itemWide = Scaled( _itemWidePadding ) - InsetLeft - InsetTop + _menuWide;
		var itemTall = Scaled( _menuItemHeight );

		foreach ( var item in _items )
			item.SetSize( itemWide, itemTall );

		SetSize( Scaled( 5 ) + (_sizedForScrollBar ? _scrollerSize : 0) + _menuWide, InsetRight + y + InsetBottom );

		if ( _scroller.Visible )
		{
			_scroller.SetEnabled( false );
			_scroller.SetRangeWindow( _numVisibleLines );
			_scroller.SetRange( 0, _items.Count );
			_scroller.SetButtonPressedScrollValue( 1 );
			_scroller.SetPos( Wide - InsetTop - _scrollerSize, 1 );
			_scroller.SetSize( _scrollerSize, Tall - InsetRight - InsetBottom );
		}

		foreach ( var item in _items )
			item.InvalidateLayout();
	}
}
