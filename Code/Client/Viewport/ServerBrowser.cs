using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

public sealed class ServerBrowserTablePanel : TablePanel
{
	private readonly Hud _hud;
	private readonly Label _label;
	private int _mouseOverRow;

	public ServerBrowserTablePanel( Hud hud, int x, int y, int wide, int tall, int columnCount ) : base( x, y, wide, tall, columnCount )
	{
		_hud = hud;
		_label = new Label( "", 0, 0 );
		_mouseOverRow = 0;
	}

	public void SetMouseOverRow( int row ) => _mouseOverRow = row;

	public void DoSort( string sortKey ) => _hud.Servers?.SortServers( sortKey );

	public void DoRefresh()
	{
		_hud.Servers?.RequestList();
		_hud.Servers?.RequestBroadcastList( 0 );
	}

	public void DoBroadcastRefresh() => _hud.Servers?.RequestBroadcastList( 1 );

	public void DoStop() => _hud.Servers?.CancelRequest();

	public void DoCancel() => Client.ClientCmd( "togglebrowser\n" );

	public void DoConnect()
	{
		var info = _hud.Servers?.GetServerInfo( _mouseOverRow );

		if ( info is null )
			return;

		Client.ClientCmd( $"connect {Info.ValueForKey( info, "address" )}\n" );

		DoCancel();
	}

	public void DoPing()
	{
		_hud.Servers?.ServerPing( 0 );
		_hud.Servers?.ServerRules( 0 );
		_hud.Servers?.ServerPlayers( 0 );
	}

	public override int GetRowCount()
	{
		GetSize( out _, out var height );

		height = (int)(height - VguiUtil.YRes( 20 ));
		height = Math.Max( 0, height );

		return (int)(height / VguiUtil.YRes( 15 ));
	}

	public override int GetCellTall( int row ) => (int)(VguiUtil.YRes( 15 ) - 2);

	public override Panel GetCellRenderer( int column, int row, bool columnSelected, bool rowSelected, bool cellSelected )
	{
		var info = _hud.Servers?.GetServerInfo( row );

		if ( row == _mouseOverRow )
			_label.SetFgColor( 200, 240, 63, 100 );
		else
			_label.SetFgColor( 255, 255, 255, 0 );

		_label.SetBgColor( 0, 0, 0, 200 );
		_label.SetContentAlignment( Label.Alignment.West );
		_label.SetFont( SchemeFont.Primary2 );

		if ( info is not null )
		{
			switch ( column )
			{
				case 0:
					SetCellText( Info.ValueForKey( info, "address" ) );
					break;

				case 1:
					SetCellText( Info.ValueForKey( info, "hostname" ) );
					break;

				case 2:
					SetCellText( Info.ValueForKey( info, "map" ) );
					break;

				case 3:
					var current = Info.ValueForKey( info, "current" );
					var max = Info.ValueForKey( info, "max" );

					if ( current is not null && max is not null )
						_label.SetText( VguiUtil.Format( VguiUtil.Truncate( $"{current}/{max}", 32 ) ) );

					break;

				case 4:
					SetCellText( Info.ValueForKey( info, "ping" ) );
					break;
			}
		}
		else
		{
			if ( row == 0 && column == 0 )
				_label.SetText( _hud.Servers is { } servers && servers.IsQuerying() ? "Waiting for servers to respond..." : "Press 'Refresh' to search for servers..." );
			else
				_label.SetText( "" );
		}

		return _label;
	}

	private void SetCellText( string value )
	{
		if ( value is not null )
			_label.SetText( VguiUtil.Format( VguiUtil.Truncate( value, 32 ) ) );
	}

	public override Panel StartCellEditing( int column, int row ) => null;
}

public sealed class BrowserInputSignal( ServerBrowserTablePanel browser ) : DefaultInputSignal
{
	private void SetRow( Panel panel )
	{
		panel.GetApp().GetCursorPos( out var x, out var y );
		panel.ScreenToLocal( ref x, ref y );

		browser.SetMouseOverRow( (int)(y / VguiUtil.YRes( 15 )) );
	}

	public override void MousePressed( MouseCode code, Panel panel )
	{
		if ( code != MouseCode.Left )
			return;

		SetRow( panel );
	}

	public override void MouseDoublePressed( MouseCode code, Panel panel )
	{
		if ( code != MouseCode.Left )
			return;

		SetRow( panel );

		browser.DoConnect();
	}
}

public sealed class BrowserActionHandler( Action action ) : ActionSignal
{
	public void ActionPerformed( Panel panel ) => action();
}

public sealed class LabelSortInputHandler( ServerBrowserTablePanel browser, string name ) : DefaultInputSignal
{
	public override void MousePressed( MouseCode code, Panel panel ) => browser.DoSort( name );

	public override void MouseDoublePressed( MouseCode code, Panel panel ) => browser.DoSort( name );
}

