using System;

namespace GoldSrc.GameUI;

public sealed class ListPanelItem
{
	public KeyValues Data;
	public int UserData;
	public bool Visible;
	public int SortRank;
	public int SecondarySortRank;
}

public delegate int ListPanelSortFunc( ListPanel panel, ListPanelItem item1, ListPanelItem item2 );

[Flags]
public enum ListPanelColumnFlags
{
	FixedWidth = 1,
	ResizeWithWindow = 2,
	Image = 4,
	Hidden = 8,
	Unhidable = 16
}

public sealed class ImageList
{
	private readonly List<Image> _images = [null];

	public int AddImage( Image image )
	{
		_images.Add( image );

		return _images.Count - 1;
	}

	public bool IsValidIndex( int index ) => index >= 0 && index < _images.Count;

	public Image GetImage( int index ) => _images[index];
}

public sealed class ListPanelColumnButton( ListPanel parent, string name, string text ) : Button( parent.Vgui, parent, name, text )
{
	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetContentAlignment( Alignment.West );
		SetFont( GetFont( "DefaultSmall" ) );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !Enabled || code == MouseCode.Right || !IsMouseClickEnabled( code ) || !IsUseCaptureMouseEnabled )
			return;

		SetSelected( true );

		Vgui.Input.SetMouseCapture( this );
	}
}

public sealed class ListPanelColumnDragger : Panel
{
	private readonly int _column;

	private bool _dragging;
	private bool _movable = true;
	private int _dragPos;

	public ListPanelColumnDragger( Vgui vgui, int column ) : base( vgui, null, null )
	{
		_column = column;

		SetPaintBackgroundEnabled( false );
		SetPaintEnabled( false );
		SetPaintBorderEnabled( false );
		SetCursor( CursorCode.SizeWe );
	}

	public void SetMovable( bool state )
	{
		_movable = state;

		if ( Visible )
			SetCursor( state ? CursorCode.SizeWe : CursorCode.Arrow );
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( !_movable )
			return;

		Vgui.Input.SetMouseCapture( this );

		_dragPos = Vgui.Input.CursorPos.X;
		_dragging = true;
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( _movable && Parent is not null )
			PostMessage( Parent, new Message( "ResizeColumnToContents" ) { Int = _column } );
	}

	public override void OnMouseReleased( MouseCode code )
	{
		if ( !_movable )
			return;

		Vgui.Input.SetMouseCapture( null );

		_dragging = false;
	}

	public override void OnCursorMoved( int x, int y )
	{
		if ( !_dragging )
			return;

		var position = Vgui.Input.CursorPos.X;

		if ( Parent is not null )
			PostMessage( Parent, new Message( "ColumnResized" ) { Int = _column, Int2 = position - _dragPos } );

		_dragPos = position;
	}
}

public sealed class ListPanel : Panel
{
	private sealed class Column
	{
		public ListPanelColumnButton Header;
		public ListPanelColumnDragger Resizer;
		public ListPanelSortFunc SortFunc;
		public int MinWidth;
		public int MaxWidth;
		public bool ResizesWithWindow;
		public bool Hidden;
		public bool Unhidable;
		public bool TypeIsText;
	}

	private readonly List<Column> _columns = [];
	private readonly List<ListPanelItem> _items = [];
	private readonly List<int> _visibleItems = [];
	private readonly List<int> _selectedItems = [];
	private readonly ScrollBar _vertScrollBar;
	private readonly Label _label;
	private readonly TextImage _textImage;
	private readonly TextImage _emptyListText;

	private int _headerHeight;
	private int _rowHeight;
	private int _columnDraggerMoved = -1;
	private int _lastBarWidth;
	private int _sortColumn = -1;
	private int _sortColumnSecondary = -1;
	private bool _sortAscending = true;
	private bool _needsSort;
	private int _lastItemSelected = -1;
	private int _tableStartX;
	private int _tableStartY;
	private Color32 _labelFgColor;
	private Color32 _selectionFgColor;
	private ImageList _imageList;
	private readonly Stack<int> _freeItems = [];

