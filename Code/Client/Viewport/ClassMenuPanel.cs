using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public sealed class ClassMenuPanel : MenuPanel
{
	private const int MaxTeams = 64;
	private const int PlayersOnTeamStringLength = 128;

	private readonly Hud _hud;
	private readonly TransparentPanel[] _classInfoPanel = new TransparentPanel[TeamFortress.PcLastClass];
	private readonly Label[] _players = new Label[TeamFortress.PcLastClass];
	private readonly ClassButton[] _buttons = new ClassButton[TeamFortress.PcLastClass];
	private readonly CommandButton _cancelButton;
	private readonly ScrollPanel _scrollPanel;
	private readonly ImageLabel[,] _classImages = new ImageLabel[MaxTeams, TeamFortress.PcLastClass];
	private int _currentInfo;
	private string _playersOnTeamString = "";

	private static float TopLeftButtonX => VguiUtil.XRes( 40 );

	private static float TopLeftButtonY => VguiUtil.YRes( 80 );

	private static float ButtonSizeX => VguiUtil.XRes( 124 );

	private static float ButtonSizeY => VguiUtil.YRes( 24 );

	private static float ButtonSpacerY => VguiUtil.YRes( 8 );

	public ClassMenuPanel( Hud hud, int trans, int removeMe, int x, int y, int wide, int tall ) : base( trans, removeMe, x, y, wide, tall )
	{
		_hud = hud;

		var showClassGraphic = VguiUtil.ScreenWidth >= 640;
		var schemes = hud.ViewPort.GetSchemeManager();
		var titleScheme = schemes.GetSchemeHandle( "Title Font" );
		var classWindowText = schemes.GetSchemeHandle( "Briefing Text" );

		var windowSizeX = VguiUtil.XRes( 424 );
		var windowSizeY = VguiUtil.YRes( 312 );
		var windowTextX = VguiUtil.XRes( 150 );
		var windowTextY = VguiUtil.YRes( 80 );

		var label = new Label( "", (int)VguiUtil.XRes( 40 ), (int)VguiUtil.YRes( 32 ) );

		label.SetParent( this );
		label.SetFont( schemes.GetFont( titleScheme ) );

		schemes.GetFgColor( titleScheme, out var r, out var g, out var b, out var a );
		label.SetFgColor( r, g, b, a );

		schemes.GetBgColor( titleScheme, out r, out g, out b, out a );
		label.SetBgColor( r, g, b, a );

		label.SetContentAlignment( Label.Alignment.West );
		label.SetText( hud.TextMessage.BufferedLocaliseTextString( "#Title_SelectYourClass" ) );

		_scrollPanel = new TFScrollPanel( (int)VguiUtil.XRes( 176 ), (int)VguiUtil.YRes( 80 ), (int)windowSizeX, (int)windowSizeY );
		_scrollPanel.SetParent( this );
		_scrollPanel.SetScrollBarAutoVisible( false, false );
		_scrollPanel.SetScrollBarVisible( true, true );
		_scrollPanel.SetBorder( new LineBorder( new Color( 178, 118, 0, 0 ) ) );
		_scrollPanel.Validate();

		var clientWide = _scrollPanel.GetClient().GetWide();

		_scrollPanel.SetScrollBarAutoVisible( false, true );
		_scrollPanel.SetScrollBarVisible( false, false );
		_scrollPanel.Validate();

		for ( var i = 0; i <= TeamFortress.PcRandom; i++ )
		{
			var yPos = (int)(TopLeftButtonY + ((ButtonSizeY + ButtonSpacerY) * i));
			var signal = new MenuHandlerStringCommandClassSelect( TeamFortress.ClassSelection[i], 1 );

			_buttons[i] = new ClassButton( i, hud.TextMessage.BufferedLocaliseTextString( TeamFortress.LocalisedClasses[i] ), (int)TopLeftButtonX, yPos, (int)ButtonSizeX, (int)ButtonSizeY, true );
			_buttons[i].SetBoundKey( i >= 1 && i <= 9 ? (char)('0' + i) : '0' );
			_buttons[i].SetContentAlignment( Label.Alignment.West );
			_buttons[i].AddActionSignal( signal );
			_buttons[i].AddInputSignal( new HandlerMenuButtonOver( this, i ) );
			_buttons[i].SetParent( this );

			_classInfoPanel[i] = new TransparentPanel( 255, 0, 0, clientWide, (int)windowSizeY );
			_classInfoPanel[i].SetParent( _scrollPanel.GetClient() );

			var textOffs = (int)VguiUtil.XRes( 8 );

			if ( showClassGraphic )
				textOffs = (int)VguiUtil.XRes( 150 );

			var localName = hud.TextMessage.BufferedLocaliseTextString( $"#Title_{TeamFortress.ClassSelection[i]}" );
			var nameLabel = new Label( "", textOffs, (int)VguiUtil.YRes( 8 ) );

			nameLabel.SetFont( schemes.GetFont( titleScheme ) );
			nameLabel.SetParent( _classInfoPanel[i] );

			schemes.GetFgColor( titleScheme, out r, out g, out b, out a );
			nameLabel.SetFgColor( r, g, b, a );

			schemes.GetBgColor( titleScheme, out r, out g, out b, out a );
			nameLabel.SetBgColor( r, g, b, a );

			nameLabel.SetContentAlignment( Label.Alignment.West );
			nameLabel.SetText( localName );

			if ( showClassGraphic )
			{
				for ( var team = 0; team < 2; team++ )
				{
					var imageName = team == 1 ? $"{TeamFortress.ClassSelection[i]}red" : $"{TeamFortress.ClassSelection[i]}blue";
					var image = new ImageLabel( imageName, 0, 0, (int)windowTextX, (int)windowTextY );

					_classImages[team, i] = image;

					image.SetParent( _classInfoPanel[i] );

					if ( team != 1 )
						image.SetVisible( false );

					nameLabel.GetTextSize( out _, out var yOut );

					image.SetPos( (int)((windowTextX - image.GetWide()) / 2), yOut / 2 );
				}
			}

			_playersOnTeamString = hud.TextMessage.LocaliseTextString( "#Title_CurrentlyOnYourTeam", PlayersOnTeamStringLength );

			_players[i] = new Label( "", textOffs, (int)VguiUtil.YRes( 42 ) );
			_players[i].SetParent( _classInfoPanel[i] );
			_players[i].SetBgColor( 0, 0, 0, 255 );
			_players[i].SetContentAlignment( Label.Alignment.West );
			_players[i].SetFont( schemes.GetFont( classWindowText ) );

			var text = Server.LoadMountText( $"classes/short_{TeamFortress.ClassSelection[i]}.txt" ) ?? "Class Description not available.";
			var textWindow = new TextPanel( text, textOffs, (int)windowTextY, (int)(windowSizeX - textOffs - 5), (int)(windowSizeY - windowTextY) );

			textWindow.SetParent( _classInfoPanel[i] );
			textWindow.SetFont( schemes.GetFont( classWindowText ) );

			schemes.GetFgColor( classWindowText, out r, out g, out b, out a );
			textWindow.SetFgColor( r, g, b, a );

			schemes.GetBgColor( classWindowText, out r, out g, out b, out a );
			textWindow.SetBgColor( r, g, b, a );

			textWindow.GetTextImage().GetTextSizeWrapped( out var textWide, out var textTall );
			textWindow.SetSize( textWide, textTall );
			textWindow.GetPos( out var xx, out var yy );

			var maxX = xx + textWide;
			var maxY = yy + textTall;

			if ( _classImages[0, i] is { } first )
			{
				first.GetPos( out _, out yy );

				if ( yy + first.GetTall() > maxY )
					maxY = yy + first.GetTall();
			}

			_classInfoPanel[i].SetSize( maxX, maxY );
		}

		_cancelButton = new CommandButton( hud.TextMessage.BufferedLocaliseTextString( "#Menu_Cancel" ), (int)TopLeftButtonX, 0, (int)ButtonSizeX, (int)ButtonSizeY );
		_cancelButton.SetParent( this );
		_cancelButton.AddActionSignal( new MenuHandlerTextWindow( MenuHandlerTextWindow.HideTextWindow ) );

		_currentInfo = 0;
	}

	public override void Reset()
	{
		base.Reset();

		_currentInfo = 0;
	}

	public void Update()
	{
		var dll = _hud.Dll;

		if ( dll.TeamNumber == 0 )
			return;

		var yPos = (int)TopLeftButtonY;

		for ( var i = 0; i <= TeamFortress.PcRandom; i++ )
		{
			var civilian = _hud.ViewPort.GetValidClasses( dll.TeamNumber ) == -1;

			if ( civilian )
			{
				if ( i == 0 )
				{
					_buttons[0].SetVisible( true );

					SetActiveInfo( 0 );

					yPos = (int)(yPos + ButtonSizeY + ButtonSpacerY);
				}
				else
				{
					_buttons[i].SetVisible( false );
				}
			}
			else
			{
				if ( _buttons[i].IsNotValid() || i == 0 )
				{
					_buttons[i].SetVisible( false );
				}
				else
				{
					_buttons[i].SetVisible( true );
					_buttons[i].SetPos( (int)TopLeftButtonX, yPos );

					yPos = (int)(yPos + ButtonSizeY + ButtonSpacerY);

					if ( _currentInfo == 0 )
						SetActiveInfo( i );
				}
			}

			var total = 0;

			for ( var j = 1; j < Hud.MaxPlayers; j++ )
			{
				var player = _hud.PlayerInfoList[j];
				var extra = _hud.GetExtraPlayerInfo( j );

				if ( player.Name is null )
					continue;

				if ( extra.TeamName.Length == 0 )
					continue;

				if ( player.ThisPlayer )
					continue;

				if ( extra.TeamNumber != dll.TeamNumber )
					continue;

				if ( extra.PlayerClass != i && !civilian )
					continue;

				total++;
			}

			_players[i].SetText( VguiUtil.Format( _playersOnTeamString, total ) );

			var color = dll.TeamNumber % VguiUtil.NumberOfTeamColors;

			_players[i].SetFgColor( VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 0 );

			for ( var team = 0; team < MaxTeams; team++ )
			{
				_classImages[team, i]?.SetVisible( false );

				if ( _classImages[dll.TeamNumber - 1, i] is { } current )
					current.SetVisible( true );
				else
					_classImages[0, i]?.SetVisible( true );
			}
		}

		if ( dll.PlayerClass != 0 )
		{
			_cancelButton.SetPos( (int)TopLeftButtonX, yPos );
			_cancelButton.SetVisible( true );
		}
		else
		{
			_cancelButton.SetVisible( false );
		}
	}

	public override bool SlotInput( int slot )
	{
		if ( slot < 0 || slot > 9 )
			return false;

		if ( _buttons[slot] is null )
			return false;

		if ( slot == 0 )
		{
			if ( _hud.ViewPort.GetValidClasses( _hud.Dll.TeamNumber ) == -1 )
			{
				_buttons[0].FireActionSignal();

				return true;
			}

			slot = 10;
		}

		if ( !_buttons[slot].IsNotValid() )
		{
			_buttons[slot].FireActionSignal();

			return true;
		}

		return false;
	}

	public override void Open()
	{
		Update();

		base.Open();
	}

	public void Initialize()
	{
		SetVisible( false );

		_scrollPanel.SetScrollValue( 0, 0 );
	}

	public override void SetActiveInfo( int input )
	{
		for ( var i = 0; i <= TeamFortress.PcRandom; i++ )
		{
			_buttons[i].SetArmed( false );
			_classInfoPanel[i].SetVisible( false );
		}

		if ( input > TeamFortress.PcRandom || input < 0 )
			input = 0;

		_buttons[input].SetArmed( true );
		_classInfoPanel[input].SetVisible( true );
		_currentInfo = input;

		_scrollPanel.SetScrollValue( 0, 0 );
		_scrollPanel.Validate();
	}
}
