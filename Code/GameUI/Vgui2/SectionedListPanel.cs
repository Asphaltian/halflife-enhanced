using System;

namespace GoldSrc.GameUI;

[Flags]
public enum SectionedListColumn
{
	None,
	HeaderImage = 1,
	ColumnImage = 2,
	ColumnBright = 4,
	ColumnCenter = 8,
	ColumnRight = 16
}

public delegate bool SectionSortFunc( SectionedListPanel list, int itemID1, int itemID2 );

public sealed class SectionHeader : Label
{
	private const int ColumnDataGap = 2;

	private readonly SectionedListPanel _listPanel;
	private readonly int _sectionID;

	private Color32 _sectionDividerColor;

	public SectionHeader( SectionedListPanel parent, string name, int sectionID ) : base( parent.Vgui, parent, name, "" )
	{
		_listPanel = parent;
		_sectionID = sectionID;

		SetTextImageIndex( -1 );
		SetScaleImages( false );
		ClearImages();
		SetPaintBackgroundEnabled( false );
	}

	public void SetDividerColor( Color32 color ) => _sectionDividerColor = color;

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetFgColor( GetSchemeColor( "SectionTextColor" ) );

		_sectionDividerColor = GetSchemeColor( "SectionDividerColor" );

		SetBgColor( GetSchemeColor( "BuddyListBgColor", GetBgColor() ) );
		SetFont( GetFont( "DefaultVerySmall" ) );
	}

	public override void Paint()
	{
		base.Paint();

		var y = Tall - 2;
		var surface = Vgui.Surface;

		surface.DrawSetColor( _sectionDividerColor );
		surface.DrawFilledRect( 1, y, Wide - 2, y + 1 );
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		var colCount = _listPanel.GetColumnCountBySection( _sectionID );

		if ( colCount != ImageCount )
		{
			for ( var i = 0; i < colCount; i++ )
			{
				TextImage image = null;

				if ( (_listPanel.GetColumnFlagsBySection( _sectionID, i ) & SectionedListColumn.HeaderImage) == 0 )
				{
					image = new TextImage( Vgui, "" );
					image.SetFont( Font );
					image.Color = GetFgColor();
				}

				SetImageAtIndex( i, image, 0 );
			}
		}

		for ( var repeat = 0; repeat <= 1; repeat++ )
		{
			var xpos = 0;

			for ( var i = 0; i < colCount; i++ )
			{
				var columnFlags = _listPanel.GetColumnFlagsBySection( _sectionID, i );
				var columnWidth = _listPanel.GetColumnWidthBySection( _sectionID, i );
				var drawWidth = columnWidth;

				if ( GetImageAtIndex( i ) is not { } image )
				{
					xpos += columnWidth;
					continue;
				}

				var contentWide = image.GetContentSize().Wide;
				var wide = contentWide;

				if ( (columnFlags & SectionedListColumn.ColumnRight) == 0 )
				{
					for ( var j = i + 1; j < colCount; j++ )
					{
						if ( (GetImageAtIndex( j )?.GetContentSize().Wide ?? 0) == 0 )
							drawWidth += _listPanel.GetColumnWidthBySection( _sectionID, j );
					}
				}

				if ( drawWidth >= 0 )
					wide = drawWidth;

				if ( (columnFlags & SectionedListColumn.ColumnCenter) != 0 )
					SetImageBounds( i, xpos + (wide / 2) - (contentWide / 2), wide - contentWide - ColumnDataGap );
				else if ( (columnFlags & SectionedListColumn.ColumnRight) != 0 )
					SetImageBounds( i, xpos + wide - contentWide, contentWide - ColumnDataGap );
				else
					SetImageBounds( i, xpos, wide - ColumnDataGap );

				xpos += columnWidth;

				if ( (columnFlags & SectionedListColumn.HeaderImage) != 0 || image is not TextImage textImage )
					continue;

				textImage.SetText( _listPanel.GetColumnTextBySection( _sectionID, i ) );
				textImage.SetDrawWidth( drawWidth );
			}
		}
	}
}