	public ListPanel( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_headerHeight = Scaled( 20 );
		_rowHeight = Scaled( 20 );

		var horizScrollBar = new ScrollBar( vgui, this, "HorizScrollBar", false );

		horizScrollBar.AddActionSignalTarget( this );
		horizScrollBar.SetVisible( false );

		_vertScrollBar = new ScrollBar( vgui, this, "VertScrollBar", true );
		_vertScrollBar.SetVisible( false );
		_vertScrollBar.AddActionSignalTarget( this );

		_label = new Label( vgui, this, null, "" );
		_label.SetVisible( false );
		_label.SetPaintBackgroundEnabled( false );
		_label.SetContentAlignment( Alignment.West );
		_label.SetScaleImages( false );

		_textImage = new TextImage( vgui, "" );
		_emptyListText = new TextImage( vgui, "" );
	}

	public void AddColumnHeader( int index, string columnName, string columnText, int width, ListPanelColumnFlags flags )
	{
		if ( (flags & (ListPanelColumnFlags.FixedWidth | ListPanelColumnFlags.ResizeWithWindow)) == ListPanelColumnFlags.FixedWidth )
			AddColumnHeader( index, columnName, columnText, width, width, width, flags );
		else
			AddColumnHeader( index, columnName, columnText, width, Scaled( 20 ), Scaled( 10000 ), flags );
	}

	private void AddColumnHeader( int index, string columnName, string columnText, int width, int minWidth, int maxWidth, ListPanelColumnFlags flags )
	{
		var header = new ListPanelColumnButton( this, columnName, columnText );

		header.MakeReadyForUse();
		header.SetSize( width, Scaled( 24 ) );
		header.AddActionSignalTarget( this );
		header.SetContentAlignment( Alignment.West );
		header.SetTextInset( Scaled( 5 ), 0 );

		var dragger = new ListPanelColumnDragger( Vgui, index );

		dragger.SetParent( this );
		dragger.AddActionSignalTarget( this );
		dragger.MoveToFront();

		if ( minWidth == maxWidth || (flags & ListPanelColumnFlags.FixedWidth) == ListPanelColumnFlags.FixedWidth )
			dragger.SetMovable( false );

		_columns.Insert( index, new Column
		{
			Header = header,
			Resizer = dragger,
			MinWidth = minWidth,
			MaxWidth = maxWidth,
			ResizesWithWindow = (flags & ListPanelColumnFlags.ResizeWithWindow) == ListPanelColumnFlags.ResizeWithWindow,
			TypeIsText = (flags & ListPanelColumnFlags.Image) != ListPanelColumnFlags.Image,
			Unhidable = (flags & ListPanelColumnFlags.Unhidable) == ListPanelColumnFlags.Unhidable
		} );

		ResetColumnHeaderCommands();

		_vertScrollBar.MoveToFront();

		SetColumnVisible( index, (flags & ListPanelColumnFlags.Hidden) != ListPanelColumnFlags.Hidden );

		InvalidateLayout();
	}

	public void SetColumnHeaderText( int column, string text ) => _columns[column].Header.SetText( text );

	public void SetColumnHeaderImage( int column, int imageListIndex )
	{
		var header = _columns[column].Header;

		header.SetTextImageIndex( -1 );
		header.SetImageAtIndex( 0, _imageList.GetImage( imageListIndex ), 0 );
	}

	public void SetColumnVisible( int column, bool visible )
	{
		var data = _columns[column];
		var hidden = !visible;

		if ( data.Hidden == hidden || data.Unhidable )
			return;

		data.Hidden = hidden;
		data.Header.SetVisible( !hidden );
		data.Resizer.SetVisible( !hidden );

		InvalidateLayout();
	}

	public void SetImageList( ImageList imageList ) => _imageList = imageList;

	public void SetFont( SchemeFont font )
	{
		if ( font is null )
			return;

		_textImage.SetFont( font );
		_rowHeight = Surface.GetFontTall( font ) + 2;
	}

	private void ResetColumnHeaderCommands()
	{
		for ( var i = 0; i < _columns.Count; i++ )
			_columns[i].Header.SetCommand( new Message( "SetSortColumn" ) { Int = i } );
	}

	public void SetSortFunc( int column, ListPanelSortFunc func ) => _columns[column].SortFunc = func;

	public void SetSortColumn( int column ) => _sortColumn = column;

	public int AddItem( KeyValues data, bool scrollToItem, bool sortOnAdd ) => AddItem( data, 0, scrollToItem, sortOnAdd );