public sealed class SBLabel : Label
{
	private readonly string _sortKey;
	private ServerBrowserTablePanel _browser;

	public SBLabel( string name, string sortKey ) : base( name )
	{
		_browser = null;
		_sortKey = sortKey;

		SetContentAlignment( Alignment.West );
		SetFgColor( 255, 0, 0, 0 );
		SetBgColor( 120, 75, 32, 200 );
		SetFont( SchemeFont.Primary2 );
	}

	public void SetTable( ServerBrowserTablePanel browser )
	{
		_browser = browser;

		AddInputSignal( new LabelSortInputHandler( _browser, _sortKey ) );
	}
}

public sealed class ServerBrowser : TransparentPanel
{
	private const int NumColumns = 5;

	private readonly HeaderPanel _headerPanel;
	private readonly ServerBrowserTablePanel _tablePanel;
	private readonly CommandButton _connectButton;
	private readonly CommandButton _refreshButton;
	private readonly CommandButton _stopButton;
	private readonly CommandButton _cancelButton;

	private static float HeaderSizeY => VguiUtil.YRes( 18 );

	public ServerBrowser( Hud hud, int x, int y, int wide, int tall ) : base( 100, x, y, wide, tall )
	{
		_headerPanel = new HeaderPanel( 0, 0, wide, (int)HeaderSizeY );
		_headerPanel.SetParent( this );
		_headerPanel.SetFgColor( 100, 100, 100, 100 );
		_headerPanel.SetBgColor( 0, 0, 0, 100 );

		SBLabel[] labels =
		[
			new( "Address", "address" ),
			new( "Server", "hostname" ),
			new( "Map", "map" ),
			new( "Current", "current" ),
			new( "Latency", "ping" )
		];

		for ( var i = 0; i < 5; i++ )
			_headerPanel.AddSectionPanel( labels[i] );

		_headerPanel.SetSliderPos( 0, (int)VguiUtil.XRes( 200 ) );
		_headerPanel.SetSliderPos( 1, (int)VguiUtil.XRes( 400 ) );
		_headerPanel.SetSliderPos( 2, (int)VguiUtil.XRes( 500 ) );
		_headerPanel.SetSliderPos( 3, (int)VguiUtil.XRes( 570 ) );
		_headerPanel.SetSliderPos( 4, (int)VguiUtil.XRes( 640 ) );

		_tablePanel = new ServerBrowserTablePanel( hud, 0, (int)HeaderSizeY, wide, (int)(tall - HeaderSizeY), NumColumns );
		_tablePanel.SetParent( this );
		_tablePanel.SetHeaderPanel( _headerPanel );
		_tablePanel.SetFgColor( 100, 100, 100, 100 );
		_tablePanel.SetBgColor( 0, 0, 0, 100 );
		_tablePanel.AddInputSignal( new BrowserInputSignal( _tablePanel ) );

		for ( var i = 0; i < 5; i++ )
			labels[i].SetTable( _tablePanel );

		const int bw = 80;
		const int bh = 15;

		var by = (int)(tall - HeaderSizeY);
		var btnx = 10;

		_connectButton = new CommandButton( "Connect", btnx, by, bw, bh );
		_connectButton.SetParent( this );
		_connectButton.AddActionSignal( new BrowserActionHandler( _tablePanel.DoConnect ) );

		btnx += bw;

		_refreshButton = new CommandButton( "Refresh", btnx, by, bw, bh );
		_refreshButton.SetParent( this );
		_refreshButton.AddActionSignal( new BrowserActionHandler( _tablePanel.DoRefresh ) );

		btnx += bw;

		_stopButton = new CommandButton( "Stop", btnx, by, bw, bh );
		_stopButton.SetParent( this );
		_stopButton.AddActionSignal( new BrowserActionHandler( _tablePanel.DoStop ) );

		btnx += bw;

		_cancelButton = new CommandButton( "Close", btnx, by, bw, bh );
		_cancelButton.SetParent( this );
		_cancelButton.AddActionSignal( new BrowserActionHandler( _tablePanel.DoCancel ) );

		SetPaintBorderEnabled( false );
		SetPaintBackgroundEnabled( false );
		SetPaintEnabled( false );
	}

	public override void SetSize( int wide, int tall )
	{
		PanelSetSize( wide, tall );

		var buttonY = (int)(tall - HeaderSizeY);

		_headerPanel.SetBounds( 0, 0, wide, (int)HeaderSizeY );
		_tablePanel.SetBounds( 0, (int)HeaderSizeY, wide, (int)(tall - HeaderSizeY) );

		_connectButton.SetBounds( 5, buttonY, 75, 15 );
		_refreshButton.SetBounds( 85, buttonY, 75, 15 );
		_stopButton.SetBounds( 165, buttonY, 75, 15 );
		_cancelButton.SetBounds( 245, buttonY, 75, 15 );
	}
}