public sealed class SectionedListItem : Label
{
	private const int ColumnDataIndent = 6;
	private const int ColumnDataGap = 2;

	private readonly SectionedListPanel _listPanel;
	private readonly List<TextImage> _textImages = [];

	private KeyValues _data;
	private Color32 _fgColor2;
	private Color32 _armedFgColor1;
	private Color32 _armedFgColor2;
	private Color32 _armedBgColor;
	private Color32 _selectionBG2Color;
	private bool _selected;
	private bool _overrideColors;

	public SectionedListItem( SectionedListPanel parent, int itemID ) : base( parent.Vgui, parent, null, "< item >" )
	{
		_listPanel = parent;

		ID = itemID;

		SetScaleImages( false );
		Clear();
	}

	public int ID { get; set; }

	public int SectionID { get; private set; } = -1;

	public KeyValues Data => _data;

	public bool IsSelected => _selected;

	public void Clear()
	{
		_selected = false;
		_overrideColors = false;

		SectionID = -1;

		SetPaintBackgroundEnabled( false );
		SetTextImageIndex( -1 );
		ClearImages();
	}

	public void SetSectionID( int sectionID )
	{
		if ( sectionID != SectionID )
		{
			ClearImages();

			_textImages.Clear();

			InvalidateLayout();
		}

		SectionID = sectionID;
	}

	public void SetData( KeyValues data )
	{
		_data = data.MakeCopy();

		InvalidateLayout();
	}

	public void SetOverrideColors( bool state ) => _overrideColors = state;

	public void SetSelected( bool state )
	{
		if ( _selected == state )
			return;

		if ( state )
			RequestFocus();

		_selected = state;

		SetPaintBackgroundEnabled( state );
		InvalidateLayout();
	}

	public void GetCellBounds( int column, out int xpos, out int columnWide )
	{
		xpos = 0;
		columnWide = 0;

		var colCount = _listPanel.GetColumnCountBySection( SectionID );

		for ( var i = 0; i < colCount; i++ )
		{
			if ( GetImageAtIndex( i ) is not { } image )
				continue;

			var maxWidth = _listPanel.GetColumnWidthBySection( SectionID, i );
			var wide = maxWidth >= 0 ? maxWidth : image.GetContentSize().Wide;

			if ( i == column )
			{
				columnWide = wide;
				return;
			}

			xpos += wide;
		}
	}