	public int AddItem( KeyValues data, int userData, bool scrollToItem, bool sortOnAdd )
	{
		var item = new ListPanelItem { Data = data.MakeCopy(), UserData = userData, Visible = true };
		int itemID;

		if ( _freeItems.Count > 0 )
		{
			itemID = _freeItems.Pop();
			_items[itemID] = item;
		}
		else
		{
			itemID = _items.Count;
			_items.Add( item );
		}

		_visibleItems.Add( itemID );

		if ( sortOnAdd )
			_needsSort = true;

		InvalidateLayout();

		if ( scrollToItem )
			_vertScrollBar.SetValue( _visibleItems.Count - 1 );

		return itemID;
	}

	public int GetItemCount() => _visibleItems.Count;

	public int GetSelectedItemsCount() => _selectedItems.Count;

	public void RereadAllItems() => InvalidateLayout();

	public void DeleteAllItems()
	{
		_items.Clear();
		_freeItems.Clear();
		_visibleItems.Clear();
		_selectedItems.Clear();

		InvalidateLayout();
	}

	public void SetEmptyListText( string text )
	{
		_emptyListText.SetText( text );
	}

	public bool IsValidItemID( int itemID ) => itemID >= 0 && itemID < _items.Count && _items[itemID] is not null;

	public KeyValues GetItem( int itemID ) => IsValidItemID( itemID ) ? _items[itemID].Data : null;

	public ListPanelItem GetItemData( int itemID ) => IsValidItemID( itemID ) ? _items[itemID] : null;

	public int GetItemUserData( int itemID ) => IsValidItemID( itemID ) ? _items[itemID].UserData : 0;

	public void SetUserData( int itemID, int userData )
	{
		if ( IsValidItemID( itemID ) )
			_items[itemID].UserData = userData;
	}

	public void ApplyItemChanges( int itemID ) => InvalidateLayout();

	public void RemoveItem( int itemID )
	{
		if ( !IsValidItemID( itemID ) )
			return;

		_selectedItems.Remove( itemID );
		_visibleItems.Remove( itemID );

		_items[itemID] = null;
		_freeItems.Push( itemID );

		InvalidateLayout();
	}

	public void SetItemVisible( int itemID, bool state )
	{
		if ( !IsValidItemID( itemID ) )
			return;

		var item = _items[itemID];

		if ( item.Visible == state )
			return;

		_needsSort = true;

		item.Visible = state;

		if ( state )
		{
			_visibleItems.Add( itemID );
			return;
		}

		_selectedItems.Remove( itemID );
		_visibleItems.Remove( itemID );

		InvalidateLayout();
	}

	public int GetItemIDFromRow( int row ) => row >= 0 && row < _visibleItems.Count ? _visibleItems[row] : -1;

	public int GetSelectedItem( int selectionIndex ) => selectionIndex >= 0 && selectionIndex < _selectedItems.Count ? _selectedItems[selectionIndex] : -1;

	public void ClearSelectedItems() => _selectedItems.Clear();

	public void AddSelectedItem( int itemID )
	{
		if ( !IsValidItemID( itemID ) )
			return;

		_selectedItems.Add( itemID );

		PostActionSignal( new Message( "ItemSelected" ) );
	}

	public void SetSingleSelectedItem( int itemID )
	{
		ClearSelectedItems();
		AddSelectedItem( itemID );
	}

	private float RowsPerPage => (float)(Tall - _headerHeight) / _rowHeight;

	private int StartItem => RowsPerPage < _visibleItems.Count ? _vertScrollBar.Value : 0;

	private static int DefaultSortFunc( string columnName, bool typeIsText, ListPanelItem item1, ListPanelItem item2 )
	{
		if ( typeIsText && item1.Data.FindKey( columnName, true ) is not { IsInt: true } )
			return string.Compare( item1.Data.GetString( columnName ), item2.Data.GetString( columnName ), StringComparison.OrdinalIgnoreCase );

		var value1 = item1.Data.GetInt( columnName );
		var value2 = item2.Data.GetInt( columnName );

		if ( value1 < value2 )
			return -1;

		return value2 < value1 ? 1 : 0;
	}

	private int CompareColumn( int column, ListPanelItem item1, ListPanelItem item2 )
	{
		return _columns[column].SortFunc is { } func ? func( this, item1, item2 ) : DefaultSortFunc( _columns[column].Header.Name, _columns[column].TypeIsText, item1, item2 );
	}

