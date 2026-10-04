using GoldSrc.Vgui1;

namespace HalfLife;

public class ListBox : Panel
{
	private sealed class ListBoxSignal( ListBox listBox ) : IntChangeSignal
	{
		public void IntChanged( int value, Panel panel ) => listBox.SetPixelScroll( -value );
	}

	protected readonly List<Panel> _items = [];
	protected readonly Panel _itemsPanel = new( 0, 0, 0, 0 );
	protected int _itemOffset;
	protected readonly Slider2 _slider = new( 0, 0, 10, 40, true );
	protected readonly ScrollBar2 _scrollBar = new( 0, 0, 0, 0, true );
	private readonly ListBoxSignal _signal;
	protected int _scrollMax;

	public ListBox() : base( 0, 0, 0, 0 )
	{
		_signal = new ListBoxSignal( this );

		_itemsPanel.SetParent( this );
		_itemsPanel.SetBgColor( 0, 0, 0, 255 );

		_slider.SetRangeWindow( 50 );
		_slider.SetRangeWindowEnabled( true );

		_scrollBar.SetParent( this );
		_scrollBar.AddIntChangeSignal( _signal );
		_scrollBar.SetSlider( _slider );
		_scrollBar.SetButtonPressedScrollValue( 1 );

		_itemOffset = 0;
		_scrollMax = -1;
	}

	public void Init()
	{
		Term();
	}

	public void Term()
	{
		_itemOffset = 0;

		foreach ( var item in _items )
			item.SetParent( null );

		_items.Clear();
	}

	public void AddItem( Panel panel )
	{
		panel.SetParent( _itemsPanel );

		_items.Add( panel );

		_scrollBar.SetRange( 0, GetScrollMax() );
		_slider.SetRangeWindow( 50 );
		_slider.SetRangeWindowEnabled( true );

		InternalLayout();
	}

	public int GetNumItems() => _items.Count;

	public int GetItemWidth()
	{
		_itemsPanel.GetSize( out var wide, out _ );

		return wide;
	}

	public int GetScrollPos() => _itemOffset;

	public void SetScrollPos( int pos )
	{
		var maxItems = GetScrollMax();

		if ( maxItems < 0 )
			return;

		_itemOffset = pos < 0 ? 0 : pos > maxItems ? maxItems : pos;

		InternalLayout();
	}

	public override void SetPos( int x, int y )
	{
		base.SetPos( x, y );

		InternalLayout();
	}

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		InternalLayout();
	}

	public virtual void SetPixelScroll( int value )
	{
		_itemOffset = _scrollBar.GetValue();

		InternalLayout();
	}

	protected void InternalLayout()
	{
		GetBounds( out _, out _, out var wide, out var tall );

		_itemsPanel.SetBounds( 0, 0, wide - 15, tall );
		_scrollBar.SetBounds( wide - 15, 0, 15, tall );

		var needScrollbar = false;

		var curItem = 0;
		var curY = 0;
		var maxItem = GetScrollMax();

		foreach ( var item in _items )
		{
			if ( curItem < _itemOffset )
			{
				item.SetVisible( false );
				needScrollbar = true;
			}
			else if ( curItem >= maxItem )
			{
				item.SetVisible( false );
			}
			else
			{
				item.SetVisible( true );

				item.GetSize( out _, out var itemHeight );

				item.SetBounds( 0, curY, wide, itemHeight );

				curY += itemHeight;

				if ( curY > tall )
					needScrollbar = true;
			}

			++curItem;
		}

		_scrollBar.SetVisible( needScrollbar );

		Repaint();
	}

	public override void PaintBackground()
	{
	}

	public void SetScrollRange( int maxScroll )
	{
		_scrollMax = maxScroll;
		_scrollBar.SetRange( 0, maxScroll );

		InternalLayout();
	}

	public int GetScrollMax()
	{
		if ( _scrollMax < 0 )
			return GetNumItems() - 1;

		return _scrollMax;
	}
}