	private bool HasListFocus => HasFocus || (Vgui.Input.Focus is { } focus && focus.HasParent( Parent ));

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_armedFgColor1 = GetSchemeColor( "BuddyButton/ArmedFgColor1" );
		_armedFgColor2 = GetSchemeColor( "BuddyButton/ArmedFgColor2" );
		_armedBgColor = GetSchemeColor( "BuddyButton/ArmedBgColor" );
		_fgColor2 = GetSchemeColor( "BuddyButton/FgColor2" );
		_selectionBG2Color = GetSchemeColor( "SelectionBG2" );
	}

	public override void OnSetFocus() => InvalidateLayout();

	public override void OnKillFocus() => InvalidateLayout();

	public override void OnMousePressed( MouseCode code )
	{
		if ( code == MouseCode.Left )
			_listPanel.PostActionSignal( new Message( "ItemLeftClick" ) { Int = ID } );
		else if ( code == MouseCode.Right )
			_listPanel.PostActionSignal( new Message( "ItemContextMenu" ) { Int = ID } );

		_listPanel.SetSelectedItem( this );
	}

	public override void OnMouseDoublePressed( MouseCode code )
	{
		if ( code == MouseCode.Left )
		{
			_listPanel.PostActionSignal( new Message( "ItemDoubleLeftClick" ) { Int = ID } );
			_listPanel.OnKeyCodeTyped( KeyCode.Enter );
		}
		else
		{
			OnMousePressed( code );
		}

		_listPanel.SetSelectedItem( this );
	}

	public override void PaintBackground()
	{
		var surface = Vgui.Surface;

		if ( _selected && !_listPanel.IsInEditMode )
			surface.DrawSetColor( HasListFocus ? _armedBgColor : _selectionBG2Color );
		else
			surface.DrawSetColor( GetBgColor() );

		surface.DrawFilledRect( 0, 0, Wide, Tall );
	}

	public override void PerformLayout()
	{
		var colCount = _listPanel.GetColumnCountBySection( SectionID );

		if ( _data is null || colCount < 1 )
		{
			SetText( "< unset >" );
		}
		else
		{
			if ( colCount != ImageCount )
			{
				for ( var i = 0; i < colCount; i++ )
				{
					if ( (_listPanel.GetColumnFlagsBySection( SectionID, i ) & SectionedListColumn.ColumnImage) != 0 )
						continue;

					var image = new TextImage( Vgui, "" );

					_textImages.Add( image );

					image.SetFont( Font );

					SetImageAtIndex( i, image, 0 );
				}

				for ( var i = ImageCount; i < colCount; i++ )
					AddImage( null, 0 );
			}

			var xpos = 0;

			for ( var i = 0; i < colCount; i++ )
			{
				var keyName = _listPanel.GetColumnNameBySection( SectionID, i );
				var columnFlags = _listPanel.GetColumnFlagsBySection( SectionID, i );
				var maxWidth = _listPanel.GetColumnWidthBySection( SectionID, i );
				var isImage = (columnFlags & SectionedListColumn.ColumnImage) != 0;
				TextImage textImage = null;

				if ( !isImage )
				{
					textImage = GetImageAtIndex( i ) as TextImage;

					if ( textImage is not null )
					{
						textImage.SetText( _data.GetString( keyName ) );
						textImage.SetDrawWidth( maxWidth );

						if ( _selected && HasListFocus )
							textImage.Color = _armedFgColor2;
						else if ( _overrideColors )
							textImage.Color = GetFgColor();
						else if ( (columnFlags & SectionedListColumn.ColumnBright) != 0 )
							textImage.Color = _armedFgColor1;
						else
							textImage.Color = _fgColor2;
					}
				}

				var imageWide = textImage?.GetContentSize().Wide ?? 0;
				var wide = maxWidth >= 0 ? maxWidth : imageWide;

				if ( i == 0 && !isImage )
				{
					SetImageBounds( i, xpos + ColumnDataIndent, wide - (ColumnDataIndent + ColumnDataGap) );
				}
				else if ( (columnFlags & SectionedListColumn.ColumnCenter) != 0 )
				{
					var offset = (wide / 2) - (imageWide / 2);

					SetImageBounds( i, xpos + offset, wide - offset - ColumnDataGap );
				}
				else if ( (columnFlags & SectionedListColumn.ColumnRight) != 0 )
				{
					SetImageBounds( i, xpos + wide - imageWide, wide - ColumnDataGap );
				}
				else
				{
					SetImageBounds( i, xpos, wide - ColumnDataGap );
				}

				xpos += wide;
			}
		}

		base.PerformLayout();
	}
}

public class SectionedListPanel : Panel
{
	private const int ButtonHeightDefault = 20;
	private const int ButtonHeightSpacer = 7;
	private const int DefaultLineSpacing = 20;
	private const int SectionGap = 8;
	private const int ScrollBarSize = 18;
	private const int Margin = 5;
	private const int MouseWheelScroll = 60;

	private sealed class Column
	{
		public string Name;
		public string Text;
		public SectionedListColumn Flags;
		public int Width;
	}

	private sealed class Section
	{
		public int ID;
		public bool AlwaysVisible;
		public SectionHeader Header;
		public SectionSortFunc SortFunc;
		public readonly List<Column> Columns = [];
	}