	private void RankColumn( int column, bool secondary )
	{
		if ( column < 0 || column >= _columns.Count )
			return;

		var sorted = new List<ListPanelItem>();

		foreach ( var item in _items )
		{
			if ( item is null )
				continue;

			var position = sorted.FindIndex( x => CompareColumn( column, item, x ) > 0 );

			sorted.Insert( position < 0 ? sorted.Count : position, item );
		}

		var rank = 1;

		for ( var i = 0; i < sorted.Count; i++ )
		{
			if ( i == 0 || CompareColumn( column, sorted[i - 1], sorted[i] ) != 0 )
				rank++;

			if ( secondary )
				sorted[i].SecondarySortRank = rank;
			else
				sorted[i].SortRank = rank;
		}
	}

	private int CompareItems( int itemID1, int itemID2 )
	{
		var item1 = _items[itemID1];
		var item2 = _items[itemID2];
		int result;

		if ( item1.SortRank != item2.SortRank )
			result = item1.SortRank < item2.SortRank ? 1 : -1;
		else if ( item1.SecondarySortRank != item2.SecondarySortRank )
			result = item1.SecondarySortRank < item2.SecondarySortRank ? 1 : -1;
		else
			result = itemID1 < itemID2 ? 1 : -1;

		return _sortAscending ? result : -result;
	}

	public void SortList()
	{
		_needsSort = false;

		if ( _visibleItems.Count <= 1 )
			return;

		var startItem = StartItem;
		var screenPosition = -1;

		if ( _lastItemSelected != -1 && _selectedItems.Count > 0 )
		{
			var selectedItemRow = _visibleItems.IndexOf( _lastItemSelected );

			if ( selectedItemRow >= startItem && selectedItemRow <= (int)RowsPerPage + startItem )
				screenPosition = selectedItemRow - startItem;
		}

		RankColumn( _sortColumn, false );
		RankColumn( _sortColumnSecondary, true );

		_visibleItems.Sort( ( x, y ) => x == y ? 0 : CompareItems( x, y ) );

		if ( screenPosition != -1 )
		{
			var selectedItemRow = _visibleItems.IndexOf( _lastItemSelected );

			_vertScrollBar.SetValue( screenPosition < selectedItemRow ? selectedItemRow - screenPosition : 0 );
		}

		InvalidateLayout();
	}

	private void OnSetSortColumn( int column )
	{
		if ( _sortColumn == column )
			_sortAscending = !_sortAscending;
		else
			_sortColumnSecondary = _sortColumn;

		SetSortColumn( column );
		SortList();
	}

	private void OnColumnResized( int column, int delta )
	{
		_columnDraggerMoved = column;

		var header = _columns[column].Header;
		var wide = Math.Min( Math.Max( header.Wide + delta, _columns[column].MinWidth ), _columns[column].MaxWidth );
		var restColumnsMinWidth = 0;

		for ( var i = column + 1; i < _columns.Count; i++ )
			restColumnsMinWidth += _columns[i].MinWidth;

		var maxSize = Wide - (Scaled( 20 ) + restColumnsMinWidth + header.X);

		if ( maxSize < wide )
			wide = maxSize;

		header.SetSize( wide, header.Tall );
		header.InvalidateLayout();

		InvalidateLayout();
	}

	private void ResizeColumnToContents( int column )
	{
		var header = _columns[column].Header;
		var minRequiredWidth = header.GetContentSize().Wide;

		foreach ( var itemID in _visibleItems )
		{
			_textImage.SetText( _items[itemID].Data.GetString( header.Name ) );

			var wide = _textImage.GetContentSize().Wide;

			if ( minRequiredWidth < wide )
				minRequiredWidth = wide + 2;
		}

		OnColumnResized( column, minRequiredWidth - header.Wide );
	}

	public override void OnMessage( Message message, Panel from )
	{
		switch ( message.Name )
		{
			case "ScrollBarSliderMoved":
				InvalidateLayout();
				break;

			case "ColumnResized":
				OnColumnResized( message.Int, message.Int2 );
				break;

			case "SetSortColumn":
				OnSetSortColumn( message.Int );
				break;

			case "ResizeColumnToContents":
				ResizeColumnToContents( message.Int );
				break;

			default:
				base.OnMessage( message, from );
				break;
		}
	}

