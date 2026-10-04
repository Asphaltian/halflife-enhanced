using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public sealed class MessageWindowPanel : MenuPanel
{
	private readonly TransparentPanel _backgroundPanel;

	public static MenuPanel Create( Hud hud, string motd, string title, bool shadeFullscreen, int removeMe, int x, int y, int wide, int tall )
	{
		return new MessageWindowPanel( hud, motd, title, shadeFullscreen, removeMe, x, y, wide, tall );
	}

	public MessageWindowPanel( Hud hud, string motd, string title, bool shadeFullscreen, int removeMe, int x, int y, int wide, int tall ) : base( shadeFullscreen ? 100 : 255, removeMe, x, y, wide, tall )
	{
		var schemes = hud.ViewPort.GetSchemeManager();
		var titleScheme = schemes.GetSchemeHandle( "Title Font" );
		var motdText = schemes.GetSchemeHandle( "Briefing Text" );
		var titleY = VguiUtil.YRes( 16 );

		_backgroundPanel = new TransparentPanel( shadeFullscreen ? 255 : 100, (int)VguiUtil.XRes( 112 ), (int)VguiUtil.YRes( 80 ), (int)VguiUtil.XRes( 424 ), (int)VguiUtil.YRes( 312 ) );
		_backgroundPanel.SetParent( this );
		_backgroundPanel.SetBorder( new LineBorder( new Color( 178, 118, 0, 0 ) ) );
		_backgroundPanel.SetVisible( true );

		_backgroundPanel.GetPos( out var xPos, out var yPos );
		_backgroundPanel.GetSize( out var xSize, out var ySize );

		var label = new Label( "", (int)(xPos + VguiUtil.XRes( 16 )), (int)(yPos + titleY) );

		label.SetParent( this );
		label.SetFont( schemes.GetFont( titleScheme ) );
		label.SetFont( SchemeFont.Primary1 );

		schemes.GetFgColor( titleScheme, out var r, out var g, out var b, out var a );
		label.SetFgColor( r, g, b, a );
		label.SetFgColor( SchemeColor.Primary1 );

		schemes.GetBgColor( titleScheme, out r, out g, out b, out a );
		label.SetBgColor( r, g, b, a );

		label.SetContentAlignment( Label.Alignment.West );
		label.SetText( title );

		var scrollPanel = new TFScrollPanel( (int)(xPos + VguiUtil.XRes( 16 )), (int)(yPos + (titleY * 2) + VguiUtil.YRes( 16 )), (int)(xSize - VguiUtil.XRes( 32 )), (int)(ySize - (VguiUtil.YRes( 48 ) + (VguiUtil.ButtonSizeY * 2))) );

		scrollPanel.SetParent( this );
		scrollPanel.SetScrollBarAutoVisible( false, false );
		scrollPanel.SetScrollBarVisible( true, true );
		scrollPanel.Validate();

		var text = new TextPanel( "", 0, 0, 64, 64 );

		text.SetParent( scrollPanel.GetClient() );
		text.SetFont( schemes.GetFont( motdText ) );

		schemes.GetFgColor( motdText, out r, out g, out b, out a );
		text.SetFgColor( r, g, b, a );

		schemes.GetBgColor( motdText, out r, out g, out b, out a );
		text.SetBgColor( r, g, b, a );

		text.SetText( motd );

		text.GetTextImage().SetSize( scrollPanel.GetClientClip().GetWide(), scrollPanel.GetClientClip().GetTall() );
		text.GetTextImage().GetTextSizeWrapped( out var scrollSizeX, out var scrollSizeY );
		text.SetSize( scrollSizeX, scrollSizeY );

		scrollPanel.SetScrollBarAutoVisible( true, true );
		scrollPanel.SetScrollBarVisible( false, false );
		scrollPanel.Validate();

		var button = new CommandButton( hud.TextMessage.BufferedLocaliseTextString( "#Menu_OK" ), (int)(xPos + VguiUtil.XRes( 16 )), (int)(yPos + ySize - VguiUtil.YRes( 16 ) - VguiUtil.ButtonSizeY), (int)VguiUtil.CMenuSizeX, (int)VguiUtil.ButtonSizeY );

		button.AddActionSignal( new MenuHandlerTextWindow( MenuHandlerTextWindow.HideTextWindow ) );
		button.SetParent( this );
	}
}