	private readonly List<Section> _sections = [];
	private readonly List<SectionedListItem> _items = [];
	private readonly List<int> _itemOrder = [];
	private readonly List<int> _freeIDs = [];
	private readonly List<SectionedListItem> _freeItems = [];
	private readonly List<SectionedListItem> _sortedItems = [];
	private readonly ScrollBar _scrollBar;
	private readonly int _scrollBarSize;

	private SectionedListItem _selectedItem;
	private Panel _editModePanel;
	private int _editModeItemID;
	private int _contentHeight;
	private int _lineSpacing = DefaultLineSpacing;
	private bool _sortNeeded;
	private bool _verticalScrollbarEnabled = true;

	public SectionedListPanel( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_scrollBar = new ScrollBar( vgui, this, "SectionedScrollBar", true );
		_scrollBar.SetVisible( false );
		_scrollBar.AddActionSignalTarget( this );

		_scrollBarSize = IsProportional ? (int)((float)vgui.Surface.ScreenWide / vgui.Scheme.GetProportionalBaseWide( ProportionalMode ) * ScrollBarSize) : ScrollBarSize;
	}

	public bool IsInEditMode => _editModePanel is not null;

	public int ItemCount => _sortedItems.Count;

	public int HighestItemID => _items.Count;

	public void SetVerticalScrollbar( bool state ) => _verticalScrollbarEnabled = state;

	private Section FindSection( int sectionID )
	{
		foreach ( var section in _sections )
		{
			if ( section.ID == sectionID )
				return section;
		}

		return null;
	}

	private Column FindColumn( int sectionID, int columnIndex )
	{
		return FindSection( sectionID ) is { } section && columnIndex < section.Columns.Count ? section.Columns[columnIndex] : null;
	}

	public void AddSection( int sectionID, string name, SectionSortFunc sortFunc = null )
	{
		_sections.Add( new Section { ID = sectionID, Header = new SectionHeader( this, name, sectionID ), SortFunc = sortFunc } );
	}

	public void RemoveAllSections()
	{
		foreach ( var section in _sections )
		{
			section.Header.SetVisible( false );
			section.Header.MarkForDeletion();
		}

		_sections.Clear();
		_sortedItems.Clear();

		InvalidateLayout();
		ReSortList();
	}

	public void SetSectionFgColor( int sectionID, Color32 color )
	{
		if ( FindSection( sectionID ) is not { } section )
			return;

		section.Header.SetDividerColor( color );
		section.Header.SetFgColor( color );
	}

	public void SetSectionAlwaysVisible( int sectionID, bool visible = true )
	{
		if ( FindSection( sectionID ) is { } section )
			section.AlwaysVisible = visible;
	}

	public bool AddColumnToSection( int sectionID, string columnName, string columnText, SectionedListColumn columnFlags, int width )
	{
		if ( FindSection( sectionID ) is not { } section )
			return false;

		section.Columns.Add( new Column { Name = columnName, Text = Vgui.Localize( columnText ), Flags = columnFlags, Width = width } );

		return true;
	}