	private bool GetCellAtPos( int x, int y, out int row, out int column )
	{
		(x, y) = ScreenToLocal( x, y );

		x -= _tableStartX;
		y -= _tableStartY;

		var startItem = StartItem;

		if ( x >= 0 && y >= 0 )
		{
			for ( row = startItem; row < _visibleItems.Count; row++ )
			{
				if ( y < (row - startItem + 1) * _rowHeight )
					break;
			}

			var startx = 0;

			for ( column = 0; column < _columns.Count; column++ )
			{
				startx += _columns[column].Header.Wide;

				if ( x < startx )
					break;
			}

			if ( row != _visibleItems.Count && column != _columns.Count )
				return true;
		}

		row = -1;
		column = -1;

		return false;
	}

	public override void OnMousePressed( MouseCode code )
	{
		if ( code is not (MouseCode.Left or MouseCode.Right) )
			return;

		var input = Vgui.Input;
		var (x, y) = input.CursorPos;

		GetCellAtPos( x, y, out var row, out _ );

		if ( _lastItemSelected < 0 && _visibleItems.Count > 0 && row < _visibleItems.Count )
			_lastItemSelected = 0;

		if ( _lastItemSelected != -1 )
		{
			var itemID = GetItemIDFromRow( row );

			if ( (code != MouseCode.Right || !_selectedItems.Contains( itemID )) && row >= 0 && row < _visibleItems.Count )
			{
				if ( input.IsShiftDown )
				{
					var lastSelectedRow = _visibleItems.IndexOf( _lastItemSelected );
					var startRow = Math.Max( Math.Min( lastSelectedRow, row ), 0 );
					var endRow = Math.Max( lastSelectedRow, row );

					if ( !input.IsControlDown )
						ClearSelectedItems();

					for ( var i = startRow; i <= endRow; i++ )
					{
						if ( !_selectedItems.Contains( _visibleItems[i] ) )
							AddSelectedItem( _visibleItems[i] );
					}
				}
				else if ( input.IsControlDown )
				{
					if ( !_selectedItems.Remove( itemID ) )
						AddSelectedItem( itemID );

					_lastItemSelected = itemID;
				}
				else
				{
					_lastItemSelected = itemID;

					SetSingleSelectedItem( itemID );
				}
			}
		}

		RequestFocus();

		if ( code == MouseCode.Right )
			PostActionSignal( new Message( "OpenContextMenu" ) { Int = GetSelectedItem( 0 ) } );
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( code != MouseCode.Left )
			return;

		OnMousePressed( MouseCode.Left );

		if ( _selectedItems.Count <= 0 )
			return;

		OnKeyCodeTyped( KeyCode.Enter );

		PostActionSignal( new Message( "ItemDoubleClicked" ) { Int = _selectedItems[0] } );
	}

	private const int MouseWheelRows = 3;

	public override void OnMouseWheeled( int delta ) => _vertScrollBar.SetValue( _vertScrollBar.Value - (delta * MouseWheelRows) );

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( _visibleItems.Count == 0 )
		{
			base.OnKeyCodeTyped( code );
			return;
		}

		var nTotalRows = _visibleItems.Count;
		var rowsPerPage = RowsPerPage;
		var nRowsPerPage = (int)rowsPerPage;
		var nSelectedRow = _lastItemSelected >= 0 && _lastItemSelected < _items.Count ? _visibleItems.IndexOf( _lastItemSelected ) : 0;
		var nStartItem = StartItem;

		switch ( code )
		{
			case KeyCode.Home:
				nSelectedRow = 0;
				break;

			case KeyCode.End:
				nSelectedRow = nTotalRows - 1;
				break;

			case KeyCode.Up:
				nSelectedRow--;
				break;

			case KeyCode.PageUp:
				nSelectedRow = nSelectedRow <= nStartItem ? nSelectedRow + (1 - nRowsPerPage) : nStartItem;
				break;

			case KeyCode.PageDown:
				nSelectedRow = nStartItem - 1 + nRowsPerPage <= nSelectedRow ? nSelectedRow - 1 + nRowsPerPage : nStartItem - 1 + nRowsPerPage;
				break;

			case KeyCode.Down:
				nSelectedRow++;
				break;

			default:
				base.OnKeyCodeTyped( code );
				return;
		}

		nSelectedRow = Math.Max( Math.Min( nSelectedRow, nTotalRows - 1 ), 0 );

		_lastItemSelected = _visibleItems[nSelectedRow];

		SetSingleSelectedItem( _lastItemSelected );

		if ( nRowsPerPage < (float)nTotalRows )
		{
			var value = _vertScrollBar.Value;

			if ( nSelectedRow < value )
				_vertScrollBar.SetValue( nSelectedRow );
			else if ( nSelectedRow >= value + nRowsPerPage )
				_vertScrollBar.SetValue( nSelectedRow - nRowsPerPage + 1 );
		}

