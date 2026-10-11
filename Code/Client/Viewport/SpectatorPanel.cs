using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public sealed class SpectatorHandlerCommand( SpectatorPanel father, int cmd ) : ActionSignal
{
	public void ActionPerformed( Panel panel ) => father.ActionSignal( cmd );
}

public sealed class SpectatorPanel : Panel
{
	public const int CmdNone = 0;
	public const int CmdOptions = 1;
	public const int CmdPrevPlayer = 2;
	public const int CmdNextPlayer = 3;
	public const int CmdHideMenu = 4;
	public const int CmdToggleInset = 5;
	public const int CmdCamera = 6;
	public const int CmdPlayers = 7;

	public const int PanelHeight = 64;
	public const int BannerWidth = 256;
	public const int BannerHeight = 64;
	public const int OptionsButtonX = 96;
	public const int CamOptionsButtonX = 200;
	public const int SeperatorWidth = 15;
	public const int SeperatorHeight = 15;
	public const int TeamNumber = 2;

	private readonly Hud _hud;

	public DropDownButton OptionButton;
	public ImageButton PrevPlayerButton;
	public ImageButton NextPlayerButton;
	public DropDownButton CamButton;
	public TransparentPanel TopBorder;
	public TransparentPanel BottomBorder;
	public ColorButton InsetViewButton;
	public DropDownButton BottomMainButton;
	public ImageLabel TimerImage;
	public Label BottomMainLabel;
	public Label CurrentTime;
	public Label ExtraInfo;
	public Panel Separator;
	public readonly Label[] TeamScores = new Label[TeamNumber];
	public ImageLabel TopBanner;
	public bool MenuVisible;
	public bool InsetVisible;

	public SpectatorPanel( Hud hud, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_hud = hud;
	}

	public void ActionSignal( int cmd )
	{
		var viewPort = _hud.ViewPort;

		switch ( cmd )
		{
			case CmdNone:
				break;

			case CmdOptions:
				viewPort.ShowCommandMenu( viewPort.SpectatorOptionsMenu );
				break;

			case CmdNextPlayer:
				_hud.Spectator.FindNextPlayer( true );
				break;

			case CmdPrevPlayer:
				_hud.Spectator.FindNextPlayer( false );
				break;

			case CmdPlayers:
				viewPort.ShowCommandMenu( viewPort.PlayerMenu );
				break;

			case CmdHideMenu:
				ShowMenu( false );
				break;

			case CmdCamera:
				viewPort.ShowCommandMenu( viewPort.SpectatorCameraMenu );
				break;

			case CmdToggleInset:
				_hud.Spectator.SetModes( -1, _hud.Spectator.ToggleInset( false ) );
				break;

			default:
				GoldSrc.Con.DPrintf( $"Unknown SpectatorPanel ActionSingal {cmd}.\n" );
				break;
		}
	}

	private void SetButtonColors( ColorButton button )
	{
		button.SetUnArmedBorderColor( 59, 58, 34, 48 );
		button.SetArmedBorderColor( 194, 202, 54, 0 );
		button.SetUnArmedColor( 143, 143, 54, 0 );
		button.SetArmedColor( 194, 202, 54, 0 );
	}

