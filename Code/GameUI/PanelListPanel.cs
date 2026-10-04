namespace GoldSrc.GameUI;

public sealed class PanelListPanel : Panel
{
	private const int TableStartY = 0;

	private readonly List<Panel> _dataItems = [];
	private readonly ScrollBar _vbar;
	private readonly Panel _embedded;

	public PanelListPanel( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		SetBounds( 0, 0, Scaled( 100 ), Scaled( 100 ) );

		_vbar = new ScrollBar( vgui, this, "CPanelListPanelVScroll", true );
		_vbar.SetBounds( 0, 0, Scaled( 20 ), Scaled( 20 ) );
		_vbar.SetVisible( false );
		_vbar.AddActionSignalTarget( this );

		_embedded = new Panel( vgui, this, "PanelListEmbedded" );
		_embedded.SetBounds( 0, 0, Scaled( 20 ), Scaled( 20 ) );
		_embedded.SetPaintBackgroundEnabled( false );
		_embedded.SetPaintBorderEnabled( false );
	}

	public int ItemCount => _dataItems.Count;

	public Panel GetItem( int index ) => index >= 0 && index < _dataItems.Count ? _dataItems[index] : null;

	private int ComputeVPixelsNeeded()
	{
		var pixels = 0;

		foreach ( var item in _dataItems )
			pixels += item.Tall;

		return pixels + Scaled( 5 );
	}

	public void AddItem( Panel panel )
	{
		InvalidateLayout();

		panel.SetParent( _embedded );

		if ( !_dataItems.Contains( panel ) )
			_dataItems.Add( panel );
	}

	public void RemoveItem( int index )
	{
		if ( GetItem( index ) is not { } item )
			return;

		item.MarkForDeletion();

		_dataItems.RemoveAt( index );

		InvalidateLayout();
	}

	public void DeleteAllItems()
	{
		foreach ( var item in _dataItems )
			item.MarkForDeletion();

		_dataItems.Clear();

		InvalidateLayout();
	}

	public override void OnMouseWheeled( int delta ) => _vbar.SetValue( _vbar.Value - (Scaled( 15 ) * delta) );

	public override void PerformLayout()
	{
		var wide = Wide;
		var tall = Tall;
		var pixels = ComputeVPixelsNeeded();

		_vbar.SetVisible( true );
		_vbar.SetEnabled( false );
		_vbar.SetRange( 0, pixels - tall + Scaled( 24 ) );
		_vbar.SetRangeWindow( Scaled( 24 ) );
		_vbar.SetButtonPressedScrollValue( Scaled( 24 ) );
		_vbar.SetPos( wide - Scaled( 20 ), TableStartY );
		_vbar.SetSize( Scaled( 18 ), tall - TableStartY - Scaled( 2 ) );
		_vbar.InvalidateLayout();

		_embedded.SetPos( 0, -_vbar.Value );
		_embedded.SetSize( wide - Scaled( 20 ), pixels );

		var y = 0;

		foreach ( var item in _dataItems )
		{
			var height = item.Tall;

			item.SetBounds( Scaled( 8 ), y, wide - Scaled( 36 ), height );

			y += height;
		}
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		SetBorder( Vgui.Scheme.GetBorder( "ButtonDepressedBorder" ) );
		SetBgColor( GetSchemeColor( "LabelBgColor", GetSchemeColor( "WindowDisabledBgColor" ) ) );
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
}