	public bool ModifyColumn( int sectionID, string columnName, string columnText, int width )
	{
		if ( FindSection( sectionID ) is not { } section )
			return false;

		foreach ( var column in section.Columns )
		{
			if ( !column.Name.Equals( columnName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( columnText is null && column.Width == width )
				return true;

			if ( width >= 0 )
				column.Width = width;

			if ( columnText is not null )
				column.Text = columnText;

			section.Header.InvalidateLayout();

			return true;
		}

		return false;
	}

	public int GetColumnCountBySection( int sectionID ) => FindSection( sectionID )?.Columns.Count ?? 0;

	public string GetColumnNameBySection( int sectionID, int columnIndex ) => FindColumn( sectionID, columnIndex )?.Name;

	public string GetColumnTextBySection( int sectionID, int columnIndex ) => FindColumn( sectionID, columnIndex )?.Text;

	public SectionedListColumn GetColumnFlagsBySection( int sectionID, int columnIndex ) => FindColumn( sectionID, columnIndex )?.Flags ?? SectionedListColumn.None;

	public int GetColumnWidthBySection( int sectionID, int columnIndex ) => FindColumn( sectionID, columnIndex )?.Width ?? 0;

	public bool IsItemIDValid( int itemID ) => itemID >= 0 && itemID < _items.Count && _items[itemID] is not null;

	public int GetItemIDFromRow( int row ) => row >= 0 && row < _sortedItems.Count ? _sortedItems[row].ID : -1;

	public KeyValues GetItemData( int itemID ) => IsItemIDValid( itemID ) ? _items[itemID].Data : null;

	public int GetItemSection( int itemID ) => IsItemIDValid( itemID ) ? _items[itemID].SectionID : -1;

	public int GetSelectedItem() => _selectedItem?.ID ?? -1;

	private int GetNewItemButton()
	{
		int itemID;

		if ( _freeIDs.Count != 0 )
		{
			itemID = _freeIDs[^1];

			_freeIDs.RemoveAt( _freeIDs.Count - 1 );
		}
		else
		{
			itemID = _items.Count;

			_items.Add( null );
		}

		_itemOrder.Add( itemID );

		if ( _freeItems.Count != 0 )
		{
			var item = _freeItems[0];

			_freeItems.RemoveAt( 0 );
			_items[itemID] = item;

			item.ID = itemID;
			item.SetVisible( true );
		}
		else
		{
			_items[itemID] = new SectionedListItem( this, itemID );
		}

		return itemID;
	}

	public int AddItem( int sectionID, KeyValues data )
	{
		var itemID = GetNewItemButton();

		ModifyItem( itemID, sectionID, data );

		_sortedItems.Add( _items[itemID] );
		_sortNeeded = true;

		return itemID;
	}

	public bool ModifyItem( int itemID, int sectionID, KeyValues data )
	{
		if ( !IsItemIDValid( itemID ) )
			return false;

		InvalidateLayout();

		var item = _items[itemID];

		item.SetSectionID( sectionID );
		item.SetData( data );
		item.InvalidateLayout();

		_sortNeeded = true;

		return true;
	}

	public bool RemoveItem( int itemID )
	{
		if ( !IsItemIDValid( itemID ) )
			return false;

		var item = _items[itemID];

		_sortedItems.Remove( item );
		_sortNeeded = true;

		item.MarkForDeletion();

		_items[itemID] = null;
		_itemOrder.Remove( itemID );
		_freeIDs.Add( itemID );

		InvalidateLayout();

		return true;
	}

	public void DeleteAllItems()
	{
		foreach ( var itemID in _itemOrder )
		{
			var item = _items[itemID];

			item.SetVisible( false );
			item.Clear();

			_freeItems.Add( item );
		}

		_items.Clear();
		_itemOrder.Clear();
		_freeIDs.Clear();
		_sortedItems.Clear();
		_selectedItem = null;

		InvalidateLayout();

		_sortNeeded = true;
	}

	public void RemoveAll() => DeleteAllItems();

	public void SetItemFgColor( int itemID, Color32 color )
	{
		if ( !IsItemIDValid( itemID ) )
			return;

		var item = _items[itemID];

		item.SetFgColor( color );
		item.SetOverrideColors( true );
		item.InvalidateLayout();
	}

	public void InvalidateItem( int itemID )
	{
		if ( IsItemIDValid( itemID ) )
			_items[itemID].InvalidateLayout();
	}

	public void SetSelectedItem( int itemID )
	{
		if ( IsItemIDValid( itemID ) )
			SetSelectedItem( _items[itemID] );
	}

	public void SetSelectedItem( SectionedListItem item )
	{
		if ( _selectedItem == item )
			return;

		_selectedItem?.SetSelected( false );
		_selectedItem = item;
		_selectedItem?.SetSelected( true );

		PostActionSignal( new Message( "ItemSelected" ) { Int = _selectedItem?.ID ?? -1 } );
	}

	public bool GetCellBounds( int itemID, int column, out int x, out int y, out int wide, out int tall )
	{
		x = y = wide = tall = 0;

		if ( !IsItemIDValid( itemID ) || !_items[itemID].Visible )
			return false;

		var item = _items[itemID];

		y = item.Y;
		tall = item.Tall;

		item.GetCellBounds( column, out x, out wide );

		return true;
	}

	public void EnterEditMode( int itemID, int column, Panel editPanel )
	{
		_editModePanel = editPanel;
		_editModeItemID = itemID;

		editPanel.SetParent( this );
		editPanel.SetVisible( true );
		editPanel.RequestFocus();
		editPanel.MoveToFront();

		InvalidateLayout();
	}

	public void LeaveEditMode()
	{
		if ( _editModePanel is null )
			return;

		InvalidateItem( _editModeItemID );

		_editModePanel.SetVisible( false );
		_editModePanel.SetParent( null );
		_editModePanel = null;
	}

	public void ScrollToItem( int itemID )
	{
		var tall = GetSectionTall();
		var currentValue = _scrollBar.Value;
		var itemY = _items[itemID].Y + currentValue;

		if ( Tall >= _contentHeight )
			_scrollBar.SetValue( 0 );
		else if ( itemY < currentValue )
			_scrollBar.SetValue( itemY );
		else if ( itemY > currentValue + Tall - tall )
			_scrollBar.SetValue( itemY - Tall + tall );
	}

	private int GetSectionTall()
	{
		if ( _sections.Count != 0 && _sections[0].Header.Font is { } font )
			return Surface.GetFontTall( font ) + ButtonHeightSpacer;

		return ButtonHeightDefault;
	}

	private void ReSortList()
	{
		_sortedItems.Clear();

		foreach ( var section in _sections )
		{
			var sectionStart = _sortedItems.Count;

			foreach ( var itemID in _itemOrder )
			{
				var item = _items[itemID];

				if ( item.SectionID != section.ID )
					continue;

				var insertionPoint = _sortedItems.Count;

				if ( section.SortFunc is not null )
				{
					for ( insertionPoint = sectionStart; insertionPoint < _sortedItems.Count; insertionPoint++ )
					{
						if ( section.SortFunc( this, itemID, _sortedItems[insertionPoint].ID ) )
							break;
					}
				}

				_sortedItems.Insert( insertionPoint, item );
			}
		}
	}

	private void LayoutPanels( out int contentTall )
	{
		var tall = GetSectionTall();
		var wide = Wide - (Margin * 2);
		var y = Margin;

		if ( _scrollBar.Visible )
		{
			y -= _scrollBar.Value;
			wide -= _scrollBar.Wide;
		}

		foreach ( var section in _sections )
		{
			var start = -1;
			var end = -1;

			for ( var i = 0; i < _sortedItems.Count; i++ )
			{
				if ( _sortedItems[i].SectionID != section.ID )
					continue;

				if ( start == -1 )
					start = i;

				end = i;
			}

			if ( start == -1 && !section.AlwaysVisible )
			{
				section.Header.SetVisible( false );
				continue;
			}

			section.Header.SetBounds( Margin, y, wide, tall );
			section.Header.SetVisible( true );

			y += tall;

			for ( var i = start; start != -1 && i <= end; i++ )
			{
				var item = _sortedItems[i];

				item.SetBounds( Margin, y, wide, _lineSpacing );

				if ( _editModePanel is not null && _editModeItemID == item.ID )
				{
					item.GetCellBounds( 1, out var cellX, out var cellWide );

					_editModePanel.SetBounds( cellX, y, cellWide, tall );
				}

				y += _lineSpacing;
			}

			y += SectionGap;
		}

		contentTall = y;

		if ( _scrollBar.Visible )
			contentTall += _scrollBar.Value;
	}

	public override void PerformLayout()
	{
		if ( _sortNeeded )
		{
			ReSortList();

			_sortNeeded = false;
		}

		base.PerformLayout();

		LayoutPanels( out _contentHeight );

		if ( _contentHeight > Tall && _verticalScrollbarEnabled )
		{
			_scrollBar.SetVisible( true );
			_scrollBar.MoveToFront();
			_scrollBar.SetPos( Wide - _scrollBarSize - 2, 0 );
			_scrollBar.SetSize( _scrollBarSize, Tall - 2 );
			_scrollBar.SetRangeWindow( Tall );
			_scrollBar.SetRange( 0, _contentHeight );
			_scrollBar.InvalidateLayout();

			LayoutPanels( out _contentHeight );
			return;
		}

		_scrollBar.SetValue( 0 );
		_scrollBar.SetVisible( false );
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		base.OnSizeChanged( wide, tall );

		_scrollBar.SetValue( 0 );

		InvalidateLayout();
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBgColor( GetSchemeColor( "BuddyListBgColor", GetBgColor() ) );
		SetBorder( Vgui.Scheme.GetBorder( "ButtonDepressedBorder" ) );
	}

	public override void ApplySettings( KeyValues settings )
	{
		base.ApplySettings( settings );

		_lineSpacing = settings.GetInt( "linespacing" );

		if ( _lineSpacing == 0 )
			_lineSpacing = DefaultLineSpacing;

		if ( IsProportional )
			_lineSpacing = Scaled( _lineSpacing );
	}

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ScrollBarSliderMoved" )
		{
			InvalidateLayout();
			return;
		}

		base.OnMessage( message, from );
	}

	public override void OnSetFocus()
	{
		if ( _selectedItem is not null )
			_selectedItem.RequestFocus();
		else
			base.OnSetFocus();
	}

	public override void OnMousePressed( MouseCode code ) => SetSelectedItem( (SectionedListItem)null );

	public override void OnMouseWheeled( int delta )
	{
		if ( _editModePanel is not null )
		{
			CallParentFunction( new Message( "MouseWheeled" ) { Int = delta } );
			return;
		}

		_scrollBar.SetValue( _scrollBar.Value - (delta * MouseWheelScroll) );
	}

	private void MoveSelection( int rows )
	{
		if ( _sortedItems.Count == 0 || (_selectedItem is null && rows > 0) )
			return;

		var row = (_selectedItem is null ? _sortedItems.Count : _sortedItems.IndexOf( _selectedItem )) + rows;

		if ( row < 0 || row >= _sortedItems.Count )
			return;

		var itemID = _sortedItems[row].ID;

		SetSelectedItem( _sortedItems[row] );
		ScrollToItem( itemID );
	}

	private void MoveSelectionPage( int direction )
	{
		if ( _selectedItem is null )
			return;

		var rowsPerPage = Tall / GetSectionTall();
		var item = _selectedItem;
		var sectionID = item.SectionID;
		var row = _sortedItems.IndexOf( item );

		for ( var i = 0; i < rowsPerPage; )
		{
			row += direction;

			if ( row < 0 || row >= _sortedItems.Count )
			{
				if ( direction > 0 )
					break;

				SetSelectedItem( item );

				_scrollBar.SetValue( 0 );
				return;
			}

			item = _sortedItems[row];
			i++;

			if ( item.SectionID == sectionID )
				continue;

			sectionID = item.SectionID;
			i++;
		}

		SetSelectedItem( item );
		ScrollToItem( item.ID );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( _editModePanel is not null )
		{
			CallParentFunction( new Message( "KeyCodeTyped" ) { Int = (int)code } );
			return;
		}

		switch ( code )
		{
			case KeyCode.Down:
				MoveSelection( 1 );
				break;

			case KeyCode.Up:
				MoveSelection( -1 );
				break;

			case KeyCode.PageDown:
				MoveSelectionPage( 1 );
				break;

			case KeyCode.PageUp:
				MoveSelectionPage( -1 );
				break;

			case KeyCode.Left:
			case KeyCode.Right:
				break;

			default:
				base.OnKeyCodeTyped( code );
				break;
		}
	}
}