		InvalidateLayout();
	}

	private Image GetCellImage( int itemID, int column )
	{
		if ( GetItem( itemID ) is not { } data || column < 0 || column >= _columns.Count )
			return null;

		var name = _columns[column].Header.Name;

		if ( string.IsNullOrEmpty( name ) || _imageList is null )
			return null;

		var index = data.GetInt( name );

		return _imageList.IsValidIndex( index ) && index > 0 ? _imageList.GetImage( index ) : null;
	}

	private void SetSelectionBgColor()
	{
		var focus = Vgui.Input.Focus;
		var focused = HasFocus || (focus is not null && focus.HasParent( Parent ));

		_label.SetBgColor( GetSchemeColor( focused ? "Menu/ArmedBgColor" : "SelectionBG2" ) );
	}

	private Label GetCellRenderer( int itemID, int column )
	{
		var header = _columns[column].Header;

		if ( !_columns[column].TypeIsText )
		{
			var selected = _selectedItems.Contains( itemID );

			if ( selected )
				SetSelectionBgColor();

			_label.SetPaintBackgroundEnabled( selected );
			_label.SetImageAtIndex( 0, GetCellImage( itemID, column ), 0 );
			_label.SetScaleImages( true );

			return _label;
		}

		_label.SetScaleImages( false );

		_textImage.SetText( _items[itemID].Data.GetString( header.Name ) );
		_textImage.SetSize( header.Wide - Scaled( 5 ), _textImage.GetContentSize().Tall );

		_label.SetImageAtIndex( 0, _textImage, 3 );

		if ( !_selectedItems.Contains( itemID ) )
		{
			_textImage.Color = _labelFgColor;

			_label.SetPaintBackgroundEnabled( false );

			return _label;
		}

		SetSelectionBgColor();

		_textImage.Color = _selectionFgColor;

		_label.SetPaintBackgroundEnabled( true );

		return _label;
	}

	public override void ApplySchemeSettings()
	{
		_label.InvalidateLayout( true );

		base.ApplySchemeSettings();

		SetBgColor( GetSchemeColor( "WindowBgColor" ) );
		SetBorder( Vgui.Scheme.GetBorder( "ButtonDepressedBorder" ) );

		_label.SetBgColor( GetSchemeColor( "Menu/ArmedBgColor" ) );

		SetBgColor( GetSchemeColor( "ListBgColor", GetSchemeColor( "WindowDisabledBgColor" ) ) );

		_labelFgColor = GetSchemeColor( "WindowFgColor" );
		_selectionFgColor = GetSchemeColor( "ListSelectionFgColor", _labelFgColor );

		_emptyListText.Color = GetSchemeColor( "LabelDimText" );

		var font = GetFont( "Default" );

		SetFont( font );

		_emptyListText.SetFont( font );
	}

	public override void PerformLayout()
	{
		if ( _needsSort )
			SortList();

		_vertScrollBar.SetVisible( true );
		_vertScrollBar.SetEnabled( false );
		_vertScrollBar.SetRangeWindow( (int)RowsPerPage );
		_vertScrollBar.SetRange( 0, _visibleItems.Count );
		_vertScrollBar.SetButtonPressedScrollValue( 1 );
		_vertScrollBar.SetPos( Wide - Scaled( 20 ), 0 );
		_vertScrollBar.SetSize( Scaled( 18 ), Tall - Scaled( 2 ) );
		_vertScrollBar.InvalidateLayout();

		var buttonMaxXPos = Wide - Scaled( 20 );
		var nColumns = _columns.Count;
		var numToResize = _columnDraggerMoved == -1 ? _columns.Count( x => x.ResizesWithWindow && !x.Hidden ) : 1;
		var lastColumnIndex = _columns.FindLastIndex( x => !x.Hidden );

		if ( lastColumnIndex < 0 )
			lastColumnIndex = nColumns - 1;

		var lastColumnX = lastColumnIndex >= 0 && !_columns[lastColumnIndex].Hidden ? _columns[lastColumnIndex].Header.X : 0;
		var forceShrink = false;
		var dxPerBar = 0;

		if ( numToResize == 0 )
		{
			var totalDesiredWidth = _columns.Sum( x => x.Hidden ? 0 : x.MinWidth );

			if ( totalDesiredWidth > buttonMaxXPos )
			{
				forceShrink = true;
				numToResize = nColumns;
				dxPerBar = (int)((float)(buttonMaxXPos - totalDesiredWidth) / numToResize);
			}
		}
		else if ( lastColumnX == 0 )
		{
			dxPerBar = (int)((float)(buttonMaxXPos - _columns.Sum( x => x.Hidden ? 0 : x.Header.Wide )) / numToResize);
		}
		else
		{
			dxPerBar = (int)((float)(buttonMaxXPos - _lastBarWidth) / numToResize);
		}

		_lastBarWidth = buttonMaxXPos;

		while ( true )
		{
			var x = -1;
			int i;

			for ( i = 0; i < nColumns; i++ )
			{
				var column = _columns[i];
				var header = column.Header;

				if ( column.Hidden )
				{
					header.SetVisible( false );
					continue;
				}

				header.SetPos( x, 0 );
				header.SetVisible( true );

				if ( buttonMaxXPos <= column.MinWidth + x && !forceShrink )
					break;

				var wide = header.Wide;

				if ( i == lastColumnIndex )
					wide = buttonMaxXPos - x;
				else if ( i == _columnDraggerMoved || (_columnDraggerMoved == -1 && (column.ResizesWithWindow || forceShrink)) )
					wide += dxPerBar;

				if ( wide < column.MinWidth && !forceShrink )
					wide = column.MinWidth;
				else if ( wide > column.MaxWidth )
					wide = column.MaxWidth;

				header.SetSize( wide, Scaled( 18 ) );

				x += wide;

				column.Resizer.SetVisible( i != lastColumnIndex );
				column.Resizer.MoveToFront();
				column.Resizer.SetPos( x - Scaled( 4 ), 0 );
				column.Resizer.SetSize( Scaled( 8 ), Scaled( 18 ) );
			}

			if ( i == nColumns )
				break;

			var totalWidth = _columns.Sum( c => c.Hidden ? 0 : c.Header.Wide );
			int last;

			for ( last = nColumns - 1; last >= 0; last-- )
			{
				var column = _columns[last];

				if ( column.Hidden )
					continue;

				totalWidth -= column.Header.Wide;

				if ( column.MinWidth + totalWidth <= buttonMaxXPos )
				{
					column.Header.SetSize( buttonMaxXPos - totalWidth, Scaled( 18 ) );
					break;
				}

				totalWidth += column.MinWidth;

				column.Header.SetSize( column.MinWidth, Scaled( 18 ) );
			}

			if ( last == -1 )
				break;
		}

		_columnDraggerMoved = -1;
	}

	public override void Paint()
	{
		if ( _needsSort )
			SortList();

		_tableStartX = 0;
		_tableStartY = Scaled( 1 ) + _headerHeight;

		var surface = Vgui.Surface;
		var nTotalRows = _visibleItems.Count;
		var nStartItem = (int)RowsPerPage <= (float)nTotalRows ? _vertScrollBar.Value : 0;
		var done = false;
		var drawcount = 0;

		for ( var i = nStartItem; i < nTotalRows && !done; i++ )
		{
			var x = 0;
			var itemID = _visibleItems[i];

			for ( var j = 0; j < _columns.Count; j++ )
			{
				var header = _columns[j].Header;
				var render = GetCellRenderer( itemID, j );

				if ( !header.Visible )
					continue;

				var wide = header.Wide;

				if ( render.Parent != this )
					render.SetParent( this );

				if ( !render.Visible )
					render.SetVisible( true );

				render.SetPos( Scaled( 2 ) + _tableStartX + x, (_rowHeight * drawcount) + _tableStartY );
				render.SetSize( wide, _rowHeight - 1 );
				render.SchemeTraverse();
				render.ThinkTraverse();
				render.SolveTraverse();

				if ( render.ClipY1 - render.ClipY0 < _rowHeight - Scaled( 3 ) )
				{
					done = true;
					break;
				}

				surface.PaintTraverse( render );

				x += wide;
			}

			drawcount++;
		}

		_label.SetVisible( false );

		if ( nTotalRows >= 1 )
			return;

		surface.PushMakeCurrent( this, true );

		_emptyListText.SetPos( Scaled( 8 ) + _tableStartX, Scaled( 4 ) + _tableStartY );
		_emptyListText.SetSize( Wide - Scaled( 8 ), _rowHeight );
		_emptyListText.Paint( surface );
	}
}