	public void Initialize()
	{
		GetBounds( out _, out _, out var wide, out _ );

		var schemes = _hud.ViewPort.GetSchemeManager();
		var smallFont = schemes.GetFont( schemes.GetSchemeHandle( "Team Info Text" ) );
		var screenWidth = VguiUtil.ScreenWidth;
		var screenHeight = VguiUtil.ScreenHeight;
		var panelTall = (int)VguiUtil.YResHd( PanelHeight );
		var buttonY = (int)VguiUtil.YResHd( 6 );
		var buttonTall = (int)VguiUtil.YResHd( 20 );

		TopBorder = new TransparentPanel( 64, 0, 0, screenWidth, panelTall );
		TopBorder.SetParent( this );

		BottomBorder = new TransparentPanel( 64, 0, (int)(screenHeight - VguiUtil.YResHd( PanelHeight )), screenWidth, panelTall );
		BottomBorder.SetParent( this );

		SetPaintBackgroundEnabled( false );

		ExtraInfo = new Label( "Extra Info", 0, 0, wide, panelTall );
		ExtraInfo.SetParent( TopBorder );
		ExtraInfo.SetFont( smallFont );
		ExtraInfo.SetPaintBackgroundEnabled( false );
		ExtraInfo.SetFgColor( 143, 143, 54, 0 );
		ExtraInfo.SetContentAlignment( Label.Alignment.West );

		TimerImage = new ImageLabel( "timer", 0, 0, 14, 14 );
		TimerImage.SetParent( TopBorder );

		TopBanner = new ImageLabel( "banner", 0, 0, (int)VguiUtil.XResHd( BannerWidth ), (int)VguiUtil.YResHd( BannerHeight ) );
		TopBanner.SetParent( this );

		CurrentTime = new Label( "00:00", 0, 0, wide, panelTall );
		CurrentTime.SetParent( TopBorder );
		CurrentTime.SetFont( smallFont );
		CurrentTime.SetPaintBackgroundEnabled( false );
		CurrentTime.SetFgColor( 143, 143, 54, 0 );
		CurrentTime.SetContentAlignment( Label.Alignment.West );

		Separator = new Panel( 0, 0, (int)VguiUtil.XResHd( 64 ), (int)VguiUtil.YResHd( 96 ) );
		Separator.SetParent( TopBorder );
		Separator.SetFgColor( 59, 58, 34, 48 );
		Separator.SetBgColor( 59, 58, 34, 48 );

		for ( var j = 0; j < TeamNumber; j++ )
		{
			TeamScores[j] = new Label( "   ", 0, 0, wide, panelTall );
			TeamScores[j].SetParent( TopBorder );
			TeamScores[j].SetFont( smallFont );
			TeamScores[j].SetPaintBackgroundEnabled( false );
			TeamScores[j].SetFgColor( 143, 143, 54, 0 );
			TeamScores[j].SetContentAlignment( Label.Alignment.West );
			TeamScores[j].SetVisible( false );
		}

		OptionButton = new DropDownButton( _hud.TextMessage.BufferedLocaliseTextString( "#SPECT_OPTIONS" ), (int)VguiUtil.XResHd( 15 ), buttonY, (int)VguiUtil.XResHd( OptionsButtonX ), buttonTall, false, false );
		OptionButton.SetParent( BottomBorder );
		OptionButton.SetContentAlignment( Label.Alignment.Center );
		OptionButton.SetBoundKey( (char)255 );
		OptionButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdOptions ) );
		SetButtonColors( OptionButton );

		CamButton = new DropDownButton( _hud.TextMessage.BufferedLocaliseTextString( "#CAM_OPTIONS" ), (int)(screenWidth - VguiUtil.XResHd( CamOptionsButtonX + 15 )), buttonY, (int)VguiUtil.XResHd( CamOptionsButtonX ), buttonTall, false, false );
		CamButton.SetParent( BottomBorder );
		CamButton.SetContentAlignment( Label.Alignment.Center );
		CamButton.SetBoundKey( (char)255 );
		CamButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdCamera ) );
		SetButtonColors( CamButton );

		PrevPlayerButton = new ImageButton( "arrowleft", (int)VguiUtil.XResHd( 15 + OptionsButtonX + 15 ), buttonY, (int)VguiUtil.XResHd( 24 ), buttonTall, false, false );
		PrevPlayerButton.SetParent( BottomBorder );
		PrevPlayerButton.SetContentAlignment( Label.Alignment.Center );
		PrevPlayerButton.SetBoundKey( (char)255 );
		PrevPlayerButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdPrevPlayer ) );
		SetButtonColors( PrevPlayerButton );

		NextPlayerButton = new ImageButton( "arrowright", (int)(screenWidth - VguiUtil.XResHd( CamOptionsButtonX + 15 ) - VguiUtil.XResHd( 24 + 15 )), buttonY, (int)VguiUtil.XResHd( 24 ), buttonTall, false, false );
		NextPlayerButton.SetParent( BottomBorder );
		NextPlayerButton.SetContentAlignment( Label.Alignment.Center );
		NextPlayerButton.SetBoundKey( (char)255 );
		NextPlayerButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdNextPlayer ) );
		SetButtonColors( NextPlayerButton );

		var labelSize = screenWidth - VguiUtil.XResHd( CamOptionsButtonX + 15 ) - VguiUtil.XResHd( 24 + 15 ) - VguiUtil.XResHd( 15 + OptionsButtonX + 15 + 38 );
		var labelX = (int)VguiUtil.XResHd( 15 + OptionsButtonX + 15 + 31 );

		BottomMainButton = new DropDownButton( "Spectator Bottom", labelX, buttonY, (int)labelSize, buttonTall, false, false );
		BottomMainButton.SetParent( BottomBorder );
		BottomMainButton.SetPaintBackgroundEnabled( false );
		BottomMainButton.SetFgColor( SchemeColor.Primary1 );
		BottomMainButton.SetContentAlignment( Label.Alignment.Center );
		BottomMainButton.SetBorder( new LineBorder( new Color( 59, 58, 34, 48 ) ) );
		BottomMainButton.SetBoundKey( (char)255 );
		BottomMainButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdPlayers ) );
		SetButtonColors( BottomMainButton );

		BottomMainLabel = new Label( "Spectator Bottom", labelX, buttonY, (int)labelSize, buttonTall );
		BottomMainLabel.SetParent( BottomBorder );
		BottomMainLabel.SetPaintBackgroundEnabled( false );
		BottomMainLabel.SetFgColor( SchemeColor.Primary1 );
		BottomMainLabel.SetContentAlignment( Label.Alignment.Center );
		BottomMainLabel.SetBorder( null );
		BottomMainLabel.SetVisible( false );

		InsetViewButton = new ColorButton( "", (int)VguiUtil.XResHd( 2 ), (int)VguiUtil.YResHd( 2 ), (int)VguiUtil.XResHd( 240 ), (int)VguiUtil.YResHd( 180 ), false, false );
		InsetViewButton.SetParent( this );
		InsetViewButton.SetBoundKey( (char)255 );
		InsetViewButton.AddActionSignal( new SpectatorHandlerCommand( this, CmdToggleInset ) );
		SetButtonColors( InsetViewButton );

		MenuVisible = false;
		InsetVisible = false;

		CamButton.SetVisible( false );
		OptionButton.SetVisible( false );
		NextPlayerButton.SetVisible( false );
		PrevPlayerButton.SetVisible( false );
		TopBanner.SetVisible( false );
		ExtraInfo.SetVisible( false );
		Separator.SetVisible( false );
		TimerImage.SetVisible( false );
	}

	public void ShowMenu( bool isVisible )
	{
		OptionButton.SetVisible( isVisible );
		OptionButton.SetArmed( false );
		CamButton.SetVisible( isVisible );
		CamButton.SetArmed( false );
		NextPlayerButton.SetVisible( isVisible );
		NextPlayerButton.SetArmed( false );
		PrevPlayerButton.SetVisible( isVisible );
		PrevPlayerButton.SetArmed( false );

		if ( !isVisible )
		{
			BottomMainLabel.SetVisible( true );
			BottomMainButton.SetVisible( false );

			BottomMainLabel.GetSize( out var labelSizeX, out _ );
			BottomMainLabel.SetPos( (VguiUtil.ScreenWidth / 2) - (labelSizeX / 2), (int)VguiUtil.YResHd( 6 ) );
		}
		else
		{
			BottomMainButton.SetPos( (int)VguiUtil.XResHd( 15 + OptionsButtonX + 15 + 31 ), (int)VguiUtil.YResHd( 6 ) );
			BottomMainLabel.SetVisible( false );
			BottomMainButton.SetVisible( true );
		}

		if ( !isVisible )
		{
			_hud.ViewPort.HideCommandMenu();

			if ( MenuVisible && IsVisible() )
				_hud.TextMessage.TextMsg( HudTextMessage.HudPrintCenter, VguiUtil.Truncate( _hud.TextMessage.BufferedLocaliseTextString( "#Spec_Duck" ), 63 ) );
		}

		MenuVisible = isVisible;

		_hud.ViewPort.UpdateCursorState();
	}

	private static string GetSpectatorLabel( int mode )
	{
		return mode switch
		{
			HudSpectator.ObsChaseLocked => "#OBS_CHASE_LOCKED",
			HudSpectator.ObsChaseFree => "#OBS_CHASE_FREE",
			HudSpectator.ObsRoaming => "#OBS_ROAMING",
			HudSpectator.ObsInEye => "#OBS_IN_EYE",
			HudSpectator.ObsMapFree => "#OBS_MAP_FREE",
			HudSpectator.ObsMapChase => "#OBS_MAP_CHASE",
			_ => "#OBS_NONE"
		};
	}

	public void EnableInsetView( bool isEnabled )
	{
		var overview = _hud.Spectator.OverviewData;
		var x = overview.InsetWindowX;
		var y = overview.InsetWindowY;
		var wide = overview.InsetWindowWidth;
		var tall = overview.InsetWindowHeight;
		var offset = x + wide + 2;

		if ( isEnabled )
		{
			TopBorder.SetBounds( (int)VguiUtil.XResHd( offset ), 0, (int)(VguiUtil.ScreenWidth - VguiUtil.XResHd( offset )), (int)VguiUtil.YResHd( PanelHeight ) );

			if ( _hud.Engine.IsSpectateOnly )
			{
				TopBanner.SetVisible( true );
				TopBanner.SetPos( (int)VguiUtil.XResHd( offset ), 0 );
			}
			else
			{
				TopBanner.SetVisible( false );
			}

			InsetViewButton.SetBounds( (int)VguiUtil.XResHd( x - 1 ), (int)VguiUtil.YResHd( y ), (int)VguiUtil.XResHd( wide + 2 ), (int)VguiUtil.YResHd( tall ) );
			InsetViewButton.SetVisible( true );
		}
		else
		{
			if ( _hud.Engine.IsSpectateOnly )
			{
				TopBanner.SetVisible( true );
				TopBanner.SetPos( 0, 0 );
			}
			else
			{
				TopBanner.SetVisible( false );
			}

			TopBorder.SetBounds( 0, 0, VguiUtil.ScreenWidth, (int)VguiUtil.YResHd( PanelHeight ) );

			InsetViewButton.SetVisible( false );
		}

		InsetVisible = isEnabled;

		Update();

		CamButton.SetText( _hud.TextMessage.BufferedLocaliseTextString( GetSpectatorLabel( _hud.Dll.User1 ) ) );
	}

	public void Update()
	{
		var overview = _hud.Spectator.OverviewData;
		var offset = InsetVisible ? overview.InsetWindowX + overview.InsetWindowWidth + 2 : 0;
		var visible = HudSpectator.DrawStatus != 0f;

		ExtraInfo.SetVisible( visible );
		TimerImage.SetVisible( visible );
		CurrentTime.SetVisible( visible );
		Separator.SetVisible( visible );

		for ( var j = 0; j < TeamNumber; j++ )
			TeamScores[j].SetVisible( visible );

		if ( !visible )
			return;

		ExtraInfo.GetTextSize( out var textWidth, out var textHeight );
		CurrentTime.GetTextSize( out var timeWidth, out var timeHeight );

		var separatorWide = VguiUtil.XResHd( SeperatorWidth );
		var timerWide = VguiUtil.XResHd( (SeperatorWidth * 2) + 1 );

		timeWidth = (int)(timeWidth + timerWide);
		timeWidth = (int)(timeWidth + (separatorWide - (timeWidth % (int)separatorWide)));

		if ( timeWidth > textWidth )
			textWidth = timeWidth;

		var xPos = (int)(VguiUtil.ScreenWidth - (textWidth + VguiUtil.XResHd( SeperatorWidth + offset )));

		ExtraInfo.SetBounds( xPos, SeperatorHeight, textWidth, textHeight );

		TimerImage.SetBounds( xPos, SeperatorHeight + textHeight, (int)timerWide, SeperatorHeight + 1 );

		CurrentTime.SetBounds( (int)(xPos + timerWide), SeperatorHeight + textHeight, timeWidth, timeHeight );

		Separator.SetPos( (int)(VguiUtil.ScreenWidth - (textWidth + VguiUtil.XResHd( (2 * SeperatorWidth) + (SeperatorWidth / 2) + offset ))), (int)VguiUtil.YResHd( 5 ) );
		Separator.SetSize( (int)VguiUtil.XResHd( 1 ), (int)VguiUtil.YResHd( PanelHeight - 10 ) );

		for ( var j = 0; j < TeamNumber; j++ )
		{
			TeamScores[j].GetTextSize( out var iwidth, out var iheight );
			TeamScores[j].SetBounds( (int)(VguiUtil.ScreenWidth - (textWidth + VguiUtil.XResHd( (2 * SeperatorWidth) + (2 * SeperatorWidth / 2) + offset ) + iwidth)), SeperatorHeight + (iheight * j), iwidth, iheight );
		}
	}
}
