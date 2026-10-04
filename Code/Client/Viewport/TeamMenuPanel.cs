using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public sealed class TeamMenuPanel : MenuPanel
{
	private const int PlayerListSize = (Hud.MaxPlayerNameLength + 3) * 31;

	private readonly Hud _hud;

	public ScrollPanel ScrollPanel;
	public TransparentPanel TeamWindow;
	public Label MapTitle;
	public TextPanel Briefing;
	public readonly TextPanel[] TeamInfoPanel = new TextPanel[6];
	public readonly CommandButton[] Buttons = new CommandButton[6];
	public bool UpdatedMapName;
	public CommandButton CancelButton;
	public CommandButton SpectateButton;
	public int CurrentInfo;

	private static float TopLeftButtonX => VguiUtil.XRes( 40 );

	private static float TopLeftButtonY => VguiUtil.YRes( 80 );

	private static float ButtonSizeX => VguiUtil.XRes( 124 );

	private static float ButtonSizeY => VguiUtil.YRes( 24 );

	private static float ButtonSpacerY => VguiUtil.YRes( 8 );

	public TeamMenuPanel( Hud hud, int trans, int removeMe, int x, int y, int wide, int tall ) : base( trans, removeMe, x, y, wide, tall )
	{
		_hud = hud;

		var schemes = hud.ViewPort.GetSchemeManager();
		var titleScheme = schemes.GetSchemeHandle( "Title Font" );
		var teamWindowText = schemes.GetSchemeHandle( "Briefing Text" );
		var teamInfoText = schemes.GetSchemeHandle( "Team Info Text" );
		var titleFont = schemes.GetFont( titleScheme );

		var windowSizeX = VguiUtil.XRes( 424 );
		var windowTextX = VguiUtil.XRes( 16 );
		var windowTextSizeY = VguiUtil.YRes( 178 );
		var windowInfoX = VguiUtil.XRes( 16 );
		var windowInfoY = VguiUtil.YRes( 234 );

		var label = new Label( "", (int)VguiUtil.XRes( 40 ), (int)VguiUtil.YRes( 32 ) );

		label.SetParent( this );
		label.SetFont( titleFont );

		schemes.GetFgColor( titleScheme, out var r, out var g, out var b, out var a );
		label.SetFgColor( r, g, b, a );

		schemes.GetBgColor( titleScheme, out r, out g, out b, out a );
		label.SetBgColor( r, g, b, a );

		label.SetContentAlignment( Label.Alignment.West );
		label.SetText( hud.TextMessage.BufferedLocaliseTextString( "#Title_SelectYourTeam" ) );

		TeamWindow = new TransparentPanel( 255, (int)VguiUtil.XRes( 176 ), (int)VguiUtil.YRes( 80 ), (int)windowSizeX, (int)VguiUtil.YRes( 312 ) );
		TeamWindow.SetParent( this );
		TeamWindow.SetBorder( new LineBorder( new Color( 178, 118, 0, 0 ) ) );

		MapTitle = new Label( "", (int)VguiUtil.XRes( 16 ), (int)VguiUtil.YRes( 16 ) );
		MapTitle.SetFont( titleFont );
		MapTitle.SetParent( TeamWindow );

		schemes.GetFgColor( titleScheme, out r, out g, out b, out a );
		MapTitle.SetFgColor( r, g, b, a );

		schemes.GetBgColor( titleScheme, out r, out g, out b, out a );
		MapTitle.SetBgColor( r, g, b, a );

		MapTitle.SetContentAlignment( Label.Alignment.West );

		ScrollPanel = new TFScrollPanel( (int)windowTextX, (int)VguiUtil.YRes( 48 ), (int)(windowSizeX - (windowTextX * 2)), (int)windowTextSizeY );
		ScrollPanel.SetParent( TeamWindow );
		ScrollPanel.SetScrollBarVisible( false, false );

		Briefing = new TextPanel( "", 0, 0, (int)(windowSizeX - windowTextX), (int)windowTextSizeY );
		Briefing.SetParent( ScrollPanel.GetClient() );
		Briefing.SetFont( schemes.GetFont( teamWindowText ) );

		schemes.GetFgColor( teamWindowText, out r, out g, out b, out a );
		Briefing.SetFgColor( r, g, b, a );

		schemes.GetBgColor( teamWindowText, out r, out g, out b, out a );
		Briefing.SetBgColor( r, g, b, a );

		Briefing.SetText( hud.TextMessage.BufferedLocaliseTextString( "#Map_Description_not_available" ) );

		for ( var i = 1; i <= 5; i++ )
		{
			var yPos = (int)(TopLeftButtonY + ((ButtonSizeY + ButtonSpacerY) * i));

			Buttons[i] = new CommandButton( "", (int)TopLeftButtonX, yPos, (int)ButtonSizeX, (int)ButtonSizeY, true );
			Buttons[i].SetParent( this );
			Buttons[i].SetContentAlignment( Label.Alignment.West );
			Buttons[i].SetVisible( false );

			if ( i == 5 )
			{
				Buttons[5].SetBoundKey( '5' );
				Buttons[5].SetText( hud.TextMessage.BufferedLocaliseTextString( "#Team_AutoAssign" ) );
				Buttons[5].SetVisible( true );
			}

			Buttons[i].AddActionSignal( new MenuHandlerStringCommandWatch( $"jointeam {i}", 1 ) );
			Buttons[i].AddInputSignal( new HandlerMenuButtonOver( this, i ) );

			var color = i % VguiUtil.NumberOfTeamColors;

			TeamInfoPanel[i] = new TextPanel( "", (int)windowInfoX, (int)windowInfoY, (int)(windowSizeX - windowInfoX), (int)(windowSizeX - windowInfoY) );
			TeamInfoPanel[i].SetParent( TeamWindow );
			TeamInfoPanel[i].SetFont( schemes.GetFont( teamInfoText ) );
			TeamInfoPanel[i].SetFgColor( VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 0 );
			TeamInfoPanel[i].SetBgColor( 0, 0, 0, 255 );
		}

		CancelButton = new CommandButton( hud.TextMessage.BufferedLocaliseTextString( "#Menu_Cancel" ), (int)TopLeftButtonX, 0, (int)ButtonSizeX, (int)ButtonSizeY );
		CancelButton.SetParent( this );
		CancelButton.AddActionSignal( new MenuHandlerTextWindow( MenuHandlerTextWindow.HideTextWindow ) );

		SpectateButton = new SpectateButton( hud.TextMessage.BufferedLocaliseTextString( "#Menu_Spectate" ), (int)TopLeftButtonX, 0, (int)ButtonSizeX, (int)ButtonSizeY, true );
		SpectateButton.SetParent( this );
		SpectateButton.AddActionSignal( new MenuHandlerStringCommand( "spectate", 1 ) );
		SpectateButton.SetBoundKey( '6' );
		SpectateButton.AddInputSignal( new HandlerMenuButtonOver( this, 6 ) );

		Initialize();
	}

	public void Initialize()
	{
		UpdatedMapName = false;
		CurrentInfo = 0;

		ScrollPanel.SetScrollValue( 0, 0 );
	}

	public override void Reset()
	{
		base.Reset();

		CurrentInfo = 0;
	}

	private static string Append( string text, string source, int size )
	{
		var count = size - text.Length;

		if ( count > 0 )
			text += source.Length > count ? source[..count] : source;

		return text;
	}

	public void Update()
	{
		var viewPort = _hud.ViewPort;
		var yPos = (int)TopLeftButtonY;

		for ( var i = 1; i <= 4; i++ )
		{
			if ( Buttons[i] is null )
				continue;

			if ( i > viewPort.GetNumberOfTeams() )
			{
				Buttons[i].SetVisible( false );
				continue;
			}

			Buttons[i].SetText( viewPort.GetTeamName( i ) );
			Buttons[i].SetBoundKey( (char)('0' + i) );
			Buttons[i].SetVisible( true );
			Buttons[i].SetPos( (int)TopLeftButtonX, yPos );

			yPos = (int)(yPos + ButtonSizeY + ButtonSpacerY);

			if ( CurrentInfo == 0 )
				SetActiveInfo( i );

			var playerList = "\n";
			var total = 0;

			for ( var j = 1; j < Hud.MaxPlayers; j++ )
			{
				var player = _hud.PlayerInfoList[j];

				if ( player.Name is null )
					continue;

				if ( player.ThisPlayer )
					continue;

				if ( _hud.GetExtraPlayerInfo( j ).TeamNumber != i )
					continue;

				total++;

				if ( total > 1 )
					playerList = Append( playerList, ", ", PlayerListSize );

				playerList = VguiUtil.Truncate( Append( playerList, player.Name, PlayerListSize ), PlayerListSize );
			}

			if ( total > 0 )
			{
				var text = $"{viewPort.GetTeamName( i )}: {total} {(total == 1 ? "Player" : "Players")} ({_hud.TeamInfo[i].Frags} points)";

				TeamInfoPanel[i].SetText( VguiUtil.Truncate( Append( text, playerList, PlayerListSize + 256 ), PlayerListSize + 256 ) );
			}
			else
			{
				TeamInfoPanel[i].SetText( "" );
			}
		}

		Buttons[5].SetPos( (int)TopLeftButtonX, yPos );

		yPos = (int)(yPos + ButtonSizeY + ButtonSpacerY);

		if ( SpectateButton.IsNotValid() )
		{
			SpectateButton.SetVisible( false );
		}
		else
		{
			SpectateButton.SetPos( (int)TopLeftButtonX, yPos );
			SpectateButton.SetVisible( true );

			yPos = (int)(yPos + ButtonSizeY + ButtonSpacerY);
		}

		if ( _hud.Dll.TeamNumber != 0 )
		{
			CancelButton.SetPos( (int)TopLeftButtonX, yPos );
			CancelButton.SetVisible( true );
		}
		else
		{
			CancelButton.SetVisible( false );
		}

		if ( !UpdatedMapName )
		{
			var level = _hud.Engine.LevelName;

			if ( !string.IsNullOrEmpty( level ) )
			{
				var slash = level.IndexOf( '/' );

				if ( slash < 0 )
					slash = level.IndexOf( '\\' );

				var title = level[(slash + 1)..];

				MapTitle.SetText( title[..title.IndexOf( '.' )] );

				if ( Server.LoadMountText( $"{level[..level.IndexOf( '.' )]}.txt" ) is { } file )
				{
					Briefing.SetText( file );
					Briefing.GetTextImage().GetTextSize( out var xSize, out var ySize );
					Briefing.SetSize( xSize, ySize );
				}

				UpdatedMapName = true;
			}
		}

		ScrollPanel.Validate();
	}

	public override bool SlotInput( int slot )
	{
		if ( slot == 5 )
		{
			Buttons[5].FireActionSignal();

			return true;
		}

		if ( slot == 6 )
		{
			SpectateButton.FireActionSignal();

			return true;
		}

		if ( slot < 1 || slot > _hud.ViewPort.GetNumberOfTeams() )
			return false;

		if ( Buttons[slot] is null )
			return false;

		if ( Buttons[slot].IsVisible() )
		{
			Buttons[slot].FireActionSignal();

			return true;
		}

		return false;
	}

	public override void Open()
	{
		Update();

		base.Open();
	}

	public override void PaintBackground()
	{
		if ( !UpdatedMapName )
			Update();

		base.PaintBackground();
	}

	public override void SetActiveInfo( int input )
	{
		SpectateButton.SetArmed( false );

		for ( var i = 1; i <= 5; i++ )
		{
			Buttons[i].SetArmed( false );
			TeamInfoPanel[i].SetVisible( false );
		}

		if ( input == 6 )
		{
			SpectateButton.SetArmed( true );
		}
		else
		{
			Buttons[input].SetArmed( true );
			TeamInfoPanel[input].SetVisible( true );
		}

		CurrentInfo = input;

		ScrollPanel.Validate();
	}
}
